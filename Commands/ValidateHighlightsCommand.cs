using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimComparison.Core;
using BimComparison.Visualization;
using Newtonsoft.Json;
namespace BimComparison.Commands
{
    // Optional integration check for Add-In Manager. Not placed on the production Ribbon.
    // All test modifications, including any existing highlight state, roll back as a group.
    [Transaction(TransactionMode.Manual)]
    public sealed class ValidateHighlightsCommand : IExternalCommand
    {
        private static string State(View view,ElementId id)
        {using(var settings=view.GetElementOverrides(id))return JsonConvert.SerializeObject(OverrideState.Capture(settings));}
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)
        {
            try
            {
                var app=data.Application;var ui=app.ActiveUIDocument??throw new InvalidOperationException("Open a project, then select one model element.");
                var id=ui.Selection.GetElementIds().FirstOrDefault()??throw new InvalidOperationException("Select one model element to run the reversible highlight integration check.");
                var doc=ui.Document;var view=ui.ActiveView;var element=doc.GetElement(id);int checks=0;
                using(var group=new TransactionGroup(doc,"BIM Comparison: Reversible Integration Test"))
                {
                    group.Start();
                    try
                    {
                        new HighlightService().Clear(app);
                        using(var transaction=new Transaction(doc,"Test original graphics"))
                        {
                            transaction.Start();
                            using(var settings=view.GetElementOverrides(id))
                            {
                                settings.SetProjectionLineColor(new Color(23,45,67));settings.SetCutLineColor(new Color(80,90,100));
                                settings.SetSurfaceTransparency(37);settings.SetHalftone(true);settings.SetProjectionLineWeight(5);
                                view.SetElementOverrides(id,settings);
                            }
                            if(transaction.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException("Test setup failed.");
                        }
                        string before=State(view,id);
                        var report=new ComparisonReport{ProjectB=doc.ProjectInformation.UniqueId};
                        var result=new ElementResult{Status="Modified",Element=new ElementSnapshot{UniqueId=element.UniqueId}};
                        report.ModifiedElements.Add(result);
                        new HighlightService().Apply(app,report,new[]{result},false,true);
                        if(State(view,id)==before)throw new InvalidOperationException("Highlight did not change the selected element override.");checks++;
                        result.Changes.Add(new PropertyChange{ChangeType="GeometryChanged"});
                        new HighlightService().Apply(app,report,new[]{result},true,false);
                        // A new service instance must recover persisted originals, not the preceding highlight color.
                        var cleared=new HighlightService().Clear(app);
                        if(cleared.Restored!=1||State(view,id)!=before)throw new InvalidOperationException("Clear did not restore the complete original override after repeated highlighting.");checks++;
                        if(new HighlightService().Clear(app).Restored!=0)throw new InvalidOperationException("Repeated Clear must be a no-op.");checks++;
                        new HighlightService().Apply(app,report,new[]{result},false,true);
                        var replacement=new ComparisonReport{ProjectB=report.ProjectB};
                        new HighlightService().Apply(app,replacement,Enumerable.Empty<ElementResult>(),false,true);
                        if(State(view,id)!=before)throw new InvalidOperationException("Replacing with an empty result did not restore stale highlights.");checks++;
                    }
                    finally
                    {
                        if(group.GetStatus()==TransactionStatus.Started&&group.RollBack()!=TransactionStatus.RolledBack)throw new InvalidOperationException("Integration test group could not roll back.");
                    }
                }
                ui.RefreshActiveView();TaskDialog.Show("Highlight validation",checks+" Revit integration checks passed. All test modifications were rolled back.");
                return Result.Succeeded;
            }
            catch(Exception ex){message=ex.ToString();return Result.Failed;}
        }
    }
}
