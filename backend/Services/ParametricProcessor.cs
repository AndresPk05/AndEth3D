using System.Text;
using System.Text.Json;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace AndEth3D.Backend.Services;

public static class ParametricData
{
    public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static string Write(object value)=>JsonSerializer.Serialize(value,Json);
    public static T Read<T>(string value)=>JsonSerializer.Deserialize<T>(value,Json)!;
    public static bool CurrentProfile(ParametricJob job,ParametricOptions options)=>job.ProfileJson is not null && options.Profiles.Any(p=>ParametricRouting.Identity(p)==ParametricRouting.Identity(Read<SlicingProfile>(job.ProfileJson)));
    public static DesignEstimate Estimate(DesignBrief b,ManufacturingConfiguration config,int version,SlicingProfile profile,decimal grams,decimal seconds)
    {
        var price=config.Pricing;var material=config.Materials.Single(m=>m.Id==b.MaterialId);
        var cost=((grams*material.CostPerKgCop/1000+seconds/3600*price.MachineHourCop+profile.FinishingHours*price.FinishingHourCop)*b.Quantity!.Value+profile.DesignHours*price.DesignHourCop)/(1-price.GrossMargin);
        var low=decimal.Floor(Math.Max(price.MinimumOrderCop,cost*.85m)*100)/100;
        var high=decimal.Ceiling(Math.Max(price.MinimumOrderCop,cost*1.15m)*100)/100;
        return new("Indicative","Cotización basada en laminado; requiere revisión técnica y confirmación.","sliced-template-v1",version,0,"template",b.Quantity.Value,
            new(grams,grams),new(decimal.Round(seconds/3600,4),decimal.Round(seconds/3600,4)),new(decimal.Floor(low/b.Quantity.Value*100)/100,decimal.Ceiling(high/b.Quantity.Value*100)/100),new(low,high),decimal.Round((low+high)/2,2),
            ["Modelo de plantilla explícita; no reproduce detalles de la imagen conceptual ni instrucciones libres.","Consumo reportado por laminador más la purga no incluida verificada en el perfil; soportes y torre no se vuelven a sumar.",
             "Una pieza por laminado. Cantidad multiplica consumo y tiempo; diseño una vez, acabado por pieza.","Rango comercial ±15% sobre costo con margen, respetando mínimo. No es exactitud estadística.","Sin envío ni impuestos adicionales no definidos. El precio se confirma después de revisión."]);
    }
}

public class ParametricProcessor(AppDbContext db,IParametricSlicer slicer,IOptions<ParametricOptions> settings)
{
    public async Task<bool> Process(CancellationToken ct)
    {
        var now=DateTime.UtcNow;
        var job=await db.ParametricJobs.Where(j=>j.Status=="Queued" || j.Status=="Processing"&&j.LeaseUntil<=now).OrderBy(j=>j.CreatedAt).FirstOrDefaultAsync(ct);
        if(job is null)return false;
        if(job.Status=="Processing"){await Finish(job,"Interrupted");return true;}
        var conversation=await db.ChatConversations.AsNoTracking().SingleAsync(c=>c.Id==job.ConversationId,ct);
        var revision=await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r=>r.Version).FirstAsync(ct);
        if(!settings.Value.Enabled || !ParametricData.CurrentProfile(job,settings.Value)){await Finish(job,"ProfileUnavailable");return true;}
        if(conversation.BusyUntil>now)return false;
        if(job.Fingerprint!=VisualPolicy.Fingerprint(conversation.BriefJson)||revision.Id!=job.ManufacturingRevisionId){await Finish(job,"Stale");return true;}
        job.Status="Processing";job.Version++;job.LeaseUntil=now.AddSeconds(settings.Value.TimeoutSeconds+30);
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){db.ChangeTracker.Clear();return true;}
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(settings.Value.TimeoutSeconds));
        try{
            var profile=ParametricData.Read<SlicingProfile>(job.ProfileJson!);
            var config=ManufacturingService.Read(revision);var printer=config.Printers.Single(p=>p.Id==profile.PrinterId);
            if(job.ModelHash!=ParametricGeometry.Hash(job.ModelStl) || !string.Equals(profile.ProfileSha256,ParametricGeometry.Hash(Encoding.UTF8.GetBytes(job.ProfileIni!)),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException();
            var output=await slicer.Slice(job.ModelStl,job.ProfileIni!,profile,printer.WidthMm/2,printer.DepthMm/2,timeout.Token).WaitAsync(timeout.Token);
            if(output.EngineVersion!=profile.EngineVersion || output.Grams is <=0 or >100000 || output.Seconds is <=0 or >10000000)throw new InvalidDataException();
            job.Grams=output.Grams+profile.UnreportedPurgeGrams;job.Seconds=output.Seconds;
            job.EstimateJson=ParametricData.Write(ParametricData.Estimate(DesignPolicy.Read(job.BriefJson),config,revision.Version,profile,job.Grams.Value,job.Seconds.Value));
            job.ReproductionHash=ParametricGeometry.Hash(Encoding.UTF8.GetBytes(ParametricData.Write(new{job.ModelHash,profile.ProfileSha256,profile.Image,output.EngineVersion,output.EffectiveConfigHash,job.TemplateVersion,job.ParametersJson})));
            await Finish(job,null);
        }catch(Exception ex) when(ex is IOException or InvalidOperationException or OperationCanceledException or JsonException or System.ComponentModel.Win32Exception){await Finish(job,ex is OperationCanceledException?"Interrupted":"SlicingFailed");}
        return true;
    }
    private async Task Finish(ParametricJob job,string? failure)
    {
        job.Status=failure is null?"Completed":"ReviewRequired";job.Failure=failure;job.LeaseUntil=null;job.CompletedAt=DateTime.UtcNow;job.Version++;
        if(failure is not null){job.Grams=null;job.Seconds=null;job.EstimateJson=null;job.ReproductionHash=null;}
        try{await db.SaveChangesAsync(CancellationToken.None);}catch(DbUpdateConcurrencyException){db.ChangeTracker.Clear();}
    }
}
public class ParametricWorker(IServiceScopeFactory scopes,IOptions<ParametricOptions> options,ILogger<ParametricWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(!options.Value.RunWorker)return;
        while(!stoppingToken.IsCancellationRequested){
            try{using var scope=scopes.CreateScope();if(await scope.ServiceProvider.GetRequiredService<ParametricProcessor>().Process(stoppingToken))continue;}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("Parametric worker failed ({ErrorType}).",ex.GetType().Name);}
            try{await Task.Delay(3000,stoppingToken);}catch(OperationCanceledException){break;}
        }
    }
}
