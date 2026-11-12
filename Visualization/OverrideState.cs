using Autodesk.Revit.DB;
namespace BimComparison.Visualization
{
    // All editable Revit 2023 OverrideGraphicSettings properties, including unset values.
    // This is stored on the view so Clear also works after Save/Close/Reopen and Undo/Redo.
    public sealed class OverrideState
    {
        public int[] ProjectionColor {get;set;}
        public int[] CutColor {get;set;}
        public int[] SurfaceForegroundColor {get;set;}
        public int[] SurfaceBackgroundColor {get;set;}
        public int[] CutForegroundColor {get;set;}
        public int[] CutBackgroundColor {get;set;}
        public int ProjectionPattern {get;set;}
        public int CutPattern {get;set;}
        public int SurfaceForegroundPattern {get;set;}
        public int SurfaceBackgroundPattern {get;set;}
        public int CutForegroundPattern {get;set;}
        public int CutBackgroundPattern {get;set;}
        public int ProjectionWeight {get;set;}
        public int CutWeight {get;set;}
        public int Transparency {get;set;}
        public bool Halftone {get;set;}
        public int DetailLevel {get;set;}
        public bool SurfaceForegroundVisible {get;set;}
        public bool SurfaceBackgroundVisible {get;set;}
        public bool CutForegroundVisible {get;set;}
        public bool CutBackgroundVisible {get;set;}
        private static int[] Read(Color c)=>c.IsValid?new[]{(int)c.Red,c.Green,c.Blue}:null;
        private static Color Color(int[] rgb)=>rgb==null?Autodesk.Revit.DB.Color.InvalidColorValue:new Color((byte)rgb[0],(byte)rgb[1],(byte)rgb[2]);
        public static OverrideState Capture(OverrideGraphicSettings g)=>new OverrideState {
            ProjectionColor=Read(g.ProjectionLineColor),CutColor=Read(g.CutLineColor),
            SurfaceForegroundColor=Read(g.SurfaceForegroundPatternColor),SurfaceBackgroundColor=Read(g.SurfaceBackgroundPatternColor),
            CutForegroundColor=Read(g.CutForegroundPatternColor),CutBackgroundColor=Read(g.CutBackgroundPatternColor),
            ProjectionPattern=g.ProjectionLinePatternId.IntegerValue,CutPattern=g.CutLinePatternId.IntegerValue,
            SurfaceForegroundPattern=g.SurfaceForegroundPatternId.IntegerValue,SurfaceBackgroundPattern=g.SurfaceBackgroundPatternId.IntegerValue,
            CutForegroundPattern=g.CutForegroundPatternId.IntegerValue,CutBackgroundPattern=g.CutBackgroundPatternId.IntegerValue,
            ProjectionWeight=g.ProjectionLineWeight,CutWeight=g.CutLineWeight,Transparency=g.Transparency,Halftone=g.Halftone,DetailLevel=(int)g.DetailLevel,
            SurfaceForegroundVisible=g.IsSurfaceForegroundPatternVisible,SurfaceBackgroundVisible=g.IsSurfaceBackgroundPatternVisible,
            CutForegroundVisible=g.IsCutForegroundPatternVisible,CutBackgroundVisible=g.IsCutBackgroundPatternVisible };
        public OverrideGraphicSettings Restore()
        {
            var g=new OverrideGraphicSettings();
            g.SetProjectionLineColor(Color(ProjectionColor));g.SetCutLineColor(Color(CutColor));
            g.SetSurfaceForegroundPatternColor(Color(SurfaceForegroundColor));g.SetSurfaceBackgroundPatternColor(Color(SurfaceBackgroundColor));
            g.SetCutForegroundPatternColor(Color(CutForegroundColor));g.SetCutBackgroundPatternColor(Color(CutBackgroundColor));
            g.SetProjectionLinePatternId(new ElementId(ProjectionPattern));g.SetCutLinePatternId(new ElementId(CutPattern));
            g.SetSurfaceForegroundPatternId(new ElementId(SurfaceForegroundPattern));g.SetSurfaceBackgroundPatternId(new ElementId(SurfaceBackgroundPattern));
            g.SetCutForegroundPatternId(new ElementId(CutForegroundPattern));g.SetCutBackgroundPatternId(new ElementId(CutBackgroundPattern));
            g.SetProjectionLineWeight(ProjectionWeight);g.SetCutLineWeight(CutWeight);g.SetSurfaceTransparency(Transparency);g.SetHalftone(Halftone);g.SetDetailLevel((ViewDetailLevel)DetailLevel);
            g.SetSurfaceForegroundPatternVisible(SurfaceForegroundVisible);g.SetSurfaceBackgroundPatternVisible(SurfaceBackgroundVisible);
            g.SetCutForegroundPatternVisible(CutForegroundVisible);g.SetCutBackgroundPatternVisible(CutBackgroundVisible);
            return g;
        }
    }
}
