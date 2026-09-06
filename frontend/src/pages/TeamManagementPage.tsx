import { useEffect, useMemo, useState, useRef } from 'react';
import { createPortal } from 'react-dom';
import { Edit3, KeyRound, Plus, Search, Trash2, UsersRound, X, Upload, Download } from 'lucide-react';
import { deleteJson, getJson, postJson, putJson } from '../lib/api';

import {UserHrProfileDialog} from '../components/UserHrProfileDialog';

function EmployeeBusinessUnitLookup({
 value,onChange,orgUnits
}:{
 value:number|null|undefined,
 onChange:(id:number|null)=>void,
 orgUnits:any[]
}){
 const [open,setOpen]=useState(false)
 const [query,setQuery]=useState('')
 const selected=orgUnits.find((x:any)=>Number(x.id)===Number(value||0))
 const shown=open?query:(selected?`${selected.code||''}${selected.code?' — ':''}${selected.name||''}`:'')
 const q=query.trim().toLowerCase()
 const rows=(q?orgUnits.filter((x:any)=>`${x.code||''} ${x.name||''}`.toLowerCase().includes(q)):orgUnits).slice(0,40)

 return <div className="employee-bu-lookup">
  <input value={shown} placeholder="Type Business Unit code or name..." autoComplete="off"
   onFocus={()=>{setQuery('');setOpen(true)}}
   onChange={e=>{setQuery(e.target.value);setOpen(true)}}
   onKeyDown={e=>{
    if(e.key==='Escape')setOpen(false)
    if(e.key==='Enter'&&rows.length){
     e.preventDefault();onChange(Number(rows[0].id));setOpen(false);setQuery('')
    }
   }}/>
  <button type="button" onClick={()=>{setQuery('');setOpen(v=>!v)}}>v</button>
  {open&&<div className="employee-bu-results">
   <button type="button" onMouseDown={e=>e.preventDefault()} onClick={()=>{onChange(null);setOpen(false);setQuery('')}}>Unassigned</button>
   {rows.map((x:any)=><button type="button" key={x.id}
    onMouseDown={e=>e.preventDefault()}
    onClick={()=>{onChange(Number(x.id));setOpen(false);setQuery('')}}>
    <b>{x.code||''}{x.code?' — ':''}{x.name||''}</b>
   </button>)}
  </div>}
 </div>
}



