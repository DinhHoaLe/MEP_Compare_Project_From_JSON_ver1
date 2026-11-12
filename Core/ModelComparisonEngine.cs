using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
namespace BimComparison.Core
{
    public sealed class ModelComparisonEngine
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public ComparisonReport Compare(ModelSnapshot a, ModelSnapshot b, ComparisonSettings s, CancellationToken token = default(CancellationToken))
        {
            Validate(a); Validate(b); ValidateSettings(s);
            if (!s.Categories.OrderBy(x=>x).SequenceEqual(b.Settings.Categories.OrderBy(x=>x)) || !a.Settings.Categories.OrderBy(x=>x).SequenceEqual(b.Settings.Categories.OrderBy(x=>x)) ||
                (s.Parameters && (!a.Settings.Parameters || !b.Settings.Parameters)) || (s.Location && (!a.Settings.Location || !b.Settings.Location)))
                throw new InvalidDataException("Incompatible snapshot scopes/categories. Re-export using identical settings.");
            foreach(var snapshot in new[]{a,b})
                if((s.Geometry&&(!snapshot.Settings.Geometry||snapshot.SchemaVersion!="2.0"||snapshot.Settings.GeometryMode!=s.GeometryMode))||
                   (s.Mep&&(!snapshot.Settings.Mep||snapshot.SchemaVersion!="2.0"))||(s.Connectors&&(!snapshot.Settings.Connectors||snapshot.SchemaVersion!="2.0")))
                    throw new InvalidDataException("Requested Phase 2 data is unavailable or geometry modes differ. Re-export both models with matching Phase 2 settings, or disable these comparisons for old JSON.");
            var r = new ComparisonReport { ModelA=a.ModelPath, ModelB=b.ModelPath, ProjectA=a.ProjectUniqueId, ProjectB=b.ProjectUniqueId,
                Settings=s, TotalA=a.Elements.Count, TotalB=b.Elements.Count };
            r.Warnings.AddRange(a.Warnings.Select(x=>"A: "+x).Concat(b.Warnings.Select(x=>"B: "+x)));
            r.ExtractionErrors.AddRange(a.ExtractionErrors.Select(x=>"A: "+x).Concat(b.ExtractionErrors.Select(x=>"B: "+x)));
            if(r.ExtractionErrors.Count>0) r.Warnings.Add("Incomplete extraction: Unchanged refers only to successfully extracted data.");
            if(a.ProjectUniqueId!=b.ProjectUniqueId) r.Warnings.Add("Project identities differ; these may be unrelated models.");
            r.Warnings.Add("Host model only; probable matching is not implemented. Recreated elements remain Added/Deleted. Geometry is compared in internal world coordinates; movement may also be GeometryChanged.");
            if(s.Geometry)r.Warnings.Add(s.GeometryMode=="Lightweight"?"Lightweight geometry uses measures/counts/bounds and may miss shape changes with equal measures. Use Detailed for tessellation comparison.":"Detailed geometry compares Fine tessellation; topology/tessellation changes can be reported even when surfaces are equivalent.");
            var old=a.Elements.ToDictionary(x=>x.UniqueId,StringComparer.Ordinal);
            var newer=b.Elements.ToDictionary(x=>x.UniqueId,StringComparer.Ordinal);
            foreach(var e in b.Elements)
            {
                token.ThrowIfCancellationRequested();
                ElementSnapshot before;
                var item=new ElementResult { Element=e };
                if(!old.TryGetValue(e.UniqueId,out before)) { item.Status="Added"; r.AddedElements.Add(item); continue; }
                CompareIdentity(before,e,item.Changes);
                if(s.Parameters) CompareParameters(before,e,s,item.Changes);
                if(s.Location) CompareLocation(before.Location,e.Location,s,item.Changes);
                if(s.Geometry)AdvancedComparison.Geometry(before.Geometry,e.Geometry,s,item.Changes,token);
                if(s.Mep)AdvancedComparison.Mep(before.Mep,e.Mep,s,item.Changes);
                if(s.Connectors)AdvancedComparison.Connectors(before.Connectors,e.Connectors,s,item.Changes);
                item.Status=item.Changes.Count==0?"Unchanged":"Modified";
                if(item.Status=="Modified") r.ModifiedElements.Add(item); else r.UnchangedElements.Add(item);
            }
            foreach(var e in a.Elements) { token.ThrowIfCancellationRequested(); if(!newer.ContainsKey(e.UniqueId)) r.DeletedElements.Add(new ElementResult {Status="Deleted",Element=e}); }
            return r;
        }
        public static void ValidateSettings(ComparisonSettings s)
        {
            if(s==null || s.Categories==null || s.Categories.Count==0 || s.Categories.Distinct().Count()!=s.Categories.Count) throw new InvalidDataException("Select distinct categories.");
            if(s.GeometryMode!="Detailed"&&s.GeometryMode!="Lightweight")throw new InvalidDataException("Unknown geometry mode.");
            foreach(var t in new[]{s.LengthToleranceFeet,s.NumberTolerance,s.AngleToleranceRadians,s.AreaToleranceSquareFeet,s.VolumeToleranceCubicFeet})
                if(double.IsNaN(t)||double.IsInfinity(t)||t<0) throw new InvalidDataException("Tolerances must be finite and nonnegative.");
        }
        private static void Validate(ModelSnapshot s)
        {
            if(s==null || !((s.SchemaVersion=="1.0"&&s.Scope=="Phase1-Identity-Parameters-Location")||(s.SchemaVersion=="2.0"&&s.Scope=="Phase2-Geometry-MEP-Connectors")) || s.Elements==null || s.Warnings==null || s.ExtractionErrors==null) throw new InvalidDataException("Unsupported/malformed snapshot.");
            ValidateSettings(s.Settings);
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var e in s.Elements)
            {
                if(e==null || string.IsNullOrWhiteSpace(e.UniqueId)||!ids.Add(e.UniqueId)||e.Parameters==null) throw new InvalidDataException("Missing/duplicate UniqueId or parameters.");
                if(!s.Settings.Categories.Contains(e.CategoryId)) throw new InvalidDataException("Element outside declared category scope.");
                AdvancedComparison.Validate(e,s.Settings);
                var keys=new HashSet<string>(StringComparer.Ordinal);
                if(e.Parameters.Any(p=>p==null || string.IsNullOrEmpty(p.Key)||!keys.Add(p.Key))) throw new InvalidDataException("Missing/duplicate stable parameter key.");
                foreach(var p in e.Parameters.Where(p=>p.HasValue&&p.StorageType=="Double"))
                {
                    double number;
                    if(!double.TryParse(p.RawValue,NumberStyles.Float,Inv,out number)||double.IsInfinity(number)||double.IsNaN(number)) throw new InvalidDataException("Invalid numeric parameter value.");
                }
                if(e.Location!=null)
                {
                    foreach(var point in new[]{e.Location.Point,e.Location.Start,e.Location.End}.Where(p=>p!=null))
                        foreach(var n in new[]{point.X,point.Y,point.Z}) if(double.IsNaN(n)||double.IsInfinity(n)) throw new InvalidDataException("Invalid location coordinate.");
                    if(e.Location.Rotation.HasValue&&(double.IsNaN(e.Location.Rotation.Value)||double.IsInfinity(e.Location.Rotation.Value))) throw new InvalidDataException("Invalid rotation.");
                }
            }
        }
        private static void Add(List<PropertyChange> c,string type,string name,string a,string b,string unit=null)
        { if(!string.Equals(a,b,StringComparison.Ordinal)) c.Add(new PropertyChange {ChangeType=type,Property=name,OldValue=a,NewValue=b,Unit=unit}); }
        private static void CompareIdentity(ElementSnapshot a,ElementSnapshot b,List<PropertyChange> c)
        {
            Add(c,"FamilyTypeChanged","TypeUniqueId",a.TypeUniqueId,b.TypeUniqueId);
            Add(c,"FamilyTypeChanged","Family",a.Family,b.Family); Add(c,"FamilyTypeChanged","Type",a.Type,b.Type);
            Add(c,"IdentityChanged","CategoryId",a.CategoryId.ToString(Inv),b.CategoryId.ToString(Inv));
            Add(c,"WorksetChanged","Workset",a.Workset,b.Workset); Add(c,"IdentityChanged","DesignOption",a.DesignOption,b.DesignOption);
            Add(c,"IdentityChanged","CreatedPhase",a.CreatedPhase,b.CreatedPhase); Add(c,"IdentityChanged","DemolishedPhase",a.DemolishedPhase,b.DemolishedPhase);
        }
        private static string Describe(ParameterSnapshot p)=>p==null?"<absent>":p.StorageType+":"+(p.HasValue?p.RawValue:"<unset>")+" ["+p.Spec+"]";
        private static void CompareParameters(ElementSnapshot a,ElementSnapshot b,ComparisonSettings s,List<PropertyChange> c)
        {
            var old=a.Parameters.ToDictionary(x=>x.Key,StringComparer.Ordinal); var newer=b.Parameters.ToDictionary(x=>x.Key,StringComparer.Ordinal);
            foreach(var key in old.Keys.Union(newer.Keys).OrderBy(x=>x,StringComparer.Ordinal))
            {
                ParameterSnapshot x,y; old.TryGetValue(key,out x); newer.TryGetValue(key,out y);
                if(x!=null&&y!=null&&x.StorageType==y.StorageType&&x.HasValue==y.HasValue&&x.Spec==y.Spec)
                {
                    double n,m;
                    if(x.StorageType=="Double"&&double.TryParse(x.RawValue,NumberStyles.Float,Inv,out n)&&double.TryParse(y.RawValue,NumberStyles.Float,Inv,out m))
                    {
                        double t=x.Spec!=null&&x.Spec.StartsWith("autodesk.spec.aec:length-",StringComparison.Ordinal)?s.LengthToleranceFeet:
                            x.Spec!=null&&x.Spec.StartsWith("autodesk.spec.aec:angle-",StringComparison.Ordinal)?s.AngleToleranceRadians:s.NumberTolerance;
                        if(Math.Abs(n-m)<=t) continue;
                    }
                    else if(x.RawValue==y.RawValue) continue;
                }
                c.Add(new PropertyChange {ChangeType="ParameterChanged",Property=key+" / "+(y??x).Name,OldValue=Describe(x),NewValue=Describe(y),Unit="internal; spec="+(y??x).Spec});
            }
        }
        private static string Point(PointData p)=>p==null?"<none>":string.Join(",",p.X.ToString("R",Inv),p.Y.ToString("R",Inv),p.Z.ToString("R",Inv));
        private static void ComparePoint(string name,PointData a,PointData b,double t,List<PropertyChange> c)
        {
            if(a!=null&&b!=null&&Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)+Math.Pow(a.Z-b.Z,2))<=t) return;
            Add(c,"LocationChanged",name,Point(a),Point(b),"ft");
        }
        private static void CompareLocation(LocationSnapshot a,LocationSnapshot b,ComparisonSettings s,List<PropertyChange> c)
        {
            Add(c,"LocationChanged","Location.Kind",a?.Kind,b?.Kind);
            ComparePoint("Location.Point",a?.Point,b?.Point,s.LengthToleranceFeet,c); ComparePoint("Location.Start",a?.Start,b?.Start,s.LengthToleranceFeet,c); ComparePoint("Location.End",a?.End,b?.End,s.LengthToleranceFeet,c);
            if(a?.Rotation!=null&&b?.Rotation!=null&&Math.Abs(Math.IEEERemainder(a.Rotation.Value-b.Rotation.Value,Math.PI*2))<=s.AngleToleranceRadians) return;
            Add(c,"RotationChanged","Location.Rotation",a?.Rotation?.ToString("R",Inv),b?.Rotation?.ToString("R",Inv),"rad");
        }
    }
}
