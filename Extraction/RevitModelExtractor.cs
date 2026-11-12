using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using BimComparison.Core;
namespace BimComparison.Extraction
{
    public static class SupportedCategories
    {
        public static readonly BuiltInCategory[] All = {
            BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_DuctAccessory, BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_CableTray, BuiltInCategory.OST_Conduit, BuiltInCategory.OST_GenericModel };
    }
    public sealed class RevitModelExtractor
    {
        private static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        public ModelSnapshot Extract(Document doc,ComparisonSettings settings,Action<string> progress=null)
        {
            ModelComparisonEngine.ValidateSettings(settings);
            if(settings.Categories.Any(id=>!SupportedCategories.All.Contains((BuiltInCategory)id))) throw new ArgumentException("Unsupported category.");
            var snapshot=new ModelSnapshot {SchemaVersion="2.0",Scope="Phase2-Geometry-MEP-Connectors",ModelName=doc.Title,ModelPath=doc.PathName,ProjectUniqueId=doc.ProjectInformation.UniqueId,
                RevitVersion=doc.Application.VersionNumber,Settings=settings};
            snapshot.Warnings.Add("Host model instances only; links and element types are excluded as independent rows. Type parameters are captured on instances. Coordinates use internal origin and feet.");
            snapshot.Warnings.Add("Location curves record endpoints; enable Detailed geometry to compare tessellated shape. Geometry uses Fine detail and excludes non-visible objects.");
            using(var links=new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)))
                foreach(RevitLinkType link in links)
                    snapshot.Warnings.Add("Excluded Revit link: "+link.Name+"; loaded="+RevitLinkType.IsLoaded(doc,link.Id));
            using(var filter=new ElementMulticategoryFilter(settings.Categories.Select(x=>(BuiltInCategory)x).ToList()))
            using(var collector=new FilteredElementCollector(doc).WhereElementIsNotElementType().WherePasses(filter))
            {
                int count=0;
                foreach(var e in collector)
                {
                    try
                    {
                        var type=doc.GetElement(e.GetTypeId()) as ElementType;
                        var item=new ElementSnapshot {UniqueId=e.UniqueId,ElementId=e.Id.IntegerValue,CategoryId=e.Category.Id.IntegerValue,
                            Category=e.Category.Name,Family=type?.FamilyName,Type=type?.Name,TypeUniqueId=type?.UniqueId};
                        // Keep the element row even if one component cannot be extracted.
                        snapshot.Elements.Add(item);
                        Try(snapshot,e,"identity",()=> {
                            item.Workset=doc.GetWorksetTable().GetWorkset(e.WorksetId)?.Name;
                            item.DesignOption=e.DesignOption?.UniqueId;
                            item.CreatedPhase=doc.GetElement(e.CreatedPhaseId)?.UniqueId;
                            item.DemolishedPhase=doc.GetElement(e.DemolishedPhaseId)?.UniqueId;
                        });
                        if(settings.Parameters)
                        {
                            ExtractParameters(doc,e,"instance:",item,snapshot);
                            if(type!=null) ExtractParameters(doc,type,"type:",item,snapshot);
                        }
                        if(settings.Location) Try(snapshot,e,"location",()=>item.Location=Location(e.Location));
                        if(settings.Geometry)
                        {
                            item.Geometry=new GeometryExtractor().Extract(e,settings.GeometryMode);
                            snapshot.ExtractionErrors.AddRange(item.Geometry.Errors.Select(error=>e.Id.IntegerValue+" geometry: "+error));
                        }
                        if(settings.Mep||settings.Connectors)
                        {
                            new MEPSystemExtractor().Extract(e,item,settings.Mep,settings.Connectors);
                            if(settings.Mep)snapshot.ExtractionErrors.AddRange(item.Mep.Errors.Select(error=>e.Id.IntegerValue+" MEP: "+error));
                            if(settings.Connectors)snapshot.ExtractionErrors.AddRange(item.Connectors.Errors.Select(error=>e.Id.IntegerValue+" connectors: "+error));
                        }
                    }
                    catch(Exception ex) { snapshot.ExtractionErrors.Add(e.Id.IntegerValue+" element: "+ex.Message); }
                    if(++count%250==0) progress?.Invoke("Extracting "+doc.Title+": "+count+" elements");
                }
            }
            return snapshot;
        }
        private static void Try(ModelSnapshot s,Element e,string component,Action action)
        { try {action();} catch(Exception ex) {s.ExtractionErrors.Add(e.Id.IntegerValue+" "+component+": "+ex.Message);} }
        private static void ExtractParameters(Document doc,Element e,string scope,ElementSnapshot item,ModelSnapshot snapshot)
        {
            foreach(Parameter p in e.Parameters)
            {
                try
                {
                    string key; string guid=null; string builtIn=null;
                    if(p.IsShared) {guid=p.GUID.ToString("D"); key="shared:"+guid;}
                    else if(p.Id.IntegerValue<0) {builtIn=p.Id.IntegerValue.ToString(Inv); key="builtin:"+builtIn;}
                    else
                    {
                        var definition=doc.GetElement(p.Id);
                        if(definition==null) throw new InvalidOperationException("No stable ParameterElement identity for "+p.Definition.Name);
                        key="project:"+definition.UniqueId;
                    }
                    var spec=p.Definition.GetDataType();
                    var value=new ParameterSnapshot {Key=scope+key,Name=p.Definition.Name,BuiltInId=builtIn,Guid=guid,
                        StorageType=p.StorageType.ToString(),HasValue=p.HasValue,IsReadOnly=p.IsReadOnly,Spec=spec.TypeId};
                    if(p.HasValue)
                    {
                        switch(p.StorageType)
                        {
                            case StorageType.Double: value.RawValue=p.AsDouble().ToString("R",Inv); break;
                            case StorageType.Integer: value.RawValue=p.AsInteger().ToString(Inv); break;
                            case StorageType.String: value.RawValue=p.AsString(); break;
                            case StorageType.ElementId:
                                var id=p.AsElementId();
                                value.RawValue=id.IntegerValue<0?"sentinel:"+id.IntegerValue.ToString(Inv):doc.GetElement(id)?.UniqueId;
                                if(id.IntegerValue>=0&&value.RawValue==null) throw new InvalidOperationException("Unresolved ElementId parameter reference "+id.IntegerValue);
                                break;
                        }
                        value.DisplayValue=p.AsValueString();
                        if(p.StorageType==StorageType.Double&&UnitUtils.IsMeasurableSpec(spec)) value.Unit=p.GetUnitTypeId().TypeId;
                    }
                    if(item.Parameters.Any(x=>x.Key==value.Key)) throw new InvalidOperationException("Duplicate parameter key "+value.Key);
                    item.Parameters.Add(value);
                }
                catch(Exception ex) {snapshot.ExtractionErrors.Add(e.Id.IntegerValue+" parameter "+p.Id.IntegerValue+": "+ex.Message);}
            }
        }
        private static PointData Point(XYZ p)=>new PointData {X=p.X,Y=p.Y,Z=p.Z};
        private static LocationSnapshot Location(Location loc)
        {
            var point=loc as LocationPoint;
            if(point!=null) return new LocationSnapshot {Kind="Point",Point=Point(point.Point),Rotation=point.Rotation};
            var curve=loc as LocationCurve;
            if(curve!=null) return new LocationSnapshot {Kind="Curve:"+curve.Curve.GetType().Name,Start=Point(curve.Curve.GetEndPoint(0)),End=Point(curve.Curve.GetEndPoint(1))};
            return new LocationSnapshot {Kind=loc==null?"None":"Unsupported:"+loc.GetType().Name};
        }
    }
}
