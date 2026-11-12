using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimComparison.Core;
using BimComparison.Visualization;
namespace BimComparison.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class HighlightChangesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)
        {
            try
            {
                var report=Session.LastReport??throw new InvalidOperationException("Run a comparison first.");
                var result=new HighlightService().Apply(data.Application,report,ComparisonReportService.All(report),false,true);
                TaskDialog.Show("BIM Model Comparison",result.Message+"\nAdded: green; Modified: yellow. Deleted elements are available in the report only.");
                return Result.Succeeded;
            }
            catch(Exception ex){message=ex.ToString();return Result.Failed;}
        }
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class ClearHighlightsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)
        {
            try
            {
                var result=new HighlightService().Clear(data.Application);
                TaskDialog.Show("BIM Model Comparison",result.Restored+" original element overrides restored in the active view. "+result.Missing+" elements no longer exist.");
                return Result.Succeeded;
            }
            catch(Exception ex){message=ex.ToString();return Result.Failed;}
        }
    }
}
