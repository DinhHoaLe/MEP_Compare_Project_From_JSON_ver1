using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.UI;
using BimComparison.Core;
using BimComparison.Extraction;
using Microsoft.Win32;
namespace BimComparison.UI
{
    public partial class ComparisonWindow : Window
    {
        private readonly UIApplication app;
        private readonly ComparisonViewModel vm=new ComparisonViewModel(SupportedCategories.All.Select(x=>new CategoryChoice{Id=(int)x,Name=x.ToString().Replace("OST_","")}));
        private CancellationTokenSource cancellation;
        public ComparisonWindow(UIApplication app,bool export)
        {
            this.app=app; InitializeComponent();DataContext=vm;
            vm.ModelB=app.ActiveUIDocument?.Document.PathName??"No active model — JSON comparison is available";
            vm.Status=export?"Choose categories, then Export active model JSON.":"Choose RVT A or two JSON snapshots.";
            Closing+=(s,e)=>{if(cancellation!=null){cancellation.Cancel();e.Cancel=true;}};
        }
        private void Error(Exception ex) {vm.Status="Failed: "+ex.Message;MessageBox.Show(this,ex.Message,"BIM Model Comparison",MessageBoxButton.OK,MessageBoxImage.Error);}
        private void Progress(string text) {vm.Status=text; /* No dispatcher pumping: prevents reentrant Revit API calls. */}
        private void BrowseA(object sender,RoutedEventArgs e)
        {var d=new OpenFileDialog{Filter="Model or snapshot|*.rvt;*.json"};if(d.ShowDialog(this)==true)vm.ModelA=d.FileName;}
        private void BrowseB(object sender,RoutedEventArgs e)
        {var d=new OpenFileDialog{Filter="JSON snapshot|*.json"};if(d.ShowDialog(this)==true)vm.ModelB=d.FileName;}
        private void ExportJson(object sender,RoutedEventArgs e)
        {
            try
            {
                var doc=app.ActiveUIDocument?.Document??throw new InvalidOperationException("Open a project first.");
                var settings=vm.Settings();var d=new SaveFileDialog{Filter="JSON snapshot|*.json",FileName=doc.Title+".json"};
                if(d.ShowDialog(this)!=true)return;
                var snapshot=new RevitModelExtractor().Extract(doc,settings,Progress);
                JsonSerializerService.Write(d.FileName,snapshot);
                vm.Warnings=string.Join("\n",snapshot.Warnings)+"\n"+string.Join("\n",snapshot.ExtractionErrors);
                vm.Status=$"Exported {snapshot.Elements.Count} elements; {snapshot.ExtractionErrors.Count} extraction errors. See warnings.";
            }
            catch(Exception ex){Error(ex);}
        }
        private void CompareRvt(object sender,RoutedEventArgs e)
        {
            try {var r=ModelWorkflow.CompareRvt(app,vm.ModelA,vm.Settings(),Progress);Session.LastReport=r;vm.Display(r);}
            catch(Exception ex){Error(ex);}
        }
        private async void CompareJson(object sender,RoutedEventArgs e)
        {
            try
            {
                var settings=vm.Settings();var a=vm.ModelA;var b=vm.ModelB;
                cancellation=new CancellationTokenSource();var token=cancellation.Token;
                ControlsPanel.IsEnabled=false;CancelButton.IsEnabled=true;vm.Status="Reading JSON and comparing...";
                var r=await Task.Run(()=> {
                    token.ThrowIfCancellationRequested();var old=JsonSerializerService.Read<ModelSnapshot>(a);
                    token.ThrowIfCancellationRequested();var newer=JsonSerializerService.Read<ModelSnapshot>(b);
                    return new ModelComparisonEngine().Compare(old,newer,settings,token);
                },token);
                Session.LastReport=r;vm.Display(r);
            }
            catch(OperationCanceledException){vm.Status="Comparison cancelled.";}
            catch(Exception ex){Error(ex);}
            finally {cancellation?.Dispose();cancellation=null;ControlsPanel.IsEnabled=true;CancelButton.IsEnabled=false;}
        }
        private void Cancel(object sender,RoutedEventArgs e)=>cancellation?.Cancel();
        private void ExportReport(object sender,RoutedEventArgs e)
        {
            try
            {
                if(Session.LastReport==null)throw new InvalidOperationException("Compare models first.");
                var d=new SaveFileDialog{Filter="JSON report|*.json|CSV report|*.csv",FileName="comparison.json"};
                if(d.ShowDialog(this)==true){ComparisonReportService.Export(d.FileName,Session.LastReport);vm.Status="Report exported.";}
            }
            catch(Exception ex){Error(ex);}
        }
        private void SelectElement(object sender,RoutedEventArgs e)=>Navigate(false);
        private void ZoomElement(object sender,RoutedEventArgs e)=>Navigate(true);
        private void Navigate(bool zoom)
        {
            try
            {
                var row=Results.SelectedItem as ResultRow??throw new InvalidOperationException("Select a result row.");
                if(row.Status=="Deleted")throw new InvalidOperationException("Deleted elements exist only in Model A. Use the report to locate them in the source model.");
                var ui=app.ActiveUIDocument??throw new InvalidOperationException("Open Model B first.");
                if(ui.Document.ProjectInformation.UniqueId!=Session.LastReport.ProjectB)throw new InvalidOperationException("The active project does not match Model B.");
                var element=ui.Document.GetElement(row.UniqueId)??throw new InvalidOperationException("Element is absent from the active model.");
                ui.Selection.SetElementIds(new[]{element.Id});if(zoom)ui.ShowElements(element.Id);
            }
            catch(Exception ex){Error(ex);}
        }
    }
}
