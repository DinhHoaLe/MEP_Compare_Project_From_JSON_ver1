using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimComparison.Core;
namespace BimComparison.Extraction
{
    public static class ModelWorkflow
    {
        // Must be called on Revit's API thread from IExternalCommand. No transactions are opened.
        public static ComparisonReport CompareRvt(UIApplication app,string source,ComparisonSettings settings,Action<string> progress)
        {
            var active=app.ActiveUIDocument?.Document ?? throw new InvalidOperationException("Open Model B first.");
            source=Path.GetFullPath(source);
            if(!File.Exists(source)||!source.EndsWith(".rvt",StringComparison.OrdinalIgnoreCase)) throw new IOException("Select an existing RVT file.");
            foreach(Document d in app.Application.Documents)
                if(!string.IsNullOrEmpty(d.PathName)&&string.Equals(Path.GetFullPath(d.PathName),source,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Model A is already open. Export it to JSON, or close it before comparing RVT files.");
            ModelSnapshot a;
            Document opened=null;
            try
            {
                using(var info=BasicFileInfo.Extract(source))
                using(var options=new OpenOptions())
                {
                    if(info.IsSavedInLaterVersion) throw new InvalidOperationException("Model A was saved in a newer Revit version.");
                    if(info.IsWorkshared)
                    {
                        options.DetachFromCentralOption=DetachFromCentralOption.DetachAndPreserveWorksets;
                        using(var worksets=new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets)) options.SetOpenWorksetsConfiguration(worksets);
                    }
                    progress?.Invoke("Opening Model A (Revit may display link/worksharing warnings)...");
                    opened=app.Application.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(source),options);
                    a=new RevitModelExtractor().Extract(opened,settings,progress);
                    a.ModelPath=source;
                    if(info.IsWorkshared) a.Warnings.Add("Model A opened detached with all worksets; worksharing metadata can differ from active Model B.");
                }
            }
            finally
            {
                if(opened!=null&&!opened.Close(false)) throw new InvalidOperationException("Revit could not close source document. Close it manually without saving.");
            }
            progress?.Invoke("Extracting active Model B...");
            var b=new RevitModelExtractor().Extract(active,settings,progress);
            return new ModelComparisonEngine().Compare(a,b,settings);
        }
    }
}
