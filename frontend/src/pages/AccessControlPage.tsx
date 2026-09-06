import {useEffect,useMemo,useState} from 'react';
import {ShieldCheck,UsersRound,Network} from 'lucide-react';
import {getJson,putJson} from '../lib/api';
import {SettingsPage} from './SettingsPage';

type Scope={mode:string;values:string[]};
type Tab='permissions'|'users'|'projectTeam';

const moduleLabel=(m:string)=>({
  SETTINGS:'Settings',
  ACCESS_CONTROL:'Access Control',
  USERS:'Organization / Employee',
  PROJECT_TEAM:'Project Assignments',
  PERSONAL_FC:'Personal FC',
  CAPITAL:'Capital Management',
  INVESTMENT:'Investment Management'
} as Record<string,string>)[m]||m.replaceAll('_',' ');

export function AccessControlPage(){
 const[accessUserSearch,setAccessUserSearch]=useState('');
 const[accessUserPickerOpen,setAccessUserPickerOpen]=useState(false);
 const[catalog,setCatalog]=useState<any>(null);
 const[selfAccess,setSelfAccess]=useState<any>(null);
 const[userId,setUserId]=useState(0);
 const[access,setAccess]=useState<any>(null);
 const[msg,setMsg]=useState('');
 const[busy,setBusy]=useState(false);
 const[tab,setTab]=useState<Tab>('permissions');
 const[permView,setPermView]=useState<'functional'|'scope'>('functional');
 const[projectScopeSearch,setProjectScopeSearch]=useState('');

 useEffect(()=>{
   Promise.all([
     getJson<any>('/access/catalog'),
     getJson<any>('/access/me')
   ]).then(([c,me])=>{
     setCatalog(c);
     setSelfAccess(me);
     if(c.users?.length)setUserId(c.users[0].id);
   }).catch(e=>setMsg(String(e)));
 },[]);

 useEffect(()=>{
   if(userId)getJson<any>(`/access/users/${userId}`).then(setAccess).catch(e=>setMsg(String(e)));
 },[userId]);

 const user=useMemo(()=>catalog?.users?.find((x:any)=>x.id===userId),[catalog,userId]);

 const filteredAccessUsers=useMemo(()=>{
  const q=accessUserSearch.trim().toLowerCase();
  const users=catalog?.users||[];
  if(!q)return users.slice(0,30);
  return users.filter((u:any)=>[u.name,u.email,u.employeeCode,u.jobTitle,u.role,u.orgUnit,u.department].filter(Boolean).join(' ').toLowerCase().includes(q)).slice(0,30);
 },[catalog,accessUserSearch]);
 const canUsers=!!selfAccess?.modules?.includes('USERS');
 const canProjectTeam=!!selfAccess?.modules?.includes('PROJECT_TEAM');

 if(!catalog||!access||!selfAccess)
   return <div className="panel" style={{padding:20}}>{msg||'Loading Access Control…'}</div>;

 const has=(m:string,a:string)=>(access.permissions?.[m]||[]).includes(a);

 const toggle=(m:string,a:string)=>{
   const x=new Set<string>(access.permissions?.[m]||[]);
   x.has(a)?x.delete(a):x.add(a);
   if(a==='FULL'&&x.has('FULL'))catalog.actions.filter((a:any)=>a!=='DOWNLOAD').forEach((v:string)=>x.add(v));
   setAccess({...access,permissions:{...access.permissions,[m]:Array.from(x)}});
 };

 const setScope=(m:string,mode:string)=>
   setAccess({...access,scopes:{...access.scopes,[m]:{mode,values:[]}}});

 const toggleValue=(module:string,value:string)=>{
   const current:Scope=access.scopes?.[module]??{mode:'OWN',values:[]};
   const values=new Set<string>(current.values??[]);
   values.has(value)?values.delete(value):values.add(value);
   setAccess({...access,scopes:{...access.scopes,[module]:{...current,values:Array.from(values)}}});
 };

 const save=async()=>{
   setBusy(true);setMsg('');
   try{
     setAccess(await putJson<any>(`/access/users/${userId}`,{
       permissions:access.permissions,
       scopes:{...access.scopes,PROJECTS:access.scopes?.PROJECTS?.mode==='OWN'?{...access.scopes.PROJECTS,mode:'ASSIGNED_PROJECTS'}:access.scopes?.PROJECTS}
     }));
     setMsg('Permissions saved.');
   }catch(e){setMsg(String(e))}
   finally{setBusy(false)}
 };

 return <>
   <div className="page-title access-control-title-inline">
    <div className="access-control-title-left">
     <h1>Access Control</h1>
     <div className="access-main-tabs access-main-tabs-inline">
      <button className={tab==='permissions'?'active':''} onClick={()=>setTab('permissions')}>
       <ShieldCheck size={16}/> Permissions
      </button>
      {canUsers&&
       <button className={tab==='users'?'active':''} onClick={()=>setTab('users')}>
        <UsersRound size={16}/> Users
       </button>}
      {canProjectTeam&&
       <button className={tab==='projectTeam'?'active':''} onClick={()=>setTab('projectTeam')}>
        <Network size={16}/> Project Assignments
       </button>}
     </div>
    </div>
    {tab==='permissions'&&
      <button className="budget-primary" disabled={busy} onClick={save}>
       {busy?'Saving…':'Save permissions'}
      </button>}
   </div>

   {tab==='permissions'&&<>
    <div className="access-permission-subtabs">
     <button className={permView==='functional'?'active':''} onClick={()=>setPermView('functional')}>Functional Permissions</button>
     <button className={permView==='scope'?'active':''} onClick={()=>setPermView('scope')}>Data Scope</button>
    </div>
    <div className="access-userbar">
     <label className="access-user-lookup-label">User
 <div className="access-user-lookup">
  <input value={accessUserSearch} onFocus={()=>setAccessUserPickerOpen(true)} onChange={e=>{setAccessUserSearch(e.target.value);setAccessUserPickerOpen(true)}} onKeyDown={e=>{if(e.key==='Escape')setAccessUserPickerOpen(false);if(e.key==='Enter'&&filteredAccessUsers.length){e.preventDefault();const u=filteredAccessUsers[0];setUserId(Number(u.id));setAccessUserSearch(`${u.name} — ${u.email||''}`);setAccessUserPickerOpen(false)}}} placeholder="Search by name, email or employee code..." autoComplete="off"/>
  {!!accessUserSearch&&<button type="button" className="access-user-clear" onClick={()=>{setAccessUserSearch('');setAccessUserPickerOpen(true)}}>×</button>}
  {accessUserPickerOpen&&<div className="access-user-results">{filteredAccessUsers.length?filteredAccessUsers.map((u:any)=><button type="button" key={u.id} className={Number(u.id)===Number(userId)?'selected':''} onMouseDown={e=>e.preventDefault()} onClick={()=>{setUserId(Number(u.id));setAccessUserSearch(`${u.name} — ${u.email||''}`);setAccessUserPickerOpen(false)}}><b>{u.name}</b><span>{u.email||'—'}{u.employeeCode?` · ${u.employeeCode}`:''}</span><small>{u.orgUnit||u.department||'—'}{u.jobTitle?` · ${u.jobTitle}`:''}</small></button>):<div className="access-user-noresult">No matching user</div>}</div>}
 </div>
</label>
     <div><b>{user?.name}</b><span>{user?.role} · {user?.orgUnit||user?.department||'—'}</span></div>
    </div>

    {permView==='functional'&&<div className="access-grid">
     <div className="access-perm panel">
      <table>
       <thead><tr><th>Module</th>{catalog.actions.map((a:string)=><th key={a}>{a}</th>)}</tr></thead>
       <tbody>
        {catalog.modules.map((m:string)=><tr key={m}>
         <td><b>{moduleLabel(m)}</b></td>
         {catalog.actions.map((a:string)=><td key={a}>
          <input type="checkbox" checked={has(m,a)} onChange={()=>toggle(m,a)}/>
         </td>)}
        </tr>)}
       </tbody>
      </table>
     </div>

     <div className="access-scopes panel">
      <h3>Data Scope — Employee, Budget, KPI, Capital & Investment</h3>
      {['USERS','BUDGET','KPI','CAPITAL','INVESTMENT'].map(m=>{
       const rawScope:Scope=access.scopes?.[m]||{mode:'OWN',values:[]}; const s:Scope=m==='PROJECTS'&&rawScope.mode==='OWN'?{...rawScope,mode:'ASSIGNED_PROJECTS'}:rawScope;
       return <div className="scope-card" key={m}>
        <b>{m}</b>
        <select value={s.mode} onChange={e=>setScope(m,e.target.value)}>
         {catalog.scopeModes.map((x:string)=><option key={x}>{x}</option>)}
        </select>
        {s.mode==='SELECTED_ORGS'&&<div className="scope-values">
         {catalog.orgUnits.map((o:any)=><label key={o.id}>
          <input type="checkbox" checked={(s.values||[]).includes(o.code)} onChange={()=>toggleValue(m,o.code)}/>
          {o.code} — {o.name}
         </label>)}
        </div>}
        {s.mode==='SELECTED_USERS'&&<div className="scope-values">
         {catalog.users.filter((u:any)=>u.id!==userId).map((u:any)=><label key={u.id}>
          <input type="checkbox" checked={(s.values||[]).includes(String(u.id))} onChange={()=>toggleValue(m,String(u.id))}/>
          {u.name}
         </label>)}
        </div>}
       </div>
      })}
     </div>
    </div>}

    {permView==='scope'&&<div className="access-data-scope-panel panel">
     <h3>Data Scope</h3>
     <p className="muted">Data visibility is configured independently from functional permissions.</p>

     {(()=>{
      const sc:Scope=access.scopes?.PROJECTS||{mode:'ALL',values:[]};
      const projectOptions=catalog.projects||[];
      const filteredProjects=projectOptions.filter((p:any)=>{
       const k=projectScopeSearch.trim().toLowerCase();
       return !k||`${p.code||''} ${p.name||''}`.toLowerCase().includes(k);
      });

      return <div className="data-scope-row data-scope-projects">
       <div className="data-scope-module"><b>Projects</b></div>

       <select value={sc.mode} onChange={e=>setScope('PROJECTS',e.target.value)}>
        <option value="ALL">All Projects</option>
        <option value="OWN_ORG">Own Business Unit</option>
        <option value="SELECTED_ORGS">Selected Business Units</option>
        <option value="ASSIGNED_PROJECTS">Assigned Projects</option>
        <option value="SELECTED_PROJECTS">Selected Projects</option>
       </select>

       <div>
        {sc.mode==='SELECTED_ORGS'&&<div className="scope-values">
         {catalog.orgUnits.map((o:any)=><label key={o.id}>
          <input type="checkbox" checked={(sc.values||[]).includes(o.code)} onChange={()=>toggleValue('PROJECTS',o.code)}/>
          {o.code} — {o.name}
         </label>)}
        </div>}

        {sc.mode==='SELECTED_PROJECTS'&&<>
         <div className="project-scope-tools">
          <input placeholder="Search projects..." value={projectScopeSearch} onChange={e=>setProjectScopeSearch(e.target.value)}/>
          <button type="button" className="secondary" onClick={()=>{
           const current=new Set<string>(sc.values||[]);
           filteredProjects.forEach((p:any)=>current.add(String(p.id)));
           setAccess({...access,scopes:{...access.scopes,PROJECTS:{...sc,values:Array.from(current)}}});
          }}>Select All</button>
          <button type="button" className="secondary" onClick={()=>setAccess({...access,scopes:{...access.scopes,PROJECTS:{...sc,values:[]}}})}>Clear</button>
         </div>

         <div className="scope-values project-scope-values">
          {filteredProjects.map((p:any)=><label key={p.id}>
           <input type="checkbox" checked={(sc.values||[]).includes(String(p.id))} onChange={()=>toggleValue('PROJECTS',String(p.id))}/>
           <b>{p.code||'—'}</b> — {p.name}
          </label>)}
         </div>
         <small>{(sc.values||[]).length} project(s) selected</small>
        </>}
       </div>
      </div>
     })()}

     {['BUDGET','FINANCE','CONTRACTS','CAPITAL','INVESTMENT','PERSONAL_FC','IT_ASSETS','DOCUMENTS','TASKS','RISKS','SUPPLIERS'].map(m=>{
      const sc:Scope=access.scopes?.[m]||{mode:'OWN_ORG',values:[]};
      return <div className="data-scope-row" key={m}>
       <div className="data-scope-module"><b>{moduleLabel(m)}</b></div>
       <select value={sc.mode} onChange={e=>setScope(m,e.target.value)}>
        <option value="OWN">Own Records</option><option value="ALL">All</option><option value="OWN_ORG">Own Business Unit</option><option value="SELECTED_ORGS">Selected Business Units</option>
       </select>
       {sc.mode==='SELECTED_ORGS'&&<div className="scope-values">{catalog.orgUnits.map((o:any)=><label key={o.id}><input type="checkbox" checked={(sc.values||[]).includes(o.code)} onChange={()=>toggleValue(m,o.code)}/>{o.code} — {o.name}</label>)}</div>}
      </div>
     })}
     {(()=>{const sc:Scope=access.scopes?.USERS||{mode:'OWN',values:[]};return <div className="data-scope-row">
      <div className="data-scope-module"><b>Organization / Employee</b></div>
      <select value={sc.mode} onChange={e=>setScope('USERS',e.target.value)}>
       <option value="OWN">Own Profile</option><option value="OWN_ORG">Own Business Unit</option><option value="SELECTED_USERS">Selected Employees</option><option value="SELECTED_ORGS">Selected Business Units</option><option value="ALL">All Employees</option>
      </select>
      {sc.mode==='SELECTED_USERS'&&<div className="scope-values">{catalog.users.filter((u:any)=>u.id!==userId).map((u:any)=><label key={u.id}><input type="checkbox" checked={(sc.values||[]).includes(String(u.id))} onChange={()=>toggleValue('USERS',String(u.id))}/>{u.name}{u.orgUnit?` · ${u.orgUnit}`:''}</label>)}</div>}
      {sc.mode==='SELECTED_ORGS'&&<div className="scope-values">{catalog.orgUnits.map((o:any)=><label key={o.id}><input type="checkbox" checked={(sc.values||[]).includes(o.code)} onChange={()=>toggleValue('USERS',o.code)}/>{o.code} — {o.name}</label>)}</div>}
     </div>})()}
     {(()=>{const sc:Scope=access.scopes?.KPI||{mode:'OWN',values:[]};return <div className="data-scope-row">
      <div className="data-scope-module"><b>KPI</b></div>
      <select value={sc.mode} onChange={e=>setScope('KPI',e.target.value)}>
       <option value="OWN">Own KPI</option><option value="OWN_TEAM">Own Team</option><option value="OWN_ORG">Own Business Unit</option><option value="SELECTED_USERS">Selected Users</option><option value="SELECTED_ORGS">Selected Business Units</option><option value="ALL">All</option>
      </select>
      {sc.mode==='SELECTED_USERS'&&<div className="scope-values">{catalog.users.map((u:any)=><label key={u.id}><input type="checkbox" checked={(sc.values||[]).includes(String(u.id))} onChange={()=>toggleValue('KPI',String(u.id))}/>{u.name}</label>)}</div>}
      {sc.mode==='SELECTED_ORGS'&&<div className="scope-values">{catalog.orgUnits.map((o:any)=><label key={o.id}><input type="checkbox" checked={(sc.values||[]).includes(o.code)} onChange={()=>toggleValue('KPI',o.code)}/>{o.code} — {o.name}</label>)}</div>}
     </div>})()}
    </div>}
   </>}

   {tab==='users'&&canUsers&&
     <SettingsPage key="access-users" context="access" initialTab="members"/>}

   {tab==='projectTeam'&&canProjectTeam&&
     <SettingsPage key="access-project-team" context="access" initialTab="projectTeam"/>}
 </>;
}
