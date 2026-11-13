const fs=require('fs');
const path=require('path');
const dir=__dirname,d=JSON.parse(fs.readFileSync(path.join(dir,'region.json'),'utf8'));
const sub=(a,b)=>a.map((v,i)=>v-b[i]),dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),add=(a,b)=>a.map((v,i)=>v+b[i]),mul=(a,t)=>a.map(v=>v*t);
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const norm=a=>Math.sqrt(dot(a,a));
function closest(p,a,b,c){
 const ab=sub(b,a),ac=sub(c,a),ap=sub(p,a),d1=dot(ab,ap),d2=dot(ac,ap);if(d1<=0&&d2<=0)return a;
 const bp=sub(p,b),d3=dot(ab,bp),d4=dot(ac,bp);if(d3>=0&&d4<=d3)return b;
 const vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return add(a,mul(ab,d1/(d1-d3)));
 const cp=sub(p,c),d5=dot(ab,cp),d6=dot(ac,cp);if(d6>=0&&d5<=d6)return c;
 const vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return add(a,mul(ac,d2/(d2-d6)));
 const va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return add(b,mul(sub(c,b),(d4-d3)/((d4-d3)+(d5-d6))));
 const den=1/(va+vb+vc);return add(a,add(mul(ab,vb*den),mul(ac,vc*den)));
}
const roi=[14529000,14537000,5214000,5222000];
const ducts=d.ducts.filter(e=>e.width&&e.centerline&&e.centerline.length===2&&e.bounds.every(p=>p[0]>roi[0]&&p[0]<roi[1]&&p[1]>roi[2]&&p[1]<roi[3])&&norm(sub(e.centerline[0],e.centerline[1]))>650&&Math.abs(e.centerline[0][2]-e.centerline[1][2])<1).sort((a,b)=>b.width-a.width);
const tris=d.primitives.filter(p=>p.kind==='triangle'&&(p.layer==='CAD'||/^RLT_/.test(p.layer))).map(p=>{
 const n=cross(sub(p.points[1],p.points[0]),sub(p.points[2],p.points[0]));const len=norm(n);
 return {...p,n:mul(n,1/len),min:[0,1,2].map(i=>Math.min(...p.points.map(v=>v[i]))),max:[0,1,2].map(i=>Math.max(...p.points.map(v=>v[i])))};
}).filter(t=>t.n.every(Number.isFinite));
const results=[];
for(const e of ducts){
 const a=e.centerline[0],b=e.centerline[1],axis=mul(sub(b,a),1/norm(sub(b,a))),side=cross([0,0,1],axis),length=norm(sub(b,a)),w=e.width/2,h=e.height/2;
 const candidates=tris.filter(t=>[0,1,2].every(i=>t.max[i]>=Math.min(...e.bounds.map(p=>p[i]))-1000&&t.min[i]<=Math.max(...e.bounds.map(p=>p[i]))+1000));
 const points=[];const stations=Math.ceil((length-100)/100);
 for(let j=0;j<=stations;j++){const center=add(a,mul(axis,50+(length-100)*j/stations));
  for(const sign of [-1,1])for(const fraction of [-0.75,-0.25,0.25,0.75]){
   points.push({p:add(center,add(mul(side,sign*w),[0,0,fraction*h])),normal:side});
   points.push({p:add(center,add(mul(side,fraction*w),[0,0,sign*h])),normal:[0,0,1]});
  }
 }
 const measurements=[];
 for(const {p,normal} of points){let best=Infinity,found=null,layer=null;
  for(const t of candidates){let lower=0;for(let i=0;i<3;i++){const delta=Math.max(t.min[i]-p[i],0,p[i]-t.max[i]);lower+=delta*delta;}if(lower>=best)continue;
   const q=closest(p,...t.points),delta=sub(q,p),dist2=dot(delta,delta);if(dist2<best){best=dist2;found=q;layer=t.layer;}
  }
  measurements.push({rvt:p,cad:found,delta:found?sub(found,p):null,distance:Math.sqrt(best),layer});
 }
 const max=[0,1,2].map(i=>Math.max(...measurements.map(m=>m.delta?Math.abs(m.delta[i]):Infinity))),max3=Math.max(...measurements.map(m=>m.distance)),pass=measurements.filter(m=>m.delta&&m.delta.every(x=>Math.abs(x)<=10)).length;
 const worst=measurements.reduce((a,b)=>a.distance>b.distance?a:b);const r={id:e.id,uniqueId:e.uniqueId,width:e.width,height:e.height,length,centerline:e.centerline,samples:measurements.length,maxAbsXYZ:max,maxDistance3D:max3,within10:pass,coveragePercent:100*pass/measurements.length,status:pass===measurements.length?'SAMPLED_SURFACES_WITHIN_10MM':'SAMPLED_SURFACE_MISMATCH',layers:[...new Set(measurements.map(m=>m.layer))],worst,measurements};results.push(r);
 console.log(JSON.stringify({...r,measurements:undefined}));
}
const report={model:d.model,view:d.view,cadLinks:d.cadLinks,toleranceMm:10,roi,units:'mm',method:'One-way screening: nominal rectangular straight RVT duct side surfaces to nearest CAD triangle surface (including sloped transitions). 100 mm maximum longitudinal sampling pitch; 4 points across each side; 50 mm end trim. XYZ residual = CAD nearest surface point minus RVT sample. No fitting, translation, rotation, or Z compensation. This is not a bijective object match or full BRep equivalence proof. Candidate CAD includes generic-layer solids; unknown semantics require review. Fittings, insulation, round ducts, end faces and other ducts excluded.',extractionErrors:d.errors,results};
fs.writeFileSync(path.join(dir,'comparison.json'),JSON.stringify(report,null,2));
const rows=['ElementId,Width_mm,Height_mm,Length_mm,Samples,MaxAbsDeltaX_mm,MaxAbsDeltaY_mm,MaxAbsDeltaZ_mm,MaxDistance3D_mm,CoverageWithin10_percent,Status'];for(const r of results)rows.push([r.id,r.width,r.height,r.length,r.samples,...r.maxAbsXYZ,r.maxDistance3D,r.coveragePercent,r.status].join(','));fs.writeFileSync(path.join(dir,'comparison.csv'),'\ufeff'+rows.join('\r\n'));
