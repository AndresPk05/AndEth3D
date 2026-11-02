using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Services;
public static class ParametricRouting
{
    public const string Policy="priority-load-volume-v1";
    public static string Identity(SlicingProfile p)=>ParametricData.Write(new{p.Id,p.PrinterId,p.MaterialId,p.ProfileSha256,p.Image,p.EngineVersion,p.DesignHours,p.FinishingHours,p.TotalsVerified,p.UnreportedPurgeGrams});
    public static IEnumerable<SlicingProfile> Candidates(IEnumerable<SlicingProfile> profiles,ManufacturingConfiguration config,DesignBrief brief,ParametricParameters parameters,IReadOnlyDictionary<string,int> activeLoads)=>
        profiles.Where(p=>p.Valid()&&p.MaterialId==brief.MaterialId&&config.Printers.Any(m=>m.Id==p.PrinterId&&ParametricGeometry.Fits(brief,parameters,m)))
            .OrderBy(p=>p.Priority).ThenBy(p=>activeLoads.GetValueOrDefault(p.PrinterId))
            .ThenBy(p=>{var m=config.Printers.Single(m=>m.Id==p.PrinterId);return (m.WidthMm-2*m.MarginMm)*(m.DepthMm-2*m.MarginMm)*(m.HeightMm-m.MarginMm);})
            .ThenBy(p=>p.Id,StringComparer.Ordinal);
}
