using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Web.Script.Serialization;

namespace CadProbe {
[Transaction(TransactionMode.Manual)]
public class ExportRegion : IExternalCommand {
 Document doc; XYZ right,up; double loX,hiX,loY,hiY;
 List<object> primitives=new List<object>(); List<string> errors=new List<string>();
 const string Output=@"F:\Hoa Le\1. Working\Tools\MEP_Compare_Project_From_JSON_ver1\MEP_Compare_Project_From_JSON_ver1\CadProbe";
 double[] P(XYZ p){return new[]{p.X*304.8,p.Y*304.8,p.Z*304.8};}
 bool Hit(IEnumerable<XYZ> pts) {var a=pts.ToList();return a.Count>0 && a.Max(p=>p.DotProduct(right))>=loX && a.Min(p=>p.DotProduct(right))<=hiX && a.Max(p=>p.DotProduct(up))>=loY && a.Min(p=>p.DotProduct(up))<=hiY;}
 string Layer(GeometryObject g,string inherited){try{var s=doc.GetElement(g.GraphicsStyleId) as GraphicsStyle;return s==null?inherited:s.GraphicsStyleCategory.Name;}catch{return inherited;}}
 void Add(string kind,int owner,string layer,XYZ[] p){if(Hit(p))primitives.Add(new{kind=kind,owner=owner,layer=layer,points=p.Select(P).ToArray()});}
 void Walk(GeometryElement ge,Transform t,int owner,string parent,int depth){
  if(ge==null)return; if(depth>30){errors.Add("Nested depth exceeded: "+owner);return;}
  foreach(GeometryObject g in ge)try{
   string layer=Layer(g,parent);var gi=g as GeometryInstance;
   if(gi!=null){Walk(gi.GetSymbolGeometry(),t.Multiply(gi.Transform),owner,layer,depth+1);continue;}
   var solid=g as Solid;
   if(solid!=null){foreach(Face f in solid.Faces){var mesh=f.Triangulate();for(int i=0;i<mesh.NumTriangles;i++){var tri=mesh.get_Triangle(i);Add("triangle",owner,layer,new[]{t.OfPoint(tri.get_Vertex(0)),t.OfPoint(tri.get_Vertex(1)),t.OfPoint(tri.get_Vertex(2))});}}continue;}
   var m=g as Mesh;if(m!=null){for(int i=0;i<m.NumTriangles;i++){var tri=m.get_Triangle(i);Add("triangle",owner,layer,new[]{t.OfPoint(tri.get_Vertex(0)),t.OfPoint(tri.get_Vertex(1)),t.OfPoint(tri.get_Vertex(2))});}continue;}
   var c=g as Curve;if(c!=null){Add("curve",owner,layer,c.Tessellate().Select(t.OfPoint).ToArray());continue;}
   var pl=g as PolyLine;if(pl!=null){Add("polyline",owner,layer,pl.GetCoordinates().Select(t.OfPoint).ToArray());continue;}
   if(!(g is Autodesk.Revit.DB.Point))errors.Add("Unsupported "+g.GetType().Name+" owner "+owner);
  }catch(Exception ex){errors.Add(owner+": "+ex.Message);}
 }
 double? Param(Element e,BuiltInParameter b){var p=e.get_Parameter(b);return p==null||!p.HasValue?(double?)null:p.AsDouble()*304.8;}
 public Result Execute(ExternalCommandData data,ref string message,ElementSet elems){try{
  var ui=data.Application.ActiveUIDocument;doc=ui.Document;var v=doc.ActiveView;
  if(v.Name!="E04_HoL"){TaskDialog.Show("CAD probe","Open E04_HoL first. No model changes made.");return Result.Cancelled;}
  right=v.RightDirection;up=v.UpDirection;
  var box=ui.Selection.PickBox(PickBoxStyle.Crossing,"Choose confirmed sample region near RLT 05-01. Read-only XYZ export; no clouds.");
  loX=Math.Min(box.Min.DotProduct(right),box.Max.DotProduct(right));hiX=Math.Max(box.Min.DotProduct(right),box.Max.DotProduct(right));loY=Math.Min(box.Min.DotProduct(up),box.Max.DotProduct(up));hiY=Math.Max(box.Min.DotProduct(up),box.Max.DotProduct(up));
  var items=new List<object>();var links=new List<object>();var opts=new Options{DetailLevel=ViewDetailLevel.Fine,IncludeNonVisibleObjects=false,ComputeReferences=false};
  var visible=new FilteredElementCollector(doc,v.Id).WhereElementIsNotElementType().ToElements();
  foreach(var e in visible){
   var cad=e as ImportInstance;
   if(cad!=null){var t=cad.GetTotalTransform();links.Add(new{id=e.Id.IntegerValue,name=doc.GetElement(e.GetTypeId()).Name,isLinked=cad.IsLinked,viewSpecific=e.ViewSpecific,ownerView=e.OwnerViewId.IntegerValue,origin=P(t.Origin),basisX=new[]{t.BasisX.X,t.BasisX.Y,t.BasisX.Z},basisY=new[]{t.BasisY.X,t.BasisY.Y,t.BasisY.Z},basisZ=new[]{t.BasisZ.X,t.BasisZ.Y,t.BasisZ.Z},scale=t.Scale});Walk(e.get_Geometry(opts),Transform.Identity,e.Id.IntegerValue,"CAD",0);continue;}
   if(!(e is Duct))continue;
   var bb=e.get_BoundingBox(null);if(bb==null)continue;
   var corners=new List<XYZ>();for(int i=0;i<8;i++)corners.Add(bb.Transform.OfPoint(new XYZ((i&1)==0?bb.Min.X:bb.Max.X,(i&2)==0?bb.Min.Y:bb.Max.Y,(i&4)==0?bb.Min.Z:bb.Max.Z)));
   if(!Hit(corners))continue;var lc=e.Location as LocationCurve;
   items.Add(new{id=e.Id.IntegerValue,uniqueId=e.UniqueId,name=e.Name,category=e.Category.Name,width=Param(e,BuiltInParameter.RBS_CURVE_WIDTH_PARAM),height=Param(e,BuiltInParameter.RBS_CURVE_HEIGHT_PARAM),diameter=Param(e,BuiltInParameter.RBS_CURVE_DIAMETER_PARAM),centerline=lc==null?null:lc.Curve.Tessellate().Select(P).ToArray(),bounds=corners.Select(P).ToArray()});
   Walk(e.get_Geometry(opts),Transform.Identity,e.Id.IntegerValue,"RVT_DUCT",0);
  }
  var output=new{model=doc.Title,path=doc.PathName,view=v.Name,viewId=v.Id.IntegerValue,units="mm",coordinateSystem="Revit internal world; accumulated symbol-instance transforms",toleranceMm=10,roi=new{min=P(box.Min),max=P(box.Max),right=new[]{right.X,right.Y,right.Z},up=new[]{up.X,up.Y,up.Z},description="Screen rectangle extruded through all Z; geometry retained if projected bounds intersects region"},ducts=items,cadLinks=links,primitives=primitives,errors=errors,documentModified=doc.IsModified};
  var json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=200}.Serialize(output);Directory.CreateDirectory(Output);File.WriteAllText(System.IO.Path.Combine(Output,"region.json"),json);
  var img=new ImageExportOptions{FilePath=System.IO.Path.Combine(Output,"E04_HoL_sample"),ExportRange=ExportRange.VisibleRegionOfCurrentView,HLRandWFViewsFileType=ImageFileType.PNG,ShadowViewsFileType=ImageFileType.PNG,ZoomType=ZoomFitType.FitToPage,PixelSize=2400};try{doc.ExportImage(img);}catch(Exception ex){File.WriteAllText(System.IO.Path.Combine(Output,"image-error.txt"),ex.Message);}
  TaskDialog.Show("Read-only CAD probe","Export complete. Ducts: "+items.Count+"; CAD links: "+links.Count+"; geometry primitives: "+primitives.Count+"; warnings: "+errors.Count+".\nNo transaction, cloud, save or synchronization performed.\n"+Output);
  return Result.Succeeded;
 }catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}catch(Exception ex){message=ex.ToString();File.WriteAllText(System.IO.Path.Combine(Output,"error.txt"),message);return Result.Failed;}}
}
}
