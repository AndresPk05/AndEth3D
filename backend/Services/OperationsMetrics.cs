using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Services;
public static class OperationsMetrics
{
    public static decimal? Percent(decimal? actual,decimal? baseline)=>actual is not null&&baseline>0?decimal.Round((actual.Value-baseline.Value)/baseline.Value*100,2):null;
    public static decimal Cost(ProductionRecord r)=>r.MaterialCostCop+r.MachineCostCop+r.LaborCostCop+r.OtherCostCop;
    public static decimal? Margin(ProductionRecord r)=>r.ConfirmedTotalCop>0?decimal.Round((r.ConfirmedTotalCop.Value-Cost(r))/r.ConfirmedTotalCop.Value*100,2):null;
    public static CalibrationSample? Sample(ProductionRecord r,int revisionId)
    {
        if(r.ManufacturingRevisionId!=revisionId||r.Quantity<=0||r.ColorCount is null||r.WidthMm is null||r.DepthMm is null||r.HeightMm is null||
            r.Grams/r.Quantity is <.01m or >100000 || r.MachineHours/r.Quantity is <.01m or >10000 ||r.FinishingHours/r.Quantity>1000)return null;
        return new(){CategoryId=r.CategoryId,MaterialId=r.MaterialId,PrinterId=r.PrinterId,Complexity=r.Complexity,ColorCount=r.ColorCount.Value,
            WidthMm=r.WidthMm.Value,DepthMm=r.DepthMm.Value,HeightMm=r.HeightMm.Value,Grams=r.Grams/r.Quantity,MachineHours=r.MachineHours/r.Quantity,
            DesignHours=r.DesignHours,FinishingHours=r.FinishingHours/r.Quantity,Evidence=$"production:{r.Id}"};
    }
}
