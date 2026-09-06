import {useEffect,useRef,useState} from 'react';
import { Boxes, ClipboardCheck, KeyRound, ServerCog, Wrench, Plus, RefreshCw, Printer, Upload, Download, History, RotateCcw, Warehouse, Search } from 'lucide-react';
import {deleteJson,getJson,postJson,putJson} from '../lib/api';
import {ITDomainsTab} from './ITDomainsTab';

type Tab='overview'|'assets'|'assignments'|'inventory'|'licenses'|'domains'|'services'|'maintenance';
const today=()=>new Date().toISOString().slice(0,10);
const money=(n:number,c='VND')=>new Intl.NumberFormat('en-US',{maximumFractionDigits:0}).format(Number(n||0))+' '+c;
const fmt=(d:any)=>d?new Date(d+'T00:00:00').toLocaleDateString():'—';

const confirmDeactivate=async(kind:string,row:any)=>{if(!confirm('Are you sure you want to delete this item?'))return false;try{await deleteJson(`/it-assets/${kind}/${row.id}`);return true}catch(e:any){const m=String(e?.message||e||'');if(m.includes('403')||m.toLowerCase().includes('forbidden')||m.toLowerCase().includes('permission'))throw new Error('You do not have permission to delete this item.');throw e}};
const num=(v:any)=>Number(String(v??'').replaceAll(',',''))||0;
const nullableNum=(v:any)=>String(v??'').trim()===''?null:num(v);
const bool=(v:any)=>['1','true','yes','y'].includes(String(v??'').trim().toLowerCase());
function csvEscape(v:any){const s=v==null?'':String(v);return /[",\n\r]/.test(s)?`"${s.replaceAll('"','""')}"`:s}
function downloadCsv(name:string,rows:any[],keys:string[]){const body=[keys.join(','),...rows.map(r=>keys.map(k=>csvEscape(r[k])).join(','))].join('\n');const b=new Blob(['\uFEFF'+body],{type:'text/csv'});const a=document.createElement('a');a.href=URL.createObjectURL(b);a.download=name;a.click();URL.revokeObjectURL(a.href)}
function parseCsv(text:string){const rows:string[][]=[];let row:string[]=[],cell='',q=false;for(let i=0;i<text.length;i++){const ch=text[i];if(q){if(ch==='"'&&text[i+1]==='"'){cell+='"';i++}else if(ch==='"')q=false;else cell+=ch}else{if(ch==='"')q=true;else if(ch===','){row.push(cell);cell=''}else if(ch==='\n'){row.push(cell);rows.push(row);row=[];cell=''}else if(ch!=='\r')cell+=ch}}row.push(cell);if(row.some(x=>x))rows.push(row);if(rows.length<2)return[];const h=rows[0].map(x=>x.trim().replace(/^\uFEFF/,''));return rows.slice(1).filter(r=>r.some(x=>x.trim())).map(r=>Object.fromEntries(h.map((k,i)=>[k,(r[i]??'').trim()])))}

export function ITAssetsPage({canDownload=true}:{canDownload?:boolean}){
 const[tab,setTab]=useState<Tab>('overview');
 const[overview,setOverview]=useState<any>(null);
 const[assets,setAssets]=useState<any[]>([]);
 const[assignments,setAssignments]=useState<any[]>([]);
 const[inventory,setInventory]=useState<any[]>([]);
 const[licenses,setLicenses]=useState<any[]>([]);
 const[services,setServices]=useState<any[]>([]);
 const[maintenance,setMaintenance]=useState<any[]>([]);
 const[options,setOptions]=useState<any>({users:[],orgUnits:[],suppliers:[],contracts:[],projects:[],budgets:[]});
 const[modal,setModal]=useState<any|null>(null);
 const[returnModal,setReturnModal]=useState<any|null>(null);
 const[history,setHistory]=useState<any|null>(null);
 const[msg,setMsg]=useState('');
 const[importing,setImporting]=useState(false);
 const importRef=useRef<HTMLInputElement|null>(null);

 const load=async()=>{try{const[o,a,as,inv,l,s,m,opt]=await Promise.all([
  getJson<any>('/it-assets/overview'),getJson<any[]>('/it-assets/assets'),getJson<any[]>('/it-assets/assignments'),getJson<any[]>('/it-assets/inventory'),
  getJson<any[]>('/it-assets/licenses'),getJson<any[]>('/it-assets/services'),getJson<any[]>('/it-assets/maintenance'),getJson<any>('/it-assets/options')
 ]);setOverview(o);setAssets((a||[]).filter((x:any)=>{
      const st=String(x?.status??'').trim().toUpperCase();
      return st!=='DEACTIVATED' && st!=='DEACTIVATE' && st!=='DELETED'
        && x?.isDeleted!==true && x?.isActive!==false;
    }));setAssignments((as||[]).filter((x:any)=>{
      const st=String(x?.status??'').trim().toUpperCase();
      return st!=='DEACTIVATED' && st!=='DEACTIVATE' && st!=='DELETED'
        && x?.isDeleted!==true && x?.isActive!==false;
    }));setInventory(inv);setLicenses((l||[]).filter((x:any)=>{
      const st=String(x?.status??'').trim().toUpperCase();
      return st!=='DEACTIVATED' && st!=='DEACTIVATE' && st!=='DELETED'
        && x?.isDeleted!==true && x?.isActive!==false;
    }));setServices((s||[]).filter((x:any)=>{
      const st=String(x?.status??'').trim().toUpperCase();
      return st!=='DEACTIVATED' && st!=='DEACTIVATE' && st!=='DELETED'
        && x?.isDeleted!==true && x?.isActive!==false;
    }));setMaintenance((m||[]).filter((x:any)=>{
      const st=String(x?.status??'').trim().toUpperCase();
      return st!=='DEACTIVATED' && st!=='DEACTIVATE' && st!=='DELETED'
        && x?.isDeleted!==true && x?.isActive!==false;
    }));setOptions({
      users: opt?.users ?? [],
      orgUnits: opt?.orgUnits ?? [],
      suppliers: opt?.suppliers ?? [],
      contracts: opt?.contracts ?? [],
      projects: opt?.projects ?? [],
      budgets: opt?.budgets ?? []
    });setMsg('')}catch(e){setMsg(String(e))}};
 useEffect(()=>{load()},[]);

 const tabs:[Tab,string,any][]=[['overview','Overview',Boxes],['assets','Assets',ServerCog],['assignments','Assignment & Handover',ClipboardCheck],['inventory','IT Stock / Available',Warehouse],['licenses','Licenses',KeyRound],['domains','Domains',ServerCog],['services','IT Services',ServerCog],['maintenance','Maintenance & Warranty',Wrench]];

 const exportData=()=>{
  if(tab==='assets')downloadCsv(`MPMS-IT-Assets-${today()}.csv`,assets,['assetCode','category','assetName','brand','model','serialNumber','specification','supplierId','contractId','projectId','budgetLineId','purchaseDate','purchasePrice','currency','warrantyExpiry','location','department','condition','status','notes']);
  if(tab==='assignments')downloadCsv(`MPMS-IT-Handover-${today()}.csv`,assignments,['handoverNo','assetId','assetCode','userId','user','assignmentType','assignmentDate','returnDate','fromLocation','toLocation','conditionOut','conditionIn','accessories','status','returnReason','returnLocation','accessoriesReturned','missingItems','notes']);
  if(tab==='inventory')downloadCsv(`MPMS-IT-Inventory-${today()}.csv`,inventory,['assetCode','category','assetName','brand','model','serialNumber','location','department','condition','status','warrantyExpiry','purchasePrice','currency']);
  if(tab==='licenses')downloadCsv(`MPMS-IT-Licenses-${today()}.csv`,licenses,['code','productName','licensee','licenseKey','vendor','licenseType','orgUnitId','assignedUserId','assignedAssetId','quantity','assignedQuantity','startDate','expiryDate','activationStatus','activationDate','cost','currency','supplierId','contractId','autoRenew','status','notes']);
  if(tab==='services')downloadCsv(`MPMS-IT-Services-${today()}.csv`,services,['code','serviceType','serviceName','provider','site','department','supplierId','contractId','startDate','expiryDate','monthlyCost','annualCost','currency','status','notes']);
  if(tab==='maintenance')downloadCsv(`MPMS-IT-Maintenance-${today()}.csv`,maintenance,['assetId','assetCode','type','openDate','closeDate','vendor','description','cost','currency','status','notes']);
  if(tab==='overview')downloadCsv(`MPMS-IT-Overview-${today()}.csv`,[overview||{}],['totalAssets','assigned','inStock','repair','retired','assetValue','activeAssignments','openMaintenance','warrantyExpiring','licensesExpiring','servicesExpiring']);
 };

 const supplier=(r:any)=>{const key=String(r.supplierCode||r.vendor||r.provider||'').toLowerCase();return (options?.suppliers ?? []).find((x:any)=>Number(x.id)===nullableNum(r.supplierId)||String(x.code).toLowerCase()===key||String(x.name).toLowerCase()===key)};
 const bu=(v:any)=>{const k=String(v||'').toLowerCase();const x=(options?.orgUnits ?? []).find((o:any)=>String(o.code).toLowerCase()===k||String(o.name).toLowerCase()===k);return x?.name||String(v||'')};
 const aid=(r:any)=>nullableNum(r.assetId)||assets.find(x=>String(x.assetCode).toLowerCase()===String(r.assetCode||'').toLowerCase())?.id||0;
 const uid=(r:any)=>nullableNum(r.userId)||(options?.users ?? []).find((x:any)=>String(x.name).toLowerCase()===String(r.user||'').toLowerCase()||String(x.email).toLowerCase()===String(r.email||'').toLowerCase())?.id||0;

 const importData=async(file:File)=>{if(['overview','inventory'].includes(tab))return;setImporting(true);try{const rows=parseCsv(await file.text());let created=0,updated=0,skipped=0;for(const r of rows){
  if(tab==='assets'){const sp=supplier(r);const body={assetCode:r.assetCode||'',category:r.category||'LAPTOP',assetName:r.assetName||'',brand:r.brand||'',model:r.model||'',serialNumber:r.serialNumber||'',specification:r.specification||'',supplierId:sp?.id||nullableNum(r.supplierId),contractId:nullableNum(r.contractId),projectId:nullableNum(r.projectId),budgetLineId:nullableNum(r.budgetLineId),purchaseDate:r.purchaseDate||null,purchasePrice:num(r.purchasePrice),currency:r.currency||'VND',warrantyExpiry:r.warrantyExpiry||null,location:r.location||'',department:bu(r.department),condition:r.condition||'GOOD',status:r.status||'IN_STOCK',notes:r.notes||''};if(!body.assetName){skipped++;continue}const ex=assets.find(x=>body.assetCode&&String(x.assetCode).toLowerCase()===body.assetCode.toLowerCase());if(ex){await putJson(`/it-assets/assets/${ex.id}`,body);updated++}else{await postJson('/it-assets/assets',body);created++}}
  if(tab==='assignments'){const a=aid(r),u=uid(r);if(!a||!u){skipped++;continue}await postJson('/it-assets/assignments',{assetId:a,userId:u,assignmentType:'ASSIGN',assignmentDate:r.assignmentDate||today(),fromLocation:r.fromLocation||'',toLocation:r.toLocation||'',conditionOut:r.conditionOut||'GOOD',accessories:r.accessories||'',handoverNo:r.handoverNo||'',notes:r.notes||'',status:'ACTIVE'});created++}
  if(tab==='licenses'){const sp=supplier(r);const body={code:r.code||'',productName:r.productName||'',vendor:sp?.name||r.vendor||'',licenseType:r.licenseType||'SUBSCRIPTION',licensee:r.licensee||'',licenseKey:r.licenseKey||'',orgUnitId:nullableNum(r.orgUnitId),assignedUserId:nullableNum(r.assignedUserId),assignedAssetId:nullableNum(r.assignedAssetId),quantity:num(r.quantity),assignedQuantity:num(r.assignedQuantity),startDate:r.startDate||null,expiryDate:r.expiryDate||null,activationStatus:r.activationStatus||'NOT_ACTIVATED',activationDate:r.activationDate||null,cost:num(r.cost),currency:r.currency||'VND',supplierId:sp?.id||nullableNum(r.supplierId),contractId:nullableNum(r.contractId),autoRenew:bool(r.autoRenew),status:r.status||'ACTIVE',notes:r.notes||''};const ex=licenses.find(x=>body.code&&String(x.code).toLowerCase()===body.code.toLowerCase());if(ex){await putJson(`/it-assets/licenses/${ex.id}`,body);updated++}else{await postJson('/it-assets/licenses',body);created++}}
  if(tab==='services'){const sp=supplier(r);const body={code:r.code||'',serviceType:r.serviceType||'INTERNET',serviceName:r.serviceName||'',provider:sp?.name||r.provider||'',site:r.site||'',department:bu(r.department),supplierId:sp?.id||nullableNum(r.supplierId),contractId:nullableNum(r.contractId),startDate:r.startDate||null,expiryDate:r.expiryDate||null,monthlyCost:num(r.monthlyCost),annualCost:num(r.annualCost),currency:r.currency||'VND',status:r.status||'ACTIVE',notes:r.notes||''};const ex=services.find(x=>body.code&&String(x.code).toLowerCase()===body.code.toLowerCase());if(ex){await putJson(`/it-assets/services/${ex.id}`,body);updated++}else{await postJson('/it-assets/services',body);created++}}
  if(tab==='maintenance'){const a=aid(r);if(!a){skipped++;continue}await postJson('/it-assets/maintenance',{assetId:a,type:r.type||'REPAIR',openDate:r.openDate||today(),closeDate:r.closeDate||null,vendor:supplier(r)?.name||r.vendor||'',description:r.description||'',cost:num(r.cost),currency:r.currency||'VND',status:r.status||'OPEN',notes:r.notes||''});created++}
 }await load();alert(`Import completed. Created ${created}, Updated ${updated}, Skipped ${skipped}`)}catch(e){alert(String(e))}finally{setImporting(false);if(importRef.current)importRef.current.value=''}};

 const openHistory=async(r:any)=>{try{setHistory({loading:true,asset:r});setHistory(await getJson<any>(`/it-assets/assets/${r.id||r.assetId}/history`))}catch(e){alert(String(e));setHistory(null)}};

 return <>
 <div className="page-title"><div><h1>IT Assets & Services</h1><p>IT equipment lifecycle, handover, return to stock, licenses, services and maintenance.</p></div><div className="project-actions">
  <button className="secondary" onClick={load}><RefreshCw size={16}/> Refresh</button>
  {!['overview','inventory','domains'].includes(tab)&&<><input ref={importRef} hidden type="file" accept=".csv,text/csv" onChange={e=>e.target.files?.[0]&&importData(e.target.files[0])}/><button className="secondary" disabled={importing} onClick={()=>importRef.current?.click()}><Upload size={16}/>{importing?'Importing…':'Import CSV'}</button></>}
  {canDownload&&<button className="secondary" onClick={exportData}><Download size={16}/> Export CSV</button>}
  {!['overview','inventory','domains'].includes(tab)&&<button className="budget-primary" onClick={()=>setModal({kind:tab})}><Plus size={16}/> New</button>}
 </div></div>
 {msg&&<div className="budget-error">{msg}</div>}
 <div className="it-tabs">{tabs.map(([k,l,I])=><button key={k} className={tab===k?'active':''} onClick={()=>setTab(k)}><I size={16}/>{l}</button>)}</div>
 {tab==='overview'&&<Overview data={overview}/>} 
 {tab==='assets'&&<Assets rows={assets} assignments={assignments} options={options} add={()=>setModal({kind:'assets'})} edit={r=>setModal({kind:'assets',row:r})} history={openHistory} deactivate={async r=>{try{if(await confirmDeactivate('assets',r))await load()}catch(e:any){setMsg(e?.message||'Unable to deactivate item.')}}}/>}
 {tab==='assignments'&&<Assignments rows={assignments} options={options} assets={assets} ret={r=>setReturnModal(r)} history={r=>openHistory({id:r.assetId})} deactivate={async r=>{try{if(await confirmDeactivate('assignments',r))await load()}catch(e:any){setMsg(e?.message||'Unable to deactivate item.')}}}/>}
 {tab==='inventory'&&<Inventory rows={inventory} options={options} assign={r=>setModal({kind:'assignments',row:{assetId:r.id,fromLocation:r.location,toLocation:r.location,conditionOut:r.condition}})} history={openHistory}/>}
 {tab==='licenses'&&<Licenses rows={licenses} options={options} assets={assets} add={()=>setModal({kind:'licenses'})} edit={r=>setModal({kind:'licenses',row:r})} deactivate={async r=>{try{if(await confirmDeactivate('licenses',r))await load()}catch(e:any){setMsg(e?.message||'Unable to deactivate item.')}}}/>} 
 {tab==='domains'&&<ITDomainsTab canDownload={canDownload}/>}
 {tab==='services'&&<Services rows={services} options={options} add={()=>setModal({kind:'services'})} edit={r=>setModal({kind:'services',row:r})} deactivate={async r=>{try{if(await confirmDeactivate('services',r))await load()}catch(e:any){setMsg(e?.message||'Unable to deactivate item.')}}}/>}
 {tab==='maintenance'&&<Maintenance rows={maintenance} options={options} assets={assets} add={()=>setModal({kind:'maintenance'})} edit={r=>setModal({kind:'maintenance',row:r})} deactivate={async r=>{try{if(await confirmDeactivate('maintenance',r))await load()}catch(e:any){setMsg(e?.message||'Unable to deactivate item.')}}}/>}
 {modal&&<Editor modal={modal} options={options} assets={assets} close={()=>setModal(null)} saved={async()=>{setModal(null);await load()}}/>}
 {returnModal&&<ReturnModal row={returnModal} users={options.users} close={()=>setReturnModal(null)} saved={async()=>{setReturnModal(null);await load()}}/>}
 {history&&<HistoryModal data={history} close={()=>setHistory(null)}/>}
 </>;
}

function Overview({data}:{data:any}){
 if(!data)return <div className="panel" style={{padding:20}}>Loading...</div>;

 const pct=(a:any,b:any)=>Math.max(0,Math.min(100,b?Math.round(Number(a||0)*100/Number(b||0)):0));
 const shortMoney=(n:any)=>{
   const v=Number(n||0);
   if(v>=1000000000)return `${(v/1000000000).toFixed(1)}B VND`;
   if(v>=1000000)return `${(v/1000000).toFixed(1)}M VND`;
   if(v>=1000)return `${(v/1000).toFixed(0)}K VND`;
   return `${v.toLocaleString()} VND`;
 };
 const maxBuCost=Math.max(1,...(data.businessUnits??[]).map((x:any)=>Number(x.totalCost||0)));
 const maxCat=Math.max(1,...(data.categories??[]).map((x:any)=>Number(x.value||0)));

 return <div className="it-exec-overview">
  <section className="it-exec-topgrid">
   <article className="panel it-exec-card">
    <div className="it-exec-card-head"><div><h3>IT Assets</h3><p>Equipment lifecycle and utilization</p></div><span className="it-exec-total">{data.totalAssets||0}</span></div>
    <div className="it-exec-biglabel">Total assets in scope</div>
    <div className="it-exec-progress"><i style={{width:`${pct(data.assigned,data.totalAssets)}%`}}/></div>
    <div className="it-exec-mini-grid">
     <span><b>{data.assigned||0}</b><small>Assigned / In use</small></span>
     <span><b>{data.inStock||0}</b><small>Available / Stock</small></span>
     <span><b>{data.repair||0}</b><small>Repair</small></span>
     <span><b>{data.retired||0}</b><small>Retired</small></span>
    </div>
   </article>

   <article className="panel it-exec-card">
    <div className="it-exec-card-head"><div><h3>Software Licenses</h3><p>Seat allocation and availability</p></div><span className="it-exec-total">{data.licenseSeats||0}</span></div>
    <div className="it-exec-biglabel">{data.assignedLicenseSeats||0} / {data.licenseSeats||0} seats assigned</div>
    <div className="it-exec-progress"><i style={{width:`${pct(data.assignedLicenseSeats,data.licenseSeats)}%`}}/></div>
    <div className="it-exec-mini-grid">
     <span><b>{data.activeLicenses||0}</b><small>Active products</small></span>
     <span><b>{data.availableLicenseSeats||0}</b><small>Seats available</small></span>
     <span><b>{data.licensesExpiring||0}</b><small>Expiring under 60d</small></span>
     <span><b>{data.licensesExpired||0}</b><small>Expired</small></span>
    </div>
   </article>

   <article className="panel it-exec-card">
    <div className="it-exec-card-head"><div><h3>IT Cost Overview</h3><p>Assets, licenses and services</p></div><span className="it-exec-total it-exec-money">{shortMoney(data.totalITCost)}</span></div>
    <div className="it-exec-cost-list">
     <span><em>Equipment / Assets</em><b>{shortMoney(data.assetValue)}</b></span>
     <span><em>Software licenses</em><b>{shortMoney(data.licenseCost)}</b></span>
     <span><em>IT services / year</em><b>{shortMoney(data.serviceCost)}</b></span>
     <span><em>Maintenance</em><b>{shortMoney(data.maintenanceCost)}</b></span>
    </div>
   </article>
  </section>

  <section className="it-exec-midgrid">
   <article className="panel it-exec-section it-bu-comparison">
    <div className="it-exec-section-head"><div><h3>Business Unit Comparison</h3><p>IT footprint and cost by Business Unit</p></div><strong>{(data.businessUnits??[]).length} units</strong></div>
    <div className="it-bu-list">
     {(data.businessUnits??[]).slice(0,10).map((x:any)=><div className="it-bu-row" key={x.id}>
      <div className="it-bu-name"><b>{x.code||x.name}</b><small>{x.name}</small></div>
      <div className="it-bu-metric"><b>{x.assets}</b><small>Assets</small></div>
      <div className="it-bu-metric"><b>{x.licenseSeats}</b><small>License seats</small></div>
      <div className="it-bu-cost">
       <div><span>Total IT cost</span><b>{shortMoney(x.totalCost)}</b></div>
       <div className="it-exec-progress small"><i style={{width:`${Math.max(2,Math.round(Number(x.totalCost||0)*100/maxBuCost))}%`}}/></div>
      </div>
     </div>)}
     {!(data.businessUnits??[]).length&&<div className="it-exec-empty">No Business Unit data in current scope.</div>}
    </div>
   </article>

   <article className="panel it-exec-section">
    <div className="it-exec-section-head"><div><h3>Attention Required</h3><p>Items requiring IT follow-up</p></div></div>
    <div className="it-attention-grid">
     <span><b>{data.attention?.warrantyExpiring||0}</b><small>Warranty expiring</small></span>
     <span><b>{data.attention?.licensesExpiring||0}</b><small>Licenses expiring</small></span>
     <span><b>{data.attention?.licensesExpired||0}</b><small>Expired licenses</small></span>
     <span><b>{data.attention?.openMaintenance||0}</b><small>Open maintenance</small></span>
     <span><b>{data.attention?.unassignedAssets||0}</b><small>Unassigned assets</small></span>
     <span><b>{data.attention?.overAssignedLicenses||0}</b><small>Over-assigned licenses</small></span>
    </div>
   </article>
  </section>

  <section className="it-exec-bottomgrid">
   <article className="panel it-exec-section">
    <div className="it-exec-section-head"><div><h3>License Usage by Product</h3><p>Assigned seats versus purchased quantity</p></div></div>
    <div className="it-product-list">
     {(data.licenseProducts??[]).map((x:any)=><div className="it-product-row" key={x.name}>
      <div className="it-product-title"><b>{x.name}</b><span>{x.assigned} / {x.quantity}</span></div>
      <div className="it-exec-progress"><i style={{width:`${pct(x.assigned,x.quantity)}%`}}/></div>
      <small>{x.available} available</small>
     </div>)}
     {!(data.licenseProducts??[]).length&&<div className="it-exec-empty">No license data available.</div>}
    </div>
   </article>

   <article className="panel it-exec-section">
    <div className="it-exec-section-head"><div><h3>Asset Distribution</h3><p>Equipment mix by category</p></div></div>
    <div className="it-category-list">
     {(data.categories??[]).slice(0,8).map((x:any)=><div className="it-category-row" key={x.name}>
      <div><b>{x.name}</b><span>{x.value}</span></div>
      <div className="it-exec-progress"><i style={{width:`${Math.max(2,Math.round(Number(x.value||0)*100/maxCat))}%`}}/></div>
     </div>)}
     {!(data.categories??[]).length&&<div className="it-exec-empty">No asset category data available.</div>}
    </div>
   </article>

   <article className="panel it-exec-section">
    <div className="it-exec-section-head"><div><h3>Service & Operations</h3><p>Current operational commitments</p></div></div>
    <div className="it-service-summary">
     <span><b>{data.activeServices||0}</b><small>Active IT services</small></span>
     <span><b>{data.servicesExpiring||0}</b><small>Services expiring under 90d</small></span>
     <span><b>{data.activeAssignments||0}</b><small>Active handovers</small></span>
     <span><b>{data.openMaintenance||0}</b><small>Open maintenance</small></span>
    </div>
   </article>
  </section>
 </div>
}
function Assets({rows,assignments,options,add,edit,history,remove,deactivate}:{rows:any[];assignments?:any[];options:any;add:()=>void;edit:(r:any)=>void;history:(r:any)=>void;remove?:(r:any)=>void;deactivate:(r:any)=>void}){
 const[q,setQ]=useState(''); const[buFilter,setBuFilter]=useState(''); const[categoryFilter,setCategoryFilter]=useState(''); const[statusFilter,setStatusFilter]=useState(''); const[page,setPage]=useState(1); const pageSize=10;
 const bu=(r:any)=>{const x=(options?.orgUnits??[]).find((o:any)=>Number(o.id)===Number(r.orgUnitId));return x?`${x.code} - ${x.name}`:(r.department||'-')};
 const categories=Array.from(new Set((rows??[]).map((x:any)=>String(x.category||'')).filter(Boolean))).sort();
 const statuses=Array.from(new Set((rows??[]).map((x:any)=>String(x.status||'')).filter(Boolean))).sort();
 const filtered=(rows??[]).filter((r:any)=>{const st=[r.assetCode,r.category,r.assetName,r.brand,r.model,r.serialNumber,r.location,r.department,r.condition,r.status,bu(r)].join(' ').toLowerCase();return(!q||st.includes(q.trim().toLowerCase()))&&(!buFilter||(String(r.orgUnitId||'')===buFilter||String(r.department||'')===buFilter))&&(!categoryFilter||String(r.category||'')===categoryFilter)&&(!statusFilter||String(r.status||'')===statusFilter)});
 const pages=Math.max(1,Math.ceil(filtered.length/pageSize)),safePage=Math.min(page,pages),shown=filtered.slice((safePage-1)*pageSize,safePage*pageSize);
 return <div className="panel it-list-shell"><div className="it-list-filterbar">
  <label className="it-filter-search"><Search size={17}/><input value={q} onChange={e=>{setQ(e.target.value);setPage(1)}} placeholder="Search code, asset, serial, location..."/></label>
  <label className="it-filter-select"><span>Business Unit</span><select value={buFilter} onChange={e=>{setBuFilter(e.target.value);setPage(1)}}><option value="">All</option>{(options?.orgUnits??[]).map((x:any)=><option key={x.id} value={String(x.id)}>{x.code} - {x.name}</option>)}</select></label>
  <label className="it-filter-select"><span>Category</span><select value={categoryFilter} onChange={e=>{setCategoryFilter(e.target.value);setPage(1)}}><option value="">All</option>{categories.map((x:any)=><option key={x}>{x}</option>)}</select></label>
  <label className="it-filter-select"><span>Status</span><select value={statusFilter} onChange={e=>{setStatusFilter(e.target.value);setPage(1)}}><option value="">All</option>{statuses.map((x:any)=><option key={x}>{x}</option>)}</select></label>
  <button className="budget-primary it-add-row-btn" onClick={add}><Plus size={16}/> Add Asset</button>
 </div><div className="it-table-wrap"><table><thead><tr><th>Code</th><th>Category</th><th>Asset</th><th>Business Unit</th><th>Brand / Model</th><th>Serial</th><th>Location</th><th>Warranty Expiry</th><th>Status</th><th>Value</th><th>Actions</th></tr></thead><tbody>{shown.map((r:any)=><tr key={r.id}><td><b>{r.assetCode}</b></td><td>{r.category}</td><td>{r.assetName}</td><td>{bu(r)}</td><td>{r.brand} {r.model}</td><td>{r.serialNumber||'—'}</td><td>{r.location||'—'}</td><td>{r.warrantyExpiry||'—'}</td><td><span className="it-status">{r.status}</span></td><td>{money(r.purchasePrice,r.currency)}</td><td className="it-actions"><button onClick={()=>edit(r)}>Edit</button><button onClick={()=>history(r)}><History size={14}/> History</button><button className="danger" onClick={()=>deactivate(r)}>Delete</button></td></tr>)}</tbody></table></div><ListFooter count={filtered.length} page={safePage} pages={pages} pageSize={pageSize} setPage={setPage} label="assets"/></div>
}
function Assignments({rows,options,assets,ret,history,deactivate}:{rows:any[];options:any;assets:any[];ret:(r:any)=>void;history:(r:any)=>void;deactivate:(r:any)=>void}){
 const[q,setQ]=useState(''); const[buFilter,setBuFilter]=useState(''); const[statusFilter,setStatusFilter]=useState(''); const[userFilter,setUserFilter]=useState(''); const[page,setPage]=useState(1); const pageSize=10;
 const asset=(r:any)=>assets.find((a:any)=>Number(a.id)===Number(r.assetId));
 const assetBu=(r:any)=>{const a=asset(r);const x=(options?.orgUnits??[]).find((o:any)=>Number(o.id)===Number(a?.orgUnitId));return x?`${x.code} - ${x.name}`:(a?.department||'-')};
 const statuses=Array.from(new Set((rows??[]).map((x:any)=>String(x.status||'')).filter(Boolean))).sort();
 const filtered=(rows??[]).filter((r:any)=>{const st=[r.handoverNo,r.assetCode,r.assetName,r.user,r.email,r.status,r.assignmentType,assetBu(r)].join(' ').toLowerCase();return(!q||st.includes(q.trim().toLowerCase()))&&(!buFilter||String(asset(r)?.orgUnitId||'')===buFilter)&&(!statusFilter||String(r.status||'')===statusFilter)&&(!userFilter||String(r.userId||'')===userFilter)});
 const pages=Math.max(1,Math.ceil(filtered.length/pageSize)),safePage=Math.min(page,pages),shown=filtered.slice((safePage-1)*pageSize,safePage*pageSize);
 const upload=(r:any,type='HANDOVER')=>{const i=document.createElement('input');i.type='file';i.accept='.pdf,.jpg,.jpeg,.png,.doc,.docx';i.onchange=async()=>{if(!i.files?.[0])return;const f=new FormData();f.append('file',i.files[0]);f.append('attachmentType',type);const res=await fetch(`/api/it-assets/assignments/${r.id}/attachment`,{method:'POST',body:f});alert(res.ok?'File uploaded.':await res.text())};i.click()};
 return <div className="panel it-list-shell"><div className="it-list-filterbar">
  <label className="it-filter-search"><Search size={17}/><input value={q} onChange={e=>{setQ(e.target.value);setPage(1)}} placeholder="Search handover, asset, user..."/></label>
  <label className="it-filter-select"><span>Business Unit</span><select value={buFilter} onChange={e=>{setBuFilter(e.target.value);setPage(1)}}><option value="">All</option>{(options?.orgUnits??[]).map((x:any)=><option key={x.id} value={String(x.id)}>{x.code} - {x.name}</option>)}</select></label>
  <label className="it-filter-select"><span>Status</span><select value={statusFilter} onChange={e=>{setStatusFilter(e.target.value);setPage(1)}}><option value="">All</option>{statuses.map((x:any)=><option key={x}>{x}</option>)}</select></label>
  <label className="it-filter-select"><span>User</span><select value={userFilter} onChange={e=>{setUserFilter(e.target.value);setPage(1)}}><option value="">All</option>{(options?.users??[]).map((u:any)=><option key={u.id} value={String(u.id)}>{u.name}</option>)}</select></label>
 </div><div className="it-table-wrap"><table><thead><tr><th>Handover</th><th>Asset</th><th>Business Unit</th><th>User</th><th>Assigned</th><th>Returned</th><th>Status</th><th>Actions</th></tr></thead><tbody>{shown.map((r:any)=><tr key={r.id}><td><b>{r.handoverNo}</b></td><td>{r.assetCode} · {r.assetName}</td><td>{assetBu(r)}</td><td>{r.user}</td><td>{fmt(r.assignmentDate)}</td><td>{fmt(r.returnDate)}</td><td><span className="it-status">{r.status}</span></td><td className="it-actions"><a className="it-link-btn" target="_blank" href={`/api/it-assets/assignments/${r.id}/handover`}><Printer size={14}/> Handover</a><button onClick={()=>upload(r,'HANDOVER')}><Upload size={14}/> Signed file</button><button onClick={()=>history(r)}><History size={14}/> History</button><button className="danger" onClick={()=>deactivate(r)}>Delete</button>{r.status==='ACTIVE'&&<button onClick={()=>ret(r)}><RotateCcw size={14}/> Return to Stock</button>}{r.status==='RETURNED'&&<button onClick={()=>upload(r,'RETURN')}><Upload size={14}/> Return file</button>}</td></tr>)}</tbody></table></div><ListFooter count={filtered.length} page={safePage} pages={pages} pageSize={pageSize} setPage={setPage} label="assignments"/></div>
}
function Inventory({rows,options,assign,history}:{rows:any[];options:any;assign:(r:any)=>void;history:(r:any)=>void}){
 const[q,setQ]=useState(''); const[buFilter,setBuFilter]=useState(''); const[categoryFilter,setCategoryFilter]=useState(''); const[conditionFilter,setConditionFilter]=useState(''); const[page,setPage]=useState(1); const pageSize=10;
 const bu=(r:any)=>{const x=(options?.orgUnits??[]).find((o:any)=>Number(o.id)===Number(r.orgUnitId));return x?`${x.code} - ${x.name}`:(r.department||'-')};
 const categories=Array.from(new Set((rows??[]).map((x:any)=>String(x.category||'')).filter(Boolean))).sort(); const conditions=Array.from(new Set((rows??[]).map((x:any)=>String(x.condition||'')).filter(Boolean))).sort();
 const filtered=(rows??[]).filter((r:any)=>{const st=[r.assetCode,r.category,r.assetName,r.brand,r.model,r.serialNumber,r.location,r.condition,r.status,bu(r)].join(' ').toLowerCase();return(!q||st.includes(q.trim().toLowerCase()))&&(!buFilter||String(r.orgUnitId||'')===buFilter)&&(!categoryFilter||String(r.category||'')===categoryFilter)&&(!conditionFilter||String(r.condition||'')===conditionFilter)});
 const pages=Math.max(1,Math.ceil(filtered.length/pageSize)),safePage=Math.min(page,pages),shown=filtered.slice((safePage-1)*pageSize,safePage*pageSize);
 return <div className="panel it-list-shell"><div className="it-list-filterbar"><label className="it-filter-search"><Search size={17}/><input value={q} onChange={e=>{setQ(e.target.value);setPage(1)}} placeholder="Search available assets..."/></label><label className="it-filter-select"><span>Business Unit</span><select value={buFilter} onChange={e=>{setBuFilter(e.target.value);setPage(1)}}><option value="">All</option>{(options?.orgUnits??[]).map((x:any)=><option key={x.id} value={String(x.id)}>{x.code} - {x.name}</option>)}</select></label><label className="it-filter-select"><span>Category</span><select value={categoryFilter} onChange={e=>{setCategoryFilter(e.target.value);setPage(1)}}><option value="">All</option>{categories.map((x:any)=><option key={x}>{x}</option>)}</select></label><label className="it-filter-select"><span>Condition</span><select value={conditionFilter} onChange={e=>{setConditionFilter(e.target.value);setPage(1)}}><option value="">All</option>{conditions.map((x:any)=><option key={x}>{x}</option>)}</select></label></div><div className="it-table-wrap"><table><thead><tr><th>Code</th><th>Category</th><th>Asset</th><th>Business Unit</th><th>Brand / Model</th><th>Serial</th><th>Location</th><th>Condition</th><th>Status</th><th>Actions</th></tr></thead><tbody>{shown.map((r:any)=><tr key={r.id}><td><b>{r.assetCode}</b></td><td>{r.category}</td><td>{r.assetName}</td><td>{bu(r)}</td><td>{r.brand} {r.model}</td><td>{r.serialNumber||'—'}</td><td>{r.location||'—'}</td><td>{r.condition}</td><td><span className="it-status">{r.status}</span></td><td className="it-actions">{['IN_STOCK','AVAILABLE'].includes(r.status)&&<button onClick={()=>assign(r)}><ClipboardCheck size={14}/> Assign</button>}<button onClick={()=>history(r)}><History size={14}/> History</button></td></tr>)}</tbody></table></div><ListFooter count={filtered.length} page={safePage} pages={pages} pageSize={pageSize} setPage={setPage} label="assets"/></div>
}
function Licenses({rows,options,assets,add,edit,deactivate}:{rows:any[];options:any;assets:any[];add:()=>void;edit:(r:any)=>void;deactivate:(r:any)=>void}){
 const[q,setQ]=useState('');
 const[buFilter,setBuFilter]=useState('');
 const[statusFilter,setStatusFilter]=useState('');
 const[vendorFilter,setVendorFilter]=useState('');
 const[page,setPage]=useState(1);
 const pageSize=10;

 const bu=(id:any)=>{
   const x=(options?.orgUnits??[]).find((o:any)=>Number(o.id)===Number(id));
   return x?`${x.code} - ${x.name}`:'-';
 };
 const usr=(id:any)=>(options?.users??[]).find((u:any)=>Number(u.id)===Number(id));

 const vendors=Array.from(new Set((rows??[]).map((x:any)=>String(x.vendor||'')).filter(Boolean))).sort();
 const statuses=Array.from(new Set((rows??[]).map((x:any)=>String(x.status||'')).filter(Boolean))).sort();

 const filtered=(rows??[]).filter((r:any)=>{
   const u=usr(r.assignedUserId);
   const searchText=[
     r.code,r.productName,r.vendor,r.licensee,r.licenseKey,r.licenseType,
     bu(r.orgUnitId),u?.name,u?.email,r.currency,r.notes
   ].join(' ').toLowerCase();

   return (!q || searchText.includes(q.trim().toLowerCase()))
     && (!buFilter || String(r.orgUnitId||'')===buFilter)
     && (!statusFilter || String(r.status||'')===statusFilter)
     && (!vendorFilter || String(r.vendor||'')===vendorFilter);
 });

 const pages=Math.max(1,Math.ceil(filtered.length/pageSize));
 const safePage=Math.min(page,pages);
 const shown=filtered.slice((safePage-1)*pageSize,safePage*pageSize);

 const pageNumbers=Array.from({length:pages},(_,i)=>i+1)
   .filter(n=>pages<=7 || n===1 || n===pages || Math.abs(n-safePage)<=2);

 return <div className="panel it-list-shell">
  <div className="it-list-filterbar">
   <label className="it-filter-search">
    <Search size={17}/>
    <input
      value={q}
      onChange={e=>{setQ(e.target.value);setPage(1)}}
      placeholder="Search code, product, vendor, licensee, business unit..."
    />
   </label>

   <label className="it-filter-select">
    <span>Business Unit</span>
    <select value={buFilter} onChange={e=>{setBuFilter(e.target.value);setPage(1)}}>
     <option value="">All</option>
     {(options?.orgUnits??[]).map((x:any)=>
      <option key={x.id} value={String(x.id)}>{x.code} - {x.name}</option>
     )}
    </select>
   </label>

   <label className="it-filter-select">
    <span>Status</span>
    <select value={statusFilter} onChange={e=>{setStatusFilter(e.target.value);setPage(1)}}>
     <option value="">All</option>
     {statuses.map((x:any)=><option key={x} value={x}>{x}</option>)}
    </select>
   </label>

   <label className="it-filter-select">
    <span>Vendor</span>
    <select value={vendorFilter} onChange={e=>{setVendorFilter(e.target.value);setPage(1)}}>
     <option value="">All</option>
     {vendors.map((x:any)=><option key={x} value={x}>{x}</option>)}
    </select>
   </label>

   <button className="budget-primary it-add-row-btn" onClick={add}>
    <Plus size={16}/> Add License
   </button>
  </div>

  <div className="it-table-wrap">
   <table className="it-compact-table it-license-list">
    <thead>
     <tr>
      <th>Code</th>
      <th>Product</th>
      
      <th>Licensee</th>
      <th>License Key</th>
      <th>License Type</th>
      <th>Business Unit</th>
      <th>Installed For User</th>
      <th>Status</th>
      <th>Quantity</th>
      <th>Available</th>
      <th>Cost</th>
      <th>Currency</th>
      <th>Expiry Date</th>
      <th>Notes</th>
      <th>Vendor</th><th>Actions</th>
     </tr>
    </thead>
    <tbody>
     {[...(shown)].sort((a:any,b:any)=>{const ta=Date.parse(String(a.createdAt||a.createdDate||a.createdOn||''))||0;const tb=Date.parse(String(b.createdAt||b.createdDate||b.createdOn||''))||0;if(tb!==ta)return tb-ta;const ia=Number(a.id||0),ib=Number(b.id||0);if(ib!==ia)return ib-ia;return String(a.code||'').localeCompare(String(b.code||''),undefined,{numeric:true,sensitivity:'base'})}).map((r:any)=>{
      const u=usr(r.assignedUserId);
      return <tr key={r.id}>
       <td><b>{r.code}</b></td>
       <td><span className="it-cell-ellipsis" title={r.productName||''}>{r.productName||'-'}</span></td>
       
       <td><span className="it-cell-multiline" title={r.licensee||''}>{r.licensee||'-'}</span></td>
       <td><span className="it-cell-ellipsis it-key-short" title={r.licenseKey||''}>{r.licenseKey||'-'}</span></td>
       <td><span className="it-cell-ellipsis" title={r.licenseType||''}>{r.licenseType||'-'}</span></td>
       <td><span className="it-cell-ellipsis" title={bu(r.orgUnitId)}>{bu(r.orgUnitId)}</span></td>
       <td><span className="it-cell-ellipsis" title={u?`${u.name} - ${u.email||''}`:''}>{u?.name||'-'}</span></td>
       <td><span className="it-status">{r.status||'-'}</span></td>
       <td>{r.quantity||0}</td>
       <td>{Math.max(0,Number(r.quantity||0)-Number(r.assignedQuantity||0))}</td>
       <td>{Number(r.cost||0)>0?Number(r.cost||0).toLocaleString():'-'}</td>
       <td>{r.currency||'-'}</td>
       <td>{fmt(r.expiryDate)}</td>
       <td><span className="it-cell-ellipsis" title={r.notes||''}>{r.notes||'-'}</span></td>
       <td><span className="it-cell-ellipsis" title={r.vendor||''}>{r.vendor||'-'}</span></td><td><button className="it-more-btn" onClick={()=>edit(r)} title="Edit License">...</button></td>
      </tr>
     })}
    </tbody>
   </table>
  </div>

  <div className="it-list-footer">
   <span>
    Showing {filtered.length?((safePage-1)*pageSize+1):0}
    {' '}to {Math.min(safePage*pageSize,filtered.length)}
    {' '}of {filtered.length} licenses
   </span>

   <div className="it-pagination">
    <button disabled={safePage<=1} onClick={()=>setPage(Math.max(1,safePage-1))}>‹</button>
    {pageNumbers.map((n:any,i:number)=>{
      const prev=pageNumbers[i-1];
      return <span key={n} className="it-page-wrap">
       {prev && n-prev>1 && <em>...</em>}
       <button className={n===safePage?'active':''} onClick={()=>setPage(n)}>{n}</button>
      </span>
    })}
    <button disabled={safePage>=pages} onClick={()=>setPage(Math.min(pages,safePage+1))}>›</button>
   </div>
  </div>
 </div>
}
function Services({rows,options,add,edit,deactivate}:{rows:any[];options:any;add:()=>void;edit:(r:any)=>void;deactivate:(r:any)=>void}){
 const[q,setQ]=useState(''); const[buFilter,setBuFilter]=useState(''); const[statusFilter,setStatusFilter]=useState(''); const[providerFilter,setProviderFilter]=useState(''); const[page,setPage]=useState(1); const pageSize=10;
 const providers=Array.from(new Set((rows??[]).map((x:any)=>String(x.provider||'')).filter(Boolean))).sort(); const statuses=Array.from(new Set((rows??[]).map((x:any)=>String(x.status||'')).filter(Boolean))).sort();
 const filtered=(rows??[]).filter((r:any)=>{const st=[r.code,r.serviceType,r.serviceName,r.provider,r.site,r.department,r.status,r.notes].join(' ').toLowerCase();return(!q||st.includes(q.trim().toLowerCase()))&&(!buFilter||String(r.department||'')===buFilter||String(r.orgUnitId||'')===buFilter)&&(!statusFilter||String(r.status||'')===statusFilter)&&(!providerFilter||String(r.provider||'')===providerFilter)});
 const pages=Math.max(1,Math.ceil(filtered.length/pageSize)),safePage=Math.min(page,pages),shown=filtered.slice((safePage-1)*pageSize,safePage*pageSize);
 return <div className="panel it-list-shell"><div className="it-list-filterbar"><label className="it-filter-search"><Search size={17}/><input value={q} onChange={e=>{setQ(e.target.value);setPage(1)}} placeholder="Search code, service, provider, site..."/></label><label className="it-filter-select"><span>Business Unit</span><select value={buFilter} onChange={e=>{setBuFilter(e.target.value);setPage(1)}}><option value="">All</option>{(options?.orgUnits??[]).map((x:any)=><option key={x.id} value={String(x.id)}>{x.code} - {x.name}</option>)}</select></label><label className="it-filter-select"><span>Status</span><select value={statusFilter} onChange={e=>{setStatusFilter(e.target.value);setPage(1)}}><option value="">All</option>{statuses.map((x:any)=><option key={x}>{x}</option>)}</select></label><label className="it-filter-select"><span>Provider</span><select value={providerFilter} onChange={e=>{setProviderFilter(e.target.value);setPage(1)}}><option value="">All</option>{providers.map((x:any)=><option key={x}>{x}</option>)}</select></label><button className="budget-primary it-add-row-btn" onClick={add}><Plus size={16}/> Add Service</button></div><div className="it-table-wrap"><table><thead><tr><th>Code</th><th>Service</th><th>Provider</th><th>Site / BU</th><th>Expiry</th><th>Annual Cost</th><th>Status</th><th></th></tr></thead><tbody>{shown.map((r:any)=><tr key={r.id}><td><b>{r.code}</b></td><td>{r.serviceName}</td><td>{r.provider}</td><td>{r.site||r.department||'—'}</td><td>{fmt(r.expiryDate)}</td><td>{money(r.annualCost,r.currency)}</td><td>{r.status}</td><td><button onClick={()=>edit(r)}>Edit</button><button className="danger" onClick={()=>deactivate(r)}>Delete</button></td></tr>)}</tbody></table></div><ListFooter count={filtered.length} page={safePage} pages={pages} pageSize={pageSize} setPage={setPage} label="services"/></div>
}
function Maintenance({rows,options,assets,add,edit,deactivate}:{rows:any[];options:any;assets:any[];add:()=>void;edit:(r:any)=>void;deactivate:(r:any)=>void}){
 const[q,setQ]=useState(''); const[buFilter,setBuFilter]=useState(''); const[statusFilter,setStatusFilter]=useState(''); const[vendorFilter,setVendorFilter]=useState(''); const[typeFilter,setTypeFilter]=useState(''); const[page,setPage]=useState(1); const pageSize=10;
 const asset=(r:any)=>assets.find((a:any)=>Number(a.id)===Number(r.assetId)); const assetBu=(r:any)=>{const a=asset(r);const x=(options?.orgUnits??[]).find((o:any)=>Number(o.id)===Number(a?.orgUnitId));return x?`${x.code} - ${x.name}`:(a?.department||'-')};
 const vendors=Array.from(new Set((rows??[]).map((x:any)=>String(x.vendor||'')).filter(Boolean))).sort(),statuses=Array.from(new Set((rows??[]).map((x:any)=>String(x.status||'')).filter(Boolean))).sort(),types=Array.from(new Set((rows??[]).map((x:any)=>String(x.type||'')).filter(Boolean))).sort();
 const filtered=(rows??[]).filter((r:any)=>{const st=[r.assetCode,r.assetName,r.type,r.vendor,r.description,r.status,r.notes,assetBu(r)].join(' ').toLowerCase();return(!q||st.includes(q.trim().toLowerCase()))&&(!buFilter||String(asset(r)?.orgUnitId||'')===buFilter)&&(!statusFilter||String(r.status||'')===statusFilter)&&(!vendorFilter||String(r.vendor||'')===vendorFilter)&&(!typeFilter||String(r.type||'')===typeFilter)});
 const pages=Math.max(1,Math.ceil(filtered.length/pageSize)),safePage=Math.min(page,pages),shown=filtered.slice((safePage-1)*pageSize,safePage*pageSize);
 return <div className="panel it-list-shell"><div className="it-list-filterbar"><label className="it-filter-search"><Search size={17}/><input value={q} onChange={e=>{setQ(e.target.value);setPage(1)}} placeholder="Search asset, vendor, description..."/></label><label className="it-filter-select"><span>Business Unit</span><select value={buFilter} onChange={e=>{setBuFilter(e.target.value);setPage(1)}}><option value="">All</option>{(options?.orgUnits??[]).map((x:any)=><option key={x.id} value={String(x.id)}>{x.code} - {x.name}</option>)}</select></label><label className="it-filter-select"><span>Type</span><select value={typeFilter} onChange={e=>{setTypeFilter(e.target.value);setPage(1)}}><option value="">All</option>{types.map((x:any)=><option key={x}>{x}</option>)}</select></label><label className="it-filter-select"><span>Status</span><select value={statusFilter} onChange={e=>{setStatusFilter(e.target.value);setPage(1)}}><option value="">All</option>{statuses.map((x:any)=><option key={x}>{x}</option>)}</select></label><label className="it-filter-select"><span>Vendor</span><select value={vendorFilter} onChange={e=>{setVendorFilter(e.target.value);setPage(1)}}><option value="">All</option>{vendors.map((x:any)=><option key={x}>{x}</option>)}</select></label><button className="budget-primary it-add-row-btn" onClick={add}><Plus size={16}/> Add Maintenance</button></div><div className="it-table-wrap"><table><thead><tr><th>Asset</th><th>Business Unit</th><th>Type</th><th>Opened</th><th>Vendor</th><th>Description</th><th>Cost</th><th>Status</th><th></th></tr></thead><tbody>{shown.map((r:any)=><tr key={r.id}><td>{r.assetCode} · {r.assetName}</td><td>{assetBu(r)}</td><td>{r.type}</td><td>{fmt(r.openDate)}</td><td>{r.vendor}</td><td>{r.description}</td><td>{money(r.cost,r.currency)}</td><td>{r.status}</td><td><button onClick={()=>edit(r)}>Edit</button><button className="danger" onClick={()=>deactivate(r)}>Delete</button></td></tr>)}</tbody></table></div><ListFooter count={filtered.length} page={safePage} pages={pages} pageSize={pageSize} setPage={setPage} label="maintenance records"/></div>
}

function ListFooter({count,page,pages,pageSize,setPage,label}:{count:number;page:number;pages:number;pageSize:number;setPage:(n:number)=>void;label:string}){
 const nums=Array.from({length:pages},(_,i)=>i+1).filter(n=>pages<=7||n===1||n===pages||Math.abs(n-page)<=2);
 return <div className="it-list-footer"><span>Showing {count?((page-1)*pageSize+1):0} to {Math.min(page*pageSize,count)} of {count} {label}</span><div className="it-pagination"><button disabled={page<=1} onClick={()=>setPage(Math.max(1,page-1))}>‹</button>{nums.map((n,i)=>{const prev=nums[i-1];return <span key={n} className="it-page-wrap">{prev&&n-prev>1&&<em>...</em>}<button className={n===page?'active':''} onClick={()=>setPage(n)}>{n}</button></span>})}<button disabled={page>=pages} onClick={()=>setPage(Math.min(pages,page+1))}>›</button></div></div>
}

// IT_SEARCHABLE_LOOKUPS_V1: reusable typed lookup for BU, vendor, user, asset and contract fields.
function SearchLookup({label,value,items,getValue,getLabel,getAliases,onChange,onSelect,placeholder='Type to search and select...'}:{label:string;value:any;items:any[];getValue:(item:any)=>any;getLabel:(item:any)=>string;getAliases?:(item:any)=>any[];onChange:(value:any)=>void;onSelect?:(item:any|null)=>void;placeholder?:string}){
 const selected=(items??[]).find(item=>String(getValue(item))===String(value??''));
 const[text,setText]=useState(selected?getLabel(selected):'');
 useEffect(()=>{setText(selected?getLabel(selected):'')},[value,items]);
 const match=(raw:string)=>{const key=raw.trim().toLowerCase();if(!key)return null;return (items??[]).find(item=>[getLabel(item),...(getAliases?.(item)??[])].some(x=>String(x??'').trim().toLowerCase()===key))??null};
 const choose=(item:any|null)=>{onChange(item?getValue(item):'');onSelect?.(item);setText(item?getLabel(item):'')};
 const listId=`it-lookup-${label.toLowerCase().replace(/[^a-z0-9]+/g,'-')}`;
 return <label>{label}<input type="text" list={listId} autoComplete="off" value={text} placeholder={placeholder} onChange={e=>{const raw=e.target.value;setText(raw);const item=match(raw);if(item)choose(item);else if(!raw.trim())choose(null)}} onBlur={()=>{const item=match(text);if(item)choose(item);else setText(selected?getLabel(selected):'')}}/><datalist id={listId}>{(items??[]).map(item=><option key={String(getValue(item))} value={getLabel(item)}/>)}</datalist></label>
}

const activeUsers=(users:any[])=>(users??[]).filter((u:any)=>{const status=String(u?.status??'ACTIVE').trim().toUpperCase();return u?.isDeleted!==true&&u?.isActive!==false&&!['INACTIVE','DEACTIVATED','DISABLED','TERMINATED','DELETED'].includes(status)});
const userLabel=(u:any)=>`${u.fullName||u.name||u.displayName||u.email||''}${u.email&&u.email!==(u.fullName||u.name||u.displayName)?` - ${u.email}`:''}${u.department||u.jobTitle?` - ${u.department||u.jobTitle}`:''}`;
const buLabel=(x:any)=>`${x.code||''}${x.code&&x.name?' - ':''}${x.name||''}`;
const supplierLabel=(x:any)=>`${x.code||''}${x.code&&x.name?' - ':''}${x.name||''}`;
const assetLabel=(x:any)=>`${x.assetCode||''}${x.assetCode&&x.assetName?' - ':''}${x.assetName||''}${x.serialNumber?` - ${x.serialNumber}`:''}`;
const contractLabel=(x:any)=>`${x.contractNumber||x.code||''}${(x.contractNumber||x.code)&&x.title?' - ':''}${x.title||x.name||''}`;
function ReturnModal({row,users,close,saved}:{row:any;users:any[];close:()=>void;saved:()=>void}){const[d,setD]=useState<any>({returnDate:today(),conditionIn:'GOOD',assetStatus:'IN_STOCK',location:row.fromLocation||'IT Stock',receivedByUserId:'',returnReason:'RESIGNATION',accessoriesReturned:row.accessories||'',missingItems:'',notes:''});const[file,setFile]=useState<File|null>(null);const[busy,setBusy]=useState(false);const v=(k:string,val:any)=>setD((x:any)=>({...x,[k]:val}));const submit=async(e:any)=>{e.preventDefault();setBusy(true);try{await postJson(`/it-assets/assignments/${row.id}/return`,{...d,receivedByUserId:d.receivedByUserId?Number(d.receivedByUserId):null});if(file){const f=new FormData();f.append('file',file);f.append('attachmentType','RETURN');const r=await fetch(`/api/it-assets/assignments/${row.id}/attachment`,{method:'POST',body:f});if(!r.ok)throw new Error(await r.text())}saved()}catch(e){alert(String(e))}finally{setBusy(false)}};return <div className="budget-modal-backdrop"><form className="budget-modal it-modal" onSubmit={submit}><div className="budget-modal-head"><div><h3>Return Asset to Stock</h3><p>{row.assetCode} · {row.assetName} · {row.user}</p></div><button type="button" onClick={close}>×</button></div><div className="budget-modal-body"><div className="budget-form-grid"><label>Return Date<input type="date" value={d.returnDate} onChange={e=>v('returnDate',e.target.value)}/></label><SearchLookup label="Received By (IT / ADM)" value={d.receivedByUserId} items={activeUsers(users)} getValue={u=>u.id} getLabel={userLabel} getAliases={u=>[u.name,u.fullName,u.displayName,u.email]} onChange={value=>v('receivedByUserId',value)} placeholder="Type name or email..."/><label>Return Reason<select value={d.returnReason} onChange={e=>v('returnReason',e.target.value)}><option value="RESIGNATION">Employee resignation</option><option value="REPLACEMENT">Device replacement</option><option value="TRANSFER">Internal transfer</option><option value="UPGRADE">Upgrade</option><option value="OTHER">Other</option></select></label><label>Condition on Return<select value={d.conditionIn} onChange={e=>v('conditionIn',e.target.value)}><option>GOOD</option><option>MINOR_DAMAGE</option><option>DAMAGED</option><option>LOST</option></select></label><label>Return Location / Warehouse<input value={d.location} onChange={e=>v('location',e.target.value)}/></label><label>Asset Status<select value={d.assetStatus} onChange={e=>v('assetStatus',e.target.value)}><option>IN_STOCK</option><option>AVAILABLE</option><option>REPAIR</option><option>RETIRED</option></select></label><label>Accessories Returned<input value={d.accessoriesReturned} onChange={e=>v('accessoriesReturned',e.target.value)}/></label><label>Missing Items<input value={d.missingItems} onChange={e=>v('missingItems',e.target.value)}/></label><label>Signed Return / Handover File<input type="file" accept=".pdf,.jpg,.jpeg,.png,.doc,.docx" onChange={e=>setFile(e.target.files?.[0]||null)}/></label><label>Notes<input value={d.notes} onChange={e=>v('notes',e.target.value)}/></label></div></div><div className="budget-modal-actions"><button type="button" className="secondary" onClick={close}>Cancel</button><button className="budget-primary" disabled={busy}>{busy?'Returning…':'Return to Stock'}</button></div></form></div>}

function HistoryModal({data,close}:{data:any;close:()=>void}){if(data.loading)return <div className="budget-modal-backdrop"><div className="budget-modal it-modal"><div className="budget-modal-body">Loading…</div></div></div>;return <div className="budget-modal-backdrop"><div className="budget-modal it-modal"><div className="budget-modal-head"><div><h3>Assignment History</h3><p>{data.asset.assetCode} · {data.asset.assetName}</p></div><button onClick={close}>×</button></div><div className="budget-modal-body"><div className="it-history-list">{(data.history||[]).map((h:any)=><div className="panel it-history-card" key={h.id}><div className="it-history-top"><div><b>{h.handoverNo}</b><span>{h.user}</span></div><span className="it-status">{h.status}</span></div><div className="it-history-grid"><span><small>Assigned</small>{fmt(h.assignmentDate)}</span><span><small>Returned</small>{fmt(h.returnDate)}</span><span><small>Receiver</small>{h.receivedBy||'—'}</span><span><small>Reason</small>{h.returnReason||'—'}</span><span><small>Condition Out</small>{h.conditionOut||'—'}</span><span><small>Condition In</small>{h.conditionIn||'—'}</span><span><small>Assigned Location</small>{h.toLocation||'—'}</span><span><small>Return Location</small>{h.returnLocation||'—'}</span></div>{h.accessoriesReturned&&<p><b>Accessories returned:</b> {h.accessoriesReturned}</p>}{h.missingItems&&<p><b>Missing:</b> {h.missingItems}</p>}{(h.attachments||[]).length>0&&<div className="it-history-files">{h.attachments.map((a:any)=><a key={a.id} href={a.downloadUrl} target="_blank"><Download size={14}/>{a.originalFileName}</a>)}</div>}</div>)}</div></div><div className="budget-modal-actions"><button className="secondary" onClick={close}>Close</button></div></div></div>}

function Editor({modal,options,assets,close,saved}:{modal:any;options:any;assets:any[];close:()=>void;saved:()=>void}){const[r,setR]=useState<any>(()=>({...modal.row}));const v=(k:string,val:any)=>setR((x:any)=>({...x,[k]:val}));
 const allUsers=options?.users??[];
 const searchableUsers=allUsers.filter((u:any)=>activeUsers([u]).length||[r.managerUserId,r.usingUserId,r.userId,r.assignedUserId].some(id=>String(id??'')===String(u.id)));

 // IT_ASSET_LICENSE_USERS_PREFILL_V6
 useEffect(()=>{
   const id=Number(modal?.row?.id||0);
   const kind=modal?.kind;
   if(!id || (kind!=='assets' && kind!=='licenses')) return;

   let active=true;
   (async()=>{
     try{
       const res=await fetch(`/api/it-assets/${kind}/${id}/users`,{credentials:'same-origin'});
       if(!res.ok)return;
       const u=await res.json();
       if(active)setR((prev:any)=>({...prev,
         managerUserId:u?.managerUserId??'',
         usingUserId:u?.usingUserId??''
       }));
     }catch{}
   })();

   return()=>{active=false};
 },[modal?.kind,modal?.row?.id]);

const input=(k:string,l:string,t='text')=><label>{l}<input type={t} value={r[k]??''} onChange={e=>v(k,e.target.value)}/></label>;const submit=async(e:any)=>{e.preventDefault();try{
 if(modal.kind==='assets'){const body={assetCode:r.assetCode||'',category:r.category||'LAPTOP',assetName:r.assetName||'',brand:r.brand||'',model:r.model||'',serialNumber:r.serialNumber||'',specification:r.specification||'',supplierId:r.supplierId?Number(r.supplierId):null,contractId:r.contractId?Number(r.contractId):null,projectId:r.projectId?Number(r.projectId):null,budgetLineId:r.budgetLineId?Number(r.budgetLineId):null,purchaseDate:r.purchaseDate||null,purchasePrice:num(r.purchasePrice),currency:r.currency||'VND',warrantyExpiry:r.warrantyExpiry||null,location:r.location||'',orgUnitId:nullableNum(r.orgUnitId),managerUserId:nullableNum(r.managerUserId),usingUserId:nullableNum(r.usingUserId),department:(options?.orgUnits??[]).find((x:any)=>Number(x.id)===Number(r.orgUnitId))?.name||r.department||'',condition:r.condition||'GOOD',status:r.status||'IN_STOCK',notes:r.notes||''};/* IT_ASSET_USERS_SAVE_V6 */const savedAsset:any=modal.row?.id?(await putJson(`/it-assets/assets/${modal.row.id}`,body),{id:modal.row.id}):await postJson('/it-assets/assets',body);const savedAssetId=Number(savedAsset?.id||modal.row?.id||0);if(savedAssetId){const ur=await fetch(`/api/it-assets/assets/${savedAssetId}/users`,{method:'PUT',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify({managerUserId:nullableNum(r.managerUserId),usingUserId:nullableNum(r.usingUserId)})});if(!ur.ok)throw new Error(`Failed to save Manager / Using User (${ur.status})`);}}
 if(modal.kind==='assignments')await postJson('/it-assets/assignments',{assetId:Number(r.assetId),userId:Number(r.userId),assignmentType:'ASSIGN',assignmentDate:r.assignmentDate||today(),fromLocation:r.fromLocation||'',toLocation:r.toLocation||'',conditionOut:r.conditionOut||'GOOD',accessories:r.accessories||'',handoverNo:r.handoverNo||'',notes:r.notes||'',status:'ACTIVE'});
 if(modal.kind==='licenses'){const body={code:r.code||'',productName:r.productName||'',vendor:r.vendor||'',licenseType:r.licenseType||'SUBSCRIPTION',licensee:r.licensee||'',licenseKey:r.licenseKey||'',orgUnitId:nullableNum(r.orgUnitId),managerUserId:nullableNum(r.managerUserId),usingUserId:nullableNum(r.usingUserId),quantity:num(r.quantity),assignedQuantity:num(r.assignedQuantity),startDate:r.startDate||null,expiryDate:r.expiryDate||null,activationStatus:r.activationStatus||'NOT_ACTIVATED',activationDate:r.activationDate||null,cost:num(r.cost),currency:r.currency||'VND',supplierId:r.supplierId?Number(r.supplierId):null,contractId:r.contractId?Number(r.contractId):null,autoRenew:!!r.autoRenew,status:r.status||'ACTIVE',notes:r.notes||''};/* IT_LICENSE_USERS_SAVE_V6 */const savedLicense:any=modal.row?.id?(await putJson(`/it-assets/licenses/${modal.row.id}`,body),{id:modal.row.id}):await postJson('/it-assets/licenses',body);const savedLicenseId=Number(savedLicense?.id||modal.row?.id||0);if(savedLicenseId){const ur=await fetch(`/api/it-assets/licenses/${savedLicenseId}/users`,{method:'PUT',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify({managerUserId:nullableNum(r.managerUserId),usingUserId:nullableNum(r.usingUserId)})});if(!ur.ok)throw new Error(`Failed to save Manager / Using User (${ur.status})`);}}
 if(modal.kind==='services'){const body={code:r.code||'',serviceType:r.serviceType||'INTERNET',serviceName:r.serviceName||'',provider:r.provider||'',site:r.site||'',department:r.department||'',supplierId:r.supplierId?Number(r.supplierId):null,contractId:r.contractId?Number(r.contractId):null,startDate:r.startDate||null,expiryDate:r.expiryDate||null,monthlyCost:num(r.monthlyCost),annualCost:num(r.annualCost),currency:r.currency||'VND',status:r.status||'ACTIVE',notes:r.notes||''};modal.row?.id?await putJson(`/it-assets/services/${modal.row.id}`,body):await postJson('/it-assets/services',body)}
 if(modal.kind==='maintenance'){const body={assetId:Number(r.assetId),type:r.type||'REPAIR',openDate:r.openDate||null,closeDate:r.closeDate||null,vendor:r.vendor||'',description:r.description||'',cost:num(r.cost),currency:r.currency||'VND',status:r.status||'OPEN',notes:r.notes||''};modal.row?.id?await putJson(`/it-assets/maintenance/${modal.row.id}`,body):await postJson('/it-assets/maintenance',body)}
 saved()}catch(e){alert(String(e))}};
 return <div className="budget-modal-backdrop"><form className="budget-modal it-modal" onSubmit={submit}><div className="budget-modal-head"><h3>{modal.row?.id?'Edit':'New'} {modal.kind}</h3><button type="button" onClick={close}>×</button></div><div className="budget-modal-body"><div className="budget-form-grid">
 {modal.kind==='assets'&&<>{input('assetName','Asset Name')}{input('category','Category')}{input('brand','Brand')}{input('model','Model')}{input('serialNumber','Serial Number')}{input('specification','Specification')}{input('purchaseDate','Purchase Date','date')}<label>Purchase Price<input type="text" inputMode="numeric" value={r.purchasePrice ? Number(r.purchasePrice).toLocaleString('en-US') : ''} onChange={e=>{const raw=e.target.value.replace(/[^\d]/g,'');v('purchasePrice',raw?Number(raw):0)}}/></label>{input('warrantyExpiry','Warranty Expiry','date')}{input('location','Location')}<SearchLookup label="Business Unit" value={r.orgUnitId} items={options?.orgUnits??[]} getValue={x=>x.id} getLabel={buLabel} getAliases={x=>[x.code,x.name]} onChange={value=>v('orgUnitId',value)}/><label>Status<select value={r.status||'IN_STOCK'} onChange={e=>v('status',e.target.value)}><option>IN_STOCK</option><option>AVAILABLE</option><option>ASSIGNED</option><option>REPAIR</option><option>RETIRED</option><option>DISPOSED</option></select></label><label>Condition<select value={r.condition||'GOOD'} onChange={e=>v('condition',e.target.value)}><option>GOOD</option><option>MINOR_DAMAGE</option><option>DAMAGED</option><option>LOST</option></select></label><SearchLookup label="Supplier" value={r.supplierId} items={options?.suppliers??[]} getValue={x=>x.id} getLabel={supplierLabel} getAliases={x=>[x.code,x.name,x.email,x.taxCode]} onChange={value=>v('supplierId',value)}/><SearchLookup label="Asset Manager" value={r.managerUserId} items={searchableUsers} getValue={u=>u.id} getLabel={userLabel} getAliases={u=>[u.name,u.fullName,u.displayName,u.email]} onChange={value=>v('managerUserId',value)} placeholder="Type name or email..."/><SearchLookup label="Asset User" value={r.usingUserId} items={searchableUsers} getValue={u=>u.id} getLabel={userLabel} getAliases={u=>[u.name,u.fullName,u.displayName,u.email]} onChange={value=>v('usingUserId',value)} placeholder="Type name or email..."/>{input('notes','Notes')}</>}
 {modal.kind==='assignments'&&<><SearchLookup label="Asset" value={r.assetId} items={(assets??[]).filter(x=>['IN_STOCK','AVAILABLE'].includes(x.status)||x.id===Number(r.assetId))} getValue={x=>x.id} getLabel={assetLabel} getAliases={x=>[x.assetCode,x.assetName,x.serialNumber]} onChange={value=>v('assetId',value)} placeholder="Type asset code, name or serial..."/><SearchLookup label="User" value={r.userId} items={searchableUsers} getValue={u=>u.id} getLabel={userLabel} getAliases={u=>[u.name,u.fullName,u.displayName,u.email]} onChange={value=>v('userId',value)} placeholder="Type name or email..."/>{input('assignmentDate','Assignment Date','date')}{input('fromLocation','From Location')}{input('toLocation','To Location')}{input('accessories','Accessories')}{input('notes','Notes')}</>}
 {modal.kind==='licenses'&&<>
 {input('productName','Product')}
 {input('licensee','Licensee')}
 {input('licenseKey','License Key')}
 <label>License Type<select value={r.licenseType||'SUBSCRIPTION'} onChange={e=>v('licenseType',e.target.value)}>
  <option value="PER_USER">Per User</option><option value="PER_DEVICE">Per Device</option><option value="SUBSCRIPTION">Subscription</option><option value="PERPETUAL">Perpetual</option><option value="CONCURRENT">Concurrent</option><option value="VOLUME">Volume</option><option value="ENTERPRISE">Enterprise</option><option value="OEM">OEM</option><option value="SITE_LICENSE">Site License</option><option value="SAAS">SaaS</option><option value="OTHER">Other</option>
 </select></label>
 <SearchLookup label="Business Unit" value={r.orgUnitId} items={options?.orgUnits??[]} getValue={x=>x.id} getLabel={buLabel} getAliases={x=>[x.code,x.name]} onChange={value=>v('orgUnitId',value)}/>
 <SearchLookup label="Installed For User" value={r.assignedUserId} items={searchableUsers} getValue={u=>u.id} getLabel={userLabel} getAliases={u=>[u.name,u.fullName,u.displayName,u.email]} onChange={value=>v('assignedUserId',value)} placeholder="Type name or email..."/>
 <SearchLookup label="Installed On Device" value={r.assignedAssetId} items={assets??[]} getValue={x=>x.id} getLabel={assetLabel} getAliases={x=>[x.assetCode,x.assetName,x.serialNumber]} onChange={value=>v('assignedAssetId',value)} placeholder="Type asset code, name or serial..."/>
 <SearchLookup label="Vendor / Supplier" value={r.supplierId} items={options?.suppliers??[]} getValue={x=>x.id} getLabel={supplierLabel} getAliases={x=>[x.code,x.name,x.email,x.taxCode]} onChange={value=>v('supplierId',value)} onSelect={x=>v('vendor',x?.name||'')}/>
 <SearchLookup label="License Manager" value={r.managerUserId} items={searchableUsers} getValue={u=>u.id} getLabel={userLabel} getAliases={u=>[u.name,u.fullName,u.displayName,u.email]} onChange={value=>v('managerUserId',value)} placeholder="Type name or email..."/>
 <SearchLookup label="Using User" value={r.usingUserId} items={searchableUsers} getValue={u=>u.id} getLabel={userLabel} getAliases={u=>[u.name,u.fullName,u.displayName,u.email]} onChange={value=>v('usingUserId',value)} placeholder="Type name or email..."/>
 <label>Quantity<input type="text" inputMode="numeric" value={r.quantity ? Number(r.quantity).toLocaleString('en-US') : ''} onChange={e=>{const raw=e.target.value.replace(/[^\d]/g,'');v('quantity',raw?Number(raw):0)}}/></label>{input('assignedQuantity','Assigned','number')}{input('startDate','Valid From','date')}{input('expiryDate','Expiry Date','date')}
 <label>Activation<select value={r.activationStatus||'NOT_ACTIVATED'} onChange={e=>v('activationStatus',e.target.value)}><option value="NOT_ACTIVATED">Not Activated</option><option value="ACTIVATED">Activated</option><option value="PARTIAL">Partial</option><option value="SUSPENDED">Suspended</option><option value="EXPIRED">Expired</option></select></label>
 {input('activationDate','Activation Date','date')}<label>Cost<input type="text" inputMode="numeric" value={r.cost?Number(r.cost).toLocaleString('en-US'):''} onChange={e=>{const raw=e.target.value.replace(/[^\d]/g,'');v('cost',raw?Number(raw):0)}}/></label>
 <label>Currency<select value={r.currency||'VND'} onChange={e=>v('currency',e.target.value)}><option>VND</option><option>USD</option><option>EUR</option><option>SGD</option></select></label>
 <SearchLookup label="Contract" value={r.contractId} items={options?.contracts??[]} getValue={x=>x.id} getLabel={contractLabel} getAliases={x=>[x.contractNumber,x.code,x.title,x.name]} onChange={value=>v('contractId',value)} placeholder="Type contract number or title..."/>
 <label>Auto Renew<select value={r.autoRenew?'YES':'NO'} onChange={e=>v('autoRenew',e.target.value==='YES')}><option value="NO">No</option><option value="YES">Yes</option></select></label>
 <label>Status<select value={r.status||'ACTIVE'} onChange={e=>v('status',e.target.value)}><option>ACTIVE</option><option>SUSPENDED</option><option>EXPIRED</option><option>CANCELLED</option></select></label>
 {input('notes','Notes')}
 </>}
 {modal.kind==='services'&&<>{input('serviceName','Service Name')}{input('serviceType','Service Type')}<SearchLookup label="Vendor / Provider" value={r.supplierId} items={options?.suppliers??[]} getValue={x=>x.id} getLabel={supplierLabel} getAliases={x=>[x.code,x.name,x.email,x.taxCode]} onChange={value=>v('supplierId',value)} onSelect={x=>v('provider',x?.name||'')}/>{input('site','Site')}<SearchLookup label="Department / Business Unit" value={r.department} items={options?.orgUnits??[]} getValue={x=>x.name} getLabel={buLabel} getAliases={x=>[x.code,x.name]} onChange={value=>v('department',value)}/><SearchLookup label="Contract" value={r.contractId} items={options?.contracts??[]} getValue={x=>x.id} getLabel={contractLabel} getAliases={x=>[x.contractNumber,x.code,x.title,x.name]} onChange={value=>v('contractId',value)} placeholder="Type contract number or title..."/>{input('startDate','Start Date','date')}{input('expiryDate','Expiry Date','date')}{input('monthlyCost','Monthly Cost','number')}{input('annualCost','Annual Cost','number')}{input('notes','Notes')}</>}
 {modal.kind==='maintenance'&&<><SearchLookup label="Asset" value={r.assetId} items={assets??[]} getValue={x=>x.id} getLabel={assetLabel} getAliases={x=>[x.assetCode,x.assetName,x.serialNumber]} onChange={value=>v('assetId',value)} placeholder="Type asset code, name or serial..."/>{input('type','Type')}{input('openDate','Open Date','date')}{input('closeDate','Close Date','date')}<SearchLookup label="Vendor" value={r.vendor} items={options?.suppliers??[]} getValue={x=>x.name} getLabel={supplierLabel} getAliases={x=>[x.code,x.name,x.email,x.taxCode]} onChange={value=>v('vendor',value)}/>{input('description','Description')}{input('cost','Cost','number')}{input('notes','Notes')}</>}
 </div></div><div className="budget-modal-actions"><button type="button" className="secondary" onClick={close}>Cancel</button><button className="budget-primary">Save</button></div></form></div>}
