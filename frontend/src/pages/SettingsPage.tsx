import {useEffect,useState,useRef,useMemo} from 'react';
import {BriefcaseBusiness,Building2,UsersRound,Plus,Edit3,Trash2,X,KeyRound,RotateCcw,Power,PowerOff,Upload,Download} from 'lucide-react';
import {getJson,postJson,putJson,deleteJson} from '../lib/api';
import { TeamManagementPage } from './TeamManagementPage';
import { KpiPage } from './KpiPage';
function SettingsMasterFiltersV1({tab}:{tab:string}){
 const [q,setQ]=useState('')
 const [status,setStatus]=useState('ALL')
 const [dimension,setDimension]=useState('ALL')
 const [dimensionLabel,setDimensionLabel]=useState('Filter')
 const [dimensionOptions,setDimensionOptions]=useState<string[]>([])
 const [count,setCount]=useState({shown:0,total:0})

 useEffect(()=>{
  setQ('')
  setStatus('ALL')
  setDimension('ALL')
 },[tab])

 useEffect(()=>{
  if(!['portfolios','orgUnits','supplierKpi'].includes(tab))return

  let timer:any
  const apply=()=>{
   const row=document.querySelector('.settings-tabs-row')
   const host=row?.parentElement
   if(!host)return
   const tables=Array.from(host.querySelectorAll('table')) as HTMLTableElement[]
   const table=tables.find(t=>t.offsetParent!==null)
   if(!table)return

   const headers=Array.from(table.querySelectorAll('thead th')).map(x=>(x.textContent||'').trim().toUpperCase())
   const rows=Array.from(table.querySelectorAll('tbody tr')) as HTMLTableRowElement[]

   const statusIdx=headers.findIndex(x=>x==='STATUS')
   let dimIdx=-1
   let label='Filter'

   if(tab==='orgUnits'){
    dimIdx=headers.findIndex(x=>x==='TYPE')
    label='All Types'
   }else if(tab==='portfolios'){
    dimIdx=headers.findIndex(x=>x.includes('BUSINESS UNIT'))
    label='All Business Units'
   }else if(tab==='supplierKpi'){
    dimIdx=headers.findIndex(x=>x.includes('SUPPLIER'))
    if(dimIdx<0)dimIdx=headers.findIndex(x=>x.includes('CATEGORY'))
    label=dimIdx>=0?(headers[dimIdx].includes('SUPPLIER')?'All Suppliers':'All Categories'):'Filter'
   }

   const opts=dimIdx>=0
    ?Array.from(new Set(rows.map(r=>(r.cells[dimIdx]?.textContent||'').trim()).filter(x=>x&&x!=='—'))).sort((a,b)=>a.localeCompare(b))
    :[]

   setDimensionLabel(label)
   setDimensionOptions(opts)

   const needle=q.trim().toLowerCase()
   let shown=0
   rows.forEach(r=>{
    const text=(r.textContent||'').toLowerCase()
    const statusValue=statusIdx>=0?(r.cells[statusIdx]?.textContent||'').trim().toUpperCase():''
    const dimValue=dimIdx>=0?(r.cells[dimIdx]?.textContent||'').trim():''
    const show=(!needle||text.includes(needle))
      &&(status==='ALL'||statusValue===status)
      &&(dimension==='ALL'||dimValue===dimension)
    r.style.display=show?'':'none'
    if(show)shown++
   })
   setCount({shown,total:rows.length})
  }

  timer=setTimeout(apply,0)
  const obs=new MutationObserver(()=>{clearTimeout(timer);timer=setTimeout(apply,50)})
  const host=document.querySelector('.settings-tabs-row')?.parentElement
  if(host)obs.observe(host,{childList:true,subtree:true})

  return()=>{clearTimeout(timer);obs.disconnect()}
 },[tab,q,status,dimension])

 if(!['portfolios','orgUnits','supplierKpi'].includes(tab))return null

 return <div className="settings-master-filters-v1">
  <input
   value={q}
   onChange={e=>setQ(e.target.value)}
   placeholder={
    tab==='portfolios'?'Search portfolio code or name...':
    tab==='orgUnits'?'Search Business Unit code or name...':
    'Search Supplier KPI...'
   }
  />

  {dimensionOptions.length>0&&
   <select value={dimension} onChange={e=>setDimension(e.target.value)}>
    <option value="ALL">{dimensionLabel}</option>
    {dimensionOptions.map(x=><option key={x} value={x}>{x}</option>)}
   </select>}

  <select value={status} onChange={e=>setStatus(e.target.value)}>
   <option value="ALL">All Statuses</option>
   <option value="ACTIVE">Active</option>
   <option value="INACTIVE">Inactive</option>
  </select>

  <span>{count.shown} / {count.total}</span>

  {(q||status!=='ALL'||dimension!=='ALL')&&
   <button type="button" className="secondary"
    onClick={()=>{setQ('');setStatus('ALL');setDimension('ALL')}}>
    Clear
   </button>}
 </div>
}