function teamCsvEscape(v:any){
 const x=v==null?'':String(v);
 return /[",\n\r]/.test(x)?`"${x.replaceAll('"','""')}"`:x;
}
function teamDownloadCsv(name:string,rows:any[],keys:string[]){
 const body=[keys.join(','),...rows.map(r=>keys.map(k=>teamCsvEscape(r[k])).join(','))].join('\n');
 const blob=new Blob(['\uFEFF'+body],{type:'text/csv;charset=utf-8'});
 const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download=name;a.click();URL.revokeObjectURL(a.href);
}
function teamParseCsv(text:string){
 const rows:string[][]=[];let row:string[]=[],cell='',q=false;
 for(let i=0;i<text.length;i++){const ch=text[i];
  if(q){if(ch==='"'&&text[i+1]==='"'){cell+='"';i++}else if(ch==='"')q=false;else cell+=ch}
  else{if(ch==='"')q=true;else if(ch===','){row.push(cell);cell=''}else if(ch==='\n'){row.push(cell);rows.push(row);row=[];cell=''}else if(ch!=='\r')cell+=ch}
 }
 row.push(cell);if(row.some(x=>x.trim()))rows.push(row);
 if(rows.length<2)return[];
 const h=rows[0].map(x=>x.trim().replace(/^\uFEFF/,''));
 return rows.slice(1).filter(r=>r.some(x=>x.trim())).map(r=>Object.fromEntries(h.map((k,i)=>[k,(r[i]??'').trim()])));
}

function EmployeeAvatar({userId,name}:{userId:number;name:string}){
 const [failed,setFailed]=useState(false);
 const initial=String(name||'?').trim().charAt(0).toUpperCase();
 if(failed)return <div className="employee-name-avatar-fallback">{initial}</div>;
 return <img className="employee-name-avatar-img" src={`/api/users/${userId}/profile-image/view`} alt={name} onError={()=>setFailed(true)}/>;
}

type User={id:number;orgUnitId:number;name:string;email:string;jobTitle:string;department:string;role:string;status:string};
type Project={id:number;code:string;name:string};
type Member={id:number;projectId:number;project:string;userId:number;user:string;projectRole:string;allocationPct:number;startDate?:string;endDate?:string;isActive:boolean};

const blankUser={orgUnitId:1,name:'',email:'',jobTitle:'',department:'IT',role:'MEMBER',status:'ACTIVE'};
const blankMember={projectId:'',projectIds:[] as number[],userId:'',projectRole:'MEMBER',allocationPct:'100',startDate:'',endDate:'',isActive:true};

export function TeamManagementPage(){
 const [users,setUsers]=useState<User[]>([]);const [projects,setProjects]=useState<Project[]>([]);const [members,setMembers]=useState<Member[]>([]);
 const [q,setQ]=useState('');const [employeeOrgFilter,setEmployeeOrgFilter]=useState('ALL');const [employeeDeptFilter,setEmployeeDeptFilter]=useState('ALL');const [employeeOrgUnits,setEmployeeOrgUnits]=useState<any[]>([]);const [projectAssignSearch,setProjectAssignSearch]=useState('');const [projectId,setProjectId]=useState('ALL');const [userForm,setUserForm]=useState<any>(null);const [editingUser,setEditingUser]=useState<User|null>(null);const [memberForm,setMemberForm]=useState<any>(null);const [editingMember,setEditingMember]=useState<Member|null>(null);const [error,setError]=useState('');
 const load=async()=>{const [u,p,m]=await Promise.all([getJson<User[]>('/team/users'),getJson<Project[]>('/projects'),getJson<Member[]>('/project-members')]);setUsers(u);setProjects(p);setMembers(m)};
 useEffect(()=>{load().catch(e=>setError(String(e)))},[]);
 useEffect(()=>{getJson<any>('/settings/options').then(o=>setEmployeeOrgUnits(o?.orgUnits||[])).catch(()=>setEmployeeOrgUnits([]))},[]);
 const employeeDepartmentOptions=useMemo(()=>Array.from(new Set(users.map(u=>String(u.department||'').trim()).filter(Boolean))).sort((a,b)=>a.localeCompare(b)),[users]);
 const employeeBusinessUnitOptions=useMemo(()=>employeeOrgUnits.filter((x:any)=>users.some(u=>String(u.orgUnitId)===String(x.id))),[employeeOrgUnits,users]);
 const filteredUsers=useMemo(()=>users.filter(u=>{
 const textOk=!q||`${u.name} ${u.email} ${u.jobTitle} ${u.department}`.toLowerCase().includes(q.toLowerCase());
 const orgOk=employeeOrgFilter==='ALL'||String(u.orgUnitId)===employeeOrgFilter;
 const deptOk=employeeDeptFilter==='ALL'||String(u.department||'')===employeeDeptFilter;
 return textOk&&orgOk&&deptOk;
}),[users,q,employeeOrgFilter,employeeDeptFilter]);
 const filteredMembers=useMemo(()=>members.filter(m=>projectId==='ALL'||String(m.projectId)===projectId),[members,projectId]);
 const saveUser=async(e:React.FormEvent)=>{e.preventDefault();try{editingUser?await putJson(`/team/users/${editingUser.id}`,userForm):await postJson('/team/users',userForm);setUserForm(null);setEditingUser(null);await load()}catch(e){setError(String(e))}};
 const removeUser=async(u:User)=>{if(!confirm(`Remove ${u.name}? Referenced users will be deactivated instead.`))return;try{await deleteJson(`/team/users/${u.id}`);await load()}catch(e){setError(String(e))}};
 const resetLogin=async(u:User)=>{if(!confirm(`Create/reset login for ${u.name}?`))return;try{const r=await postJson<{temporaryPassword:string;email:string}>(`/team/users/${u.id}/reset-login`,{});alert(`Login ready for ${u.name}\nEmail: ${r.email}\nTemporary password: ${r.temporaryPassword}\nThe user must change it after first sign-in.`)}catch(e){setError(String(e))}};

 const saveMember=async(e:React.FormEvent)=>{
  e.preventDefault();
  try{
    setError('');

    if(editingMember){
      const payload={
        ...memberForm,
        projectId:Number(memberForm.projectId),
        userId:Number(memberForm.userId),
        allocationPct:Number(memberForm.allocationPct||0),
        startDate:memberForm.startDate||null,
        endDate:memberForm.endDate||null
      };
      await putJson(`/project-members/${editingMember.id}`,payload);
    }else{
      const projectIds=(memberForm.projectIds||[]).map(Number).filter(Boolean);
      if(!memberForm.userId){setError('Please select a person.');return;}
      if(!projectIds.length){setError('Please select at least one project.');return;}
      const result=await postJson<any>('/project-members/batch',{
        userId:Number(memberForm.userId),
        projectIds,
        projectRole:memberForm.projectRole||'MEMBER',
        allocationPct:Number(memberForm.allocationPct||100),
        startDate:memberForm.startDate||null,
        endDate:memberForm.endDate||null,
        isActive:memberForm.isActive!==false
      });
      alert(`Assignment completed.\nSelected: ${result.selected||projectIds.length}\nCreated: ${result.created||0}\nUpdated: ${result.updated||0}`);
    }
    setMemberForm(null);
    setEditingMember(null);
    setProjectAssignSearch('');
    await load();
  }catch(e:any){
    const message=String(e?.message||e||'Assignment failed');
    setError(message);
    alert(message);
  }
};
const removeMember=async(m:Member)=>{if(!confirm(`Remove ${m.user} from ${m.project}?`))return;try{await deleteJson(`/project-members/${m.id}`);await load()}catch(e){setError(String(e))}};
 const employeeImportRef=useRef<HTMLInputElement|null>(null);const assignmentImportRef=useRef<HTMLInputElement|null>(null);const[importing,setImporting]=useState('');


 const exportEmployeesCsv=()=>{
  teamDownloadCsv(`MPMS-Employee-Directory-${new Date().toISOString().slice(0,10)}.csv`,
   users.map(u=>({email:u.email,name:u.name,orgUnitId:u.orgUnitId,jobTitle:u.jobTitle,department:u.department,role:u.role,status:u.status})),
   ['email','name','orgUnitId','jobTitle','department','role','status']);
 };

 const importEmployeesCsv=async(file:File)=>{
  setImporting('employees');setError('');
  try{
   const data=teamParseCsv(await file.text());let created=0,updated=0,skipped=0;
   for(const r of data){
    const email=String(r.email||'').trim().toLowerCase(),name=String(r.name||'').trim();
    if(!email||!name){skipped++;continue}
    const payload={
      orgUnitId:Number(r.orgUnitId||1),
      name,email,
      jobTitle:String(r.jobTitle||'').trim(),
      department:String(r.department||'').trim(),
      role:String(r.role||'MEMBER').trim().toUpperCase(),
      status:String(r.status||'ACTIVE').trim().toUpperCase()
    };
    const old=users.find(u=>u.email.trim().toLowerCase()===email);
    if(old){await putJson(`/team/users/${old.id}`,payload);updated++}
    else{await postJson('/team/users',payload);created++}
   }
   await load();alert(`Employee import completed.\nCreated: ${created}\nUpdated: ${updated}\nSkipped: ${skipped}`);
  }catch(e:any){setError(String(e?.message||e));alert(String(e?.message||e))}
  finally{setImporting('');if(employeeImportRef.current)employeeImportRef.current.value=''}
 };

 const exportAssignmentsCsv=()=>{
  const byUser=new Map(users.map(u=>[u.id,u]));
  const byProject=new Map(projects.map(p=>[p.id,p]));
  teamDownloadCsv(`MPMS-Project-Assignments-${new Date().toISOString().slice(0,10)}.csv`,
   members.map(m=>({
    projectCode:byProject.get(m.projectId)?.code||'',
    project:byProject.get(m.projectId)?.name||m.project||'',
    userEmail:byUser.get(m.userId)?.email||'',
    user:m.user||'',
    projectRole:m.projectRole||'MEMBER',
    allocationPct:m.allocationPct??100,
    startDate:m.startDate||'',
    endDate:m.endDate||'',
    isActive:m.isActive
   })),
   ['projectCode','project','userEmail','user','projectRole','allocationPct','startDate','endDate','isActive']);
 };

 const importAssignmentsCsv=async(file:File)=>{
  setImporting('assignments');setError('');
  try{
   const data=teamParseCsv(await file.text());let created=0,updated=0,skipped=0;
   for(const r of data){
    const code=String(r.projectCode||'').trim().toLowerCase();
    const email=String(r.userEmail||'').trim().toLowerCase();
    const project=projects.find(p=>p.code.trim().toLowerCase()===code);
    const user=users.find(u=>u.email.trim().toLowerCase()===email);
    if(!project||!user){skipped++;continue}
    const payload={
      projectId:project.id,userId:user.id,
      projectRole:String(r.projectRole||'MEMBER').trim(),
      allocationPct:Number(r.allocationPct||100),
      startDate:r.startDate||null,endDate:r.endDate||null,
      isActive:!['0','false','no','n'].includes(String(r.isActive??'true').trim().toLowerCase())
    };
    const old=members.find(m=>m.projectId===project.id&&m.userId===user.id);
    if(old){await putJson(`/project-members/${old.id}`,payload);updated++}
    else{await postJson('/project-members',payload);created++}
   }
   await load();alert(`Project Assignment import completed.\nCreated: ${created}\nUpdated: ${updated}\nSkipped: ${skipped}`);
  }catch(e:any){setError(String(e?.message||e));alert(String(e?.message||e))}
  finally{setImporting('');if(assignmentImportRef.current)assignmentImportRef.current.value=''}
 };

 return <>
  
  {typeof document!=='undefined'&&document.getElementById('project-team-toolbar-host')&&createPortal(
   <div className="project-actions project-team-inline-actions">
    <button className="secondary" onClick={()=>{setEditingUser(null);setUserForm({...blankUser})}}><Plus size={16}/> Add Person</button>
    <button className="budget-primary" onClick={()=>{setEditingMember(null);setMemberForm({...blankMember})}}><UsersRound size={16}/> Assign to Project</button>
   </div>,
   document.getElementById('project-team-toolbar-host')!
  )}
  {error&&<div className="budget-error">{error}</div>}
  <section className="performance-kpis"><div><span>People</span><b>{users.length}</b></div><div><span>Active People</span><b>{users.filter(x=>x.status==='ACTIVE').length}</b></div><div><span>Project Assignments</span><b>{members.length}</b></div><div><span>Projects with Team</span><b>{new Set(members.map(x=>x.projectId)).size}</b></div></section>
  <div className="people-section-head">
    <div className="employee-directory-title-row">
      <div>
        <h2>Employee Directory</h2>
        <p>Employee profiles, account access and project resource information.</p>
      </div>

      <div className="employee-directory-title-actions">
        <input
          ref={employeeImportRef}
          hidden
          type="file"
          accept=".csv,text/csv"
          onChange={e=>{
            const f=e.target.files?.[0];
            if(f) importEmployeesCsv(f)
          }}
        />

        <input
          ref={assignmentImportRef}
          hidden
          type="file"
          accept=".csv,text/csv"
          onChange={e=>{
            const f=e.target.files?.[0];
            if(f) importAssignmentsCsv(f)
          }}
        />

        <button
          className="secondary"
          disabled={importing==='employees'}
          onClick={()=>employeeImportRef.current?.click()}
        >
          <Upload size={15}/>
          {importing==='employees' ? 'Importing…' : 'Import Employees'}
        </button>

        <button
          className="secondary"
          onClick={exportEmployeesCsv}
        >
          <Download size={15}/>
          Export Employees
        </button>

        <button
          className="secondary"
          disabled={importing==='assignments'}
          onClick={()=>assignmentImportRef.current?.click()}
        >
          <Upload size={15}/>
          {importing==='assignments' ? 'Importing…' : 'Import Assignments'}
        </button>

        <button
          className="secondary"
          onClick={exportAssignmentsCsv}
        >
          <Download size={15}/>
          Export Assignments
        </button>
      </div>
    </div>
  </div>

  <section className="data-module-panel">
    <div className="data-module-toolbar employee-directory-filters">

      <label className="module-search">
        <Search/>
        <input
          value={q}
          onChange={e=>setQ(e.target.value)}
          placeholder="Search team members..."
        />
      </label>

      <div className="employee-directory-filter-group">
        <select
          value={employeeOrgFilter}
          onChange={e=>setEmployeeOrgFilter(e.target.value)}
        >
          <option value="ALL">All Business Units</option>
          {employeeBusinessUnitOptions.map((x:any)=>
            <option key={x.id} value={String(x.id)}>
              {x.code} — {x.name}
            </option>
          )}
        </select>

        <select
          value={employeeDeptFilter}
          onChange={e=>setEmployeeDeptFilter(e.target.value)}
        >
          <option value="ALL">All Departments</option>
          {employeeDepartmentOptions.map(x=>
            <option key={x} value={x}>{x}</option>
          )}
        </select>

        <small>{filteredUsers.length} / {users.length}</small>

        {(q || employeeOrgFilter!=='ALL' || employeeDeptFilter!=='ALL') &&
          <button
            type="button"
            className="secondary"
            onClick={()=>{
              setQ('');
              setEmployeeOrgFilter('ALL');
              setEmployeeDeptFilter('ALL');
            }}
          >
            Clear
          </button>
        }
      </div>
    </div>

    <div className="module-table-wrap">
      <table className="module-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Job Title</th>
            <th>Department</th>
            <th>Business Unit</th>
            <th>Email</th>
            <th>Role</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>

        <tbody>
          {filteredUsers.map(u=>
            <tr key={u.id}>
              <td>
                <div className="employee-name-with-avatar">
                  <EmployeeAvatar userId={u.id} name={u.name}/>
                  <span className="employee-name-text">
                    <b>{u.name}</b>
                  </span>
                </div>
              </td>

              <td>{u.jobTitle || '—'}</td>
              <td>{u.department || '—'}</td>

              <td>
                {employeeOrgUnits.find(
                  (x:any)=>Number(x.id)===Number(u.orgUnitId)
                )?.name || '—'}
              </td>

              <td>{u.email}</td>
              <td>{u.role}</td>

              <td>
                <span className={`module-pill ${u.status.toLowerCase()}`}>
                  {u.status}
                </span>
              </td>

              <td>
                <div className="row-actions">
                  <UserHrProfileDialog user={u}/>

                  <button
                    title="Create / reset login"
                    onClick={()=>resetLogin(u)}
                  >
                    <KeyRound/>
                  </button>

                  <button
                    onClick={()=>{
                      setEditingUser(u);
                      setUserForm({...u});
                    }}
                  >
                    <Edit3/>
                  </button>

                  <button
                    className="danger"
                    onClick={()=>removeUser(u)}
                  >
                    <Trash2/>
                  </button>
                </div>
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  </section>

  <section className="data-module-panel"><div className="data-module-toolbar"><b>Project Assignments</b><select value={projectId} onChange={e=>setProjectId(e.target.value)}><option value="ALL">All projects</option>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></div><div className="module-table-wrap"><table className="module-table"><thead><tr><th>Project</th><th>Person</th><th>Project Role</th><th>Allocation</th><th>Start</th><th>End</th><th>Status</th><th>Actions</th></tr></thead><tbody>{filteredMembers.map(m=><tr key={m.id}><td><b>{m.project}</b></td><td>{m.user}</td><td>{m.projectRole}</td><td>{m.allocationPct}%</td><td>{m.startDate||'—'}</td><td>{m.endDate||'—'}</td><td>{m.isActive?'ACTIVE':'INACTIVE'}</td><td><div className="row-actions"><button onClick={()=>{setEditingMember(m);setMemberForm({...m,projectId:String(m.projectId),userId:String(m.userId),allocationPct:String(m.allocationPct),startDate:m.startDate||'',endDate:m.endDate||''})}}><Edit3/></button><button className="danger" onClick={()=>removeMember(m)}><Trash2/></button></div></td></tr>)}</tbody></table></div></section>
  {userForm&&<div className="budget-modal-backdrop" onMouseDown={()=>setUserForm(null)}><div className="budget-modal" onMouseDown={e=>e.stopPropagation()}><div className="budget-modal-head"><div><h3>{editingUser?'Edit':'Add'} Person</h3><p>Add anyone participating in the project team, not only people from the imported UPF file.</p></div><button onClick={()=>setUserForm(null)}><X/></button></div><form onSubmit={saveUser}><div className="budget-form-grid"><label>Name<input required value={userForm.name} onChange={e=>setUserForm({...userForm,name:e.target.value})}/></label><label>Email<input required type="email" value={userForm.email} onChange={e=>setUserForm({...userForm,email:e.target.value})}/></label><label>Job title<input value={userForm.jobTitle} onChange={e=>setUserForm({...userForm,jobTitle:e.target.value})}/></label><label>Department<input value={userForm.department} onChange={e=>setUserForm({...userForm,department:e.target.value})}/></label><label>Business Unit<EmployeeBusinessUnitLookup
 value={userForm.orgUnitId}
 onChange={v=>setUserForm({...userForm,orgUnitId:v})}
 orgUnits={employeeOrgUnits}
 /></label><label>System role<select value={userForm.role} onChange={e=>setUserForm({...userForm,role:e.target.value})}><option>MEMBER</option><option>PROJECT_MANAGER</option><option>HOD</option><option>SPONSOR</option><option>ADMIN</option><option>ROOT</option></select></label><label>Status<select value={userForm.status} onChange={e=>setUserForm({...userForm,status:e.target.value})}><option>ACTIVE</option><option>INACTIVE</option></select></label></div><div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setUserForm(null)}>Cancel</button><button className="budget-primary">Save</button></div></form></div></div>}
  {memberForm&&<div className="budget-modal-backdrop" onMouseDown={()=>setMemberForm(null)}><div className="budget-modal" onMouseDown={e=>e.stopPropagation()}><div className="budget-modal-head"><div><h3>{editingMember?'Edit':'Assign'} Project Member</h3><p>One person can be allocated to multiple projects with different roles and percentages.</p></div><button onClick={()=>setMemberForm(null)}><X/></button></div><form onSubmit={saveMember}>{editingMember
 ?<label>Project<select required disabled value={memberForm.projectId}><option value={memberForm.projectId}>{projects.find(p=>String(p.id)===String(memberForm.projectId))?.name||'Project'}</option></select></label>
 :<div className="team-project-field"><div className="team-project-head"><label>Projects</label><div><button type="button" className="secondary" onClick={()=>{const k=projectAssignSearch.trim().toLowerCase();const ids=projects.filter(p=>!k||`${p.code} ${p.name}`.toLowerCase().includes(k)).map(p=>p.id);setMemberForm({...memberForm,projectIds:Array.from(new Set([...(memberForm.projectIds||[]),...ids]))})}}>Select All</button><button type="button" className="secondary" onClick={()=>setMemberForm({...memberForm,projectIds:[]})}>Clear</button></div></div>
 <input className="team-project-search" value={projectAssignSearch} onChange={e=>setProjectAssignSearch(e.target.value)} placeholder="Search projects..."/>
 <div className="team-project-multi">{projects.filter(p=>!projectAssignSearch||`${p.code} ${p.name}`.toLowerCase().includes(projectAssignSearch.toLowerCase())).map(p=>{const checked=(memberForm.projectIds||[]).includes(p.id);return <label key={p.id} className={checked?'selected':''}><input type="checkbox" checked={checked} onChange={()=>{const cur=new Set<number>(memberForm.projectIds||[]);checked?cur.delete(p.id):cur.add(p.id);setMemberForm({...memberForm,projectIds:Array.from(cur)})}}/><span><b>{p.code}</b> — {p.name}</span></label>})}</div><small>{(memberForm.projectIds||[]).length} project(s) selected</small></div>}<label>Person<select required disabled={!!editingMember} value={memberForm.userId} onChange={e=>setMemberForm({...memberForm,userId:e.target.value})}><option value="">Select person</option>{users.filter(u=>u.status==='ACTIVE').map(u=><option key={u.id} value={u.id}>{u.name} — {u.jobTitle||u.department}</option>)}</select></label><div className="budget-form-grid"><label>Project role<input value={memberForm.projectRole} onChange={e=>setMemberForm({...memberForm,projectRole:e.target.value})}/></label><label>Allocation %<input type="number" min="0" max="100" value={memberForm.allocationPct} onChange={e=>setMemberForm({...memberForm,allocationPct:e.target.value})}/></label><label>Start<input type="date" value={memberForm.startDate} onChange={e=>setMemberForm({...memberForm,startDate:e.target.value})}/></label><label>End<input type="date" value={memberForm.endDate} onChange={e=>setMemberForm({...memberForm,endDate:e.target.value})}/></label></div><div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setMemberForm(null)}>Cancel</button><button className="budget-primary">Save Assignment</button></div></form></div></div>}
 </>;
}
