using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BimComparison.Core;
namespace BimComparison.Extraction
{
    public sealed class MEPSystemExtractor
    {
        private static PointData Point(XYZ p)=>new PointData{X=p.X,Y=p.Y,Z=p.Z};
        public void Extract(Element e,ElementSnapshot item,bool systems,bool connectors)
        {
            var mep=new MepSnapshot{Status="Complete"};var ports=new ConnectorSnapshot{Status="Complete"};
            if(systems)item.Mep=mep;if(connectors)item.Connectors=ports;
            ConnectorManager manager=null;
            try
            {
                var curve=e as MEPCurve;
                var family=e as FamilyInstance;
                manager=curve?.ConnectorManager??family?.MEPModel?.ConnectorManager;
                if(manager==null){mep.Status="NotApplicable";ports.Status="NotApplicable";}
                if(systems)
                {
                    Dimension(e,mep,"PipeDiameter",BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                    Dimension(e,mep,"DuctWidth",BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                    Dimension(e,mep,"DuctHeight",BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                    Dimension(e,mep,"DuctDiameter",BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                    Dimension(e,mep,"Length",BuiltInParameter.CURVE_ELEM_LENGTH);
                    if(mep.Dimensions.Count>0)mep.Status="Complete";
                    if(curve?.MEPSystem!=null)AddSystem(curve.MEPSystem,mep);
                    if(family?.MEPModel!=null)
                    {
                        try
                        {
                            var electrical=family.MEPModel.GetElectricalSystems();
                            if(electrical!=null)foreach(var system in electrical)AddSystem(system,mep);
                        }
                        catch(Exception ex){mep.Errors.Add("Electrical systems: "+ex.Message);}
                    }
                }
                if(manager!=null)
                    foreach(Connector c in manager.Connectors)
                    {
                        if(systems)
                        {
                            try{if(c.MEPSystem!=null)AddSystem(c.MEPSystem,mep);}
                            catch(Exception ex){mep.Errors.Add("Connector system: "+ex.Message);}
                        }
                        if(!connectors)continue;
                        var data=new ConnectorData();ports.Items.Add(data);
                        try
                        {
                            data.Domain=c.Domain.ToString();data.Type=c.ConnectorType.ToString();
                            // Logical connectors do not expose physical coordinate/shape/size data.
                            if(c.ConnectorType!=ConnectorType.Logical)
                            {
                                data.Origin=Point(c.Origin);data.Direction=Point(c.CoordinateSystem.BasisZ.Normalize());
                                data.Shape=c.Shape.ToString();
                                if(c.Shape==ConnectorProfileType.Round)data.Radius=c.Radius;
                                else if(c.Shape==ConnectorProfileType.Rectangular||c.Shape==ConnectorProfileType.Oval){data.Width=c.Width;data.Height=c.Height;}
                            }
                            foreach(Connector other in c.AllRefs)
                            {
                                if(other.Owner==null||other.Owner.Id==e.Id)continue;
                                // Owner identity is stable across revisions; connector ids are not guaranteed stable.
                                data.References.Add(other.Owner.UniqueId+"|"+other.Domain+"|"+other.ConnectorType);
                                data.ConnectedPorts.Add(new ConnectedPortReference{OwnerUniqueId=other.Owner.UniqueId,Domain=other.Domain.ToString(),Type=other.ConnectorType.ToString(),
                                    Origin=other.ConnectorType==ConnectorType.Logical?null:Point(other.Origin)});
                            }
                            data.References=data.References.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToList();
                        }
                        catch(Exception ex){ports.Errors.Add("Connector extraction: "+ex.Message);}
                    }
            }
            catch(Exception ex){if(systems)mep.Errors.Add(ex.Message);if(connectors)ports.Errors.Add(ex.Message);}
            if(mep.Errors.Count>0)mep.Status="Partial";
            if(ports.Errors.Count>0)ports.Status="Partial";
        }
        private static void Dimension(Element e,MepSnapshot m,string name,BuiltInParameter id)
        {var p=e.get_Parameter(id);if(p!=null&&p.HasValue&&p.StorageType==StorageType.Double)m.Dimensions[name]=p.AsDouble();}
        private static void AddSystem(MEPSystem system,MepSnapshot m)
        {
            if(m.Systems.Any(s=>s.UniqueId==system.UniqueId))return;
            var type=system.Document.GetElement(system.GetTypeId());
            m.Systems.Add(new SystemSnapshot{UniqueId=system.UniqueId,Name=system.Name,TypeUniqueId=type?.UniqueId,TypeName=type?.Name});
            m.Status="Complete";
        }
    }
}
