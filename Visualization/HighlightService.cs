using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using BimComparison.Core;
using Newtonsoft.Json;
namespace BimComparison.Visualization
{
    public sealed class HighlightOutcome
    {
        public int Applied {get;set;}
        public int Restored {get;set;}
        public int Missing {get;set;}
        public int Deleted {get;set;}
        public bool LinesOnly {get;set;}
        public string Message=>$"Highlighted: {Applied}; restored: {Restored}; missing: {Missing}; deleted (report only): {Deleted}. Active view only."+(LinesOnly?" No drafting solid fill found; line colors only.":"");
    }
    public sealed class HighlightService
    {
        private static readonly Guid SchemaId=new Guid("20B00BB2-DBB3-4864-B80C-DB09EB227D8B");
        private const string FieldName="OriginalOverridesJson";
        private sealed class Ledger
        {
            public int Version {get;set;}=1;
            public Dictionary<string,OverrideState> Originals {get;set;}=new Dictionary<string,OverrideState>(StringComparer.Ordinal);
        }
        private static Schema GetSchema()
        {
            var schema=Schema.Lookup(SchemaId);if(schema!=null)return schema;
            var builder=new SchemaBuilder(SchemaId);builder.SetSchemaName("BIMComparisonOriginalOverridesV1");
            builder.SetReadAccessLevel(AccessLevel.Public);builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(FieldName,typeof(string));return builder.Finish();
        }
        private static Ledger Read(View view,Schema schema)
        {
            var entity=view.GetEntity(schema);if(!entity.IsValid())return new Ledger();
            var ledger=JsonConvert.DeserializeObject<Ledger>(entity.Get<string>(schema.GetField(FieldName)),new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,MaxDepth=32});
            if(ledger==null||ledger.Version!=1||ledger.Originals==null||ledger.Originals.Any(x=>string.IsNullOrWhiteSpace(x.Key)||x.Value==null))
                throw new InvalidOperationException("Stored highlight recovery data is invalid. No graphics have been changed.");
            return ledger;
        }
        private static void Save(View view,Schema schema,Ledger ledger)
        {
            if(ledger.Originals.Count==0){view.DeleteEntity(schema);return;}
            var entity=new Entity(schema);entity.Set(schema.GetField(FieldName),JsonConvert.SerializeObject(ledger,Formatting.None));view.SetEntity(entity);
        }
        private static UIDocument Validate(UIApplication app)
        {
            var ui=app.ActiveUIDocument??throw new InvalidOperationException("Open Model B first.");
            if(ui.Document.IsReadOnly)throw new InvalidOperationException("The active document is read-only.");
            if(ui.ActiveView.IsTemplate||!ui.ActiveView.AreGraphicsOverridesAllowed())throw new InvalidOperationException("The active view does not support element graphics overrides. Open a plan, section or 3D view.");
            return ui;
        }
        private static void Commit(Transaction transaction)
        {
            if(transaction.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException("Revit did not commit the highlight transaction. No recovery data was discarded.");
        }
        private static void Restore(View view,Ledger ledger,HighlightOutcome result)
        {
            foreach(var pair in ledger.Originals)
            {
                var element=view.Document.GetElement(pair.Key);if(element==null){result.Missing++;continue;}
                using(var settings=pair.Value.Restore())view.SetElementOverrides(element.Id,settings);
                result.Restored++;
            }
            ledger.Originals.Clear();
        }
        public HighlightOutcome Apply(UIApplication app,ComparisonReport report,IEnumerable<ElementResult> elements,bool classify,bool replace)
        {
            if(report==null)throw new InvalidOperationException("Run a comparison first.");
            var ui=Validate(app);var doc=ui.Document;var view=ui.ActiveView;
            if(doc.ProjectInformation.UniqueId!=report.ProjectB)throw new InvalidOperationException("The active project does not match Model B. Open the correct Model B revision before highlighting.");
            var targets=elements.Where(e=>e!=null&&(e.Status=="Added"||e.Status=="Modified"||e.Status=="Deleted")).GroupBy(e=>e.Element.UniqueId,StringComparer.Ordinal).Select(g=>g.First()).ToList();
            var schema=GetSchema();var ledger=Read(view,schema);var outcome=new HighlightOutcome();
            ElementId solid=ElementId.InvalidElementId;
            using(var collector=new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)))
                foreach(FillPatternElement fill in collector)
                    if(fill.GetFillPattern().IsSolidFill&&fill.GetFillPattern().Target==FillPatternTarget.Drafting){solid=fill.Id;break;}
            outcome.LinesOnly=solid==ElementId.InvalidElementId;
            using(var transaction=new Transaction(doc,"BIM Comparison: Highlight Changes"))
            {
                transaction.Start();
                transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions().SetForcedModalHandling(true).SetClearAfterRollback(true));
                if(replace)Restore(view,ledger,outcome);
                foreach(var target in targets)
                {
                    if(target.Status=="Deleted"){outcome.Deleted++;continue;}
                    var rgb=HighlightPalette.For(target,classify);if(rgb==null)continue;
                    var element=doc.GetElement(target.Element.UniqueId);if(element==null){outcome.Missing++;continue;}
                    try
                    {
                        using(var original=view.GetElementOverrides(element.Id))
                        using(var settings=new OverrideGraphicSettings(original))
                        {
                            if(!ledger.Originals.ContainsKey(element.UniqueId))ledger.Originals.Add(element.UniqueId,OverrideState.Capture(original));
                            var color=new Color(rgb.R,rgb.G,rgb.B);
                            settings.SetProjectionLineColor(color);settings.SetCutLineColor(color);
                            settings.SetSurfaceTransparency(0);settings.SetHalftone(false);
                            if(solid!=ElementId.InvalidElementId)
                            {
                                settings.SetSurfaceForegroundPatternId(solid);settings.SetSurfaceForegroundPatternColor(color);settings.SetSurfaceForegroundPatternVisible(true);
                                settings.SetCutForegroundPatternId(solid);settings.SetCutForegroundPatternColor(color);settings.SetCutForegroundPatternVisible(true);
                            }
                            view.SetElementOverrides(element.Id,settings);outcome.Applied++;
                        }
                    }
                    catch(Exception ex){throw new InvalidOperationException("Could not highlight "+element.UniqueId+". The whole highlight operation will roll back. "+ex.Message,ex);}
                }
                Save(view,schema,ledger);Commit(transaction);
            }
            ui.RefreshActiveView();return outcome;
        }
        public HighlightOutcome Clear(UIApplication app)
        {
            var ui=Validate(app);var schema=Schema.Lookup(SchemaId);var outcome=new HighlightOutcome();
            if(schema==null)return outcome;
            var ledger=Read(ui.ActiveView,schema);if(ledger.Originals.Count==0)return outcome;
            using(var transaction=new Transaction(ui.Document,"BIM Comparison: Restore Original Overrides"))
            {
                transaction.Start();transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions().SetForcedModalHandling(true).SetClearAfterRollback(true));
                Restore(ui.ActiveView,ledger,outcome);Save(ui.ActiveView,schema,ledger);Commit(transaction);
            }
            ui.RefreshActiveView();return outcome;
        }
    }
}
