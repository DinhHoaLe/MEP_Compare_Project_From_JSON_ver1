using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
namespace BimComparison.Core
{
    public static class GeometryComparison
    {
        public static double Distance(PointData a,PointData b)=>Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)+Math.Pow(a.Z-b.Z,2));
        private static string P(PointData p)=>string.Join(",",p.X.ToString("R",CultureInfo.InvariantCulture),p.Y.ToString("R",CultureInfo.InvariantCulture),p.Z.ToString("R",CultureInfo.InvariantCulture));
        public static string Fingerprint(GeometrySnapshot g)
        {
            // Hash is a deterministic exact fast path, never the tolerance decision.
            var records=g.Triangles.Select(t=>"T:"+string.Join(";",new[]{P(t.A),P(t.B),P(t.C)}.OrderBy(x=>x,StringComparer.Ordinal)))
                .Concat(g.Curves.Select(c=> {var forward=string.Join(";",c.Select(P));var reverse=string.Join(";",c.AsEnumerable().Reverse().Select(P));return "C:"+(string.CompareOrdinal(forward,reverse)<0?forward:reverse);}))
                .OrderBy(x=>x,StringComparer.Ordinal);
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("|",records)))).Replace("-","");
        }
        private static bool TriangleEqual(TriangleData a,TriangleData b,double t)
        {
            var x=new[]{a.A,a.B,a.C};var y=new[]{b.A,b.B,b.C};
            int[][] permutations={new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
            return permutations.Any(p=>Enumerable.Range(0,3).All(i=>Distance(x[i],y[p[i]])<=t));
        }
        public static bool UnorderedEqual<T>(IList<T> a,IList<T> b,Func<T,T,bool> equal)
        {
            if(a.Count!=b.Count)return false;
            // Augmenting-path matching prevents ambiguous nearby candidates from causing false differences.
            var edges=a.Select(x=>Enumerable.Range(0,b.Count).Where(j=>equal(x,b[j])).ToArray()).ToArray();
            var owner=Enumerable.Repeat(-1,b.Count).ToArray();
            var seen=new int[b.Count];
            for(int i=0;i<a.Count;i++)if(!Match(i,edges,owner,seen,i+1))return false;
            return true;
        }
        private static bool Match(int i,int[][] edges,int[] owner,int[] seen,int stamp)
        {foreach(int j in edges[i]){if(seen[j]==stamp)continue;seen[j]=stamp;if(owner[j]<0||Match(owner[j],edges,owner,seen,stamp)){owner[j]=i;return true;}}return false;}
        public static bool ShapeEqual(GeometrySnapshot a,GeometrySnapshot b,double tolerance,CancellationToken token=default(CancellationToken))
        {
            // Exact canonical hashes avoid expensive matching for identical tessellation.
            if(Fingerprint(a)==Fingerprint(b))return true;
            // Spatial buckets prune triangle candidates; adjacent cells handle tolerance boundaries.
            double cell=Math.Max(tolerance,1e-9);
            Func<TriangleData,PointData> center=t=>new PointData{X=(t.A.X+t.B.X+t.C.X)/3,Y=(t.A.Y+t.B.Y+t.C.Y)/3,Z=(t.A.Z+t.B.Z+t.C.Z)/3};
            Func<PointData,string> key=p=>Math.Floor(p.X/cell)+":"+Math.Floor(p.Y/cell)+":"+Math.Floor(p.Z/cell);
            if(a.Triangles.Count!=b.Triangles.Count)return false;
            var buckets=b.Triangles.Select((t,i)=>new{t,i}).GroupBy(x=>key(center(x.t))).ToDictionary(x=>x.Key,x=>x.Select(v=>v.i).ToArray());
            var edges=new int[a.Triangles.Count][];
            for(int i=0;i<a.Triangles.Count;i++)
            {
                token.ThrowIfCancellationRequested();
                var p=center(a.Triangles[i]);var candidates=new List<int>();
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                {
                    int[] ids;var k=(Math.Floor(p.X/cell)+x)+":"+(Math.Floor(p.Y/cell)+y)+":"+(Math.Floor(p.Z/cell)+z);
                    if(buckets.TryGetValue(k,out ids))candidates.AddRange(ids.Where(j=>TriangleEqual(a.Triangles[i],b.Triangles[j],tolerance)));
                }
                edges[i]=candidates.ToArray();
            }
            var owners=Enumerable.Repeat(-1,b.Triangles.Count).ToArray();
            var visited=new int[owners.Length];
            for(int i=0;i<edges.Length;i++){token.ThrowIfCancellationRequested();if(!Match(i,edges,owners,visited,i+1))return false;}
            return UnorderedEqual(a.Curves,b.Curves,(x,y)=>x.Count==y.Count &&
                (Enumerable.Range(0,x.Count).All(i=>Distance(x[i],y[i])<=tolerance)||Enumerable.Range(0,x.Count).All(i=>Distance(x[i],y[y.Count-1-i])<=tolerance)));
        }
    }
}
