using System.Net;
using System.Net.Http.Json;
using System.Numerics;
using System.Text;
using System.Text.Json;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.EntityFrameworkCore;

class FakeParametricSlicer:IParametricSlicer
{
    public int Calls {get;private set;}
    public bool Fail {get;set;}
    public Task<SliceResult> Slice(byte[] stl,string ini,SlicingProfile profile,decimal x,decimal y,CancellationToken ct)
    {
        Calls++;
        if(Fail)throw new OperationCanceledException();
        return Task.FromResult(new SliceResult(100,7200,profile.EngineVersion,new string('a',64)));
    }
}
static class ParametricChecks
{
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static void Mesh(byte[] stl,decimal expectedVolume)
    {
        using var reader=new BinaryReader(new MemoryStream(stl));reader.ReadBytes(80);var count=reader.ReadInt32();
        var edges=new Dictionary<(Vector3,Vector3),int>();double volume=0;
        Vector3 Point()=>new(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
        void Edge(Vector3 a,Vector3 b){edges[(a,b)]=edges.GetValueOrDefault((a,b))+1;}
        var min=new Vector3(float.MaxValue);var max=new Vector3(float.MinValue);
        for(int i=0;i<count;i++){
            var n=Point();var a=Point();var b=Point();var c=Point();reader.ReadUInt16();
            CheckSilent(float.IsFinite(n.X)&&Vector3.Dot(n,Vector3.Cross(b-a,c-a))>0,"outward normals");
            Edge(a,b);Edge(b,c);Edge(c,a);volume+=Vector3.Dot(a,Vector3.Cross(b,c))/6d;
            foreach(var p in new[]{a,b,c}){min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
        }
        Check(edges.All(e=>e.Value==1&&edges.GetValueOrDefault((e.Key.Item2,e.Key.Item1))==1),"template mesh is closed and every edge has opposite winding");
        Check(Math.Abs(volume-(double)expectedVolume)<.2,"template mesh has expected material volume");
        Check(min==Vector3.Zero&&max==new Vector3(100,80,60),"template dimensions and origin match brief");
        static void CheckSilent(bool ok,string label){if(!ok)throw new Exception(label);}
    }
    public static async Task Run(HttpClient client,IServiceProvider services,ParametricOptions options,FakeParametricSlicer slicer)
    {
        var brief=new DesignBrief{Object="Caja",Use="Organizar",CategoryId="cajas",MaterialId="pla",WidthMm=100,DepthMm=80,HeightMm=60,Quantity=2,Colors=["Negro"],Personalization="Sin personalización"};
        var box=new ParametricParameters("box",2,2);var organizer=new ParametricParameters("organizer",2,2,3,2);var plate=new ParametricParameters("plate",2,2);
        foreach(var p in new[]{box,organizer,plate}){
            var bytes=ParametricGeometry.Build(brief,p);
            Mesh(bytes,p.Template=="plate"?480000:480000-(100-(p.Columns+1)*2)*(80-(p.Rows+1)*2)*58);
            Check(bytes.SequenceEqual(ParametricGeometry.Build(brief,p)),"same parameters produce byte-identical STL");
        }
        Check(ParametricProfile.Safe("post_process = \nlayer_height = 0.2\n")&&!ParametricProfile.Safe("post_process = sh bad\n"),"profiles accept empty hooks and reject executable post-processing");
        Check(!ParametricProfile.Safe("\uFEFFpost_process = sh bad\n"),"UTF8 BOM cannot hide a post-processing hook");
        void Role(string role,string user="1"){
            client.DefaultRequestHeaders.Remove("X-Test-Role");client.DefaultRequestHeaders.Add("X-Test-Role",role);
            client.DefaultRequestHeaders.Remove("X-Test-User");client.DefaultRequestHeaders.Add("X-Test-User",user);
        }
        Role("Admin");
        var current=await client.GetFromJsonAsync<JsonElement>("/api/manufacturing");
        var config=new ManufacturingConfiguration{
            Printers=[new(){Id="machine",Name="SecretMachine",Brand="SecretBrand",Model="SecretModel",Active=true,MaterialIds=["pla"]}],
            Materials=[new(){Id="pla",Name="PLA",Active=true,CostPerKgCop=80000,Colors=[new(){Name="Negro",Available=true}]}],
            Categories=[new(){Id="cajas",Name="Cajas"}],Pricing=new(){MachineHourCop=5000,GrossMargin=.2m}
        };
        Check((await client.PutAsJsonAsync("/api/manufacturing",new{expectedVersion=current.GetProperty("version").GetInt32(),configuration=config})).IsSuccessStatusCode,"manufacturing profile for slicing published");
        var ini="post_process = \nlayer_height = 0.2\n";
        var profileFile=Path.Combine(Path.GetTempPath(),"andeth-test-profile-"+Guid.NewGuid().ToString("N")+".ini");
        await File.WriteAllTextAsync(profileFile,ini,new UTF8Encoding(false));
        var profile=new SlicingProfile{Id="test",PrinterId="machine",MaterialId="pla",ProfilePath=profileFile,ProfileSha256=ParametricGeometry.Hash(Encoding.UTF8.GetBytes(ini)),
            EngineVersion="PrusaSlicer-2.9.3",Image="example/slicer@sha256:"+new string('a',64),TotalsVerified=true,UnreportedPurgeGrams=2};
        options.Profiles=[profile];options.Enabled=true;options.MaxPerConversation=20;options.MaxPerUserDay=20;
        Check(options.Valid(),"verified digest-pinned slicer configuration accepted");
        profile.TotalsVerified=false;Check(!options.Valid(),"unverified purge accounting cannot activate a profile");profile.TotalsVerified=true;
        Guid conversation;
        using(var scope=services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var c=new ChatConversation{UserId=1,BriefJson=DesignPolicy.Write(brief)};
            db.ChatConversations.Add(c);await db.SaveChangesAsync();conversation=c.Id;
        }
        var path=$"/api/parametric/conversations/{conversation}";
        async Task<JsonElement> List()=>await client.GetFromJsonAsync<JsonElement>(path);
        async Task<HttpResponseMessage> Create(Guid job,ParametricParameters? p=null,bool consent=true){var state=await List();return await client.PostAsJsonAsync(path,new{requestId=job,expectedFingerprint=state.GetProperty("fingerprint").GetString(),parameters=p??box,acceptTemplate=consent});}
        async Task Process(){using var scope=services.CreateScope();await scope.ServiceProvider.GetRequiredService<ParametricProcessor>().Process(CancellationToken.None);}
        Role("Client");
        Check((await client.GetAsync("/api/parametric/admin")).StatusCode==HttpStatusCode.Forbidden,"clients cannot read slicer profiles or worker data");
        Check((await Create(Guid.NewGuid(),consent:false)).StatusCode==HttpStatusCode.BadRequest,"template consent required");
        Check((await Create(Guid.NewGuid(),new("box",.4m,.4m))).StatusCode==HttpStatusCode.BadRequest,"configured minimum wall enforced");
        var jobId=Guid.NewGuid();
        Check((await Create(jobId)).StatusCode==HttpStatusCode.Accepted&&slicer.Calls==0,"durable model job enqueued without slicing in HTTP request");
        Check((await Create(jobId)).IsSuccessStatusCode,"model submission idempotent");
        Check((await Create(Guid.NewGuid())).StatusCode==HttpStatusCode.Conflict,"one active slicing job per conversation");
        var modelResponse=await client.GetAsync($"/api/parametric/models/{jobId}");
        Check(modelResponse.IsSuccessStatusCode&&modelResponse.Headers.CacheControl!.NoStore,"STL private download available before slicing");
        Role("Client","2");
        Check((await client.GetAsync(path)).StatusCode==HttpStatusCode.NotFound&&(await client.GetAsync($"/api/parametric/models/{jobId}")).StatusCode==HttpStatusCode.NotFound,"models isolated between clients");Role("Client");
        await Process();var listed=await List();var completed=listed.GetProperty("models")[0];
        Check(completed.GetProperty("status").GetString()=="Completed"&&slicer.Calls==1,"worker completes persisted slicing job");
        Check(completed.GetProperty("estimate").GetProperty("gramsPerUnit").GetProperty("min").GetDecimal()==102,"unreported purge correction applied exactly once");
        Check(!listed.ToString().Contains("Secret")&&!listed.ToString().Contains("PrusaSlicer")&&!listed.ToString().Contains("profile.ini"),"public result excludes printer, engine and profile internals");
        var requestId=Guid.NewGuid();
        var submitted=await client.PostAsJsonAsync($"/api/parametric/models/{jobId}/quotes",new{requestId});
        Check(submitted.IsSuccessStatusCode,"sliced template creates reviewable quote");
        var quoteId=(await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("quoteId").GetInt32();
        var retry=await client.PostAsJsonAsync($"/api/parametric/models/{jobId}/quotes",new{requestId});
        Check((await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("quoteId").GetInt32()==quoteId,"sliced quote submission idempotent");
        var frozen=await client.GetFromJsonAsync<JsonElement>($"/api/estimation/quotes/{quoteId}");
        Check(frozen.GetProperty("snapshot").GetProperty("estimate").GetProperty("method").GetString()=="sliced-template-v1"&&frozen.GetProperty("modelUrl").GetString()!.Contains(jobId.ToString()),"quote retains model and sliced conditions");
        Role("Admin");
        var admin=await client.GetFromJsonAsync<JsonElement>("/api/parametric/admin");var jobVersion=admin.GetProperty("jobs")[0].GetProperty("version").GetInt32();
        Check((await client.PutAsJsonAsync($"/api/parametric/models/{jobId}/measurement",new{expectedVersion=jobVersion,grams=110,seconds=7300,notes="Producción de prueba"})).IsSuccessStatusCode,"actual material and time recorded for comparison");
        Check((await client.PutAsJsonAsync($"/api/parametric/models/{jobId}/measurement",new{expectedVersion=jobVersion+1,grams=1,seconds=1,notes="Otra"})).StatusCode==HttpStatusCode.Conflict,"production measurement immutable");
        await client.PutAsJsonAsync($"/api/estimation/quotes/{quoteId}/review",new{expectedVersion=0,priceCop=50000,notes="Validado manualmente"});
        var converted=await client.PostAsJsonAsync($"/api/quotes/{quoteId}/convert",new{title="Modelo"});
        var orderId=(await converted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();
        Check((await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",new{status="InQueue",price=1})).StatusCode==HttpStatusCode.Conflict,"later order updates cannot bypass confirmed quote price");
        Role("Client");slicer.Fail=true;var failedId=Guid.NewGuid();await Create(failedId);await Process();slicer.Fail=false;
        Check((await List()).GetProperty("models")[0].GetProperty("status").GetString()=="ReviewRequired","timeout routes model to manual review");
        var before=slicer.Calls;await Process();Check(slicer.Calls==before,"failed slicing is not automatically duplicated");
        var interrupted=Guid.NewGuid();await Create(interrupted);
        using(var scope=services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var j=await db.ParametricJobs.SingleAsync(j=>j.Id==interrupted);j.Status="Processing";j.LeaseUntil=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();}
        await Process();Check(slicer.Calls==before,"expired lease recovered without repeating slicing");
        var stale=Guid.NewGuid();await Create(stale);
        using(var scope=services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var c=await db.ChatConversations.SingleAsync(c=>c.Id==conversation);brief.WidthMm=110;c.BriefJson=DesignPolicy.Write(brief);c.Version++;await db.SaveChangesAsync();}
        await Process();Check(slicer.Calls==before,"stale queued model skipped before slicing");
        Check((await client.PostAsJsonAsync($"/api/parametric/models/{jobId}/quotes",new{requestId=Guid.NewGuid()})).StatusCode==HttpStatusCode.Conflict,"old model cannot create a current quote");
        options.MaxPerUserDay=1;Check((await Create(Guid.NewGuid())).StatusCode==HttpStatusCode.TooManyRequests,"daily model quota enforced");options.MaxPerUserDay=20;
        options.MaxPerConversation=1;Check((await Create(Guid.NewGuid())).StatusCode==HttpStatusCode.TooManyRequests,"conversation model quota enforced");options.MaxPerConversation=20;
        options.Enabled=false;var manual=Guid.NewGuid();Check((await Create(manual)).IsSuccessStatusCode,"model creation works when slicing is disabled");
        Check((await client.PostAsJsonAsync($"/api/parametric/models/{manual}/quotes",new{requestId=Guid.NewGuid()})).IsSuccessStatusCode,"unsliced template can request manual review without fabricated consumption");
        File.Delete(profileFile);
    }
}
