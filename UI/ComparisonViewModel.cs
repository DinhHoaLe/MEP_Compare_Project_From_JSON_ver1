using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using BimComparison.Core;
namespace BimComparison.UI
{
    public sealed class CategoryChoice
    {
        public int Id {get;set;}
        public string Name {get;set;}
        public bool Selected {get;set;}=true;
    }
    public sealed class ResultRow
    {
        public ElementResult Result {get;set;}
        public PropertyChange Change {get;set;}
        public string Status=>Result.Status;
        public int ElementId=>Result.Element.ElementId;
        public string UniqueId=>Result.Element.UniqueId;
        public string Category=>Result.Element.Category;
        public string Family=>Result.Element.Family;
        public string Type=>Result.Element.Type;
        public string ChangeType=>Change?.ChangeType;
        public string Property=>Change?.Property;
        public string OldValue=>Change?.OldValue;
        public string NewValue=>Change?.NewValue;
    }
    public sealed class ComparisonViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName]string name=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
        public ObservableCollection<CategoryChoice> Categories {get;}
        public bool CompareParameters {get;set;}=true;
        public bool CompareLocation {get;set;}=true;
        public bool CompareGeometry {get;set;}=true;
        public bool CompareMep {get;set;}=true;
        public bool CompareConnectors {get;set;}=true;
        public string GeometryMode {get;set;}="Detailed";
        public string[] GeometryModes {get;}={"Lightweight","Detailed"};
        public string AreaTolerance {get;set;}="0.00001";
        public string VolumeTolerance {get;set;}="0.000001";
        public string LengthMm {get;set;}="1";
        public string NumberTolerance {get;set;}="0.00000001";
        public string AngleTolerance {get;set;}="0.000001";
        private string modelA="",modelB="",status="Ready",summary="No comparison yet",search="",filter="All",warnings="";
        public string ModelA {get=>modelA;set{modelA=value;Notify();}}
        public string ModelB {get=>modelB;set{modelB=value;Notify();}}
        public string Status {get=>status;set{status=value;Notify();}}
        public string Summary {get=>summary;set{summary=value;Notify();}}
        public string Warnings {get=>warnings;set{warnings=value;Notify();}}
        public string Search {get=>search;set{search=value;Notify();RowsView.Refresh();}}
        public string Filter {get=>filter;set{filter=value;Notify();RowsView.Refresh();}}
        public string[] Filters {get;}={"All","Added","Deleted","Modified","Unchanged"};
        public ObservableCollection<ResultRow> Rows {get;private set;}=new ObservableCollection<ResultRow>();
        public ICollectionView RowsView {get;private set;}
        public ComparisonViewModel(System.Collections.Generic.IEnumerable<CategoryChoice> categories)
        {
            Categories=new ObservableCollection<CategoryChoice>(categories);
            RowsView=CreateView(Rows);
        }
        private ICollectionView CreateView(ObservableCollection<ResultRow> rows)
        {
            var view=CollectionViewSource.GetDefaultView(rows);
            view.Filter=o=> {var r=(ResultRow)o;return (Filter=="All"||r.Status==Filter)&&(string.IsNullOrEmpty(Search)||
                string.Join(" ",r.UniqueId,r.ElementId,r.Category,r.Family,r.Type,r.ChangeType,r.Property,r.OldValue,r.NewValue).IndexOf(Search,System.StringComparison.OrdinalIgnoreCase)>=0);};
            return view;
        }
        public ComparisonSettings Settings()
        {
            var inv=System.Globalization.CultureInfo.InvariantCulture;
            var s=new ComparisonSettings {Categories=Categories.Where(x=>x.Selected).Select(x=>x.Id).ToList(),Parameters=CompareParameters,Location=CompareLocation,
                Geometry=CompareGeometry,Mep=CompareMep,Connectors=CompareConnectors,GeometryMode=GeometryMode,
                AreaToleranceSquareFeet=double.Parse(AreaTolerance,inv),VolumeToleranceCubicFeet=double.Parse(VolumeTolerance,inv),
                LengthToleranceFeet=double.Parse(LengthMm,inv)/304.8,NumberTolerance=double.Parse(NumberTolerance,inv),AngleToleranceRadians=double.Parse(AngleTolerance,inv)};
            ModelComparisonEngine.ValidateSettings(s);return s;
        }
        public void Display(ComparisonReport r)
        {
            // Build off-screen, then replace ItemsSource once. DataGrid may inspect
            // Count/CurrentItem on collection notifications, which is forbidden
            // while the bound CollectionView is inside DeferRefresh.
            var rows=new ObservableCollection<ResultRow>();
            foreach(var item in ComparisonReportService.All(r))
            {
                if(item.Changes.Count==0) rows.Add(new ResultRow{Result=item});
                else foreach(var c in item.Changes) rows.Add(new ResultRow{Result=item,Change=c});
            }
            var view=CreateView(rows);
            foreach(var sort in RowsView.SortDescriptions) view.SortDescriptions.Add(sort);
            Rows=rows;
            RowsView=view;
            Notify(nameof(Rows));
            Notify(nameof(RowsView));
            Summary=$"A: {r.TotalA} | B: {r.TotalB} | Added: {r.AddedElements.Count} | Deleted: {r.DeletedElements.Count} | Modified: {r.ModifiedElements.Count} | Unchanged: {r.UnchangedElements.Count}";
            Warnings=string.Join("\n",r.Warnings.Concat(r.ExtractionErrors));
            Status="Comparison completed";
        }
    }
}
