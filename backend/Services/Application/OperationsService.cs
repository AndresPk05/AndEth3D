using System.Text;
using System.Text.Json;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
namespace AndEth3D.Backend.Services;

public class OperationsService(AppDbContext db,ICurrentActor actor,EstimationService calibration)
{
    private async Task<ManufacturingRevision?> Current()=>await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r=>r.Version).FirstOrDefaultAsync();
    private static (DesignBrief? Brief,DesignEstimate? Estimate) Snapshot(QuoteRequest? quote)
    {
        if(quote?.DesignSnapshotJson is null)return (null,null);
        var json=JsonSerializer.Deserialize<JsonElement>(quote.DesignSnapshotJson);
        return (json.TryGetProperty("brief",out var b)?b.Deserialize<DesignBrief>(ParametricData.Json):null,
            json.TryGetProperty("estimate",out var e)?e.Deserialize<DesignEstimate>(ParametricData.Json):null);
    }
    public async Task<AppResult> Context(int id)
    {
        var order=await db.Orders.AsNoTracking().SingleOrDefaultAsync(o=>o.Id==id);if(order is null)return AppResult.NotFound();
        var quote=order.QuoteRequestId is null?null:await db.QuoteRequests.AsNoTracking().SingleAsync(q=>q.Id==order.QuoteRequestId);
        var current=await Current();var config=current is null?new ManufacturingConfiguration():ManufacturingService.Read(current);
        return AppResult.Ok(new{order,brief=Snapshot(quote).Brief,manufacturingVersion=current?.Version??0,
            printers=config.Printers.Select(p=>new{p.Id,p.Name,p.MaterialIds}),materials=config.Materials.Select(m=>new{m.Id,m.Name}),categories=config.Categories.Select(c=>new{c.Id,c.Name}),
            production=await db.ProductionRecords.AsNoTracking().SingleOrDefaultAsync(r=>r.OrderId==id)});
    }
    public async Task<AppResult> Record(int id,RecordProductionRequest request)
    {
        if(request.RequestId==Guid.Empty)return AppResult.Invalid();
        var hash=ParametricGeometry.Hash(Encoding.UTF8.GetBytes(ParametricData.Write(request)));
        var prior=await db.ProductionRecords.AsNoTracking().FirstOrDefaultAsync(r=>r.OrderId==id||r.Id==request.RequestId);
        if(prior is not null)return prior.OrderId==id&&prior.Id==request.RequestId&&prior.RequestHash==hash?AppResult.Ok(new{id=prior.Id}):AppResult.Conflict(new{message="Este pedido ya tiene datos reales registrados. No se sobrescriben."});
        var order=await db.Orders.SingleOrDefaultAsync(o=>o.Id==id);if(order is null)return AppResult.NotFound();
        if(order.Status is not (OrderStatus.Finished or OrderStatus.Delivered))return AppResult.Conflict(new{message="Registra resultados cuando el pedido esté terminado o entregado."});
        var revision=await Current();if(revision is null||revision.Version!=request.ManufacturingVersion)return AppResult.Conflict(new{message="La configuración cambió. Recarga antes de registrar."});
        var config=ManufacturingService.Read(revision);
        if(!config.Categories.Any(c=>c.Id==request.CategoryId)||!config.Materials.Any(m=>m.Id==request.MaterialId)||!config.Printers.Any(p=>p.Id==request.PrinterId&&p.MaterialIds.Contains(request.MaterialId)))
            return AppResult.Invalid(new{message="Selecciona categoría, material y equipo compatibles del catálogo."});
        var quote=order.QuoteRequestId is null?null:await db.QuoteRequests.AsNoTracking().SingleAsync(q=>q.Id==order.QuoteRequestId);
        var (brief,estimate)=Snapshot(quote);
        if(brief is not null&&(brief.Quantity!=request.Quantity||brief.CategoryId!=request.CategoryId||brief.MaterialId!=request.MaterialId))
            return AppResult.Invalid(new{message="Los datos reales deben corresponder a la cantidad, categoría y material de la cotización. Registra incidencias en las notas."});
        if(brief?.WidthMm is not null&&brief.DepthMm is not null&&brief.HeightMm is not null){
            var printer=config.Printers.Single(p=>p.Id==request.PrinterId);
            var usable=new[]{printer.WidthMm-2*printer.MarginMm,printer.DepthMm-2*printer.MarginMm,printer.HeightMm-printer.MarginMm}.Order().ToArray();
            var dimensions=new[]{brief.WidthMm.Value,brief.DepthMm.Value,brief.HeightMm.Value}.Order().ToArray();
            if(dimensions.Where((d,i)=>d>usable[i]).Any()||brief.Colors?.Count>printer.MaxColors)return AppResult.Invalid(new{message="El equipo registrado no es compatible con la ficha fabricada."});
        }
        decimal? Mid(EstimateRange? range)=>range is null?null:(range.Min+range.Max)/2;
        var r=new ProductionRecord{Id=request.RequestId,OrderId=id,ManufacturingRevisionId=revision.Id,CreatedBy=actor.Id,RequestHash=hash,
            CategoryId=request.CategoryId,MaterialId=request.MaterialId,PrinterId=request.PrinterId,Complexity=request.Complexity,Quantity=request.Quantity,
            ColorCount=brief?.Colors?.Count,WidthMm=brief?.WidthMm,DepthMm=brief?.DepthMm,HeightMm=brief?.HeightMm,
            Grams=request.Grams,MachineHours=request.MachineHours,DesignHours=request.DesignHours,FinishingHours=request.FinishingHours,
            MaterialCostCop=request.MaterialCostCop,MachineCostCop=request.MachineCostCop,LaborCostCop=request.LaborCostCop,OtherCostCop=request.OtherCostCop,
            EstimatedTotalCop=estimate?.AverageTotalCop,ConfirmedTotalCop=order.Price,EstimatedGrams=Mid(estimate?.GramsPerUnit)*request.Quantity,EstimatedHours=Mid(estimate?.MachineHoursPerUnit)*request.Quantity,Notes=request.Notes};
        db.ProductionRecords.Add(r);
        // Force the order's status concurrency predicate into the same transaction.
        order.UpdatedAt=DateTime.UtcNow;
        try{await db.SaveChangesAsync();}
        catch(DbUpdateConcurrencyException){return AppResult.Conflict(new{message="El pedido cambió. Recarga."});}
        catch(DbUpdateException ex)when(ex.InnerException is Npgsql.PostgresException{SqlState:"23505"}){return AppResult.Conflict(new{message="Otra solicitud registró este pedido. Reintenta con el mismo identificador."});}
        return AppResult.Ok(new{id=r.Id});
    }
    public async Task<AppResult> Calibration()
    {
        var revision=await Current();var latest=await db.CalibrationRevisions.AsNoTracking().OrderByDescending(r=>r.Version).FirstOrDefaultAsync();
        var existing=latest is null?[]:ParametricData.Read<List<CalibrationSample>>(latest.SamplesJson);
        var records=revision is null?[]:await db.ProductionRecords.AsNoTracking().Where(r=>r.ManufacturingRevisionId==revision.Id).OrderByDescending(r=>r.CreatedAt).Take(500).ToListAsync();
        return AppResult.Ok(new{manufacturingVersion=revision?.Version??0,calibrationVersion=latest?.Version??0,
            outdated=latest is not null&&latest.ManufacturingRevisionId!=revision?.Id,
            candidates=records.Select(r=>new{record=r,sample=OperationsMetrics.Sample(r,revision!.Id)}).Where(x=>x.sample is not null&&!existing.Any(e=>e.Evidence==x.sample.Evidence))});
    }
    public async Task<AppResult> Promote(PromoteProductionRequest request)
    {
        if(request.RecordIds.Distinct().Count()!=request.RecordIds.Count)return AppResult.Invalid(new{message="No repitas registros."});
        var revision=await Current();var latest=await db.CalibrationRevisions.AsNoTracking().OrderByDescending(r=>r.Version).FirstOrDefaultAsync();
        if(revision is null||revision.Version!=request.ManufacturingVersion||(latest?.Version??0)!=request.ExpectedCalibrationVersion||latest is not null&&latest.ManufacturingRevisionId!=revision.Id)
            return AppResult.Conflict(new{message="Revisa primero la calibración y las condiciones de fabricación actuales."});
        var records=await db.ProductionRecords.AsNoTracking().Where(r=>request.RecordIds.Contains(r.Id)).ToListAsync();
        var samples=records.Select(r=>OperationsMetrics.Sample(r,revision.Id)).ToArray();
        if(records.Count!=request.RecordIds.Count||samples.Any(s=>s is null))return AppResult.Invalid(new{message="Solo se incorporan pedidos con ficha completa, medidas y datos compatibles con la revisión actual."});
        var existing=latest is null?[]:ParametricData.Read<List<CalibrationSample>>(latest.SamplesJson);
        var additions=samples.Cast<CalibrationSample>().Where(s=>!existing.Any(e=>e.Evidence==s.Evidence)).ToArray();
        if(additions.Length==0)return AppResult.Ok(new{message="Estos pedidos ya están incorporados."});
        existing.AddRange(additions);
        if(existing.Count>1000)return AppResult.Invalid(new{message="La versión excedería 1000 muestras."});
        return await calibration.Save(new(request.ExpectedCalibrationVersion,request.ManufacturingVersion,existing));
    }
    public async Task<AppResult> Dashboard(int days)
    {
        var now=DateTime.UtcNow;var start=now.AddDays(-days);var cutoff=now.AddDays(-7);
        var conversations=await db.ChatConversations.AsNoTracking().Where(c=>c.CreatedAt>=start&&c.CreatedAt<=now).Select(c=>new{c.Id,c.CreatedAt,c.UpdatedAt}).ToListAsync();
        var ids=conversations.Select(c=>c.Id).ToArray();
        var visuals=await db.VisualProposals.AsNoTracking().Where(v=>ids.Contains(v.ConversationId)).Select(v=>new{v.Id,v.ConversationId,v.CreatedAt,v.CompletedAt,v.Status}).ToListAsync();
        var models=await db.ParametricJobs.AsNoTracking().Where(j=>ids.Contains(j.ConversationId)).Select(j=>new{j.Id,j.ConversationId,j.CreatedAt,j.CompletedAt,j.Status}).ToListAsync();
        var visualIds=visuals.Select(v=>v.Id).ToArray();var modelIds=models.Select(m=>m.Id).ToArray();
        var quotes=await db.QuoteRequests.AsNoTracking().Where(q=>q.ProposalId!=null&&visualIds.Contains(q.ProposalId.Value)||q.ParametricJobId!=null&&modelIds.Contains(q.ParametricJobId.Value))
            .Select(q=>new{q.Id,q.ProposalId,q.ParametricJobId}).ToListAsync();
        var quoted=quotes.Select(q=>q.ProposalId is not null?visuals.Single(v=>v.Id==q.ProposalId).ConversationId:models.Single(m=>m.Id==q.ParametricJobId).ConversationId).ToHashSet();
        var quoteIds=quotes.Select(q=>q.Id).ToArray();
        var orders=await db.Orders.AsNoTracking().Where(o=>o.QuoteRequestId!=null&&quoteIds.Contains(o.QuoteRequestId.Value)).Select(o=>new{o.QuoteRequestId,o.Status}).ToListAsync();
        var convertedQuoteIds=orders.Select(o=>o.QuoteRequestId).ToHashSet();
        var converted=quotes.Where(q=>convertedQuoteIds.Contains(q.Id)).Select(q=>q.ProposalId is not null?visuals.Single(v=>v.Id==q.ProposalId).ConversationId:models.Single(m=>m.Id==q.ParametricJobId).ConversationId).Distinct().Count();
        var mature=conversations.Where(c=>c.CreatedAt<=cutoff).ToArray();
        var inactive=mature.Count(c=>!quoted.Contains(c.Id)&&c.UpdatedAt<=cutoff&&
            !visuals.Any(v=>v.ConversationId==c.Id&&(v.Status is "Queued" or "Processing"||v.CreatedAt>cutoff||v.CompletedAt>cutoff))&&
            !models.Any(m=>m.ConversationId==c.Id&&(m.Status is "Queued" or "Processing"||m.CreatedAt>cutoff||m.CompletedAt>cutoff)));
        var turns=await db.ChatTurns.AsNoTracking().Where(t=>t.CreatedAt>=start&&t.CreatedAt<=now).Select(t=>new{t.ReservedUsd,t.ReportedCostUsd}).ToListAsync();
        var imageCosts=await db.VisualProposals.AsNoTracking().Where(v=>v.CreatedAt>=start||v.CompletedAt>=start||v.Status=="Processing"||v.Status=="Queued")
            .Select(v=>new{v.CreatedAt,v.ReservedUsd,v.AttemptsJson,v.Number,v.ParentProposalId}).ToListAsync();
        var attempts=imageCosts.SelectMany(v=>ParametricData.Read<List<VisualAttempt>>(v.AttemptsJson)).Where(a=>a.StartedAt>=start&&a.StartedAt<=now).ToArray();
        var newImages=imageCosts.Where(v=>v.CreatedAt>=start&&v.CreatedAt<=now).ToArray();
        var production=await db.ProductionRecords.AsNoTracking().Where(r=>r.CreatedAt>=start&&r.CreatedAt<=now).OrderByDescending(r=>r.CreatedAt).ToListAsync();
        var confirmedQuotes=await db.QuoteRequests.AsNoTracking().Where(q=>q.CreatedAt>=start&&q.CreatedAt<=now&&q.ConfirmedPriceCop!=null&&q.DesignSnapshotJson!=null).ToListAsync();
        var comparisons=confirmedQuotes.Select(q=>new{estimate=Snapshot(q).Estimate,confirmed=q.ConfirmedPriceCop}).Where(x=>x.estimate?.AverageTotalCop>0).ToArray();
        decimal? Rate(int numerator,int denominator)=>denominator==0?null:decimal.Round(100m*numerator/denominator,2);
        decimal? Mean(IEnumerable<decimal?> values){var present=values.Where(v=>v.HasValue).Select(v=>v!.Value).ToArray();return present.Length==0?null:decimal.Round(present.Average(),2);}
        return AppResult.Ok(new{asOf=now,from=start,days,
            confirmedQuotes=new{count=confirmedQuotes.Count,withEstimate=comparisons.Length,
                meanDeviationPercent=Mean(comparisons.Select(x=>OperationsMetrics.Percent(x.confirmed,x.estimate!.AverageTotalCop))),
                meanAbsoluteDeviationPercent=Mean(comparisons.Select(x=>OperationsMetrics.Percent(x.confirmed,x.estimate!.AverageTotalCop) is decimal n?(decimal?)Math.Abs(n):null)),
                outsideOriginalRange=comparisons.Count(x=>x.estimate!.TotalPriceCop is not null&&(x.confirmed<x.estimate.TotalPriceCop.Min||x.confirmed>x.estimate.TotalPriceCop.Max))},
            funnel=new{conversations=conversations.Count,withImages=visuals.Where(v=>v.Status=="Completed").Select(v=>v.ConversationId).Distinct().Count(),withModels=models.Select(m=>m.ConversationId).Distinct().Count(),quoted=quoted.Count,converted,
                conversionPercent=Rate(converted,conversations.Count),matureConversations=mature.Length,inactiveWithoutQuote=inactive,inactivityPercent=Rate(inactive,mature.Length),cancelledOrders=orders.Count(o=>o.Status==OrderStatus.Cancelled)},
            ai=new{chatReportedUsd=turns.Sum(t=>t.ReportedCostUsd??0),chatReservedUsd=turns.Sum(t=>t.ReservedUsd),chatUnknownCosts=turns.Count(t=>t.ReportedCostUsd is null),
                imagesReportedUsd=attempts.Sum(a=>a.ReportedCostUsd??0),imagesReservedUsd=newImages.Sum(v=>v.ReservedUsd),imagesUnknownCosts=attempts.Count(a=>a.ReportedCostUsd is null),imageAttempts=attempts.Length,
                generatedRequests=newImages.Length,regenerations=newImages.Count(v=>v.Number>1),edits=newImages.Count(v=>v.ParentProposalId is not null)},
            categories=production.GroupBy(r=>r.CategoryId).Select(g=>new{categoryId=g.Key,count=g.Count(),priceSamples=g.Count(r=>r.EstimatedTotalCop>0&&r.ConfirmedTotalCop is not null),
                meanPriceDeviationPercent=Mean(g.Select(r=>OperationsMetrics.Percent(r.ConfirmedTotalCop,r.EstimatedTotalCop))),
                meanAbsoluteGramsErrorPercent=Mean(g.Select(r=>OperationsMetrics.Percent(r.Grams,r.EstimatedGrams) is decimal n?(decimal?)Math.Abs(n):null)),
                meanMarginPercent=Mean(g.Select(OperationsMetrics.Margin)),actualCostCop=g.Sum(OperationsMetrics.Cost)}),
            production=production.Take(200).Select(r=>new{record=r,actualCostCop=OperationsMetrics.Cost(r),marginPercent=OperationsMetrics.Margin(r),priceDeviationPercent=OperationsMetrics.Percent(r.ConfirmedTotalCop,r.EstimatedTotalCop),gramsDeviationPercent=OperationsMetrics.Percent(r.Grams,r.EstimatedGrams),hoursDeviationPercent=OperationsMetrics.Percent(r.MachineHours,r.EstimatedHours)}),
            definitions=new[]{"Cohorte: conversaciones creadas en el período; conversión acumulada hasta la fecha de consulta, una vez por conversación.","Inactividad: sin cotización ni actividad/trabajos pendientes en 7 días. Es un indicador de posible abandono, no un abandono confirmado.","Gasto IA: costos reportados por intento en el período. Desconocidos no equivalen a cero. Reservas son límites presupuestarios y no se suman a los costos reportados.","Regeneración: solicitud de imagen posterior a la primera. Ediciones son un subconjunto, no un conteo adicional.","Producción: registros creados en el período; totales para todo el lote. Desviación = (real o confirmado - estimado) / estimado. Sin base positiva se muestra sin datos.","Precio confirmado: cotizaciones creadas en el período que actualmente tienen confirmación, aunque aún no se hayan producido. El margen compara precio confirmado con costos registrados; no representa cobros, reembolsos ni utilidad contable."}});
    }
}
