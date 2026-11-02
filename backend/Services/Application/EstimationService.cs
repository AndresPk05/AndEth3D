using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
namespace AndEth3D.Backend.Services;

public class EstimationService(AppDbContext db, ICurrentActor actor, ProposalImageStore images)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Write(object value) => JsonSerializer.Serialize(value,Json);
    private async Task<ManufacturingRevision?> Current() => await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r=>r.Version).FirstOrDefaultAsync();
    public async Task<AppResult> Calibration()
    {
        var latest = await db.CalibrationRevisions.AsNoTracking().OrderByDescending(r=>r.Version).FirstOrDefaultAsync();
        var manufacturing = await Current();
        return AppResult.Ok(new { version=latest?.Version ?? 0, manufacturingVersion=manufacturing?.Version ?? 0,
            outdated=latest is not null && latest.ManufacturingRevisionId!=manufacturing?.Id,
            samples=latest is null ? [] : JsonSerializer.Deserialize<List<CalibrationSample>>(latest.SamplesJson,Json),
            history=await db.CalibrationRevisions.AsNoTracking().OrderByDescending(r=>r.Version).Take(100).Select(r=>new{r.Version,r.CreatedAt,r.CreatedByUserId,r.ManufacturingRevisionId}).ToListAsync() });
    }
    public async Task<AppResult> Save(SaveCalibrationRequest request)
    {
        var manufacturing=await Current();
        var version=await db.CalibrationRevisions.MaxAsync(r=>(int?)r.Version) ?? 0;
        if (manufacturing is null || request.ManufacturingVersion!=manufacturing.Version || request.ExpectedVersion!=version)
            return AppResult.Conflict(new {message="La configuración cambió. Recarga y valida las muestras con las condiciones actuales."});
        var config=ManufacturingService.Read(manufacturing);
        if (request.Samples.Any(s=>s is null || !config.Categories.Any(c=>c.Id==s.CategoryId) || !config.Materials.Any(m=>m.Id==s.MaterialId) ||
            !config.Printers.Any(p=>p.Id==s.PrinterId && p.MaterialIds.Contains(s.MaterialId) && p.MaxColors>=s.ColorCount)))
            return AppResult.Invalid(new {message="Cada muestra debe corresponder a categoría, material y equipo compatibles del catálogo."});
        foreach(var sample in request.Samples) {
            var printer=config.Printers.Single(p=>p.Id==sample.PrinterId);
            var available=new[]{printer.WidthMm-2*printer.MarginMm,printer.DepthMm-2*printer.MarginMm,printer.HeightMm-printer.MarginMm}.Order().ToArray();
            var size=new[]{sample.WidthMm,sample.DepthMm,sample.HeightMm}.Order().ToArray();
            if(size.Where((d,i)=>d>available[i]).Any())return AppResult.Invalid(new{message="Una muestra excede el volumen útil del equipo seleccionado."});
        }
        db.CalibrationRevisions.Add(new(){Version=version+1,ManufacturingRevisionId=manufacturing.Id,CreatedByUserId=actor.Id,SamplesJson=Write(request.Samples)});
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException {SqlState:"23505"}) { return AppResult.Conflict(new{message="Otro administrador guardó la calibración. Recarga."}); }
        return AppResult.Ok(new {version=version+1});
    }
    private async Task<(DesignEstimate Value,string Token,ManufacturingRevision? Revision)> Calculate(ChatConversation c,string complexity)
    {
        var revision=await Current();
        var calibration=await db.CalibrationRevisions.AsNoTracking().OrderByDescending(r=>r.Version).FirstOrDefaultAsync();
        var samples=calibration?.ManufacturingRevisionId==revision?.Id && calibration is not null
            ? JsonSerializer.Deserialize<List<CalibrationSample>>(calibration.SamplesJson,Json)! : [];
        var estimate=EstimationEngine.Calculate(DesignPolicy.Read(c.BriefJson),revision is null ? new() : ManufacturingService.Read(revision),revision?.Version ?? 0,calibration?.Version ?? 0,samples,complexity);
        var token=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(c.Id+"|"+VisualPolicy.Fingerprint(c.BriefJson)+"|"+Write(estimate))));
        return (estimate,token,revision);
    }
    public async Task<AppResult> Estimate(Guid id,string complexity)
    {
        if(complexity is not ("simple" or "standard" or "complex"))return AppResult.Invalid(new{message="Selecciona una complejidad válida."});
        var c=await db.ChatConversations.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==id && c.UserId==actor.Id);
        if(c is null) return AppResult.NotFound();
        var result=await Calculate(c,complexity);
        return AppResult.Ok(new{estimate=result.Value,token=result.Token,busy=c.BusyUntil>DateTime.UtcNow});
    }
    public async Task<AppResult> Submit(Guid id,SubmitDesignQuoteRequest request)
    {
        var c=await db.ChatConversations.SingleOrDefaultAsync(c=>c.Id==id && c.UserId==actor.Id);
        if(c is null) return AppResult.NotFound();
        if(request.RequestId==Guid.Empty) return AppResult.Invalid();
        var existing=await db.QuoteRequests.AsNoTracking().SingleOrDefaultAsync(q=>q.DesignRequestId==request.RequestId);
        if(existing is not null) return existing.UserId==actor.Id && existing.ProposalId==request.ProposalId && existing.DesignEstimateToken==request.EstimateToken &&
            await db.VisualProposals.AnyAsync(p=>p.Id==existing.ProposalId && p.ConversationId==id)
            ? AppResult.Ok(new{quoteId=existing.Id}) : AppResult.Conflict(new{message="Identificador usado para otra solicitud."});
        var result=await Calculate(c,request.Complexity);
        if(c.BusyUntil>DateTime.UtcNow || result.Token!=request.EstimateToken)
            return AppResult.Conflict(new{message="La ficha o las tarifas cambiaron. Revisa la estimación actualizada antes de enviarla."});
        var p=await db.VisualProposals.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==request.ProposalId && p.ConversationId==id && p.Status=="Completed");
        if(p is null) return AppResult.NotFound();
        var b=DesignPolicy.Read(c.BriefJson);
        if(result.Revision is null || p.BriefFingerprint!=VisualPolicy.Fingerprint(c.BriefJson) || p.ManufacturingRevisionId!=result.Revision.Id ||
            DesignPolicy.Assess(b,ManufacturingService.Read(result.Revision)).Status!="ReadyForReview")
            return AppResult.Conflict(new{message="Genera una propuesta compatible con la ficha y las condiciones actuales antes de cotizar."});
        var turns=await db.ChatTurns.AsNoTracking().Where(t=>t.ConversationId==id).OrderBy(t=>t.Sequence).Select(t=>new{t.UserText,t.AssistantText,t.CreatedAt}).ToListAsync();
        var quote=new QuoteRequest{UserId=actor.Id,Description=b.Object+" — "+b.Use,Material=ManufacturingService.Read(result.Revision).Materials.Single(m=>m.Id==b.MaterialId).Name,
            ManufacturingRevisionId=result.Revision.Id,DesignRequestId=request.RequestId,ProposalId=p.Id,DesignEstimateToken=result.Token,
            DesignSnapshotJson=Write(new{conversationId=id,proposalId=p.Id,proposalNumber=p.Number,brief=b,estimate=result.Value,turns,submittedAt=DateTime.UtcNow})};
        db.QuoteRequests.Add(quote); c.Version++;
        try {await db.SaveChangesAsync();}
        catch(DbUpdateConcurrencyException){return AppResult.Conflict(new{message="La conversación cambió. Recarga antes de enviar."});}
        catch(DbUpdateException ex) when(ex.InnerException is Npgsql.PostgresException{SqlState:"23505"}){return AppResult.Conflict(new{message="Solicitud simultánea. Reintenta con el mismo identificador."});}
        return AppResult.Ok(new{quoteId=quote.Id});
    }
    public async Task<AppResult> Detail(int id)
    {
        var q=await db.QuoteRequests.AsNoTracking().SingleOrDefaultAsync(q=>q.Id==id && (q.UserId==actor.Id || actor.IsAdmin));
        if(q is null) return AppResult.NotFound();
        return AppResult.Ok(new{snapshot=q.DesignSnapshotJson is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(q.DesignSnapshotJson),
            q.ConfirmedPriceCop,q.ReviewVersion,q.AdminNotes,q.Status,imageUrl=q.ProposalId is null ? null : $"/api/estimation/quotes/{id}/image"});
    }
    public async Task<AppResult> Image(int id)
    {
        var q=await db.QuoteRequests.AsNoTracking().SingleOrDefaultAsync(q=>q.Id==id && (q.UserId==actor.Id || actor.IsAdmin));
        if(q?.ProposalId is null) return AppResult.NotFound();
        var p=await db.VisualProposals.AsNoTracking().SingleAsync(p=>p.Id==q.ProposalId);
        var path=images.Resolve(p.ImageFile);
        return path is null || !File.Exists(path) ? AppResult.NotFound() : AppResult.File(new DownloadFile(path,"image/png"));
    }
    public async Task<AppResult> Review(int id,ReviewDesignQuoteRequest request)
    {
        var q=await db.QuoteRequests.SingleOrDefaultAsync(q=>q.Id==id);
        if(q?.DesignSnapshotJson is null) return AppResult.NotFound();
        if(q.ReviewVersion!=request.ExpectedVersion || q.Status==QuoteStatus.Converted || await db.Orders.AnyAsync(o=>o.QuoteRequestId==id))
            return AppResult.Conflict(new{message="La cotización cambió o ya fue convertida."});
        q.ConfirmedPriceCop=request.PriceCop;q.AdminNotes=request.Notes;q.Status=QuoteStatus.Quoted;q.ReviewVersion++;
        await db.SaveChangesAsync();
        return AppResult.Ok(new{q.ConfirmedPriceCop,q.ReviewVersion});
    }
}
