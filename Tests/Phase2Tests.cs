using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BimComparison.Core;
internal static class Phase2Tests
{
    private static ModelSnapshot Clone(ModelSnapshot a)=>Newtonsoft.Json.JsonConvert.DeserializeObject<ModelSnapshot>(Newtonsoft.Json.JsonConvert.SerializeObject(a));
    private static PointData P(double x,double y=0,double z=0)=>new PointData{X=x,Y=y,Z=z};
    private static ConnectorData Port(double x,string reference)=>new ConnectorData{Domain="DomainPiping",Type="End",Shape="Round",Origin=P(x),Direction=P(1),Radius=0.1,References=new List<string>{reference}};
    private static ModelSnapshot Model()
    {
        var a=new ModelSnapshot{SchemaVersion="2.0",Scope="Phase2-Geometry-MEP-Connectors",ModelPath="Phase2_A.rvt",ProjectUniqueId="project"};
        a.Settings.Categories.Add(-2008044);a.Settings.Geometry=true;a.Settings.Mep=true;a.Settings.Connectors=true;
        a.Elements.Add(new ElementSnapshot{UniqueId="pipe",ElementId=10,CategoryId=-2008044,Category="Pipes",
            Geometry=new GeometrySnapshot{Status="Complete",Mode="Detailed",Volume=1,SurfaceArea=6,SolidCount=1,Minimum=P(0),Maximum=P(2,2,2),Center=P(1,1,1),
                Triangles={new TriangleData{A=P(0),B=P(1),C=P(0,1)},new TriangleData{A=P(0,0,1),B=P(1,0,1),C=P(0,1,1)}},
                Curves={new List<PointData>{P(0),P(1,1),P(2)}}},
            Mep=new MepSnapshot{Status="Complete",Systems={new SystemSnapshot{UniqueId="system-a",Name="Supply",TypeUniqueId="supply-type"},new SystemSnapshot{UniqueId="system-b",Name="Return",TypeUniqueId="return-type"}},Dimensions={{"PipeDiameter",0.2},{"Length",2}}},
            Connectors=new ConnectorSnapshot{Status="Complete",Items={Port(0,"fitting-a|DomainPiping|End"),Port(2,"fitting-b|DomainPiping|End")}}});
        return a;
    }
    private static ComparisonReport Compare(ModelSnapshot a,ModelSnapshot b)=>new ModelComparisonEngine().Compare(a,b,a.Settings);
    private static bool Has(ComparisonReport r,string classification)=>r.ModifiedElements.Any(e=>e.Changes.Any(c=>c.ChangeType==classification));
    private static void Reject(Action action,Action<bool,string> check,string name)
    {try{action();}catch(InvalidDataException){check(true,name);return;}throw new Exception("FAIL: "+name);}
    internal static void Run(Action<bool,string> check,string folder)
    {
        var a=Model();var b=Clone(a);
        check(Compare(a,b).UnchangedElements.Count==1,"P2 identical geometry/MEP/connectors");
        b.Elements[0].Geometry.Triangles.Reverse();foreach(var t in b.Elements[0].Geometry.Triangles){var p=t.A;t.A=t.C;t.C=p;}
        b.Elements[0].Geometry.Curves[0].Reverse();b.Elements[0].Mep.Systems.Reverse();b.Elements[0].Connectors.Items.Reverse();
        check(Compare(a,b).UnchangedElements.Count==1,"P2 geometry/connector/system order independence");
        check(GeometryComparison.Fingerprint(a.Elements[0].Geometry)==GeometryComparison.Fingerprint(b.Elements[0].Geometry),"P2 deterministic fingerprint");
        b=Clone(a);b.Elements[0].Geometry.Triangles[0].B.X+=a.Settings.LengthToleranceFeet/2;
        check(Compare(a,b).UnchangedElements.Count==1,"P2 geometry within tolerance despite different exact hash");
        b=Clone(a);b.Elements[0].Geometry.Triangles[0].B.X+=0.1;
        check(Has(Compare(a,b),"GeometryChanged"),"P2 shape change with identical bounding box/volume/area");
        b=Clone(a);b.Elements[0].Geometry.Curves[0][1].Y+=0.1;
        check(Has(Compare(a,b),"GeometryChanged"),"P2 curve interior shape change with unchanged endpoints");
        b=Clone(a);b.Elements[0].Geometry.Volume+=a.Settings.VolumeToleranceCubicFeet/2;b.Elements[0].Geometry.SurfaceArea+=a.Settings.AreaToleranceSquareFeet/2;
        check(Compare(a,b).UnchangedElements.Count==1,"P2 area and volume tolerance");
        b=Clone(a);b.Elements[0].Mep.Systems[0].TypeUniqueId="different-type";
        check(Has(Compare(a,b),"MEPSystemChanged"),"P2 system type change");
        b=Clone(a);b.Elements[0].Mep.Systems[0].Name="Renamed";
        check(Has(Compare(a,b),"MEPSystemChanged"),"P2 system rename");
        b=Clone(a);b.Elements[0].Mep.Dimensions["PipeDiameter"]+=0.1;
        check(Has(Compare(a,b),"MEPPropertyChanged"),"P2 dedicated MEP dimensions");
        b=Clone(a);b.Elements[0].Connectors.Items[0].Origin.X+=a.Settings.LengthToleranceFeet/2;
        b.Elements[0].Connectors.Items[0].Radius+=a.Settings.LengthToleranceFeet/2;
        check(Compare(a,b).UnchangedElements.Count==1,"P2 connector position/size tolerance");
        b=Clone(a);b.Elements[0].Connectors.Items[0].Direction=P(-1);
        check(Has(Compare(a,b),"ConnectorChanged"),"P2 connector direction reversal");
        b=Clone(a);b.Elements[0].Connectors.Items[0].Radius+=0.1;
        check(Has(Compare(a,b),"ConnectorChanged"),"P2 connector resize");
        b=Clone(a);b.Elements[0].Connectors.Items[0].References[0]="different-owner";
        check(Has(Compare(a,b),"ConnectorChanged"),"P2 connection topology change");
        b=Clone(a);b.Elements[0].Connectors.Items.RemoveAt(0);
        check(Has(Compare(a,b),"ConnectorChanged"),"P2 connector count change");
        b=Clone(a);b.Elements[0].Connectors.Items[0].ConnectedPorts.Add(new ConnectedPortReference{OwnerUniqueId="same-fitting",Origin=P(1)});
        var prior=Clone(b);b.Elements[0].Connectors.Items[0].ConnectedPorts[0].Origin=P(2);
        check(Has(Compare(prior,b),"ConnectorChanged"),"P2 connection to different port on the same owner");
        b=Clone(a);b.Elements[0].Geometry.Status="Partial";b.Elements[0].Geometry.Errors.Add("unsupported geometry");
        check(Has(Compare(a,b),"ExtractionIncomplete"),"P2 partial extraction never silently unchanged");
        b=Clone(a);b.Elements[0].Geometry=null;Reject(()=>Compare(a,b),check,"P2 missing advanced data rejected");
        b=Clone(a);b.Settings.GeometryMode="Lightweight";Reject(()=>Compare(a,b),check,"P2 geometry mode mismatch rejected");
        b=Clone(a);b.Elements[0].Geometry.Triangles[0].A.X=double.NaN;Reject(()=>Compare(a,b),check,"P2 nonfinite geometry rejected");
        b=Clone(a);b.SchemaVersion="1.0";b.Scope="Phase1-Identity-Parameters-Location";b.Settings.Geometry=false;b.Settings.Mep=false;b.Settings.Connectors=false;
        Reject(()=>Compare(a,b),check,"P2 legacy snapshot cannot pretend to have advanced data");
        var common=Clone(b); // explicit common scope comparison retains legacy compatibility
        common.Settings.Geometry=false;common.Settings.Mep=false;common.Settings.Connectors=false;
        check(new ModelComparisonEngine().Compare(a,b,common.Settings).UnchangedElements.Count==1,"P2 mixed schemas common scope comparison");
        var lightweight=Clone(a);lightweight.Settings.GeometryMode="Lightweight";lightweight.Elements[0].Geometry.Mode="Lightweight";lightweight.Elements[0].Geometry.Triangles.Clear();lightweight.Elements[0].Geometry.Curves.Clear();
        var lightB=Clone(lightweight);lightB.Elements[0].Geometry.Volume+=0.1;
        check(Has(Compare(lightweight,lightB),"GeometryChanged"),"P2 lightweight detects volume change");
        check(GeometryComparison.UnorderedEqual(new[]{1,2},new[]{1,2},(x,y)=>x==1||y==1),"P2 ambiguous tolerance candidates use full bipartite matching");
        b=Clone(a);b.ModelPath="Phase2_B.rvt";b.Elements[0].Geometry.Triangles[0].B.X+=0.1;b.Elements[0].Mep.Systems[0].Name="Changed supply";b.Elements[0].Connectors.Items[0].References[0]="replacement-fitting";
        Directory.CreateDirectory(folder);JsonSerializerService.Write(Path.Combine(folder,"ModelA.json"),a);JsonSerializerService.Write(Path.Combine(folder,"ModelB.json"),b);
        var report=Compare(JsonSerializerService.Read<ModelSnapshot>(Path.Combine(folder,"ModelA.json")),JsonSerializerService.Read<ModelSnapshot>(Path.Combine(folder,"ModelB.json")));
        ComparisonReportService.Export(Path.Combine(folder,"Comparison.json"),report);ComparisonReportService.Export(Path.Combine(folder,"Comparison.csv"),report);
        check(Has(report,"GeometryChanged")&&Has(report,"MEPSystemChanged")&&Has(report,"ConnectorChanged"),"P2 JSON roundtrip and detailed report");
    }
}
