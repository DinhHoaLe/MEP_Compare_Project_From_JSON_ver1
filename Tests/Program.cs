using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using BimComparison.Core;
internal static class Program
{
    private static int passed;
    private static void Check(bool ok,string name) {if(!ok)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++;}
    private static void Reject(Action action,string name)
    {try{action();}catch(InvalidDataException){Check(true,name);return;}throw new Exception("FAIL: "+name);}
    private static ModelSnapshot Snapshot(string name)
    {
        var s=new ModelSnapshot{ModelName=name,ModelPath=name+".rvt",ProjectUniqueId="same-project",RevitVersion="2023"};
        s.Settings.Categories.Add(-2008044);return s;
    }
    private static ElementSnapshot Element(string id,int number=1)=>new ElementSnapshot {
        UniqueId=id,ElementId=number,CategoryId=-2008044,Category="Pipes",Family="Pipe",Type="Standard",TypeUniqueId="pipe-type",
        Location=new LocationSnapshot{Kind="Point",Point=new PointData{X=1,Y=2,Z=3},Rotation=0}};
    private static ParameterSnapshot Param(string key,string value)=>new ParameterSnapshot {
        Key=key,Name="Diameter",StorageType="Double",HasValue=true,RawValue=value,Spec="autodesk.spec.aec:length-2.0.0",DisplayValue="display",Unit="mm"};
    private static ModelSnapshot Clone(ModelSnapshot s)=>Newtonsoft.Json.JsonConvert.DeserializeObject<ModelSnapshot>(Newtonsoft.Json.JsonConvert.SerializeObject(s));
    private static int Main(string[] args)
    {
        try
        {
            var engine=new ModelComparisonEngine();var a=Snapshot("RevisionA");
            a.Elements.Add(Element("stable"));a.Elements.Add(Element("deleted",2));a.Elements.Add(Element("unchanged",3));
            a.Elements[0].Parameters.Add(Param("instance:builtin:-1","1"));a.Elements[0].Parameters.Add(Param("instance:shared:guid","2"));
            var b=Clone(a);b.ModelName="RevisionB";b.ModelPath="RevisionB.rvt";
            b.Elements.RemoveAt(1);b.Elements.Add(Element("added",4));
            b.Elements[0].Parameters[0].RawValue="1.5";b.Elements[0].Location.Point.X=5;
            var r=engine.Compare(a,b,a.Settings);
            Check(r.AddedElements.Count==1&&r.DeletedElements.Count==1&&r.ModifiedElements.Count==1&&r.UnchangedElements.Count==1,"four status classes");
            Check(r.ModifiedElements[0].Changes.Any(x=>x.ChangeType=="ParameterChanged")&&r.ModifiedElements[0].Changes.Any(x=>x.ChangeType=="LocationChanged"),"multiple change classifications");
            var same=Clone(a);same.Elements.Reverse();same.Elements.Single(x=>x.UniqueId=="stable").Parameters.Reverse();
            same.Elements.Single(x=>x.UniqueId=="stable").Parameters[0].DisplayValue="different display units";
            Check(engine.Compare(a,same,a.Settings).UnchangedElements.Count==3,"ordering and display formatting ignored");
            same=Clone(a);same.Elements[0].Parameters[0].RawValue="1.00000001";same.Elements[0].Location.Point.X+=0.000001;
            same.Elements[0].Location.Rotation=Math.PI*2;
            Check(engine.Compare(a,same,a.Settings).UnchangedElements.Count==3,"length tolerance and periodic rotation");
            same=Clone(a);same.Elements[0].Parameters[0].Key="instance:shared:other-guid";
            Check(engine.Compare(a,same,a.Settings).ModifiedElements[0].Changes.Count==2,"same display name distinct stable keys");
            same=Clone(a);same.Elements[0].ElementId=9999;
            Check(engine.Compare(a,same,a.Settings).UnchangedElements.Count==3,"UniqueId primary matching ignores integer ID reassignment");
            same=Clone(a);same.Elements[0].TypeUniqueId="replacement-type";
            Check(engine.Compare(a,same,a.Settings).ModifiedElements[0].Changes[0].ChangeType=="FamilyTypeChanged","type identity change");
            same=Clone(a);same.Elements.Add(Clone(a).Elements[0]);Reject(()=>engine.Compare(a,same,a.Settings),"duplicate element rejection");
            same=Clone(a);same.Elements[0].Parameters.Add(Param("instance:builtin:-1","2"));Reject(()=>engine.Compare(a,same,a.Settings),"duplicate parameter rejection");
            same=Clone(a);same.SchemaVersion="99";Reject(()=>engine.Compare(a,same,a.Settings),"schema mismatch rejection");
            same=Clone(a);same.Settings.Categories.Add(-1);Reject(()=>engine.Compare(a,same,a.Settings),"scope mismatch rejection");
            same=Clone(a);same.Settings.Parameters=false;Reject(()=>engine.Compare(a,same,a.Settings),"missing extraction data rejection");
            same=Clone(a);same.Elements[0].Parameters[0].RawValue="NaN";Reject(()=>engine.Compare(a,same,a.Settings),"nonfinite parameter rejection");
            var cts=new CancellationTokenSource();cts.Cancel();
            try{engine.Compare(a,b,a.Settings,cts.Token);throw new Exception("Cancellation failed");}catch(OperationCanceledException){Check(true,"cancellation");}
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("vi-VN");
            Check(engine.Compare(a,b,a.Settings).ModifiedElements.Count==1,"culture independent numeric parsing");
            string folder=args.Length>0?args[0]:"Samples";Directory.CreateDirectory(folder);
            JsonSerializerService.Write(Path.Combine(folder,"ModelA.json"),a);JsonSerializerService.Write(Path.Combine(folder,"ModelB.json"),b);
            var roundtrip=engine.Compare(JsonSerializerService.Read<ModelSnapshot>(Path.Combine(folder,"ModelA.json")),JsonSerializerService.Read<ModelSnapshot>(Path.Combine(folder,"ModelB.json")),a.Settings);
            Check(roundtrip.ModifiedElements.Count==1,"streaming JSON roundtrip");
            ComparisonReportService.Export(Path.Combine(folder,"Comparison.json"),roundtrip);ComparisonReportService.Export(Path.Combine(folder,"Comparison.csv"),roundtrip);
            Check(File.ReadAllText(Path.Combine(folder,"Comparison.csv")).Contains("ParameterChanged"),"CSV report detail");
            // Synthetic large-model verification; includes dictionary matching and unchanged-result allocation.
            var large=Snapshot("Large");for(int i=0;i<100000;i++)large.Elements.Add(Element("large-"+i,i));
            var watch=System.Diagnostics.Stopwatch.StartNew();var big=engine.Compare(large,large,large.Settings);watch.Stop();
            Check(big.UnchangedElements.Count==100000,"100,000-element comparison ("+watch.ElapsedMilliseconds+" ms)");
            Phase2Tests.Run(Check,Path.Combine(folder,"Phase2"));
            Console.WriteLine(passed+" tests passed.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
