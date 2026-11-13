using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using System.Web.Script.Serialization;
namespace DuctReview {
[Transaction(TransactionMode.Manual)]
public class MarkCloud : IExternalCommand {
 const string Out=@"F:\Hoa Le\1. Working\Tools\MEP_Compare_Project_From_JSON_ver1\MEP_Compare_Project_From_JSON_ver1\CadProbe";
 const string Marker="RVT-CAD review | duct 14465104 | E04_HoL | 2026-11-13";
 static double F(double mm){return mm/304.8;}
 static double[] P(XYZ p){return new[]{p.X*304.8,p.Y*304.8,p.Z*304.8};}
 public Result Execute(ExternalCommandData c,ref string message,ElementSet elements){try{
  var ui=c.Application.ActiveUIDocument;var d=ui.Document;var duct=d.GetElement(new ElementId(14465104)) as Duct;
  if(duct==null||duct.UniqueId!="3d4a7298-6410-4422-9cc1-da9060db4b36-00dcb850")throw new Exception("Expected duct identity not found.");
  var v=new FilteredElementCollector(d).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().First(x=>x.Name=="E04_HoL"&&!x.IsTemplate);
  if(d.IsReadOnly)throw new Exception("Document is read-only.");
  var before=((LocationCurve)duct.Location).Curve;var a=before.GetEndPoint(0);var b=before.GetEndPoint(1);double width=duct.Width,height=duct.Height;
  var bb=duct.get_BoundingBox(null);double x0=bb.Min.X-F(180),x1=bb.Max.X+F(180),y0=bb.Min.Y-F(180),y1=bb.Max.Y+F(180);double z=v.GenLevel.Elevation;
  string text="Ống ID 14465104\rRVT nằm ngang còn CAD chuyển cao độ";
  RevisionCloud cloud=null;TextNote note=null;Revision rev=null;
  var prior=new FilteredElementCollector(d,v.Id).OfClass(typeof(RevisionCloud)).Cast<RevisionCloud>().FirstOrDefault(x=>x.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)!=null&&x.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString()==Marker);
  using(var tx=new Transaction(d,"Mark RVT-CAD elevation discrepancy - duct 14465104")){
   tx.Start();
   if(prior!=null){cloud=prior;rev=(Revision)d.GetElement(cloud.RevisionId);note=new FilteredElementCollector(d,v.Id).OfClass(typeof(TextNote)).Cast<TextNote>().FirstOrDefault(n=>n.Text.Replace("\r"," ").Replace("\n"," ").Contains("RVT nằm ngang còn CAD chuyển cao độ"));}
   else{
    rev=Revision.Create(d);rev.Description=Marker;rev.RevisionDate="13/11/2026";rev.Visibility=RevisionVisibility.CloudAndTagVisible;
    XYZ[] corners={new XYZ(x0,y0,z),new XYZ(x0,y1,z),new XYZ(x1,y1,z),new XYZ(x1,y0,z)};
    var curves=new List<Curve>();for(int i=0;i<4;i++)curves.Add(Line.CreateBound(corners[i],corners[(i+1)%4]));
    cloud=RevisionCloud.Create(d,v,rev.Id,curves);var comments=cloud.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);if(comments!=null&&!comments.IsReadOnly)comments.Set(Marker);
   }
   if(note==null){var typeId=d.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);if(typeId==ElementId.InvalidElementId)typeId=new FilteredElementCollector(d).OfClass(typeof(TextNoteType)).FirstElementId();
    note=TextNote.Create(d,v.Id,new XYZ(x1+F(450),y1+F(150),z),text,typeId);
    var leader=note.AddLeader(TextNoteLeaderTypes.TNLT_STRAIGHT_L);leader.End=new XYZ(x1,y1-F(140),z);leader.Elbow=new XYZ(x1+F(230),y1+F(100),z);
   }
   var red=new OverrideGraphicSettings();red.SetProjectionLineColor(new Color(220,35,35));red.SetProjectionLineWeight(5);v.SetElementOverrides(cloud.Id,red);
   if(v.GetCategoryHidden(new ElementId(BuiltInCategory.OST_RevisionClouds)))v.SetCategoryHidden(new ElementId(BuiltInCategory.OST_RevisionClouds),false);
   if(v.GetCategoryHidden(new ElementId(BuiltInCategory.OST_TextNotes)))v.SetCategoryHidden(new ElementId(BuiltInCategory.OST_TextNotes),false);
   d.Regenerate();var after=((LocationCurve)duct.Location).Curve;
   if(!a.IsAlmostEqualTo(after.GetEndPoint(0))||!b.IsAlmostEqualTo(after.GetEndPoint(1))||width!=duct.Width||height!=duct.Height)throw new Exception("Duct invariant verification failed; transaction will roll back.");
   if(tx.Commit()!=TransactionStatus.Committed)throw new Exception("Annotation transaction did not commit.");
  }
  var result=new{view=v.Name,viewId=v.Id.IntegerValue,ductId=duct.Id.IntegerValue,cloudId=cloud.Id.IntegerValue,noteId=note.Id.IntegerValue,revisionId=rev.Id.IntegerValue,revisionDescription=rev.Description,noteText=note.Text,ductStartMm=P(a),ductEndMm=P(b),widthMm=width*304.8,heightMm=height*304.8,ductGeometryVerifiedUnchanged=true,saved=false,reusedExistingCloud=prior!=null};
  File.WriteAllText(Path.Combine(Out,"cloud-result.json"),new JavaScriptSerializer().Serialize(result));
  ui.ActiveView=v;ui.Selection.SetElementIds(new List<ElementId>());ui.RefreshActiveView();var uiv=ui.GetOpenUIViews().First(x=>x.ViewId==v.Id);
  uiv.ZoomAndCenterRectangle(new XYZ(x0-F(500),y0-F(600),z),new XYZ(x1+F(2100),y1+F(900),z));ui.RefreshActiveView();return Result.Succeeded;
 }catch(Exception ex){message=ex.ToString();File.WriteAllText(Path.Combine(Out,"cloud-error.txt"),message);return Result.Failed;}}
}
[Transaction(TransactionMode.Manual)]public class CaptureCloud:IExternalCommand {
 public Result Execute(ExternalCommandData c,ref string m,ElementSet e){try{var d=c.Application.ActiveUIDocument.Document;if(d.ActiveView.Name!="E04_HoL")throw new Exception("E04_HoL must be active");d.ExportImage(new ImageExportOptions{FilePath=Path.Combine(@"F:\Hoa Le\1. Working\Tools\MEP_Compare_Project_From_JSON_ver1\MEP_Compare_Project_From_JSON_ver1\CadProbe","E04_HoL_14465104_CLOUD"),ExportRange=ExportRange.VisibleRegionOfCurrentView,HLRandWFViewsFileType=ImageFileType.PNG,ShadowViewsFileType=ImageFileType.PNG,ZoomType=ZoomFitType.FitToPage,PixelSize=2400});return Result.Succeeded;}catch(Exception ex){m=ex.ToString();return Result.Failed;}}
}
}
