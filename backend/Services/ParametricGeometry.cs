using System.Numerics;
using System.Security.Cryptography;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Services;

public static class ParametricGeometry
{
    public const string Version = "orthogonal-v2";
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string? Validate(DesignBrief b, ParametricParameters p, ManufacturingConfiguration config)
    {
        if (DesignPolicy.Assess(b,config).Status!="ReadyForReview") return "Completa una ficha compatible antes de crear el modelo.";
        var category=p.Template switch {"box"=>"cajas","plate"=>"placas","organizer"=>"organizadores","frame"=>"marcos",_=>""};
        if(category.Length==0 || category!=b.CategoryId) return "Esta categoría requiere modelado y revisión manual.";
        if(b.Colors!.Count!=1) return "Las plantillas automáticas admiten un solo color. Solicita revisión para varios colores.";
        if(b.Personalization?.Trim().ToLowerInvariant() is not ("sin personalización" or "sin personalizacion" or "ninguna" or "ninguno" or "no" or "sin"))
            return "Textos, logos y otros detalles personalizados requieren modelado manual.";
        if(p.WallMm is <.4m or >10 || p.BaseMm is <.4m or >10 || p.Columns is <1 or >6 || p.Rows is <1 or >6) return "Parámetros fuera del rango admitido.";
        if(p.Template!="organizer" && (p.Columns!=1 || p.Rows!=1)) return "Solo el organizador admite divisiones.";
        if(p.Template!="plate" && (b.WidthMm-(p.Columns+1)*p.WallMm<2*p.Columns || b.DepthMm-(p.Rows+1)*p.WallMm<2*p.Rows || (p.Template!="frame"&&b.HeightMm<=p.BaseMm)))
            return "Las paredes, divisiones y base no dejan espacio interior suficiente.";
        return null;
    }
    public static bool Fits(DesignBrief b,ParametricParameters p,PrinterProfile machine) => machine.Active && machine.MaterialIds.Contains(b.MaterialId!) &&
        b.WidthMm<=machine.WidthMm-2*machine.MarginMm && b.DepthMm<=machine.DepthMm-2*machine.MarginMm && b.HeightMm<=machine.HeightMm-machine.MarginMm &&
        (p.Template=="plate" ? b.HeightMm>=machine.MinimumWallMm : p.WallMm>=machine.MinimumWallMm && (p.Template=="frame" ? b.HeightMm>=machine.MinimumWallMm : p.BaseMm>=machine.MinimumWallMm));

    // Union of axis-aligned cells. Shared interior faces are removed; all surface
    // triangles share the same coordinate grid, avoiding coplanar overlaps/T-junctions.
    public static byte[] Build(DesignBrief b,ParametricParameters p)
    {
        decimal w=b.WidthMm!.Value,d=b.DepthMm!.Value,h=b.HeightMm!.Value,t=p.WallMm,z=p.BaseMm;
        List<(decimal Start,decimal End)> Walls(decimal length,int count) {
            var ranges=new List<(decimal,decimal)>{(0,t),(length-t,length)};
            for(int i=1;i<count;i++){var center=t+(length-2*t)*i/count;ranges.Add((center-t/2,center+t/2));}
            return ranges;
        }
        var wx=Walls(w,p.Template=="organizer"?p.Columns:1);var wy=Walls(d,p.Template=="organizer"?p.Rows:1);
        var xs=p.Template=="plate"?new[]{0m,w}:wx.SelectMany(r=>new[]{r.Start,r.End}).Distinct().Order().ToArray();
        var ys=p.Template=="plate"?new[]{0m,d}:wy.SelectMany(r=>new[]{r.Start,r.End}).Distinct().Order().ToArray();
        var zs=p.Template is "plate" or "frame"?new[]{0m,h}:new[]{0m,z,h};
        int nx=xs.Length-1,ny=ys.Length-1,nz=zs.Length-1;
        bool Solid(int x,int y,int k) => x>=0&&x<nx&&y>=0&&y<ny&&k>=0&&k<nz &&
            (p.Template=="plate" || (p.Template!="frame"&&k==0) || wx.Any(r=>xs[x]>=r.Start&&xs[x+1]<=r.End) || wy.Any(r=>ys[y]>=r.Start&&ys[y+1]<=r.End));
        var triangles=new List<(Vector3 A,Vector3 B,Vector3 C)>();
        void Face(Vector3 a,Vector3 b1,Vector3 c,Vector3 d1){triangles.Add((a,b1,c));triangles.Add((a,c,d1));}
        for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)for(int k=0;k<nz;k++)if(Solid(x,y,k)){
            float x0=(float)xs[x],x1=(float)xs[x+1],y0=(float)ys[y],y1=(float)ys[y+1],z0=(float)zs[k],z1=(float)zs[k+1];
            if(!Solid(x-1,y,k))Face(new(x0,y0,z0),new(x0,y0,z1),new(x0,y1,z1),new(x0,y1,z0));
            if(!Solid(x+1,y,k))Face(new(x1,y0,z0),new(x1,y1,z0),new(x1,y1,z1),new(x1,y0,z1));
            if(!Solid(x,y-1,k))Face(new(x0,y0,z0),new(x1,y0,z0),new(x1,y0,z1),new(x0,y0,z1));
            if(!Solid(x,y+1,k))Face(new(x0,y1,z0),new(x0,y1,z1),new(x1,y1,z1),new(x1,y1,z0));
            if(!Solid(x,y,k-1))Face(new(x0,y0,z0),new(x0,y1,z0),new(x1,y1,z0),new(x1,y0,z0));
            if(!Solid(x,y,k+1))Face(new(x0,y0,z1),new(x1,y0,z1),new(x1,y1,z1),new(x0,y1,z1));
        }
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        writer.Write(new byte[80]);writer.Write(triangles.Count);
        void Point(Vector3 v){writer.Write(v.X);writer.Write(v.Y);writer.Write(v.Z);}
        foreach(var(a,b1,c) in triangles){Point(Vector3.Normalize(Vector3.Cross(b1-a,c-a)));Point(a);Point(b1);Point(c);writer.Write((ushort)0);}
        return stream.ToArray();
    }
}
