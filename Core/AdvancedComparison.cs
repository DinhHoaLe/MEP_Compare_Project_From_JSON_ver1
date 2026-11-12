using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Threading;
namespace BimComparison.Core
{
    public static class AdvancedComparison
    {
        private static string Value(object o)=>o==null?"<missing>":JsonConvert.SerializeObject(o,Formatting.None);
        private static void Change(List<PropertyChange> c,string type,string property,object a,object b,string unit=null)
        {c.Add(new PropertyChange{ChangeType=type,Property=property,OldValue=Value(a),NewValue=Value(b),Unit=unit});}
        private static void Number(List<PropertyChange> c,string type,string name,double a,double b,double tolerance,string unit)
        {if(Math.Abs(a-b)>tolerance)Change(c,type,name,a,b,unit);}
        private static bool Near(PointData a,PointData b,double tolerance)=>a==null||b==null?a==b:GeometryComparison.Distance(a,b)<=tolerance;
        private static void Point(List<PropertyChange> c,string name,PointData a,PointData b,double tolerance)
        {if(!Near(a,b,tolerance))Change(c,"GeometryChanged",name,a,b,"ft");}
        public static void Geometry(GeometrySnapshot a,GeometrySnapshot b,ComparisonSettings s,List<PropertyChange> c,CancellationToken token=default(CancellationToken))
        {
            if(a.Status=="Partial"||b.Status=="Partial")Change(c,"ExtractionIncomplete","Geometry.Status",a.Status,b.Status);
            else if(a.Status!=b.Status)Change(c,"GeometryChanged","Geometry.Status",a.Status,b.Status);
            Number(c,"GeometryChanged","Geometry.Volume",a.Volume,b.Volume,s.VolumeToleranceCubicFeet,"ft³");
            Number(c,"GeometryChanged","Geometry.SurfaceArea",a.SurfaceArea,b.SurfaceArea,s.AreaToleranceSquareFeet,"ft²");
            Number(c,"GeometryChanged","Geometry.CurveLength",a.CurveLength,b.CurveLength,s.LengthToleranceFeet,"ft");
            Point(c,"Geometry.Minimum",a.Minimum,b.Minimum,s.LengthToleranceFeet);Point(c,"Geometry.Maximum",a.Maximum,b.Maximum,s.LengthToleranceFeet);
            if(a.SolidCount!=b.SolidCount||a.MeshCount!=b.MeshCount||a.CurveCount!=b.CurveCount)
                Change(c,"GeometryChanged","Geometry.PrimitiveCounts",new[]{a.SolidCount,a.MeshCount,a.CurveCount},new[]{b.SolidCount,b.MeshCount,b.CurveCount});
            if(s.GeometryMode=="Detailed"&&!GeometryComparison.ShapeEqual(a,b,s.LengthToleranceFeet,token))
                Change(c,"GeometryChanged","Geometry.Tessellation",GeometryComparison.Fingerprint(a),GeometryComparison.Fingerprint(b));
        }
        public static void Mep(MepSnapshot a,MepSnapshot b,ComparisonSettings s,List<PropertyChange> c)
        {
            if(a.Status=="Partial"||b.Status=="Partial")Change(c,"ExtractionIncomplete","MEP.Status",a.Status,b.Status);
            else if(a.Status!=b.Status)Change(c,"MEPSystemChanged","MEP.Status",a.Status,b.Status);
            var x=a.Systems.OrderBy(v=>v.UniqueId,StringComparer.Ordinal).ToList();var y=b.Systems.OrderBy(v=>v.UniqueId,StringComparer.Ordinal).ToList();
            if(Value(x)!=Value(y))Change(c,"MEPSystemChanged","MEP.Systems",x,y);
            foreach(var key in a.Dimensions.Keys.Union(b.Dimensions.Keys).OrderBy(k=>k,StringComparer.Ordinal))
            {
                double oldValue,newValue;
                if(!a.Dimensions.TryGetValue(key,out oldValue)||!b.Dimensions.TryGetValue(key,out newValue))
                    Change(c,"MEPPropertyChanged","MEP."+key,a.Dimensions.ContainsKey(key)?(double?)a.Dimensions[key]:null,b.Dimensions.ContainsKey(key)?(double?)b.Dimensions[key]:null,"ft");
                else Number(c,"MEPPropertyChanged","MEP."+key,oldValue,newValue,s.LengthToleranceFeet,"ft");
            }
        }
        private static bool Size(double? a,double? b,double tolerance)=>a.HasValue&&b.HasValue?Math.Abs(a.Value-b.Value)<=tolerance:a==b;
        private static bool Direction(PointData a,PointData b,double angle)
        {
            if(a==null||b==null)return a==b;
            double an=Math.Sqrt(a.X*a.X+a.Y*a.Y+a.Z*a.Z),bn=Math.Sqrt(b.X*b.X+b.Y*b.Y+b.Z*b.Z);
            return Math.Acos(Math.Max(-1,Math.Min(1,(a.X*b.X+a.Y*b.Y+a.Z*b.Z)/(an*bn))))<=angle;
        }
        private static bool Equal(ConnectorData a,ConnectorData b,ComparisonSettings s)=>a.Domain==b.Domain&&a.Type==b.Type&&a.Shape==b.Shape&&
            Near(a.Origin,b.Origin,s.LengthToleranceFeet)&&Direction(a.Direction,b.Direction,s.AngleToleranceRadians)&&
            Size(a.Radius,b.Radius,s.LengthToleranceFeet)&&Size(a.Width,b.Width,s.LengthToleranceFeet)&&Size(a.Height,b.Height,s.LengthToleranceFeet)&&
            a.References.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(b.References.OrderBy(x=>x,StringComparer.Ordinal))&&
            GeometryComparison.UnorderedEqual(a.ConnectedPorts,b.ConnectedPorts,(x,y)=>x.OwnerUniqueId==y.OwnerUniqueId&&x.Domain==y.Domain&&x.Type==y.Type&&Near(x.Origin,y.Origin,s.LengthToleranceFeet));
        public static void Connectors(ConnectorSnapshot a,ConnectorSnapshot b,ComparisonSettings s,List<PropertyChange> c)
        {
            if(a.Status=="Partial"||b.Status=="Partial")Change(c,"ExtractionIncomplete","Connectors.Status",a.Status,b.Status);
            else if(a.Status!=b.Status)Change(c,"ConnectorChanged","Connectors.Status",a.Status,b.Status);
            if(!GeometryComparison.UnorderedEqual(a.Items,b.Items,(x,y)=>Equal(x,y,s)))
                Change(c,"ConnectorChanged","Connectors (origin/direction/size/type/references)",a.Items,b.Items,"ft; direction in unit vectors");
        }
        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        private static void PointValid(PointData p)
        {if(p!=null&&(!Finite(p.X)||!Finite(p.Y)||!Finite(p.Z)))throw new InvalidDataException("Nonfinite advanced coordinate.");}
        private static void Status(string status,params string[] allowed)
        {if(!allowed.Contains(status))throw new InvalidDataException("Missing/invalid extraction status.");}
        public static void Validate(ElementSnapshot e,ComparisonSettings s)
        {
            if(s.Geometry)
            {
                var g=e.Geometry??throw new InvalidDataException("Geometry data missing; re-export both models with Phase 2.");
                Status(g.Status,"Complete","Empty","Partial");
                if(g.Mode!=s.GeometryMode||g.Algorithm!="world-triangles-fine-v1"||g.Errors==null||g.Triangles==null||g.Curves==null)throw new InvalidDataException("Incompatible geometry settings/data.");
                if(new[]{g.Volume,g.SurfaceArea,g.CurveLength}.Any(v=>!Finite(v)||v<0)||g.SolidCount<0||g.MeshCount<0||g.CurveCount<0)throw new InvalidDataException("Invalid geometry measures.");
                PointValid(g.Minimum);PointValid(g.Maximum);PointValid(g.Center);
                foreach(var t in g.Triangles){if(t==null||t.A==null||t.B==null||t.C==null)throw new InvalidDataException("Invalid triangle.");PointValid(t.A);PointValid(t.B);PointValid(t.C);}
                foreach(var curve in g.Curves){if(curve==null||curve.Count==0||curve.Any(p=>p==null))throw new InvalidDataException("Invalid geometry curve.");foreach(var p in curve)PointValid(p);}
            }
            if(s.Mep)
            {
                var m=e.Mep??throw new InvalidDataException("MEP data missing; re-export with Phase 2.");Status(m.Status,"Complete","NotApplicable","Partial");
                if(m.Systems==null||m.Dimensions==null||m.Errors==null||m.Systems.Any(x=>x==null||string.IsNullOrEmpty(x.UniqueId))||m.Systems.Select(x=>x.UniqueId).Distinct().Count()!=m.Systems.Count||m.Dimensions.Values.Any(x=>!Finite(x)))throw new InvalidDataException("Invalid MEP data.");
            }
            if(s.Connectors)
            {
                var ports=e.Connectors??throw new InvalidDataException("Connector data missing; re-export with Phase 2.");Status(ports.Status,"Complete","NotApplicable","Partial");
                if(ports.Items==null||ports.Errors==null)throw new InvalidDataException("Invalid connector data.");
                foreach(var port in ports.Items)
                {
                    if(port==null||port.References==null||port.ConnectedPorts==null||port.References.Any(x=>x==null))throw new InvalidDataException("Invalid connector.");
                    if(ports.Status!="Partial"&&(string.IsNullOrEmpty(port.Type)||string.IsNullOrEmpty(port.Domain)||(port.Type!="Logical"&&(port.Origin==null||port.Direction==null))))throw new InvalidDataException("Incomplete physical connector data.");
                    foreach(var reference in port.ConnectedPorts)
                    {
                        if(reference==null||string.IsNullOrWhiteSpace(reference.OwnerUniqueId))throw new InvalidDataException("Invalid connected port reference.");
                        PointValid(reference.Origin);
                    }
                    PointValid(port.Origin);PointValid(port.Direction);
                    if(new[]{port.Radius,port.Width,port.Height}.Any(x=>x.HasValue&&(!Finite(x.Value)||x.Value<0)))throw new InvalidDataException("Invalid connector size.");
                    if(port.Direction!=null&&port.Direction.X*port.Direction.X+port.Direction.Y*port.Direction.Y+port.Direction.Z*port.Direction.Z<1e-20)throw new InvalidDataException("Zero connector direction.");
                }
            }
        }
    }
}
