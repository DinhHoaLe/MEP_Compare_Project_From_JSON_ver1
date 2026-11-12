using System.Linq;
namespace BimComparison.Core
{
    public sealed class HighlightColor
    {
        public byte R {get;set;} public byte G {get;set;} public byte B {get;set;}
        public HighlightColor(byte r,byte g,byte b){R=r;G=g;B=b;}
    }
    public static class HighlightPalette
    {
        public static HighlightColor For(ElementResult result,bool classify)
        {
            if(result.Status=="Added")return new HighlightColor(0,200,0);
            if(result.Status=="Deleted")return new HighlightColor(230,0,0);
            if(result.Status!="Modified")return null;
            if(classify)
            {
                // An element has one view override: geometry takes priority, then location, then system.
                if(result.Changes.Any(c=>c.ChangeType=="GeometryChanged"))return new HighlightColor(255,140,0);
                if(result.Changes.Any(c=>c.ChangeType=="LocationChanged"||c.ChangeType=="RotationChanged"))return new HighlightColor(0,100,255);
                if(result.Changes.Any(c=>c.ChangeType=="MEPSystemChanged"))return new HighlightColor(160,32,240);
            }
            return new HighlightColor(255,220,0);
        }
    }
}
