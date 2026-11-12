using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace BimComparison.Core
{
    public static class ComparisonReportService
    {
        public static IEnumerable<ElementResult> All(ComparisonReport r)=>r.AddedElements.Concat(r.DeletedElements).Concat(r.ModifiedElements).Concat(r.UnchangedElements);
        public static void Export(string path,ComparisonReport r)
        {
            if(!path.EndsWith(".csv",System.StringComparison.OrdinalIgnoreCase)) { JsonSerializerService.Write(path,r); return; }
            using(var w=new StreamWriter(path,false,new UTF8Encoding(true)))
            {
                w.WriteLine("Status,ElementId,UniqueId,Category,Family,Type,ChangeType,Property,OldValue,NewValue,Unit");
                foreach(var e in All(r))
                {
                    var changes=e.Changes.Count==0?new[]{new PropertyChange()}:e.Changes.AsEnumerable();
                    foreach(var c in changes) w.WriteLine(string.Join(",",new[]{e.Status,e.Element.ElementId.ToString(),e.Element.UniqueId,e.Element.Category,e.Element.Family,e.Element.Type,c.ChangeType,c.Property,c.OldValue,c.NewValue,c.Unit}.Select(Quote)));
                }
                foreach(var warning in r.Warnings.Concat(r.ExtractionErrors)) w.WriteLine("Warning,,,,,,,"+Quote(warning)+",,,");
            }
        }
        private static string Quote(string s)
        {
            s=s??"";
            if(s.Length>0&&"=+-@\t\r".Contains(s[0])) s="'"+s; // spreadsheet formula injection
            return "\""+s.Replace("\"","\"\"")+"\"";
        }
    }
}
