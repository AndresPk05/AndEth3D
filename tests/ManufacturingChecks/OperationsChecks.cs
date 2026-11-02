using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.EntityFrameworkCore;
static class OperationsChecks
{
    public static async Task Run(HttpClient client,IServiceProvider services)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
        void Role(string role){client.DefaultRequestHeaders.Remove("X-Test-Role");client.DefaultRequestHeaders.Add("X-Test-Role",role);client.DefaultRequestHeaders.Remove("X-Test-User");}
        Role("Client");Check((await client.GetAsync("/api/operations/dashboard")).StatusCode==HttpStatusCode.Forbidden,"operational metrics restricted to admins");
        Role("Admin");Check((await client.GetAsync("/api/operations/dashboard?days=366")).StatusCode==HttpStatusCode.BadRequest,"metrics window bounded");
        var before=await client.GetFromJsonAsync<JsonElement>("/api/operations/dashboard");
        var brief=new DesignBrief{Object="Caja",Use="Organizar",CategoryId="cajas",MaterialId="pla",WidthMm=100,DepthMm=80,HeightMm=60,Quantity=2,Colors=["Negro"],Personalization="Sin personalización"};
        var estimate=new DesignEstimate("Indicative","Orientativo","test",1,1,"standard",2,new(90,110),new(1,3),new(400,600),new(800,1200),1000);
        int revisionVersion;var orders=new List<int>();
        using(var scope=services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var revision=await db.ManufacturingRevisions.OrderByDescending(r=>r.Version).FirstAsync();revisionVersion=revision.Version;
            var settings=(await db.VisualSettingsRevisions.FirstAsync()).Id;
            var inactive=new ChatConversation{UserId=1,CreatedAt=DateTime.UtcNow.AddDays(-20),UpdatedAt=DateTime.UtcNow.AddDays(-10)};
            var active=new ChatConversation{UserId=1,CreatedAt=DateTime.UtcNow.AddDays(-20),UpdatedAt=DateTime.UtcNow.AddDays(-1)};
            var pending=new ChatConversation{UserId=1,CreatedAt=DateTime.UtcNow.AddDays(-20),UpdatedAt=DateTime.UtcNow.AddDays(-15)};
            var quoted=new ChatConversation{UserId=1,CreatedAt=DateTime.UtcNow.AddDays(-20),UpdatedAt=DateTime.UtcNow.AddDays(-15),BriefJson=DesignPolicy.Write(brief)};
            db.ChatConversations.AddRange(inactive,active,pending,quoted);await db.SaveChangesAsync();
            var first=new VisualProposal{Id=Guid.NewGuid(),ConversationId=quoted.Id,Number=1,ManufacturingRevisionId=revision.Id,SettingsRevisionId=settings,Status="Failed",CreatedAt=DateTime.UtcNow.AddDays(-20)};
            var second=new VisualProposal{Id=Guid.NewGuid(),ConversationId=quoted.Id,Number=2,ParentProposalId=first.Id,ManufacturingRevisionId=revision.Id,SettingsRevisionId=settings,Status="Completed",CompletedAt=DateTime.UtcNow,ReservedUsd=3,
                AttemptsJson=ParametricData.Write(new[]{new VisualAttempt{Number=1,ReportedCostUsd=.1m,StartedAt=DateTime.UtcNow},new VisualAttempt{Number=2,StartedAt=DateTime.UtcNow}})};
            db.VisualProposals.AddRange(first,second,new(){Id=Guid.NewGuid(),ConversationId=pending.Id,Number=1,ManufacturingRevisionId=revision.Id,SettingsRevisionId=settings,Status="Queued",CreatedAt=DateTime.UtcNow.AddDays(-20)});
            db.ChatTurns.AddRange(new(){Id=Guid.NewGuid(),ConversationId=quoted.Id,Sequence=1,ReservedUsd=1},new(){Id=Guid.NewGuid(),ConversationId=quoted.Id,Sequence=2,ReservedUsd=2,ReportedCostUsd=.5m});
            await db.SaveChangesAsync();
            for(int i=0;i<3;i++){
                var q=new QuoteRequest{UserId=1,Status="Converted",ManufacturingRevisionId=revision.Id,ProposalId=i==0?second.Id:null,ConfirmedPriceCop=1200,
                    DesignSnapshotJson=ParametricData.Write(new{brief,estimate})};db.QuoteRequests.Add(q);await db.SaveChangesAsync();
                var o=new Order{UserId=1,QuoteRequestId=q.Id,Title="Producción comprobada",Status=i==2?"Printing":"Finished",Price=1200};db.Orders.Add(o);await db.SaveChangesAsync();orders.Add(o.Id);
            }
            db.QuoteRequests.Add(new(){UserId=1,ProposalId=first.Id,ManufacturingRevisionId=revision.Id});await db.SaveChangesAsync();
        }
        var after=await client.GetFromJsonAsync<JsonElement>("/api/operations/dashboard");
        decimal N(JsonElement j,string group,string key)=>j.GetProperty(group).GetProperty(key).GetDecimal();
        Check(N(after,"funnel","conversations")==N(before,"funnel","conversations")+4&&N(after,"funnel","quoted")==N(before,"funnel","quoted")+1&&N(after,"funnel","converted")==N(before,"funnel","converted")+1,"funnel counts conversations once despite multiple quotes");
        Check(N(after,"funnel","inactiveWithoutQuote")==N(before,"funnel","inactiveWithoutQuote")+1,"inactivity excludes recent activity and pending work");
        Check(N(after,"ai","chatReportedUsd")==N(before,"ai","chatReportedUsd")+.5m&&N(after,"ai","chatUnknownCosts")==N(before,"ai","chatUnknownCosts")+1&&N(after,"ai","chatReservedUsd")==N(before,"ai","chatReservedUsd")+3,"AI reported costs remain separate from reservations and unknown costs");
        Check(N(after,"ai","imagesReportedUsd")==N(before,"ai","imagesReportedUsd")+.1m&&N(after,"ai","regenerations")==N(before,"ai","regenerations")+1&&N(after,"ai","edits")==N(before,"ai","edits")+1,"image attempt costs and regeneration subsets counted correctly");
        Check(N(after,"confirmedQuotes","withEstimate")==N(before,"confirmedQuotes","withEstimate")+3,"confirmed price deviation covers quotes before production measurement");
        RecordProductionRequest Request()=>new(Guid.NewGuid(),revisionVersion,"machine","cajas","pla","standard",2,220,5,1,1,100,200,100,50,"Trabajo real con purga y soportes incluidos");
        var request=Request();
        Role("Client");Check((await client.PostAsJsonAsync($"/api/operations/orders/{orders[0]}/production",request)).StatusCode==HttpStatusCode.Forbidden,"clients cannot register internal actual costs");Role("Admin");
        Check((await client.PostAsJsonAsync($"/api/operations/orders/{orders[2]}/production",Request())).StatusCode==HttpStatusCode.Conflict,"unfinished order cannot record production");
        Check((await client.PostAsJsonAsync($"/api/operations/orders/{orders[0]}/production",request with{Quantity=3})).StatusCode==HttpStatusCode.BadRequest,"actual quantities must match the accepted quote");
        Check((await client.PostAsJsonAsync($"/api/operations/orders/{orders[0]}/production",request with{ManufacturingVersion=0})).StatusCode==HttpStatusCode.Conflict,"stale manufacturing context rejected");
        Check((await client.PostAsJsonAsync($"/api/operations/orders/{orders[0]}/production",request)).IsSuccessStatusCode,"actual production recorded");
        Check((await client.PostAsJsonAsync($"/api/operations/orders/{orders[0]}/production",request)).IsSuccessStatusCode,"production submission idempotent");
        Check((await client.PostAsJsonAsync($"/api/operations/orders/{orders[0]}/production",Request())).StatusCode==HttpStatusCode.Conflict,"production evidence cannot be overwritten");
        var dashboard=await client.GetFromJsonAsync<JsonElement>("/api/operations/dashboard");var row=dashboard.GetProperty("production").EnumerateArray().Single(x=>x.GetProperty("record").GetProperty("orderId").GetInt32()==orders[0]);
        Check(row.GetProperty("actualCostCop").GetDecimal()==450&&row.GetProperty("marginPercent").GetDecimal()==62.5m&&row.GetProperty("priceDeviationPercent").GetDecimal()==20&&row.GetProperty("gramsDeviationPercent").GetDecimal()==10&&row.GetProperty("hoursDeviationPercent").GetDecimal()==25,"cost, margin and estimate deviations computed from frozen values");
        Check(OperationsMetrics.Percent(2,0) is null&&OperationsMetrics.Percent(null,2) is null,"zero or missing baselines are not reported as zero deviation");
        var next=Request();await client.PostAsJsonAsync($"/api/operations/orders/{orders[1]}/production",next);
        Check((await client.PostAsJsonAsync($"/api/operations/orders/{orders[0]}/production",next)).StatusCode==HttpStatusCode.Conflict,"cross-order reused request id safely rejected");
        using(var scope=services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var current=await db.ManufacturingRevisions.OrderByDescending(r=>r.Version).FirstAsync();
            var latest=await db.CalibrationRevisions.OrderByDescending(r=>r.Version).FirstAsync();
            db.CalibrationRevisions.Add(new(){Version=latest.Version+1,ManufacturingRevisionId=current.Id,CreatedByUserId=1,SamplesJson="[]"});await db.SaveChangesAsync();
            var r=await db.ProductionRecords.SingleAsync(r=>r.Id==request.RequestId);var sample=OperationsMetrics.Sample(r,current.Id)!;
            Check(sample.Grams==110&&sample.MachineHours==2.5m&&sample.DesignHours==1&&sample.FinishingHours==.5m,"production calibration normalized per unit with design once");
            Check((await db.Orders.FindAsync(orders[0]))!.Price==1200,"recording actual costs does not change customer price");
        }
        var candidates=await client.GetFromJsonAsync<JsonElement>("/api/operations/calibration");
        var promote=new{expectedCalibrationVersion=candidates.GetProperty("calibrationVersion").GetInt32(),manufacturingVersion=revisionVersion,recordIds=new[]{request.RequestId,next.RequestId}};
        Check((await client.PostAsJsonAsync("/api/operations/calibration",promote)).IsSuccessStatusCode,"reviewed production creates a new calibration revision");
        Check((await client.PostAsJsonAsync("/api/operations/calibration",promote)).StatusCode==HttpStatusCode.Conflict,"stale calibration promotion rejected");
        var refreshed=await client.GetFromJsonAsync<JsonElement>("/api/operations/calibration");
        Check(refreshed.GetProperty("candidates").GetArrayLength()==0,"promoted samples not suggested twice");
        Role("Client");var publicQuotes=await client.GetStringAsync("/api/quotes/mine");Check(!publicQuotes.Contains("materialCostCop")&&!publicQuotes.Contains("actualCostCop")&&!publicQuotes.Contains("printerId"),"actual internal cost and routing data absent from client quotes");
        RoutingChecks(Check);
    }
    static void RoutingChecks(Action<bool,string> check)
    {
        var b=new DesignBrief{MaterialId="petg",WidthMm=100,DepthMm=80,HeightMm=60};var parameters=new ParametricParameters("box",2,2);
        var config=new ManufacturingConfiguration{Printers=[new(){Id="large",Active=true,WidthMm=300,DepthMm=300,HeightMm=300,MaterialIds=["petg"]},new(){Id="small",Active=true,WidthMm=180,DepthMm=180,HeightMm=180,MaterialIds=["petg"]}]};
        SlicingProfile Profile(string id)=>new(){Id=id,PrinterId=id,MaterialId="petg",ProfilePath=Path.GetFullPath("test.ini"),ProfileSha256=new string('a',64),Image="test/image@sha256:"+new string('a',64),EngineVersion="PrusaSlicer-2.9.3",TotalsVerified=true};
        var large=Profile("large");var small=Profile("small");
        check(ParametricRouting.Candidates([large,small],config,b,parameters,new Dictionary<string,int>()).First().Id=="small","routing supports added material and selects smallest suitable volume on ties");
        check(ParametricRouting.Candidates([large,small],config,b,parameters,new Dictionary<string,int>{{"small",2}}).First().Id=="large","routing considers active slicing load");
        large.Priority=1;check(ParametricRouting.Candidates([small,large],config,b,parameters,new Dictionary<string,int>()).First().Id=="large","operator routing priority honored");
        config.Printers[0].Active=false;check(ParametricRouting.Candidates([large,small],config,b,parameters,new Dictionary<string,int>()).Single().Id=="small","disabled printer never selected despite priority");
        var identity=ParametricRouting.Identity(small);small.Priority=1;small.ProfilePath=Path.GetFullPath("other.ini");check(identity==ParametricRouting.Identity(small),"routing priority and deployment paths do not invalidate historical slicing identity");
        config.Categories=[new(){Id="marcos",Name="Marcos"}];config.Materials=[new(){Id="petg",Name="PETG",Active=true,Colors=[new(){Name="Negro",Available=true}]}];
        b.Object="Marco";b.Use="Decoración";b.CategoryId="marcos";b.Quantity=1;b.Colors=["Negro"];b.Personalization="Sin personalización";
        check(ParametricGeometry.Validate(b,new("frame",2,2),config) is null,"new frame template accepts a configured additional material");
    }
}
