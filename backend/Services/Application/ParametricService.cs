using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace AndEth3D.Backend.Services;

public class ParametricService(AppDbContext db,ICurrentActor actor,IOptions<ParametricOptions> options)
{
    private async Task<ManufacturingRevision?> Current()=>await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r=>r.Version).FirstOrDefaultAsync();
    public async Task<AppResult> List(Guid id)
    {
        var conversation=await db.ChatConversations.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==id&&c.UserId==actor.Id);
        if(conversation is null)return AppResult.NotFound();
        var revision=await Current();var hash=VisualPolicy.Fingerprint(conversation.BriefJson);
        var jobs=await db.ParametricJobs.AsNoTracking().Where(j=>j.ConversationId==id).OrderByDescending(j=>j.CreatedAt).ToListAsync();
        return AppResult.Ok(new{fingerprint=hash,slicingEnabled=options.Value.Enabled,busy=conversation.BusyUntil>DateTime.UtcNow,
            models=jobs.Select(j=>new{j.Id,j.Status,j.CreatedAt,j.ModelHash,j.TemplateVersion,
                parameters=ParametricData.Read<ParametricParameters>(j.ParametersJson),
                outdated=j.Fingerprint!=hash||j.ManufacturingRevisionId!=revision?.Id||(j.ProfileJson is not null&&!ParametricData.CurrentProfile(j,options.Value)),
                estimate=j.EstimateJson is null?null:ParametricData.Read<DesignEstimate>(j.EstimateJson),
                modelUrl=$"/api/parametric/models/{j.Id}",message=j.Status switch{"Completed"=>"Modelo laminado; revisa la vista 3D antes de cotizar.","Queued" or "Processing"=>"Calculando consumo del modelo.",_=>"Modelo disponible. El laminado requiere configuración o revisión técnica."}})});
    }
    public async Task<AppResult> Create(Guid id,CreateParametricRequest request)
    {
        var c=await db.ChatConversations.SingleOrDefaultAsync(c=>c.Id==id&&c.UserId==actor.Id);
        if(c is null)return AppResult.NotFound();
        if(request.RequestId==Guid.Empty||!request.AcceptTemplate)return AppResult.Invalid(new{message="Acepta las características de la plantilla antes de continuar."});
        var parameters=ParametricData.Write(request.Parameters);
        var existing=await db.ParametricJobs.AsNoTracking().SingleOrDefaultAsync(j=>j.Id==request.RequestId);
        if(existing is not null)return existing.ConversationId==id&&existing.Fingerprint==request.ExpectedFingerprint&&existing.ParametersJson==parameters?AppResult.Ok(new{id=existing.Id}):AppResult.Conflict(new{message="Identificador usado por otra solicitud."});
        if(c.BusyUntil>DateTime.UtcNow||VisualPolicy.Fingerprint(c.BriefJson)!=request.ExpectedFingerprint)return AppResult.Conflict(new{message="La ficha cambió. Actualiza antes de crear el modelo."});
        var revision=await Current();if(revision is null)return AppResult.Invalid(new{message="Solicita revisión técnica."});
        var config=ManufacturingService.Read(revision);var brief=DesignPolicy.Read(c.BriefJson);
        var error=ParametricGeometry.Validate(brief,request.Parameters,config);
        if(error is not null)return AppResult.Invalid(new{message=error});
        if(!config.Printers.Any(p=>ParametricGeometry.Fits(brief,request.Parameters,p)))return AppResult.Invalid(new{message="Las medidas o espesores de la plantilla necesitan revisión técnica."});
        if(await db.ParametricJobs.AnyAsync(j=>j.ConversationId==id&&(j.Status=="Queued"||j.Status=="Processing")))return AppResult.Conflict(new{message="Ya hay un laminado en curso."});
        if(await db.ParametricJobs.CountAsync(j=>j.ConversationId==id)>=options.Value.MaxPerConversation)return AppResult.RateLimited(new{message="Esta conversación alcanzó su límite de modelos."});
        var allowance=await db.ParametricAllowances.SingleOrDefaultAsync(a=>a.UserId==actor.Id);
        if(allowance is null){allowance=new(){UserId=actor.Id};db.ParametricAllowances.Add(allowance);}
        if(allowance.Day!=DateTime.UtcNow.Date){allowance.Day=DateTime.UtcNow.Date;allowance.Count=0;}
        if(allowance.Count>=options.Value.MaxPerUserDay)return AppResult.RateLimited(new{message="Alcanzaste el límite diario de modelos."});
        var stl=ParametricGeometry.Build(brief,request.Parameters);
        var job=new ParametricJob{Id=request.RequestId,ConversationId=id,ManufacturingRevisionId=revision.Id,BriefJson=c.BriefJson,Fingerprint=request.ExpectedFingerprint,
            ParametersJson=parameters,TemplateVersion=ParametricGeometry.Version,ModelStl=stl,ModelHash=ParametricGeometry.Hash(stl)};
        var activeProfiles=await db.ParametricJobs.AsNoTracking().Where(j=>(j.Status=="Queued"||j.Status=="Processing")&&j.ProfileJson!=null).Select(j=>j.ProfileJson!).ToListAsync();
        var loads=activeProfiles.Select(ParametricData.Read<SlicingProfile>).GroupBy(p=>p.PrinterId).ToDictionary(g=>g.Key,g=>g.Count());
        if(options.Value.Enabled){
            foreach(var profile in ParametricRouting.Candidates(options.Value.Profiles,config,brief,request.Parameters,loads)){
                try{job.ProfileIni=await ParametricProfile.Read(profile);job.ProfileJson=ParametricData.Write(profile);job.Status="Queued";job.Failure=null;break;}
                catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException){job.Status="ReviewRequired";job.Failure="ProfileUnavailable";}
            }
        }
        db.ParametricJobs.Add(job);c.Version++;allowance.Count++;allowance.Version++;
        try{await db.SaveChangesAsync();}
        catch(DbUpdateConcurrencyException){return AppResult.Conflict(new{message="Solicitud simultánea. Actualiza y reintenta."});}
        catch(DbUpdateException ex)when(ex.InnerException is Npgsql.PostgresException{SqlState:"23505"}){return AppResult.Conflict(new{message="Solicitud simultánea. Reintenta con el mismo identificador."});}
        return AppResult.Accepted(new{id=job.Id});
    }
    public async Task<AppResult> Model(Guid id)
    {
        var job=await db.ParametricJobs.AsNoTracking().SingleOrDefaultAsync(j=>j.Id==id);
        if(job is null || !actor.IsAdmin&&!await db.ChatConversations.AnyAsync(c=>c.Id==job.ConversationId&&c.UserId==actor.Id))return AppResult.NotFound();
        return AppResult.File(new DownloadBytes(job.ModelStl,"application/octet-stream","modelo.stl"));
    }
    public async Task<AppResult> Quote(Guid id,ParametricQuoteRequest request)
    {
        var job=await db.ParametricJobs.AsNoTracking().SingleOrDefaultAsync(j=>j.Id==id);
        if(job is null)return AppResult.NotFound();
        var c=await db.ChatConversations.SingleOrDefaultAsync(c=>c.Id==job.ConversationId&&c.UserId==actor.Id);
        if(c is null)return AppResult.NotFound();
        if(request.RequestId==Guid.Empty)return AppResult.Invalid();
        var existing=await db.QuoteRequests.AsNoTracking().SingleOrDefaultAsync(q=>q.DesignRequestId==request.RequestId);
        if(existing is not null)return existing.UserId==actor.Id&&existing.ParametricJobId==id?AppResult.Ok(new{quoteId=existing.Id}):AppResult.Conflict(new{message="Solicitud ya utilizada."});
        var revision=await Current();
        if(c.BusyUntil>DateTime.UtcNow||job.Fingerprint!=VisualPolicy.Fingerprint(c.BriefJson)||revision?.Id!=job.ManufacturingRevisionId||
            job.ProfileJson is not null&&!ParametricData.CurrentProfile(job,options.Value))return AppResult.Conflict(new{message="El modelo o sus condiciones cambiaron. Genera y revisa una nueva versión."});
        if(job.Status is "Queued" or "Processing")return AppResult.Conflict(new{message="Espera el resultado del laminado."});
        var b=DesignPolicy.Read(job.BriefJson);
        var estimate=job.EstimateJson is null?new DesignEstimate("ReviewRequired","Revisión manual del modelo requerida.","parametric-manual-v1",revision!.Version,0,"template",b.Quantity!.Value):ParametricData.Read<DesignEstimate>(job.EstimateJson);
        var turns=await db.ChatTurns.AsNoTracking().Where(t=>t.ConversationId==c.Id).OrderBy(t=>t.Sequence).Select(t=>new{t.UserText,t.AssistantText,t.CreatedAt}).ToListAsync();
        var q=new QuoteRequest{UserId=actor.Id,Description=b.Object+" — plantilla paramétrica",Material=ManufacturingService.Read(revision!).Materials.Single(m=>m.Id==b.MaterialId).Name,
            ManufacturingRevisionId=revision!.Id,DesignRequestId=request.RequestId,ParametricJobId=id,DesignEstimateToken=job.ReproductionHash,
            DesignSnapshotJson=ParametricData.Write(new{conversationId=c.Id,parametricJobId=id,templateVersion=job.TemplateVersion,parameters=ParametricData.Read<ParametricParameters>(job.ParametersJson),job.ModelHash,brief=b,estimate,turns,submittedAt=DateTime.UtcNow})};
        db.QuoteRequests.Add(q);c.Version++;
        try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return AppResult.Conflict(new{message="La ficha cambió. Reintenta."});}
        catch(DbUpdateException ex)when(ex.InnerException is Npgsql.PostgresException{SqlState:"23505"}){return AppResult.Conflict(new{message="Solicitud simultánea. Reintenta con el mismo identificador."});}
        return AppResult.Ok(new{quoteId=q.Id});
    }
    public async Task<AppResult> Admin()
    {
        var jobs=await db.ParametricJobs.AsNoTracking().OrderByDescending(j=>j.CreatedAt).Take(100).ToListAsync();
        return AppResult.Ok(new{enabled=options.Value.Enabled,worker=options.Value.RunWorker,routingPolicy=ParametricRouting.Policy,profiles=options.Value.Profiles.Select(p=>new{p.Id,p.PrinterId,p.MaterialId,p.Priority,p.ProfileSha256,p.EngineVersion,p.Image,p.TotalsVerified,p.UnreportedPurgeGrams}),
            jobs=jobs.Select(j=>new{j.Id,j.Status,j.Failure,j.Version,j.CreatedAt,j.Grams,j.Seconds,j.ActualGrams,j.ActualSeconds,j.MeasurementNotes,j.MeasuredAt,j.MeasuredBy,j.ReproductionHash,
                gramErrorPercent=j.Grams>0&&j.ActualGrams is not null?100*(j.ActualGrams-j.Grams)/j.Grams:null,
                timeErrorPercent=j.Seconds>0&&j.ActualSeconds is not null?100*(j.ActualSeconds-j.Seconds)/j.Seconds:null})});
    }
    public async Task<AppResult> Measure(Guid id,MeasureParametricRequest request)
    {
        var job=await db.ParametricJobs.SingleOrDefaultAsync(j=>j.Id==id);
        if(job is null)return AppResult.NotFound();
        if(job.Status!="Completed"||job.Version!=request.ExpectedVersion||job.MeasuredAt is not null)return AppResult.Conflict(new{message="Solo se registra una medición por resultado completado y vigente."});
        job.ActualGrams=request.Grams;job.ActualSeconds=request.Seconds;job.MeasurementNotes=request.Notes;job.MeasuredAt=DateTime.UtcNow;job.MeasuredBy=actor.Id;job.Version++;
        await db.SaveChangesAsync();return AppResult.Ok();
    }
}
