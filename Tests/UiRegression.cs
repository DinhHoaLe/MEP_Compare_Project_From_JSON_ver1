using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using BimComparison.Core;
using BimComparison.UI;
internal static class UiRegression
{
    [STAThread]
    private static void Main()
    {
        var vm=new ComparisonViewModel(new[]{new CategoryChoice{Id=-2008044,Name="Pipes"}});
        // Simulate a bound control querying the view when its source changes.
        // The former DeferRefresh + Rows.Add implementation throws here.
        ((INotifyCollectionChanged)vm.Rows).CollectionChanged+=(s,e)=>vm.RowsView.MoveCurrentToFirst();
        vm.PropertyChanged+=(s,e)=>{if(e.PropertyName==nameof(vm.RowsView))vm.RowsView.MoveCurrentToFirst();};
        var r=new ComparisonReport{TotalA=1,TotalB=2};
        r.AddedElements.Add(new ElementResult{Status="Added",Element=new ElementSnapshot{UniqueId="new",ElementId=2}});
        r.ModifiedElements.Add(new ElementResult{Status="Modified",Element=new ElementSnapshot{UniqueId="old",ElementId=1},Changes={new PropertyChange{Property="Location"},new PropertyChange{Property="Diameter"}}});
        vm.Display(r);
        Check(vm.RowsView.Cast<object>().Count()==3,"initial result population");
        vm.Filter="Modified";Check(vm.RowsView.Cast<object>().Count()==2,"status filter");
        vm.Search="Diameter";Check(vm.RowsView.Cast<object>().Count()==1,"search filter");
        vm.RowsView.SortDescriptions.Add(new SortDescription("ElementId",ListSortDirection.Descending));
        vm.Display(r);Check(vm.RowsView.Cast<object>().Count()==1&&vm.RowsView.SortDescriptions.Count==1,"repeat population preserves filters and sorting");
        vm.Search="";vm.Filter="All";
        vm.Display(new ComparisonReport());Check(vm.RowsView.IsEmpty,"empty report replaces prior results");
        var settings=vm.Settings();Check(settings.Geometry&&settings.Mep&&settings.Connectors&&settings.GeometryMode=="Detailed","Phase 2 defaults");
        vm.CompareGeometry=false;vm.CompareMep=false;vm.CompareConnectors=false;
        settings=vm.Settings();Check(!settings.Geometry&&!settings.Mep&&!settings.Connectors,"legacy JSON scope toggles");
        Console.WriteLine("7 UI regression checks passed.");
    }
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
}
