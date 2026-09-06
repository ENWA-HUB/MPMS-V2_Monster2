import {useEffect,useRef,useState} from 'react';
import {Plus,Search,Upload,FileText,Download} from 'lucide-react';
import {deleteJson,getJson,postJson,putJson} from '../lib/api';
import './ITDomainsTab.css';
const fmt=(d:any)=>d?new Date(String(d).slice(0,10)+'T00:00:00').toLocaleDateString('en-GB'):'—';
export function ITDomainsTab({canDownload=true}:{canDownload?:boolean}){
 const[rows,setRows]=useState<any[]>([]),[opts,setOpts]=useState<any>({orgUnits:[],suppliers:[],contracts:[],users:[]}),[q,setQ]=useState(''),[bu,setBu]=useState(''),[renew,setRenew]=useState(''),[modal,setModal]=useState<any>(null),[page,setPage]=useState(1);const pageSize=10;
 const load=async()=>{
  const safe=async(path:string)=>{
   try{
    const x:any=await getJson<any>(path);
    return Array.isArray(x)?x:(x?.items||x?.data||[]);
   }catch(e:any){
    console.warn('DOMAIN DROPDOWN LOAD FAILED',path,e?.message||e);
    return [];
   }
  };

  const [domains,itOpts,suppliers,contracts]=await Promise.all([
   getJson<any[]>('/it-assets/domains'),
   getJson<any>('/it-assets/options'),
   safe('/suppliers'),
   safe('/contracts')
  ]);

  const active=(x:any)=>{
   const st=String(x?.status||x?.Status||'').toUpperCase();
   return st!=='DEACTIVATED' && st!=='INACTIVE' && x?.isActive!==false;
  };

  const orgUnits=(itOpts?.orgUnits||itOpts?.OrgUnits||[]).filter(active);
  const users=(itOpts?.users||itOpts?.Users||[]).filter(active);
  const supplierRows=(suppliers||[]).filter(active);
  const contractRows=(contracts||[]).filter(active);

  setRows((domains||[]).filter(active));
  setOpts({
   orgUnits,
   users,
   suppliers:supplierRows,
   contracts:contractRows
  });

  console.log('DOMAIN DROPDOWN COUNTS',{
   orgUnits:orgUnits.length,
   users:users.length,
   suppliers:supplierRows.length,
   contracts:contractRows.length
  });
 };useEffect(()=>{load()},[]);
 const filtered=rows.filter(r=>{const s=[r.domainName,r.registrar,r.loginEmail,r.dnsProvider,r.purpose].join(' ').toLowerCase();return(!q||s.includes(q.toLowerCase()))&&(!bu||String(r.orgUnitId||'')===bu)&&(!renew||r.renewalStatus===renew)});const pages=Math.max(1,Math.ceil(filtered.length/pageSize)),safe=Math.min(page,pages),shown=filtered.slice((safe-1)*pageSize,safe*pageSize);
 const del=async(r:any)=>{if(!confirm('Are you sure you want to delete this item?'))return;await deleteJson(`/it-assets/domains/${r.id}`);await load()};
 return <div className="panel it-list-shell"><div className="it-list-filterbar"><label className="it-filter-search"><Search size={17}/><input value={q} onChange={e=>setQ(e.target.value)} placeholder="Search domain, registrar, email, DNS..."/></label><label className="it-filter-select"><span>Business Unit</span><select value={bu} onChange={e=>setBu(e.target.value)}><option value="">All</option>{(opts.orgUnits||[]).map((x:any)=><option key={x.id||x.Id} value={x.id||x.Id}>{x.code||x.Code||''} - {x.name||x.Name||''}</option>)}</select></label><label className="it-filter-select"><span>Renewal</span><select value={renew} onChange={e=>setRenew(e.target.value)}><option value="">All</option><option>AUTO_RENEW</option><option>MANUAL</option><option>PENDING</option><option>DO_NOT_RENEW</option></select></label><button className="budget-primary it-add-row-btn" onClick={()=>setModal({})}><Plus size={16}/> Add Domain</button></div><div className="it-table-wrap"><table><thead><tr><th>Domain</th><th>Registrar</th><th>Business Unit</th><th>Manager</th><th>Expiry</th><th>Renewal</th><th>WHOIS</th><th>Login</th><th>Actions</th></tr></thead><tbody>{shown.map((r:any)=>{const ou=(opts.orgUnits||[]).find((x:any)=>Number(x.id)===Number(r.orgUnitId));const m=(opts.users||[]).find((x:any)=>Number(x.id)===Number(r.managerUserId));return <tr key={r.id}><td><b>{r.domainName}</b><div className="muted">{r.purpose||r.domainType}</div></td><td>{r.registrar||'—'}</td><td>{ou?`${ou.code} - ${ou.name}`:'—'}</td><td>{m?.name||'—'}</td><td>{fmt(r.expiryDate)}</td><td>{r.renewalStatus}</td><td>{r.whoisPrivacy?'Private':'Public'}</td><td>{r.loginEmail||'—'}</td><td><button onClick={()=>setModal(r)}>Edit</button><button className="danger" onClick={()=>del(r)}>Delete</button></td></tr>})}</tbody></table></div><div className="it-list-footer"><span>Showing {filtered.length?((safe-1)*pageSize+1):0} to {Math.min(safe*pageSize,filtered.length)} of {filtered.length} domains</span></div>{modal&&<Editor row={modal} opts={opts} close={()=>setModal(null)} saved={async()=>{setModal(null);await load()}}/>}</div>
}
function Editor({row,opts,close,saved}:{row:any;opts:any;close:()=>void;saved:()=>void}){
 const[d,setD]=useState<any>({...row,domainType:row.domainType||'INTERNATIONAL',renewalStatus:row.renewalStatus||'MANUAL'}),[pw,setPw]=useState('');const v=(k:string,x:any)=>setD((z:any)=>({...z,[k]:x}));const save=async(e:any)=>{e.preventDefault();const b={...d,orgUnitId:d.orgUnitId?Number(d.orgUnitId):null,supplierId:d.supplierId?Number(d.supplierId):null,contractId:d.contractId?Number(d.contractId):null,managerUserId:d.managerUserId?Number(d.managerUserId):null,loginPassword:pw};row.id?await putJson(`/it-assets/domains/${row.id}`,b):await postJson('/it-assets/domains',b);saved()};

 const[files,setFiles]=useState<any[]>([]);
 const[uploading,setUploading]=useState(false);
 const fileRef=useRef<HTMLInputElement|null>(null);

 const loadFiles=async()=>{if(!row.id)return;try{setFiles(await getJson<any[]>(`/it-assets/domains/${row.id}/attachments`))}catch{}};
 useEffect(()=>{loadFiles()},[row.id]);
 const uploadFile=async(file:File)=>{
  if(!row.id){alert('Please save the domain first, then attach files.');return;}
  setUploading(true);
  try{
   const fd=new FormData(); fd.append('file',file);
   const r=await fetch(`/api/it-assets/domains/${row.id}/attachment`,{method:'POST',body:fd});
   if(!r.ok)throw new Error(await r.text());
   await loadFiles();
  }catch(e:any){alert(e?.message||String(e))}
  finally{setUploading(false);if(fileRef.current)fileRef.current.value=''}
 };
 return <div className="budget-modal-backdrop"><form className="budget-modal it-modal domain-modal" onSubmit={save}><div className="budget-modal-head"><h3>{row.id?'Edit':'New'} Domain</h3><button type="button" onClick={close}>x</button></div><div className="budget-modal-body domain-modal-body"><div className="budget-form-grid domain-form-grid"><label>Domain Name<input required value={d.domainName||''} onChange={e=>v('domainName',e.target.value)}/></label><label>Domain Type<select value={d.domainType} onChange={e=>v('domainType',e.target.value)}><option>INTERNATIONAL</option><option>VIETNAM</option><option>COUNTRY_CODE</option><option>OTHER</option></select></label><label>Business Unit<select value={d.orgUnitId??''} onChange={e=>v('orgUnitId',e.target.value)}><option value=''>— Select —</option>{(opts.orgUnits||[]).map((x:any)=><option key={x.id||x.Id} value={x.id||x.Id}>{x.code||x.Code||''} — {x.name||x.Name||''}</option>)}</select></label><label>Manager<select value={d.managerUserId??''} onChange={e=>v('managerUserId',e.target.value)}><option value=''>— Select —</option>{(opts.users||[]).map((x:any)=><option key={x.id||x.Id} value={x.id||x.Id}>{x.name||x.Name}{(x.jobTitle||x.JobTitle)?` — ${x.jobTitle||x.JobTitle}`:''}</option>)}</select></label><label>Registrar<input value={d.registrar||''} onChange={e=>v('registrar',e.target.value)}/></label><label>Registrar URL<input value={d.registrarUrl||''} onChange={e=>v('registrarUrl',e.target.value)}/></label><label>Supplier<select value={d.supplierId??''} onChange={e=>v('supplierId',e.target.value)}><option value=''>— Select —</option>{(opts.suppliers||[]).map((x:any)=><option key={x.id||x.Id} value={x.id||x.Id}>{x.code||x.Code||''} — {x.name||x.Name||''}</option>)}</select></label><label>Contract<select value={d.contractId??''} onChange={e=>v('contractId',e.target.value)}><option value=''>— Select —</option>{(opts.contracts||[]).map((x:any)=><option key={x.id||x.Id} value={x.id||x.Id}>{x.contractNumber||x.ContractNumber||x.title||x.Title||`Contract #${x.id||x.Id}`}</option>)}</select></label><label>Login Email<input type="email" value={d.loginEmail||''} onChange={e=>v('loginEmail',e.target.value)}/></label><label>Login Password<input type="password" value={pw} onChange={e=>setPw(e.target.value)} placeholder={row.hasPassword?'••••••••':''}/></label><label>Registration Date<input type="date" value={d.registrationDate?String(d.registrationDate).slice(0,10):''} onChange={e=>v('registrationDate',e.target.value||null)}/></label><label>Expiry Date<input type="date" value={d.expiryDate?String(d.expiryDate).slice(0,10):''} onChange={e=>v('expiryDate',e.target.value||null)}/></label><label>Renewal Status<select value={d.renewalStatus} onChange={e=>v('renewalStatus',e.target.value)}><option>AUTO_RENEW</option><option>MANUAL</option><option>PENDING</option><option>DO_NOT_RENEW</option></select></label><label>WHOIS Privacy<select value={d.whoisPrivacy?'YES':'NO'} onChange={e=>v('whoisPrivacy',e.target.value==='YES')}><option value="NO">No</option><option value="YES">Yes</option></select></label><label>DNS Provider<input value={d.dnsProvider||''} onChange={e=>v('dnsProvider',e.target.value)}/></label><label>Name Servers<input value={d.nameServers||''} onChange={e=>v('nameServers',e.target.value)}/></label><label>Registrant Organization<input value={d.registrantOrganization||''} onChange={e=>v('registrantOrganization',e.target.value)}/></label><label>Registrant Contact<input value={d.registrantContact||''} onChange={e=>v('registrantContact',e.target.value)}/></label><label>Admin Contact<input value={d.adminContact||''} onChange={e=>v('adminContact',e.target.value)}/></label><label>Technical Contact<input value={d.technicalContact||''} onChange={e=>v('technicalContact',e.target.value)}/></label><label>Declaration No.<input value={d.declarationNo||''} onChange={e=>v('declarationNo',e.target.value)}/></label><label>Declaration Date<input type="date" value={d.declarationDate?String(d.declarationDate).slice(0,10):''} onChange={e=>v('declarationDate',e.target.value||null)}/></label><label>Recovery Email<input value={d.recoveryEmail||''} onChange={e=>v('recoveryEmail',e.target.value)}/></label><label>MFA Method<input value={d.mfaMethod||''} onChange={e=>v('mfaMethod',e.target.value)}/></label><label className="bu-span-2">Purpose<input value={d.purpose||''} onChange={e=>v('purpose',e.target.value)}/></label><label className="bu-span-2">Notes<textarea rows={3} value={d.notes||''} onChange={e=>v('notes',e.target.value)}/></label><label className="bu-span-2 domain-attachment-field">Attachments
  <div className="domain-attachment-input">
   <input ref={fileRef} hidden type="file" accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.zip" onChange={e=>e.target.files?.[0]&&uploadFile(e.target.files[0])}/>
   <button type="button" className="secondary" disabled={uploading} onClick={()=>fileRef.current?.click()}><Upload size={15}/>{uploading?' Uploading...':' Attach File'}</button>
   {!row.id&&<span className="muted">Save first to attach files.</span>}
  </div>
  {row.id&&files.length>0&&<div className="it-domain-files">{files.map((f:any)=><a key={f.id} className="it-link-btn" href={f.downloadUrl||f.DownloadUrl} target="_blank" rel="noreferrer"><FileText size={14}/>{f.originalFileName||f.OriginalFileName}<Download size={13}/></a>)}</div>}
 </label></div></div><div className="budget-modal-actions domain-modal-actions"><button type="button" className="secondary" onClick={close}>Cancel</button><button className="budget-primary">Save</button></div></form></div>
}
