using System.Reflection;
using Autodesk.Revit.UI;
namespace BimComparison
{
    public sealed class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            var panel=app.CreateRibbonPanel("BIM Model Comparison");
            Add(panel,"ExportJson","Export Model\nJSON",typeof(Commands.ExportJsonCommand));
            Add(panel,"CompareModels","Compare RVT\nModels",typeof(Commands.CompareModelsCommand));
            Add(panel,"ExportReport","Export Comparison\nReport",typeof(Commands.ExportReportCommand));
            return Result.Succeeded;
        }
        private static void Add(RibbonPanel panel,string id,string text,System.Type command)
        {
            var button=new PushButtonData(id,text,Assembly.GetExecutingAssembly().Location,command.FullName);
            if(command!=typeof(Commands.ExportJsonCommand)) button.AvailabilityClassName=typeof(Commands.JsonCommandAvailability).FullName;
            panel.AddItem(button);
        }
        public Result OnShutdown(UIControlledApplication app)=>Result.Succeeded;
    }
    internal static class Session { internal static Core.ComparisonReport LastReport; }
}
