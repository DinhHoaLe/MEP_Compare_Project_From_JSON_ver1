using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace CadInspectViews {
public static class Util {
 public const string Out=@"F:\Hoa Le\1. Working\Tools\MEP_Compare_Project_From_JSON_ver1\MEP_Compare_Project_From_JSON_ver1\CadProbe";
 public static View Find(Document d,string kind){return new FilteredElementCollector(d).OfClass(typeof(View)).Cast<View>().Where(v=>!v.IsTemplate&&v.Name.StartsWith("CHECK_14465104_"+kind+"_")).OrderByDescending(v=>v.Id.IntegerValue).First();}
 public static void Prep(Document d,View v){
  if(v.IsTemporaryHideIsolateActive())v.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
  var keep=new List<ElementId>{new ElementId(14465104),new ElementId(14512644)};
  v.IsolateElementsTemporary(keep);v.ConvertTemporaryHideIsolateToPermanent();
  foreach(Category cat in d.Settings.Categories){if(cat.SubCategories==null)continue;foreach(Category sub in cat.SubCategories){string n=sub.Name;if(n.StartsWith("RLT_")||n=="CAD"||n=="0")continue;try{if(cat.Name.Contains("412_11115"))v.SetCategoryHidden(sub.Id,true);}catch{}}}
  v.DisplayStyle=DisplayStyle.Wireframe;
  var b=new OverrideGraphicSettings();b.SetProjectionLineColor(new Color(30,95,210));b.SetCutLineColor(new Color(30,95,210));b.SetProjectionLineWeight(2);b.SetCutLineWeight(2);b.SetSurfaceTransparency(80);v.SetElementOverrides(new ElementId(14512644),b);
 }
 public static Result Run(ExternalCommandData c,string kind,ref string msg){try{
  var ui=c.Application.ActiveUIDocument;var d=ui.Document;var v=Find(d,kind);var duct=d.GetElement(new ElementId(14465104));var lc=(LocationCurve)duct.Location;var mid=(lc.Curve.GetEndPoint(0)+lc.Curve.GetEndPoint(1))*0.5;
  using(var tx=new Transaction(d,"Prepare inspection view "+kind)){tx.Start();Prep(d,v);if(kind=="PLAN"){var box=v.CropBox;var inverse=box.Transform.Inverse;box.Min=inverse.OfPoint(mid-new XYZ(2200/304.8,1700/304.8,1000/304.8));box.Max=inverse.OfPoint(mid+new XYZ(2200/304.8,1700/304.8,1000/304.8));v.CropBox=box;v.CropBoxActive=true;v.CropBoxVisible=false;}tx.Commit();}
  ui.ActiveView=v;ui.RefreshActiveView();var uiv=ui.GetOpenUIViews().First(x=>x.ViewId==v.Id);if(kind=="PLAN")uiv.ZoomAndCenterRectangle(mid-new XYZ(2300/304.8,1800/304.8,0),mid+new XYZ(2300/304.8,1800/304.8,0));else uiv.ZoomAndCenterRectangle(mid-new XYZ(2300/304.8,0,1050/304.8),mid+new XYZ(2300/304.8,0,1050/304.8));ui.RefreshActiveView();return Result.Succeeded;
 }catch(Exception ex){msg=ex.ToString();File.WriteAllText(Path.Combine(Out,"prepare-error.txt"),msg);return Result.Failed;}}
}
[Transaction(TransactionMode.Manual)] public class OpenPlan:IExternalCommand {public Result Execute(ExternalCommandData c,ref string m,ElementSet e){return Util.Run(c,"PLAN",ref m);}}
[Transaction(TransactionMode.Manual)] public class OpenSection:IExternalCommand {public Result Execute(ExternalCommandData c,ref string m,ElementSet e){return Util.Run(c,"SECTION",ref m);}}
[Transaction(TransactionMode.Manual)] public class ExportCurrent:IExternalCommand {public Result Execute(ExternalCommandData c,ref string m,ElementSet e){try{var d=c.Application.ActiveUIDocument.Document;var v=d.ActiveView;if(!v.Name.StartsWith("CHECK_14465104_"))throw new Exception("Open inspection view first");string kind=v is ViewPlan?"PLAN":"SECTION";d.ExportImage(new ImageExportOptions{FilePath=Path.Combine(Util.Out,"14465104_"+kind),ExportRange=ExportRange.VisibleRegionOfCurrentView,HLRandWFViewsFileType=ImageFileType.PNG,ShadowViewsFileType=ImageFileType.PNG,ZoomType=ZoomFitType.FitToPage,PixelSize=2200});return Result.Succeeded;}catch(Exception ex){m=ex.ToString();File.WriteAllText(Path.Combine(Util.Out,"capture-error.txt"),m);return Result.Failed;}}}
}
