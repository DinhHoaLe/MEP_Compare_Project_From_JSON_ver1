using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BimComparison.Core;
namespace BimComparison.Extraction
{
    public sealed class GeometryExtractor
    {
        private const int MaxPrimitives=20000;
        private static PointData Point(XYZ p)=>new PointData{X=p.X,Y=p.Y,Z=p.Z};
        public GeometrySnapshot Extract(Element element,string mode)
        {
            var result=new GeometrySnapshot{Mode=mode,Status="Complete"};
            try
            {
                using(var options=new Options{DetailLevel=ViewDetailLevel.Fine,IncludeNonVisibleObjects=false,ComputeReferences=false})
                using(var geometry=element.get_Geometry(options))
                {
                    if(geometry==null){result.Status="Empty";return result;}
                    var bounds=new List<XYZ>();
                    Walk(geometry,Transform.Identity,result,bounds);
                    if(bounds.Count>0)
                    {
                        result.Minimum=new PointData{X=bounds.Min(p=>p.X),Y=bounds.Min(p=>p.Y),Z=bounds.Min(p=>p.Z)};
                        result.Maximum=new PointData{X=bounds.Max(p=>p.X),Y=bounds.Max(p=>p.Y),Z=bounds.Max(p=>p.Z)};
                        result.Center=new PointData{X=(result.Minimum.X+result.Maximum.X)/2,Y=(result.Minimum.Y+result.Maximum.Y)/2,Z=(result.Minimum.Z+result.Maximum.Z)/2};
                    }
                    if(result.SolidCount+result.MeshCount+result.CurveCount==0&&result.Errors.Count==0)result.Status="Empty";
                }
                if(mode=="Detailed")result.Fingerprint=GeometryComparison.Fingerprint(result);
            }
            catch(Exception ex){result.Errors.Add(ex.Message);}
            if(result.Errors.Count>0)result.Status="Partial";
            return result;
        }
        private static void Walk(GeometryElement geometry,Transform transform,GeometrySnapshot r,List<XYZ> bounds)
        {
            foreach(GeometryObject obj in geometry)
            {
                try
                {
                    var instance=obj as GeometryInstance;
                    if(instance!=null)
                    {
                        using(var symbol=instance.GetSymbolGeometry())Walk(symbol,transform.Multiply(instance.Transform),r,bounds);
                        continue;
                    }
                    var solid=obj as Solid;
                    if(solid!=null)
                    {
                        if(solid.Faces.Size==0)continue;
                        if(!transform.IsConformal)throw new InvalidOperationException("Nonconformal geometry transform is unsupported.");
                        double determinant=Math.Abs(transform.Determinant);
                        r.SolidCount++;r.Volume+=Math.Abs(solid.Volume)*determinant;r.SurfaceArea+=solid.SurfaceArea*Math.Pow(determinant,2.0/3.0);
                        if(r.Mode=="Lightweight")
                        {
                            using(var box=solid.GetBoundingBox())
                            {
                                var combined=transform.Multiply(box.Transform);
                                for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                                    bounds.Add(combined.OfPoint(new XYZ(x==0?box.Min.X:box.Max.X,y==0?box.Min.Y:box.Max.Y,z==0?box.Min.Z:box.Max.Z)));
                            }
                            continue;
                        }
                        foreach(Face face in solid.Faces)
                            using(var mesh=face.Triangulate())Mesh(mesh,transform,r,bounds,false);
                        continue;
                    }
                    var meshObject=obj as Mesh;
                    if(meshObject!=null){r.MeshCount++;Mesh(meshObject,transform,r,bounds,true);continue;}
                    var curve=obj as Curve;
                    if(curve!=null)
                    {
                        r.CurveCount++;r.CurveLength+=curve.Length;
                        var points=curve.Tessellate().Select(transform.OfPoint).ToList();bounds.AddRange(points);
                        if(r.Mode=="Detailed") {Limit(r);r.Curves.Add(points.Select(Point).ToList());}
                        continue;
                    }
                    // Point and polyline geometry are included as sampled curves, preserving shape data.
                    var polyline=obj as PolyLine;
                    if(polyline!=null)
                    {
                        var points=polyline.GetCoordinates().Select(transform.OfPoint).ToList();bounds.AddRange(points);r.CurveCount++;
                        for(int i=1;i<points.Count;i++)r.CurveLength+=points[i].DistanceTo(points[i-1]);
                        if(r.Mode=="Detailed"){Limit(r);r.Curves.Add(points.Select(Point).ToList());}continue;
                    }
                    var point=obj as Autodesk.Revit.DB.Point;
                    if(point!=null){var p=transform.OfPoint(point.Coord);bounds.Add(p);r.CurveCount++;if(r.Mode=="Detailed"){Limit(r);r.Curves.Add(new List<PointData>{Point(p)});}continue;}
                    r.Errors.Add("Unsupported geometry object: "+obj.GetType().Name);
                }
                catch(InvalidOperationException ex) when(ex.Message.StartsWith("Geometry limit",StringComparison.Ordinal)){throw;}
                catch(Exception ex){r.Errors.Add(obj.GetType().Name+": "+ex.Message);}
            }
        }
        private static void Limit(GeometrySnapshot r)
        {if(r.Triangles.Count+r.Curves.Count>=MaxPrimitives)throw new InvalidOperationException("Geometry limit exceeded (20,000 primitives per element); extraction is partial.");}
        private static void Mesh(Mesh mesh,Transform transform,GeometrySnapshot r,List<XYZ> bounds,bool measureArea)
        {
            for(int i=0;i<mesh.NumTriangles;i++)
            {
                var triangle=mesh.get_Triangle(i);
                var a=transform.OfPoint(triangle.get_Vertex(0));var b=transform.OfPoint(triangle.get_Vertex(1));var c=transform.OfPoint(triangle.get_Vertex(2));
                if(measureArea)r.SurfaceArea+=(b-a).CrossProduct(c-a).GetLength()/2;
                bounds.Add(a);bounds.Add(b);bounds.Add(c);
                if(r.Mode=="Detailed"){Limit(r);r.Triangles.Add(new TriangleData{A=Point(a),B=Point(b),C=Point(c)});}
            }
        }
    }
}
