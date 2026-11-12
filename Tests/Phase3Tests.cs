using System;
using BimComparison.Core;
internal static class Phase3Tests
{
    internal static void Run(Action<bool,string> check)
    {
        var element=new ElementResult{Status="Added"};
        var color=HighlightPalette.For(element,false);check(color.G==200&&color.R==0,"P3 added is green");
        element.Status="Deleted";color=HighlightPalette.For(element,false);check(color.R==230&&color.G==0,"P3 deleted palette is red (report only in B)");
        element.Status="Unchanged";check(HighlightPalette.For(element,true)==null,"P3 unchanged has no override");
        element.Status="Modified";color=HighlightPalette.For(element,false);check(color.R==255&&color.G==220&&color.B==0,"P3 default modified is yellow");
        element.Changes.Add(new PropertyChange{ChangeType="MEPSystemChanged"});color=HighlightPalette.For(element,true);check(color.R==160&&color.B==240,"P3 system is purple");
        element.Changes.Add(new PropertyChange{ChangeType="LocationChanged"});color=HighlightPalette.For(element,true);check(color.B==255&&color.R==0,"P3 location has priority over system");
        element.Changes.Add(new PropertyChange{ChangeType="GeometryChanged"});color=HighlightPalette.For(element,true);check(color.R==255&&color.G==140,"P3 geometry has highest modified priority");
        color=HighlightPalette.For(element,false);check(color.G==220,"P3 classification toggle restores default modified color");
    }
}
