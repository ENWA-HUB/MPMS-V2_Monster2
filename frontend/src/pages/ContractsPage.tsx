import {useEffect,useMemo,useState} from 'react';
import {FileSignature,Plus,RefreshCw,Search,Upload,Download,Paperclip,Pencil,Trash2} from 'lucide-react';
import {deleteJson,getJson,postJson,putJson} from '../lib/api';

type Row={id:number;orgUnitId:number;projectId:number;supplierId:number;contractNumber:string;title:string;description:string;contractType:string;value:number;currency:string;signedDate?:string;startDate?:string;endDate?:string;ownerId?:number;status:string;project?:string;supplier?:string;owner?:string;businessUnit?:string};

const today=()=>new Date().toISOString().slice(0,10);
const fmt=(d?:string)=>d?new Date(d+'T00:00:00').toLocaleDateString():'—';
const money=(n:number,c='VND')=>{const x=Number(n||0);const z=x>=1e9?`${(x/1e9).toFixed(1)}B`:x>=1e6?`${(x/1e6).toFixed(1)}M`:x>=1e3?`${(x/1e3).toFixed(0)}K`:String(x);return c==='VND'?`${z} ₫`:c==='USD'?`$${z}`:`${z} ${c}`};
const esc=(v:any)=>{const s=String(v??'');return /[",\n\r]/.test(s)?`"${s.replaceAll('"','""')}"`:s};

export function ContractsPage({canDownload=true}:{canDownload?:boolean}){
 const[rows,setRows]=useState<Row[]>([]),[options,setOptions]=useState<any>({orgUnits:[],projects:[],suppliers:[],users:[]});
 const[q,setQ]=useState(''),[status,setStatus]=useState('ALL'),[edit,setEdit]=useState<any|null>(null),[files,setFiles]=useState<any|null>(null),[error,setError]=useState('');
 const load=async()=>{try{const[r,o]=await Promise.all([getJson<Row[]>('/contracts'),getJson<any>('/contracts/options')]);setRows(r);setOptions(o);setError('')}catch(e){setError(String(e))}};
 useEffect(()=>{load()},[]);
 const filtered=useMemo(()=>rows.filter(r=>(`${r.contractNumber} ${r.title} ${r.project||''} ${r.supplier||''} ${r.owner||''}`.toLowerCase().includes(q.toLowerCase()))&&(status==='ALL'||r.status===status)),[rows,q,status]);
 const active=rows.filter(x=>x.status==='ACTIVE').length,totalValue=rows.reduce((a,x)=>a+Number(x.value||0),0);
 const soon=new Date();soon.setMonth(soon.getMonth()+3);const now=new Date();const expiring=rows.filter(x=>x.endDate&&new Date(x.endDate)>=now&&new Date(x.endDate)<=soon).length;
 const exportCsv=()=>{const k=['contractNumber','title','contractType','project','supplier','value','currency','signedDate','startDate','endDate','owner','status','description'];const t='\uFEFF'+k.join(',')+'\n'+filtered.map(r=>k.map(x=>esc((r as any)[x])).join(',')).join('\n');const b=new Blob([t],{type:'text/csv'}),a=document.createElement('a');a.href=URL.createObjectURL(b);a.download=`MPMS-Contracts-${today()}.csv`;a.click();URL.revokeObjectURL(a.href)};
 const openFiles=async(r:Row)=>{try{setFiles({row:r,items:await getJson<any[]>(`/contracts/${r.id}/files`)})}catch(e){setError(String(e))}};
 return <>
  <div className="page-title"><div><h1>Contracts</h1><p>Manage supplier contracts, commercial value, dates, owners, documents and project linkage.</p></div><div className="project-actions"><button className="secondary" onClick={load}><RefreshCw size={16}/> Refresh</button>{canDownload&&<button className="secondary" onClick={exportCsv}><Download size={16}/> Export</button>}<button className="budget-primary" onClick={()=>setEdit({currency:'VND',contractType:'SERVICE',status:'ACTIVE'})}><Plus size={16}/> New Contract</button></div></div>
  {error&&<div className="budget-error">{error}</div>}
  <div className="contract-kpis"><div className="panel"><span>Total Contracts</span><b>{rows.length}</b></div><div className="panel"><span>Active</span><b>{active}</b></div><div className="panel"><span>Total Contract Value</span><b>{money(totalValue)}</b></div><div className="panel"><span>Expiring ≤ 3 months</span><b>{expiring}</b></div></div>
  <div className="panel contract-table-panel"><div className="contract-toolbar"><label><Search size={16}/><input placeholder="Search contract, project, supplier..." value={q} onChange={e=>setQ(e.target.value)}/></label><select value={status} onChange={e=>setStatus(e.target.value)}><option value="ALL">All status</option><option>ACTIVE</option><option>DRAFT</option><option>EXPIRED</option><option>TERMINATED</option><option>CLOSED</option></select></div>
  <div className="it-table-wrap"><table><thead><tr><th>Contract No.</th><th>Title</th><th>Business Unit</th><th>Supplier</th><th>Project</th><th>Type</th><th>Value</th><th>Period</th><th>Owner</th><th>Status</th><th>Actions</th></tr></thead><tbody>{filtered.map(r=><tr key={r.id}><td><b>{r.contractNumber}</b></td><td>{r.title}</td><td>{r.businessUnit||'—'}</td><td>{r.supplier||'—'}</td><td>{r.project||'—'}</td><td>{r.contractType}</td><td><b>{money(r.value,r.currency)}</b></td><td>{fmt(r.startDate)} → {fmt(r.endDate)}</td><td>{r.owner||'—'}</td><td><span className="it-status">{r.status}</span></td><td className="it-actions"><button onClick={()=>setEdit({...r})}><Pencil size={14}/> Edit</button><button onClick={()=>openFiles(r)}><Paperclip size={14}/> Files</button><button className="danger" onClick={async()=>{if(confirm(`Delete ${r.contractNumber}?`)){try{await deleteJson(`/contracts/${r.id}`);load()}catch(e){setError(String(e))}}}}><Trash2 size={14}/></button></td></tr>)}</tbody></table></div></div>
  {edit&&<ContractForm row={edit} options={options} close={()=>setEdit(null)} saved={async()=>{setEdit(null);await load()}}/>}
  {files&&<ContractFiles data={files} close={()=>setFiles(null)} reload={()=>openFiles(files.row)}/>}
 </>;
}

function SearchableLookup({label,value,items,onChange,placeholder,required=false,display,search}:{label:string;value:any;items:any[];onChange:(id:any)=>void;placeholder:string;required?:boolean;display:(x:any)=>string;search:(x:any)=>string}){
 const selected=items.find((x:any)=>String(x.id)===String(value??''));
 const[q,setQ]=useState('');
 const[open,setOpen]=useState(false);
 const shown=selected&&!open?display(selected):q;
 const filtered=items.filter((x:any)=>!q||String(search(x)).toLowerCase().includes(q.trim().toLowerCase())).slice(0,100);
 return <label className="contract-search-lookup">{label}{required&&' *'}
  <div className="contract-search-box">
   <input value={shown} placeholder={placeholder} autoComplete="off"
    onFocus={()=>{setOpen(true);setQ('')}}
    onChange={e=>{setQ(e.target.value);setOpen(true);if(value)onChange('')}}
    onBlur={()=>setTimeout(()=>setOpen(false),180)}
   />
   {open&&<div className="contract-search-menu">
    {filtered.length
      ?filtered.map((x:any)=><button type="button" key={x.id}
          onMouseDown={e=>e.preventDefault()}
          onClick={()=>{onChange(x.id);setQ('');setOpen(false)}}>
          <b>{display(x)}</b>{x.email&&<small>{x.email}</small>}
        </button>)
      :<span>No matching records</span>}
   </div>}
  </div>
 </label>
}

function ContractForm({row,options,close,saved}:{row:any;options:any;close:()=>void;saved:()=>void}){
 const inferredOrgUnitId=row.orgUnitId||options.projects?.find((p:any)=>String(p.id)===String(row.projectId))?.orgUnitId||'';
 const resolvedRow={...row,orgUnitId:inferredOrgUnitId};
 const[r,setR]=useState<any>({...resolvedRow}),[busy,setBusy]=useState(false),[attachmentFiles,setAttachmentFiles]=useState<File[]>([]),[existingFiles,setExistingFiles]=useState<any[]>([]);const v=(k:string,x:any)=>setR((o:any)=>({...o,[k]:x}));const inp=(k:string,l:string,t='text')=><label>{l}<input type={t} value={r[k]??''} onChange={e=>v(k,e.target.value)}/></label>;

 const loadExistingContractFiles=async()=>{
  if(!row.id){setExistingFiles([]);return;}
  try{
   const f=await getJson<any[]>(`/contracts/${row.id}/files`);
   setExistingFiles(f||[]);
  }catch{
   setExistingFiles([]);
  }
 };
 useEffect(()=>{loadExistingContractFiles()},[row.id]);
 const submit=async(e:any)=>{e.preventDefault();if(!r.projectId||!r.supplierId||!String(r.contractNumber||'').trim()||!String(r.title||'').trim())return alert('Project, Supplier, Contract No. and Title are required.');const b={orgUnitId:Number(r.orgUnitId),projectId:Number(r.projectId),supplierId:Number(r.supplierId),contractNumber:String(r.contractNumber).trim(),title:String(r.title).trim(),description:r.description||'',contractType:r.contractType||'SERVICE',value:Number(r.value||0),currency:r.currency||'VND',signedDate:r.signedDate||null,startDate:r.startDate||null,endDate:r.endDate||null,ownerId:r.ownerId?Number(r.ownerId):null,status:r.status||'ACTIVE'};setBusy(true);try{const savedRow:any=row.id?await putJson(`/contracts/${row.id}`,b):await postJson('/contracts',b);const contractId=Number(savedRow?.id||row.id||0);if(attachmentFiles.length){if(!contractId)throw new Error('Contract was saved but Contract ID was not returned for file upload.');for(const file of attachmentFiles){const fd=new FormData();fd.append('file',file);const resp=await fetch(`/api/contracts/${contractId}/files`,{method:'POST',body:fd,credentials:'same-origin'});if(!resp.ok)throw new Error(`Contract file upload failed: ${resp.status} ${await resp.text()}`)}}saved()}catch(e){alert(String(e))}finally{setBusy(false)}};

 const downloadExistingContractFile=async(f:any)=>{
  try{
   const r=await fetch(`/api/contracts/${row.id}/files/${f.id}/download`,{credentials:'same-origin'});
   if(!r.ok)throw new Error(`${r.status} ${await r.text()}`);
   const blob=await r.blob();
   const url=URL.createObjectURL(blob);
   const a=document.createElement('a');
   a.href=url;a.download=f.originalFileName||`contract-file-${f.id}`;
   document.body.appendChild(a);a.click();a.remove();
   setTimeout(()=>URL.revokeObjectURL(url),60000);
  }catch(e){alert(String(e))}
 };
 const deleteExistingContractFile=async(f:any)=>{
  if(!confirm(`Delete file "${f.originalFileName||f.id}"?`))return;
  try{
   await deleteJson(`/contracts/${row.id}/files/${f.id}`);
   await loadExistingContractFiles();
  }catch(e){alert(String(e))}
 };
 return <div className="budget-modal-backdrop"><form className="budget-modal it-modal contract-modal" onSubmit={submit}><div className="budget-modal-head"><div><h3>{row.id?'Edit Contract':'New Contract'}</h3><p>Contract master record</p></div><button type="button" onClick={close}>×</button></div><div className="budget-modal-body"><div className="budget-form-grid">
 {inp('contractNumber','Contract No. *')}{inp('title','Contract Title *')}<SearchableLookup label="Business Unit" required value={r.orgUnitId} placeholder="Type Business Unit name or code..." items={options.orgUnits||[]} onChange={id=>{v('orgUnitId',id);v('projectId','')}} display={(x:any)=>`${x.code||''} - ${x.name||''}`} search={(x:any)=>`${x.code||''} ${x.name||''}`}/>
 <SearchableLookup label="Project" required value={r.projectId} placeholder="Type project name or code..." items={(options.projects||[]).filter((x:any)=>!r.orgUnitId||String(x.orgUnitId)===String(r.orgUnitId))} onChange={id=>v('projectId',id)} display={(x:any)=>`${x.code||''} - ${x.name||''}`} search={(x:any)=>`${x.code||''} ${x.name||''}`}/>
 <SearchableLookup label="Supplier" required value={r.supplierId} placeholder="Type supplier name, code or email..." items={options.suppliers||[]} onChange={id=>v('supplierId',id)} display={(x:any)=>`${x.code||''} - ${x.name||''}`} search={(x:any)=>`${x.code||''} ${x.name||''}`}/>
 <label>Contract Type<select value={r.contractType||'SERVICE'} onChange={e=>v('contractType',e.target.value)}><option>SERVICE</option><option>IMPLEMENTATION</option><option>MAINTENANCE</option><option>LICENSE</option><option>PROCUREMENT</option><option>CONSULTING</option><option>CONSTRUCTION</option><option>OTHER</option></select></label>
 <label>Status<select value={r.status||'ACTIVE'} onChange={e=>v('status',e.target.value)}><option>DRAFT</option><option>ACTIVE</option><option>EXPIRED</option><option>TERMINATED</option><option>CLOSED</option></select></label>
 <label>Contract Value<input type="text" inputMode="numeric" value={r.value?Number(r.value).toLocaleString('en-US'):''} onChange={e=>v('value',Number(e.target.value.replace(/[^\d]/g,''))||0)}/></label><label>Currency<select value={r.currency||'VND'} onChange={e=>v('currency',e.target.value)}><option>VND</option><option>USD</option><option>EUR</option></select></label>
 {inp('signedDate','Signed Date','date')}{inp('startDate','Start Date','date')}{inp('endDate','End Date','date')}
 <SearchableLookup label="Owner" value={r.ownerId} placeholder="Type owner name or email..." items={options.users||[]} onChange={id=>v('ownerId',id)} display={(x:any)=>`${x.name||''} - ${x.email||x.department||''}`} search={(x:any)=>`${x.name||''} ${x.email||''} ${x.department||''}`}/>
 <div className="contract-span-2 contract-form-file-field">
 <label>Contract Files
  <input type="file" multiple accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png" onChange={e=>setAttachmentFiles(Array.from(e.target.files||[]))}/>
 </label>
 <small>{attachmentFiles.length?`${attachmentFiles.length} new file(s) selected`:'Attach contract, appendix, acceptance, legal or supporting documents.'}</small>

 {row.id&&<div className="contract-existing-files">
  <b>Existing files</b>
  {existingFiles.length===0
   ?<span className="contract-files-empty">No files attached.</span>
   :existingFiles.map((f:any)=><div className="contract-existing-file" key={f.id}>
     <span>{f.originalFileName||`File #${f.id}`}</span>
     <div>
      <button type="button" className="secondary" onClick={()=>downloadExistingContractFile(f)}>Download</button>
      <button type="button" className="danger" onClick={()=>deleteExistingContractFile(f)}>Delete</button>
     </div>
    </div>)}
 </div>}
</div>
<label className="contract-span-2">Description<textarea value={r.description||''} onChange={e=>v('description',e.target.value)} rows={4}/></label>
 </div></div><div className="budget-modal-actions"><button type="button" className="secondary" onClick={close}>Cancel</button><button className="budget-primary" disabled={busy}>{busy?'Saving…':'Save Contract'}</button></div></form></div>;
}

function ContractFiles({data,close,reload}:{data:any;close:()=>void;reload:()=>void}){
 const upload=()=>{const i=document.createElement('input');i.type='file';i.accept='.pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png';i.onchange=async()=>{if(!i.files?.[0])return;const f=new FormData();f.append('file',i.files[0]);const r=await fetch(`/api/contracts/${data.row.id}/files`,{method:'POST',body:f});if(!r.ok)alert(await r.text());else reload()};i.click()};
 return <div className="budget-modal-backdrop"><div className="budget-modal it-modal"><div className="budget-modal-head"><div><h3>Contract Documents</h3><p>{data.row.contractNumber} · {data.row.title}</p></div><button onClick={close}>×</button></div><div className="budget-modal-body"><button className="budget-primary" onClick={upload}><Upload size={15}/> Upload Contract File</button><div className="contract-files-list">{data.items.length?data.items.map((x:any)=><div key={x.id}><div><FileSignature size={18}/><span><b>{x.originalFileName}</b><small>{new Date(x.uploadedAt).toLocaleString()}</small></span></div><div className="it-actions"><a className="it-link-btn" href={x.downloadUrl} target="_blank"><Download size={14}/> Download</a><button className="danger" onClick={async()=>{if(confirm('Delete this file?')){await deleteJson(`/contracts/${data.row.id}/files/${x.id}`);reload()}}}><Trash2 size={14}/></button></div></div>):<p>No contract documents uploaded.</p>}</div></div><div className="budget-modal-actions"><button className="secondary" onClick={close}>Close</button></div></div></div>;
}
