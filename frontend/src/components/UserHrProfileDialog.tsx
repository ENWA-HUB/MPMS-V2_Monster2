import {useEffect,useState} from 'react';
import {UserRound} from 'lucide-react';

export function UserHrProfileDialog({user,menuMode=false,onSaved}:{user:any;menuMode?:boolean;onSaved?:()=>void}){
 const [open,setOpen]=useState(false);
 const [data,setData]=useState<any>(null);
 const [users,setUsers]=useState<any[]>([]);
 const [profileFile,setProfileFile]=useState<File|null>(null);
 const [signatureFile,setSignatureFile]=useState<File|null>(null);
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');
 useEffect(()=>{if(!open)return;Promise.all([
  fetch(`/api/users/${user.id}/profile`,{credentials:'same-origin'}).then(async r=>{if(!r.ok)throw new Error(await r.text());return r.json()}),
  fetch('/api/users',{credentials:'same-origin'}).then(r=>r.ok?r.json():[])
 ]).then(([p,u])=>{setData(p);setUsers(u)}).catch(e=>setError(String(e)))},[open,user.id]);
 const set=(k:string,v:any)=>setData((x:any)=>({...x,[k]:v}));
 const reloadProfile=async()=>{const r=await fetch(`/api/users/${user.id}/profile`,{credentials:'same-origin'});if(!r.ok)throw new Error(await r.text());const j=await r.json();setData(j);return j};
 const upload=async(path:string,file:File)=>{const f=new FormData();f.append('file',file);const r=await fetch(path,{method:'POST',credentials:'same-origin',body:f});if(!r.ok)throw new Error(await r.text())};
 const save=async()=>{if(!data)return;setBusy(true);setError('');try{
  const r=await fetch(`/api/users/${user.id}/profile`,{method:'PUT',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify({
   employeeCode:data.employeeCode||null,phone:data.phone||null,personalEmail:data.personalEmail||null,dateOfBirth:data.dateOfBirth||null,gender:data.gender||null,address:data.address||null,joinDate:data.joinDate||null,employmentType:data.employmentType||null,managerUserId:data.managerUserId?Number(data.managerUserId):null,emergencyContactName:data.emergencyContactName||null,emergencyContactPhone:data.emergencyContactPhone||null,notes:data.notes||null
  })});if(!r.ok)throw new Error(await r.text());
  if(profileFile)await upload(`/api/users/${user.id}/profile-image`,profileFile);
  if(signatureFile)await upload(`/api/users/${user.id}/signature`,signatureFile);
  alert('Employee profile saved.');onSaved?.();setOpen(false)
 }catch(e){setError(String(e))}finally{setBusy(false)}};
 return <>
 <button type="button" className={menuMode?"account-self-profile":"hr-profile-action"} title="Employee Profile" onClick={()=>setOpen(true)}>{menuMode?<><UserRound size={16}/> Edit Profile</>:<>👤</>}</button>
 {open&&<div className="budget-modal-backdrop employee-profile-overlay"><div className="budget-modal hr-profile-modal employee-profile-modal">
  <div className="budget-modal-head"><div className="hr-profile-heading"><span className="hr-profile-eyebrow">EMPLOYEE PROFILE</span><h3>{user.name}</h3><p>{user.email}</p></div><button type="button" onClick={()=>setOpen(false)}>×</button></div>
  <div className="budget-modal-body hr-profile-body employee-profile-body">{!data?<div>{error||'Loading...'}</div>:<div className="budget-form-grid hr-profile-grid">
   <label>Employee code<input value={data.employeeCode||''} onChange={e=>set('employeeCode',e.target.value)}/></label>
   <label>Phone<input value={data.phone||''} onChange={e=>set('phone',e.target.value)}/></label>
   <label>Personal email<input type="email" value={data.personalEmail||''} onChange={e=>set('personalEmail',e.target.value)}/></label>
   <label>Date of birth<input type="date" value={(data.dateOfBirth||'').slice(0,10)} onChange={e=>set('dateOfBirth',e.target.value)}/></label>
   <label>Gender<select value={data.gender||''} onChange={e=>set('gender',e.target.value)}><option value="">—</option><option value="MALE">Male</option><option value="FEMALE">Female</option><option value="OTHER">Other</option></select></label>
   <label>Join date<input type="date" value={(data.joinDate||'').slice(0,10)} onChange={e=>set('joinDate',e.target.value)}/></label>
   <label>Employment type<select value={data.employmentType||''} onChange={e=>set('employmentType',e.target.value)}><option value="">—</option><option value="PERMANENT">Permanent</option><option value="PROBATION">Probation</option><option value="CONTRACT">Contract</option><option value="PART_TIME">Part-time</option><option value="INTERN">Intern</option></select></label>
   <label>Manager<select value={data.managerUserId||''} onChange={e=>set('managerUserId',e.target.value)}><option value="">—</option>{users.filter(x=>x.id!==user.id).map(x=><option key={x.id} value={x.id}>{x.name} — {x.email}</option>)}</select></label>
   <label style={{gridColumn:'1 / -1'}}>Address<input value={data.address||''} onChange={e=>set('address',e.target.value)}/></label>
   <label>Emergency contact<input value={data.emergencyContactName||''} onChange={e=>set('emergencyContactName',e.target.value)}/></label>
   <label>Emergency phone<input value={data.emergencyContactPhone||''} onChange={e=>set('emergencyContactPhone',e.target.value)}/></label>
   <label>Profile image{data?.profileImagePath&&<div className="hr-profile-file-preview"><img src={`/api/users/${user.id}/profile-image/view?t=${encodeURIComponent(String(data.profileImagePath))}`} alt="Current profile"/></div>}<input type="file" accept=".jpg,.jpeg,.png,.webp" onChange={e=>setProfileFile(e.target.files?.[0]||null)}/><small>{data?.profileImagePath?'Current image is saved. Choose a file only to replace it.':'No profile image saved.'}</small></label>
   <label>Signature{data?.signaturePath&&<div className="hr-profile-file-preview signature"><img src={`/api/users/${user.id}/signature/view?t=${encodeURIComponent(String(data.signaturePath))}`} alt="Current signature"/></div>}<input type="file" accept=".jpg,.jpeg,.png,.webp" onChange={e=>setSignatureFile(e.target.files?.[0]||null)}/><small>{data?.signaturePath?'Current signature is saved. Choose a file only to replace it.':'No signature saved.'}</small></label>
   <label style={{gridColumn:'1 / -1'}}>Notes<textarea rows={4} value={data.notes||''} onChange={e=>set('notes',e.target.value)}/></label>
  </div>}{error&&<div className="budget-error">{error}</div>}</div>
  <div className="budget-modal-actions employee-profile-actions"><button type="button" className="secondary" onClick={()=>setOpen(false)}>Cancel</button><button type="button" className="budget-primary" disabled={busy||!data} onClick={save}>{busy?'Saving...':'Save Profile'}</button></div>
 </div></div>}
 </>;
}
