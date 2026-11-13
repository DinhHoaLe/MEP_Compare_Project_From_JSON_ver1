using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace CadInspect {
[Transaction(TransactionMode.Manual)]
public class CaptureDuct : IExternalCommand {
 const string Out=@"F:\Hoa Le\1. Working\Tools\MEP_Compare_Project_From_JSON_ver1\MEP_Compare_Project_From_JSON_ver1\CadProbe";
 static double F(double mm){return mm/304.8;}
 static void Style(View v,ElementId duct,ElementId cad){
  v.DetailLevel=ViewDetailLevel.Fine;v.DisplayStyle=DisplayStyle.HLR;v.Scale=10;
  var r=new OverrideGraphicSettings();r.SetProjectionLineColor(new Color(220,35,35));r.SetCutLineColor(new Color(220,35,35));r.SetProjectionLineWeight(5);r.SetCutLineWeight(5);v.SetElementOverrides(duct,r);
  var b=new OverrideGraphicSettings();b.SetProjectionLineColor(new Color(30,95,210));b.SetCutLineColor(new Color(30,95,210));b.SetProjectionLineWeight(2);b.SetCutLineWeight(2);v.SetElementOverrides(cad,b);
  v.IsolateElementsTemporary(new List<ElementId>{duct,cad});
 }
 static void Export(Document d,string name){d.ExportImage(new ImageExportOptions{FilePath=Path.Combine(Out,name),ExportRange=ExportRange.VisibleRegionOfCurrentView,HLRandWFViewsFileType=ImageFileType.PNG,ShadowViewsFileType=ImageFileType.PNG,ZoomType=ZoomFitType.FitToPage,PixelSize=2200});}
 public Result Execute(ExternalCommandData c,ref string message,ElementSet elements){try{
  var ui=c.Application.ActiveUIDocument;var d=ui.Document;var duct=d.GetElement(new ElementId(14465104)) as Duct;
  if(duct==null||duct.UniqueId!="3d4a7298-6410-4422-9cc1-da9060db4b36-00dcb850")throw new Exception("Expected duct identity not found. No views created.");
  var cad=d.GetElement(new ElementId(14512644)) as ImportInstance;if(cad==null)throw new Exception("Expected CAD link not found.");
  var plan=new FilteredElementCollector(d).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().First(v=>v.Name=="E04_HoL");
  var curve=((LocationCurve)duct.Location).Curve;XYZ mid=(curve.GetEndPoint(0)+curve.GetEndPoint(1))*0.5;
  ViewPlan inspectPlan;ViewSection section;string suffix=DateTime.Now.ToString("HHmmss");
  using(var tx=new Transaction(d,"Create inspection views for duct 14465104 - no clouds")){
   tx.Start();
   inspectPlan=(ViewPlan)d.GetElement(plan.Duplicate(ViewDuplicateOption.Duplicate));inspectPlan.Name="CHECK_14465104_PLAN_"+suffix;inspectPlan.ViewTemplateId=ElementId.InvalidElementId;
   Style(inspectPlan,duct.Id,cad.Id);
   var sectionType=new FilteredElementCollector(d).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(vft=>vft.ViewFamily==ViewFamily.Section);
   var t=Transform.Identity;t.Origin=mid;t.BasisX=XYZ.BasisX;t.BasisY=XYZ.BasisZ;t.BasisZ=-XYZ.BasisY;
   var bb=new BoundingBoxXYZ{Transform=t,Min=new XYZ(F(-2200),F(-950),0),Max=new XYZ(F(2200),F(950),F(650))};
   section=ViewSection.CreateSection(d,sectionType.Id,bb);section.Name="CHECK_14465104_SECTION_"+suffix;section.ViewTemplateId=ElementId.InvalidElementId;
   Style(section,duct.Id,cad.Id);section.CropBoxActive=true;section.CropBoxVisible=false;
   tx.Commit();
  }
  ui.Selection.SetElementIds(new List<ElementId>());
  ui.ActiveView=inspectPlan;ui.RefreshActiveView();var pv=ui.GetOpenUIViews().First(v=>v.ViewId==inspectPlan.Id);
  pv.ZoomAndCenterRectangle(new XYZ(mid.X-F(2200),mid.Y-F(1700),mid.Z),new XYZ(mid.X+F(2200),mid.Y+F(1700),mid.Z));ui.RefreshActiveView();Export(d,"14465104_PLAN");
  ui.ActiveView=section;ui.RefreshActiveView();var sv=ui.GetOpenUIViews().First(v=>v.ViewId==section.Id);
  sv.ZoomAndCenterRectangle(mid-new XYZ(F(2300),0,F(1050)),mid+new XYZ(F(2300),0,F(1050)));ui.RefreshActiveView();Export(d,"14465104_SECTION");
  File.WriteAllText(Path.Combine(Out,"inspection-views.txt"),"Target duct: 14465104\r\nRVT red; CAD blue.\r\nPlan: "+inspectPlan.Name+" ("+inspectPlan.Id.IntegerValue+")\r\nSection: "+section.Name+" ("+section.Id.IntegerValue+")\r\nSection plane Y="+(mid.Y*304.8).ToString("R")+" mm; looks toward -Y, X right and Z up.\r\nCenter Z="+(mid.Z*304.8).ToString("R")+" mm.\r\nOnly inspection views created. Original E04_HoL unchanged. No clouds, model geometry edits, save or synchronization.");
  return Result.Succeeded;
 }catch(Exception ex){message=ex.ToString();File.WriteAllText(Path.Combine(Out,"inspection-error.txt"),message);return Result.Failed;}}
}
}