// BUSINESS_UNIT_TYPES_V1
const BUSINESS_UNIT_TYPES=["COMPANY","SBU","DIVISION","DEPARTMENT","TEAM","BRANCH","PROJECT"] as const;

function membersCsvEscape(v:any){
 const x=v==null?'':String(v);
 return /[",\n\r]/.test(x)?`"${x.replaceAll('"','""')}"`:x;
}
function membersDownloadCsv(name:string,rows:any[],keys:string[]){
 const body=[keys.join(','),...rows.map(r=>keys.map(k=>membersCsvEscape(r[k])).join(','))].join('\n');
 const blob=new Blob(['\uFEFF'+body],{type:'text/csv;charset=utf-8'});
 const a=document.createElement('a');
 a.href=URL.createObjectURL(blob);a.download=name;a.click();URL.revokeObjectURL(a.href);
}
function membersParseCsv(text:string){
 const rows:string[][]=[];let row:string[]=[],cell='',quoted=false;
 for(let i=0;i<text.length;i++){
  const ch=text[i];
  if(quoted){
   if(ch==='"'&&text[i+1]==='"'){cell+='"';i++}
   else if(ch==='"')quoted=false;
   else cell+=ch;
  }else{
   if(ch==='"')quoted=true;
   else if(ch===','){row.push(cell);cell=''}
   else if(ch==='\n'){row.push(cell);rows.push(row);row=[];cell=''}
   else if(ch!=='\r')cell+=ch;
  }
 }
 row.push(cell);if(row.some(x=>x.trim()))rows.push(row);
 if(rows.length<2)return [];
 const head=rows[0].map(x=>x.trim().replace(/^\uFEFF/,''));
 return rows.slice(1).filter(r=>r.some(x=>x.trim())).map(r=>Object.fromEntries(head.map((h,i)=>[h,(r[i]??'').trim()])));
}

function SettingsMemberAvatar({userId,name}:{userId:number;name:string}){
 const [failed,setFailed]=useState(false);
 const initial=String(name||'?').trim().charAt(0).toUpperCase();
 if(failed)return <div className="employee-name-avatar-fallback">{initial}</div>;
 return <img
   className="employee-name-avatar-img"
   src={`/api/users/${userId}/profile-image/view`}
   alt={name}
   onError={()=>setFailed(true)}
 />;
}

type Tab='portfolios'|'orgUnits'|'members'|'categories'|'projectTeam'|'supplierKpi';
type LoginResult={temporaryPassword:string;name:string;email:string};
export function SettingsPage({context='settings',initialTab}:{context?:'settings'|'access';initialTab?:Tab}={}){
 const[memberSearch,setMemberSearch]=useState('');
 const[memberOrgFilter,setMemberOrgFilter]=useState('ALL');
 const[memberDeptFilter,setMemberDeptFilter]=useState('ALL');
 const firstTab:Tab=initialTab||(context==='access'?'members':'portfolios');
 const[tab,setTab]=useState<Tab>(firstTab),[rows,setRows]=useState<any[]>([]),[opts,setOpts]=useState<any>({orgUnits:[],users:[]}),[open,setOpen]=useState(false),[edit,setEdit]=useState<any>(null),[error,setError]=useState('');
 const path=tab==='portfolios'?'portfolios':tab==='orgUnits'?'org-units':tab==='categories'?'categories':'team-members';
 const load=async()=>{if(tab==='projectTeam'||tab==='supplierKpi')return;setRows(await getJson<any[]>('/settings/'+path));setOpts(await getJson<any>('/settings/options'))};
 useEffect(()=>{load().catch(e=>setError(String(e)))},[tab]);
 const fresh=()=>tab==='portfolios'?{orgUnitId:opts.orgUnits[0]?.id||'',code:'',name:'',description:'',ownerId:null,status:'ACTIVE'}:tab==='orgUnits'?{code:'',name:'',shortName:'',internationalName:'',type:'SBU',parentId:null,status:'ACTIVE',description:'',legalType:'',taxCode:'',taxIssueDate:'',registrationNo:'',registrationIssueDate:'',incorporationDate:'',legalRepresentative:'',representativeTitle:'',registeredAddress:'',officeAddress:'',phone:'',email:'',website:'',headName:'',financeContact:'',itContact:'',hrContact:'',defaultCurrency:'VND',fiscalYear:'',costCenter:'',companyCode:''}:tab==='categories'?{code:'',name:'',scope:'GENERAL',description:'',status:'ACTIVE'}:{orgUnitId:opts.orgUnits[0]?.id||'',name:'',email:'',jobTitle:'',department:'',role:'MEMBER',status:'ACTIVE'};
 const start=(x?:any)=>{const next=x?{...x}:fresh();setEdit(next);setOpen(true);if(tab==='orgUnits'&&x?.id)loadBuAttachments(Number(x.id));else setBuAttachments([])};
 const save=async(e:any)=>{
  e.preventDefault();
  setError('');
  try{
    let payload:any;

    if(tab==='portfolios'){
      payload={
        orgUnitId:edit.orgUnitId?Number(edit.orgUnitId):null,
        code:String(edit.code||'').trim(),
        name:String(edit.name||'').trim(),
        description:String(edit.description||''),
        ownerId:edit.ownerId?Number(edit.ownerId):null,
        status:edit.status||'ACTIVE'
      };
    }else if(tab==='orgUnits'){
      payload={
        code:String(edit.code||'').trim(),name:String(edit.name||'').trim(),shortName:String(edit.shortName||''),internationalName:String(edit.internationalName||''),
        type:edit.type||'SBU',parentId:edit.parentId?Number(edit.parentId):null,status:edit.status||'ACTIVE',description:String(edit.description||''),
        legalType:String(edit.legalType||''),taxCode:String(edit.taxCode||''),taxIssueDate:edit.taxIssueDate||null,registrationNo:String(edit.registrationNo||''),registrationIssueDate:edit.registrationIssueDate||null,incorporationDate:edit.incorporationDate||null,
        legalRepresentative:String(edit.legalRepresentative||''),representativeTitle:String(edit.representativeTitle||''),registeredAddress:String(edit.registeredAddress||''),
        officeAddress:String(edit.officeAddress||''),phone:String(edit.phone||''),email:String(edit.email||''),website:String(edit.website||''),headName:String(edit.headName||''),
        financeContact:String(edit.financeContact||''),itContact:String(edit.itContact||''),hrContact:String(edit.hrContact||''),defaultCurrency:String(edit.defaultCurrency||'VND'),
        fiscalYear:String(edit.fiscalYear||''),costCenter:String(edit.costCenter||''),companyCode:String(edit.companyCode||'')
      };
    }else if(tab==='categories'){
      payload={
        code:String(edit.code||'').trim(),
        name:String(edit.name||'').trim(),
        scope:edit.scope||'GENERAL',
        description:String(edit.description||''),
        status:edit.status||'ACTIVE'
      };
    }else{
      payload={
        orgUnitId:edit.orgUnitId?Number(edit.orgUnitId):null,
        name:String(edit.name||'').trim(),
        email:String(edit.email||'').trim(),
        jobTitle:String(edit.jobTitle||''),
        department:String(edit.department||''),
        role:edit.role||'MEMBER',
        status:edit.status||'ACTIVE'
      };
    }

    if(edit.id){
      await putJson(`/settings/${path}/${edit.id}`,payload);
    }else{
      await postJson(`/settings/${path}`,payload);
    }

    setOpen(false);
    setEdit(null);
    await load();
  }catch(err:any){
    const message=String(err?.message||err||'Error');
    setError(message);
  }
};
 const del=async(x:any)=>{if(!confirm(`Delete "${x.name}"?`))return;try{await deleteJson(`/settings/${path}/${x.id}`);await load()}catch(e){setError(String(e))}};
 const showPassword=(r:any)=>window.prompt(`Temporary login for ${r.name} (${r.email})\n\nCopy this password now. The member must change it after first login:`,r.temporaryPassword);
 const createLogin=async(x:any)=>{try{const r=await postJson<any>(`/settings/team-members/${x.id}/login/create`);showPassword(r);await load()}catch(e){setError(String(e))}};
 const resetLogin=async(x:any)=>{if(!confirm(`Reset login password for "${x.name}"? Existing sessions will be signed out.`))return;try{const r=await postJson<any>(`/settings/team-members/${x.id}/login/reset`);showPassword(r);await load()}catch(e){setError(String(e))}};
 const toggleLogin=async(x:any)=>{const next=!x.loginEnabled;if(!confirm(`${next?'Enable':'Disable'} login for "${x.name}"?`))return;try{await postJson(`/settings/team-members/${x.id}/login/toggle`,{isEnabled:next});await load()}catch(e){setError(String(e))}};

 const memberImportRef=useRef<HTMLInputElement|null>(null);
 const[memberImporting,setMemberImporting]=useState(false);
 const[buAttachments,setBuAttachments]=useState<any[]>([]);
 const[buAttachmentBusy,setBuAttachmentBusy]=useState(false);
 const buAttachmentRef=useRef<HTMLInputElement|null>(null);
 const loadBuAttachments=async(id:number)=>{if(!id){setBuAttachments([]);return}try{setBuAttachments(await getJson<any[]>(`/orgunits/${id}/attachments`))}catch{setBuAttachments([])}};
 const uploadBuAttachment=async(file:File)=>{if(!edit?.id)return;setBuAttachmentBusy(true);setError('');try{const fd=new FormData();fd.append('file',file);const r=await fetch(`/api/orgunits/${edit.id}/attachments`,{method:'POST',body:fd,credentials:'include'});if(!r.ok)throw new Error(await r.text());await loadBuAttachments(Number(edit.id))}catch(e:any){setError(String(e?.message||e))}finally{setBuAttachmentBusy(false);if(buAttachmentRef.current)buAttachmentRef.current.value=''}};
 const deleteBuAttachment=async(name:string)=>{if(!edit?.id||!confirm(`Delete attachment "${name}"?`))return;try{const r=await fetch(`/api/orgunits/${edit.id}/attachments/${encodeURIComponent(name)}`,{method:'DELETE',credentials:'include'});if(!r.ok)throw new Error(await r.text());await loadBuAttachments(Number(edit.id))}catch(e:any){setError(String(e?.message||e))}};
 const downloadBuAttachment=(name:string)=>{if(edit?.id)window.open(`/api/orgunits/${edit.id}/attachments/${encodeURIComponent(name)}/download`,'_blank')};



 const exportMembersCsv=()=>{
   const data=(rows||[]).map((x:any)=>({
     email:x.email||'',
     name:x.name||'',
     businessUnitCode:opts.orgUnits?.find((o:any)=>o.id===x.orgUnitId)?.code||'',
     businessUnit:x.orgUnit||'',
     jobTitle:x.jobTitle||'',
     department:x.department||'',
     role:x.role||'MEMBER',
     status:x.status||'ACTIVE'
   }));
   membersDownloadCsv(`MPMS-Members-${new Date().toISOString().slice(0,10)}.csv`,data,
     ['email','name','businessUnitCode','businessUnit','jobTitle','department','role','status']);
 };

 const importMembersCsv=async(file:File)=>{
   setMemberImporting(true);setError('');
   try{
     const data=membersParseCsv(await file.text());
     let created=0,updated=0,skipped=0;
     const existing=(rows||[]) as any[];

     for(const r of data){
       const email=String(r.email||'').trim().toLowerCase();
       const name=String(r.name||'').trim();
       if(!email||!name){skipped++;continue}

       const buKey=String(r.businessUnitCode||r.businessUnit||r.department||'').trim().toLowerCase();
       const org=opts.orgUnits?.find((o:any)=>
         String(o.code||'').trim().toLowerCase()===buKey ||
         String(o.name||'').trim().toLowerCase()===buKey
       );

       if(!org){skipped++;continue}

       const payload={
         orgUnitId:Number(org.id),
         name,
         email,
         jobTitle:String(r.jobTitle||'').trim(),
         department:String(r.department||org.name||'').trim(),
         role:String(r.role||'MEMBER').trim().toUpperCase(),
         status:String(r.status||'ACTIVE').trim().toUpperCase()
       };

       const old=existing.find((x:any)=>String(x.email||'').trim().toLowerCase()===email);
       if(old){
         await putJson(`/settings/team-members/${old.id}`,payload);
         updated++;
       }else{
         await postJson('/settings/team-members',payload);
         created++;
       }
     }
     await load();
     alert(`Members import completed.\nCreated: ${created}\nUpdated: ${updated}\nSkipped: ${skipped}`);
   }catch(e:any){
     setError(String(e?.message||e));
     alert(String(e?.message||e));
   }finally{
     setMemberImporting(false);
     if(memberImportRef.current)memberImportRef.current.value='';
   }
 };
 const memberDepartmentOptions=useMemo(()=>Array.from(new Set((rows||[]).map((x:any)=>String(x.department||'').trim()).filter(Boolean))).sort((a,b)=>a.localeCompare(b)),[rows]);
 const memberOrgOptions=useMemo(()=>{
  const seen=new Set<string>();
  return (rows||[])
   .map((x:any)=>({
     id:String(x.orgUnitId||''),
     name:String(x.orgUnit||opts?.orgUnits?.find((o:any)=>String(o.id)===String(x.orgUnitId))?.name||'')
   }))
   .filter((x:any)=>{
     if(!x.id||seen.has(x.id)) return false;
     seen.add(x.id);
     return true;
   })
   .sort((a:any,b:any)=>a.name.localeCompare(b.name));
 },[rows,opts]);
 const filteredMemberRows=useMemo(()=>{
  if(tab!=='members')return rows;
  const q=memberSearch.trim().toLowerCase();
  return rows.filter((x:any)=>{
   const textOk=!q||[x.name,x.email,x.employeeCode,x.orgUnit,x.department,x.jobTitle,x.role].filter(Boolean).join(' ').toLowerCase().includes(q);
   const orgOk=memberOrgFilter==='ALL'||String(x.orgUnitId||'')===memberOrgFilter;
   const deptOk=memberDeptFilter==='ALL'||String(x.department||'')===memberDeptFilter;
   return textOk&&orgOk&&deptOk;
  });
 },[rows,tab,memberSearch,memberOrgFilter,memberDeptFilter]);


 return <>{context==='settings'
  ?<div className="page-title"><div><h1>Settings</h1><p>Portfolio, Business Unit, master data and supplier configuration</p></div>{tab!=='projectTeam'&&tab!=='supplierKpi'&&<button className="budget-primary" onClick={()=>start()}><Plus size={16}/> New</button>}</div>
  :<div className="access-embedded-head"><div>{tab==='members'?<><h2>Users</h2><p>Manage users, login accounts and account status.</p></>:null}</div>
<div className="access-users-header-filters-v2">
 <input
  value={memberSearch}
  onChange={e=>setMemberSearch(e.target.value)}
  placeholder="Search name, email or employee code..."
 />
 <select value={memberOrgFilter} onChange={e=>setMemberOrgFilter(e.target.value)}>
  <option value="ALL">All Business Units</option>
  {memberOrgOptions.map((x:any)=><option key={x.id} value={x.id}>{x.name||`BU #${x.id}`}</option>)}
 </select>
 <select value={memberDeptFilter} onChange={e=>setMemberDeptFilter(e.target.value)}>
  <option value="ALL">All Departments</option>
  {memberDepartmentOptions.map((x:string)=><option key={x} value={x}>{x}</option>)}
 </select>
 <span>{filteredMemberRows.length} / {rows.length} users</span>
 {(memberSearch||memberOrgFilter!=='ALL'||memberDeptFilter!=='ALL')&&
  <button type="button" className="secondary"
   onClick={()=>{setMemberSearch('');setMemberOrgFilter('ALL');setMemberDeptFilter('ALL')}}>
   Clear
  </button>}
</div>{tab==='members'&&<div className="project-actions">
      <input ref={memberImportRef} hidden type="file" accept=".csv,text/csv" onChange={e=>{const f=e.target.files?.[0];if(f)importMembersCsv(f)}}/>
      <button className="secondary" disabled={memberImporting} onClick={()=>memberImportRef.current?.click()}><Upload size={16}/> {memberImporting?'Importing…':'Import Members'}</button>
      <button className="secondary" onClick={exportMembersCsv}><Download size={16}/> Export Members</button>
      <button className="budget-primary" onClick={()=>start()}><Plus size={16}/> New User</button>
     </div>}</div>}
 {error&&<div className="budget-error">{error}</div>}
 <div className={`settings-tabs-row ${context==='access'?'access-context':''}`}><div className="settings-tabs">
  <button className={tab==='portfolios'?'active':''} onClick={()=>setTab('portfolios')}><BriefcaseBusiness/>Portfolios</button>
  <button className={tab==='orgUnits'?'active':''} onClick={()=>setTab('orgUnits')}><Building2/>Business Units</button>
  <button className={tab==='supplierKpi'?'active':''} onClick={()=>setTab('supplierKpi')}>Supplier KPI</button>
 </div>
  {context==='settings'&&<SettingsMasterFiltersV1 tab={tab}/>}{tab==='projectTeam'&&<div id="project-team-toolbar-host" className="project-team-toolbar-host"></div>}</div>
 {tab!=='projectTeam'&&tab!=='supplierKpi'&&<div className="settings-panel"><table className="settings-table">
 {tab==='portfolios'&&<><thead><tr><th>Code</th><th>Name</th><th>Business Unit</th><th>Owner</th><th>Projects</th><th>Status</th><th>Actions</th></tr></thead><tbody>{rows.map(x=><tr key={x.id}><td><b>{x.code}</b></td><td><b>{x.name}</b></td><td>{x.orgUnit||'—'}</td><td>{x.owner||'—'}</td><td>{x.projectCount}</td><td>{x.status}</td><td><button onClick={()=>start(x)}><Edit3 size={15}/></button><button className="danger" onClick={()=>del(x)}><Trash2 size={15}/></button></td></tr>)}</tbody></>}
 {tab==='orgUnits'&&<><thead><tr><th>Code</th><th>Name</th><th>Short Name</th><th>Type</th><th>Parent Unit</th><th>Tax Code / MST</th><th>Legal Representative</th><th>Phone</th><th>Email</th><th>Address</th><th>Members</th><th>Portfolios</th><th>Projects</th><th>Status</th><th>Actions</th></tr></thead><tbody>{rows.map(x=><tr key={x.id}><td><b>{x.code}</b></td><td><b>{x.name}</b></td><td>{x.shortName||"—"}</td><td>{x.type||"—"}</td><td>{opts.orgUnits.find((o:any)=>Number(o.id)===Number(x.parentId))?.name||"—"}</td><td>{x.taxCode||"—"}</td><td>{x.legalRepresentative||"—"}</td><td>{x.phone||"—"}</td><td>{x.email||"—"}</td><td className="settings-cell-ellipsis" title={x.officeAddress||x.registeredAddress||""}>{x.officeAddress||x.registeredAddress||"—"}</td><td>{x.userCount}</td><td>{x.portfolioCount}</td><td>{x.projectCount}</td><td>{x.status}</td><td><button onClick={()=>start(x)}><Edit3 size={15}/></button><button className="danger" onClick={()=>del(x)}><Trash2 size={15}/></button></td></tr>)}</tbody></>}
 {tab==='categories'&&<><thead><tr><th>Code</th><th>Name</th><th>Scope</th><th>Status</th><th>Actions</th></tr></thead><tbody>{rows.map(x=><tr key={x.id}><td><b>{x.code}</b></td><td><b>{x.name}</b><small>{x.description}</small></td><td>{x.scope}</td><td>{x.status}</td><td><button onClick={()=>start(x)}><Edit3 size={15}/></button><button className="danger" onClick={()=>del(x)}><Trash2 size={15}/></button></td></tr>)}</tbody></>}
  {tab==='members'&&<div className={`access-users-searchbar access-users-filterbar ${context==='access'?'access-users-old-filter-hidden':''}`}>
 <span>Search users</span>
 <div className="access-users-searchbox"><input value={memberSearch} onChange={e=>setMemberSearch(e.target.value)} placeholder="Search name, email, employee code..."/>{!!memberSearch&&<button type="button" onClick={()=>setMemberSearch('')}>×</button>}</div>
 <select value={memberOrgFilter} onChange={e=>setMemberOrgFilter(e.target.value)}><option value="ALL">All Business Units</option>{memberOrgOptions.map((x:any)=><option key={x.id} value={x.id}>{x.name||`BU #${x.id}`}</option>)}</select>
 <select value={memberDeptFilter} onChange={e=>setMemberDeptFilter(e.target.value)}><option value="ALL">All Departments</option>{memberDepartmentOptions.map((x:string)=><option key={x} value={x}>{x}</option>)}</select>
 {(memberSearch||memberOrgFilter!=='ALL'||memberDeptFilter!=='ALL')&&<button type="button" className="secondary" onClick={()=>{setMemberSearch('');setMemberOrgFilter('ALL');setMemberDeptFilter('ALL')}}>Clear</button>}
 <small>{filteredMemberRows.length} / {rows.length} users</small>
 </div>}
 {tab==='members'&&<><thead><tr><th>Member</th><th>Business Unit</th><th>DEPARTMENT</th><th>Role</th><th>Projects</th><th>KPI Periods</th><th>Login</th><th>Status</th><th>Actions</th></tr></thead><tbody>{filteredMemberRows.map(x=><tr key={x.id}><td><div className="settings-member-name-with-avatar"><SettingsMemberAvatar userId={x.id} name={x.name}/><span><b>{x.name}</b><small>{x.email}</small></span></div></td><td>{x.orgUnit||'—'}</td><td>{x.department||'—'}</td><td>{x.role}</td><td>{x.projectCount}</td><td>{x.performanceCount}</td><td><span className={`settings-status ${x.hasLogin&&x.loginEnabled?'active':'inactive'}`}>{!x.hasLogin?'NO LOGIN':x.loginEnabled?(x.mustChangePassword?'TEMP PASSWORD':'ACTIVE'):'DISABLED'}</span></td><td>{x.status}</td><td><div className="member-actions"><button title="Edit" onClick={()=>start(x)}><Edit3 size={15}/></button>{!x.hasLogin?<button title="Create Login" onClick={()=>createLogin(x)}><KeyRound size={15}/></button>:<><button title="Reset Login" onClick={()=>resetLogin(x)}><RotateCcw size={15}/></button><button title={x.loginEnabled?'Disable Login':'Enable Login'} onClick={()=>toggleLogin(x)}>{x.loginEnabled?<PowerOff size={15}/>:<Power size={15}/>}</button></>}<button className="danger" title="Delete" onClick={()=>del(x)}><Trash2 size={15}/></button></div></td></tr>)}</tbody></>}
 </table></div>}

 {tab==='projectTeam'&&<div className="settings-project-team"><TeamManagementPage/></div>}
 {tab==='supplierKpi'&&<div className="settings-supplier-kpi"><KpiPage/></div>}

 {open&&tab!=='projectTeam'&&tab!=='supplierKpi'&&<div className="budget-modal-backdrop"><div className="budget-modal settings-modal"><div className="budget-modal-head"><h3>{edit.id?'Edit':'New'} {tab==='portfolios'?'Portfolio':tab==='orgUnits'?'Business Unit':tab==='categories'?'Category':'Team Member'}</h3><button onClick={()=>setOpen(false)}><X/></button></div><form onSubmit={save}><div className="budget-form-grid">
 {tab!=='members'&&<label>Code<input required value={edit.code||''} onChange={e=>setEdit({...edit,code:e.target.value})}/></label>}
 {tab!=='orgUnits'&&tab!=='categories'&&<label>Business Unit<select required value={edit.orgUnitId||''} onChange={e=>setEdit({...edit,orgUnitId:Number(e.target.value)})}>{opts.orgUnits.map((o:any)=><option key={o.id} value={o.id}>{o.code} — {o.name}</option>)}</select></label>}
 <label>Name<input required value={edit.name||''} onChange={e=>setEdit({...edit,name:e.target.value})}/></label>
 {tab==='orgUnits'&&<><label>Short Name<input value={edit.shortName||''} onChange={e=>setEdit({...edit,shortName:e.target.value})}/></label><label>International Name<input value={edit.internationalName||''} onChange={e=>setEdit({...edit,internationalName:e.target.value})}/></label><label>Type<select value={edit.type||'SBU'} onChange={e=>setEdit({...edit,type:e.target.value})}>{BUSINESS_UNIT_TYPES.map(x=><option key={x}>{x}</option>)}</select></label><label>Parent Unit<select value={edit.parentId||''} onChange={e=>setEdit({...edit,parentId:e.target.value?Number(e.target.value):null})}><option value="">No Parent</option>{opts.orgUnits.filter((o:any)=>o.id!==edit.id).map((o:any)=><option key={o.id} value={o.id}>{o.code} — {o.name}</option>)}</select></label></>}
 {tab==='members'&&<><label>Email<input required type="email" value={edit.email||''} onChange={e=>setEdit({...edit,email:e.target.value})}/></label><label>Job Title<input value={edit.jobTitle||''} onChange={e=>setEdit({...edit,jobTitle:e.target.value})}/></label><label>Department<input value={edit.department||''} onChange={e=>setEdit({...edit,department:e.target.value})}/></label><label>Role<select value={edit.role||'MEMBER'} onChange={e=>setEdit({...edit,role:e.target.value})}><option>MEMBER</option><option>PM</option><option>PROJECT_MANAGER</option><option>HOD</option><option>CIO</option><option>ADMIN</option><option>ROOT</option></select></label></>}
 {tab==='portfolios'&&<label>Owner<select value={edit.ownerId||''} onChange={e=>setEdit({...edit,ownerId:e.target.value?Number(e.target.value):null})}><option value="">No Owner</option>{opts.users.map((u:any)=><option key={u.id} value={u.id}>{u.name}</option>)}</select></label>}
 {tab==='categories'&&<><label>Scope<select value={edit.scope||'GENERAL'} onChange={e=>setEdit({...edit,scope:e.target.value})}><option>GENERAL</option><option>BUDGET</option><option>PROJECT</option><option>SUPPLIER</option><option>DOCUMENT</option></select></label><label>Description<input value={edit.description||''} onChange={e=>setEdit({...edit,description:e.target.value})}/></label></>}<label>Status<select value={edit.status||'ACTIVE'} onChange={e=>setEdit({...edit,status:e.target.value})}><option>ACTIVE</option><option>INACTIVE</option></select></label>
 </div>{tab==='orgUnits'&&<div className="bu-profile-sections">
<section className="bu-profile-section bu-span-2"><h4>Organization Profile</h4><label>Description<textarea rows={4} value={edit.description||''} onChange={e=>setEdit({...edit,description:e.target.value})}/></label></section>
<section className="bu-profile-section"><h4>Legal & Registration</h4><div className="bu-profile-grid"><label>Legal Type<input value={edit.legalType||''} onChange={e=>setEdit({...edit,legalType:e.target.value})}/></label><label>Tax Code / MST<input value={edit.taxCode||''} onChange={e=>setEdit({...edit,taxCode:e.target.value})}/></label><label>Tax Issue Date<input type="date" value={edit.taxIssueDate?String(edit.taxIssueDate).slice(0,10):''} onChange={e=>setEdit({...edit,taxIssueDate:e.target.value})}/></label><label>Registration No.<input value={edit.registrationNo||''} onChange={e=>setEdit({...edit,registrationNo:e.target.value})}/></label><label>Registration Issue Date<input type="date" value={edit.registrationIssueDate?String(edit.registrationIssueDate).slice(0,10):''} onChange={e=>setEdit({...edit,registrationIssueDate:e.target.value})}/></label><label>Incorporation Date<input type="date" value={edit.incorporationDate?String(edit.incorporationDate).slice(0,10):''} onChange={e=>setEdit({...edit,incorporationDate:e.target.value})}/></label><label>Legal Representative<input value={edit.legalRepresentative||''} onChange={e=>setEdit({...edit,legalRepresentative:e.target.value})}/></label><label>Representative Title<input value={edit.representativeTitle||''} onChange={e=>setEdit({...edit,representativeTitle:e.target.value})}/></label></div></section>
<section className="bu-profile-section"><h4>Contact & Address</h4><div className="bu-profile-grid"><label className="bu-span-2">Registered Address<textarea rows={2} value={edit.registeredAddress||''} onChange={e=>setEdit({...edit,registeredAddress:e.target.value})}/></label><label className="bu-span-2">Office Address<textarea rows={2} value={edit.officeAddress||''} onChange={e=>setEdit({...edit,officeAddress:e.target.value})}/></label><label>Phone<input value={edit.phone||''} onChange={e=>setEdit({...edit,phone:e.target.value})}/></label><label>Email<input type="email" value={edit.email||''} onChange={e=>setEdit({...edit,email:e.target.value})}/></label><label className="bu-span-2">Website<input value={edit.website||''} onChange={e=>setEdit({...edit,website:e.target.value})}/></label></div></section>
<section className="bu-profile-section"><h4>Management</h4><div className="bu-profile-grid"><label>Head / Director<input value={edit.headName||''} onChange={e=>setEdit({...edit,headName:e.target.value})}/></label><label>Finance Contact<input value={edit.financeContact||''} onChange={e=>setEdit({...edit,financeContact:e.target.value})}/></label><label>IT Contact<input value={edit.itContact||''} onChange={e=>setEdit({...edit,itContact:e.target.value})}/></label><label>HR Contact<input value={edit.hrContact||''} onChange={e=>setEdit({...edit,hrContact:e.target.value})}/></label></div></section>
<section className="bu-profile-section"><h4>Finance & System</h4><div className="bu-profile-grid"><label>Default Currency<select value={edit.defaultCurrency||'VND'} onChange={e=>setEdit({...edit,defaultCurrency:e.target.value})}><option>VND</option><option>USD</option><option>EUR</option><option>JPY</option><option>SGD</option><option>KRW</option></select></label><label>Fiscal Year<input value={edit.fiscalYear||''} onChange={e=>setEdit({...edit,fiscalYear:e.target.value})}/></label><label>Cost Center<input value={edit.costCenter||''} onChange={e=>setEdit({...edit,costCenter:e.target.value})}/></label><label>Company Code<input value={edit.companyCode||''} onChange={e=>setEdit({...edit,companyCode:e.target.value})}/></label></div></section>
<section className="bu-profile-section bu-span-2"><div className="bu-attachment-head"><div><h4>Attached Files / Brochures</h4><p className="bu-profile-note">ĐKKD, MST, quyết định thành lập, hồ sơ pháp lý, brochures, company profile và tài liệu giới thiệu đơn vị.</p></div><input ref={buAttachmentRef} hidden type="file" accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.jpg,.jpeg,.png,.zip" onChange={e=>{const f=e.target.files?.[0];if(f)uploadBuAttachment(f)}}/><button type="button" className="secondary" disabled={!edit.id||buAttachmentBusy} onClick={()=>buAttachmentRef.current?.click()}><Upload size={15}/> {buAttachmentBusy?'Uploading…':'Add File'}</button></div><div className="bu-attachment-list">{!edit.id?<div className="bu-attachment-empty">Save SBU first to attach files.</div>:buAttachments.length===0?<div className="bu-attachment-empty">No attached files yet.</div>:buAttachments.map((f:any)=><div className="bu-attachment-row" key={f.name}><div><b>{f.name}</b><small>{(Number(f.size||0)/1024).toFixed(1)} KB</small></div><div><button type="button" onClick={()=>downloadBuAttachment(f.name)}>Download</button><button type="button" className="danger" onClick={()=>deleteBuAttachment(f.name)}><Trash2 size={14}/></button></div></div>)}</div></section>
</div>}{tab==='portfolios'&&<label>Description<textarea rows={4} value={edit.description||''} onChange={e=>setEdit({...edit,description:e.target.value})}/></label>}

<div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setOpen(false)}>Cancel</button><button className="budget-primary">Save</button></div></form></div></div>}</>;
}
