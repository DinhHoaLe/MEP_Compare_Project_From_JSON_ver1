using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using BimComparison.Core;
namespace BimComparison.Commands
{
    public sealed class JsonCommandAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData,CategorySet selectedCategories)=>true;
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class CompareModelsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)=>Run(data,ref message,false);
        internal static Result Run(ExternalCommandData data,ref string message,bool export)
        {
            try
            {
                var window=new UI.ComparisonWindow(data.Application,export);
                new System.Windows.Interop.WindowInteropHelper(window).Owner=data.Application.MainWindowHandle;
                window.ShowDialog(); // modal: API calls remain within the command's valid execution context
                return Result.Succeeded;
            }
            catch(Exception ex) {message=ex.ToString();return Result.Failed;}
        }
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class ExportJsonCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)=>CompareModelsCommand.Run(data,ref message,true);
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class ExportReportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)
        {
            try
            {
                if(Session.LastReport==null) {TaskDialog.Show("Comparison","Run a comparison first.");return Result.Cancelled;}
                var dialog=new SaveFileDialog {Filter="JSON report|*.json|CSV report|*.csv",FileName="comparison.json"};
                if(dialog.ShowDialog()!=true) return Result.Cancelled;
                ComparisonReportService.Export(dialog.FileName,Session.LastReport);
                return Result.Succeeded;
            }
            catch(Exception ex) {message=ex.ToString();return Result.Failed;}
        }
    }
}
