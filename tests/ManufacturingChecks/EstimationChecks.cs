using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.EntityFrameworkCore;

static class EstimationChecks
{
    public static async Task Run(HttpClient client,IServiceProvider services)
    {
        void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
        void Role(string role,string user="1"){
            client.DefaultRequestHeaders.Remove("X-Test-Role");client.DefaultRequestHeaders.Add("X-Test-Role",role);
            client.DefaultRequestHeaders.Remove("X-Test-User");client.DefaultRequestHeaders.Add("X-Test-User",user);
        }
        var config=new ManufacturingConfiguration{
            Printers=[new(){Id="machine",Name="SecretMachine",Brand="SecretBrand",Model="SecretModel",Active=true,MaterialIds=["pla"]}],
            Materials=[new(){Id="pla",Name="PLA",Active=true,CostPerKgCop=80000,Colors=[new(){Name="Negro",Available=true}]}],
            Categories=[new(){Id="cajas",Name="Cajas"}],Pricing=new(){MachineHourCop=5000,DesignHourCop=10000,FinishingHourCop=2000,GrossMargin=.2m}
        };
        var brief=new DesignBrief{Object="Caja",Use="Organizar",CategoryId="cajas",MaterialId="pla",WidthMm=100,DepthMm=80,HeightMm=60,Quantity=2,Colors=["Negro"],Personalization="Sin personalización"};
        var samples=Enumerable.Range(1,3).Select(i=>new CalibrationSample{CategoryId="cajas",MaterialId="pla",PrinterId="machine",WidthMm=100,DepthMm=80,HeightMm=60,Grams=100,MachineHours=2,DesignHours=1,FinishingHours=.5m,Evidence="Trabajo laminado "+i}).ToList();
        var computed=EstimationEngine.Calculate(brief,config,1,1,samples,"standard");
        Check(computed.Status=="Indicative" && computed.TotalPriceCop!.Min==45000 && computed.TotalPriceCop.Max==75000 && computed.AverageTotalCop==60000,"deterministic cost includes material, machine, one design fee, finishing, quantity and gross margin");
        Check(computed.GramsPerUnit!.Min==75 && computed.GramsPerUnit.Max==125 && computed.UnitPriceCop!.Min==22500,"observed consumption uncertainty and unit allocation");
        config.Pricing.MinimumOrderCop=100000;
        Check(EstimationEngine.Calculate(brief,config,1,1,samples,"standard").TotalPriceCop!.Min==100000,"minimum applies per order");config.Pricing.MinimumOrderCop=0;
        Check(EstimationEngine.Calculate(brief,config,1,1,samples.Take(2).ToArray(),"standard").TotalPriceCop is null,"insufficient samples never invent a price");
        Check(EstimationEngine.Calculate(brief,config,1,1,samples,"complex").TotalPriceCop is null,"complexity requires matching calibration");
        brief.WidthMm=101;Check(EstimationEngine.Calculate(brief,config,1,1,samples,"standard").TotalPriceCop is null,"no extrapolation outside observed dimensions");brief.WidthMm=100;
        Role("Admin");
        var current=await client.GetFromJsonAsync<JsonElement>("/api/manufacturing");
        var version=current.GetProperty("version").GetInt32();
        Check((await client.PutAsJsonAsync("/api/manufacturing",new{expectedVersion=version,configuration=config})).IsSuccessStatusCode,"publish estimation manufacturing conditions");version++;
        Role("Client");
        Check((await client.GetAsync("/api/estimation/calibration")).StatusCode==HttpStatusCode.Forbidden,"calibration is private to administrators");
        Guid conversationId,proposalId;string imageFile;
        using(var scope=services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var c=new ChatConversation{UserId=1,BriefJson=DesignPolicy.Write(brief)};db.ChatConversations.Add(c);await db.SaveChangesAsync();conversationId=c.Id;
            imageFile=await scope.ServiceProvider.GetRequiredService<ProposalImageStore>().SaveAsync(VisualChecks.Png(),CancellationToken.None);
            var p=new VisualProposal{ConversationId=c.Id,Number=1,Status="Completed",BriefJson=c.BriefJson,BriefFingerprint=VisualPolicy.Fingerprint(c.BriefJson),ImageFile=imageFile,
                ManufacturingRevisionId=(await db.ManufacturingRevisions.OrderByDescending(r=>r.Version).FirstAsync()).Id,SettingsRevisionId=(await db.VisualSettingsRevisions.FirstAsync()).Id};
            db.VisualProposals.Add(p);await db.SaveChangesAsync();proposalId=p.Id;
        }
        var path=$"/api/estimation/conversations/{conversationId}";
        var uncalibrated=await client.GetFromJsonAsync<JsonElement>(path);
        Check(uncalibrated.GetProperty("estimate").GetProperty("status").GetString()=="ReviewRequired","uncalibrated API routes to manual review");
        Role("Admin");
        Check((await client.PutAsJsonAsync("/api/estimation/calibration",new{expectedVersion=0,manufacturingVersion=version,samples})).IsSuccessStatusCode,"publish versioned calibration");
        Check((await client.PutAsJsonAsync("/api/estimation/calibration",new{expectedVersion=0,manufacturingVersion=version,samples})).StatusCode==HttpStatusCode.Conflict,"stale calibration write rejected");
        samples[0].Grams=-1;
        Check((await client.PutAsJsonAsync("/api/estimation/calibration",new{expectedVersion=1,manufacturingVersion=version,samples})).StatusCode==HttpStatusCode.BadRequest,"invalid nested sample rejected");samples[0].Grams=100;
        Role("Client");
        Check((await client.GetAsync(path+"?complexity=unknown")).StatusCode==HttpStatusCode.BadRequest,"invalid complexity rejected");
        var response=await client.GetStringAsync(path);var estimate=JsonSerializer.Deserialize<JsonElement>(response);
        Check(!response.Contains("Secret")&&!response.Contains("costPerKg")&&!response.Contains("printerId")&&estimate.GetProperty("estimate").GetProperty("averageTotalCop").GetDecimal()==60000,"public estimate excludes equipment and internal cost rates");
        var request=new{requestId=Guid.NewGuid(),proposalId,estimateToken=estimate.GetProperty("token").GetString(),complexity="standard"};
        var submitted=await client.PostAsJsonAsync(path+"/quotes",request);
        Check(submitted.IsSuccessStatusCode,"create quote from current proposal and backend estimate");
        var quoteId=(await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("quoteId").GetInt32();
        var repeated=await client.PostAsJsonAsync(path+"/quotes",request);
        Check((await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("quoteId").GetInt32()==quoteId,"quote submission retries return same quote");
        var manual=await client.GetFromJsonAsync<JsonElement>(path+"?complexity=simple");
        Check((await client.PostAsJsonAsync(path+"/quotes",new{requestId=Guid.NewGuid(),proposalId,estimateToken=manual.GetProperty("token").GetString(),complexity="simple"})).IsSuccessStatusCode,"uncalibrated proposal can request manual quote without a numerical price");
        Check((await client.PostAsJsonAsync(path+"/quotes",new{requestId=Guid.NewGuid(),proposalId,estimateToken=new string('0',64),complexity="standard"})).StatusCode==HttpStatusCode.Conflict,"fabricated estimate token rejected");
        var detailPath=$"/api/estimation/quotes/{quoteId}";
        Check((await client.GetAsync(detailPath+"/image")).IsSuccessStatusCode,"owner can download quoted private image");
        Check((await client.PutAsJsonAsync(detailPath+"/review",new{expectedVersion=0,priceCop=65000,notes="Confirmado"})).StatusCode==HttpStatusCode.Forbidden,"client cannot confirm price");
        Role("Client","2");
        Check((await client.GetAsync(path)).StatusCode==HttpStatusCode.NotFound&&(await client.GetAsync(detailPath)).StatusCode==HttpStatusCode.NotFound&&(await client.GetAsync(detailPath+"/image")).StatusCode==HttpStatusCode.NotFound,"other user cannot access estimate, snapshot or image");
        Role("Admin");
        Check((await client.PostAsJsonAsync($"/api/quotes/{quoteId}/convert",new{price=1})).StatusCode==HttpStatusCode.Conflict,"unreviewed design cannot become an order");
        Check((await client.PutAsJsonAsync(detailPath+"/review",new{expectedVersion=0,priceCop=65000,notes="Confirmado, entrega acordada"})).IsSuccessStatusCode,"admin confirms price and conditions");
        Check((await client.PutAsJsonAsync(detailPath+"/review",new{expectedVersion=0,priceCop=1,notes="Cambio"})).StatusCode==HttpStatusCode.Conflict,"stale review cannot overwrite confirmation");
        Check((await client.PostAsJsonAsync($"/api/quotes/{quoteId}/convert",new{price=1})).StatusCode==HttpStatusCode.Conflict,"order cannot override confirmed price");
        var conversion=await client.PostAsJsonAsync($"/api/quotes/{quoteId}/convert",new{title="Caja confirmada"});
        Check(conversion.IsSuccessStatusCode,"confirmed quote converts using saved price");
        config.Pricing.MachineHourCop=9000;
        Check((await client.PutAsJsonAsync("/api/manufacturing",new{expectedVersion=version,configuration=config})).IsSuccessStatusCode,"tariff update published");
        Role("Client");
        var frozen=await client.GetFromJsonAsync<JsonElement>(detailPath);
        Check(frozen.GetProperty("snapshot").GetProperty("estimate").GetProperty("averageTotalCop").GetDecimal()==60000&&frozen.GetProperty("confirmedPriceCop").GetDecimal()==65000,"original estimate and confirmed price survive tariff changes");
        Check((await client.PostAsJsonAsync(path+"/quotes",new{requestId=Guid.NewGuid(),proposalId,request.estimateToken,complexity="standard"})).StatusCode==HttpStatusCode.Conflict,"stale estimate token rejected after tariff change");
        var changed=await client.GetFromJsonAsync<JsonElement>(path);
        Check(changed.GetProperty("estimate").GetProperty("status").GetString()=="ReviewRequired","changed manufacturing conditions require recalibration");
        Check((await client.PostAsJsonAsync(path+"/quotes",new{requestId=Guid.NewGuid(),proposalId,estimateToken=changed.GetProperty("token").GetString(),complexity="standard"})).StatusCode==HttpStatusCode.Conflict,"outdated proposal rejected even with current estimate token");
        using(var scope=services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Check((await db.Orders.SingleAsync(o=>o.QuoteRequestId==quoteId)).Price==65000,"order persists confirmed price");
            scope.ServiceProvider.GetRequiredService<ProposalImageStore>().Delete(imageFile);
        }
    }
}
