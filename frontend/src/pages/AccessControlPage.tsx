import {useEffect,useMemo,useState} from 'react';
import {getJson,putJson} from '../lib/api';
type Scope={mode:string;values:string[]};
export function AccessControlPage(){
 const[catalog,setCatalog]=useState<any>(null),[userId,setUserId]=useState(0),[access,setAccess]=useState<any>(null),[msg,setMsg]=useState(''),[busy,setBusy]=useState(false);
 useEffect(()=>{getJson<any>('/access/catalog').then(c=>{setCatalog(c);if(c.users?.length)setUserId(c.users[0].id)}).catch(e=>setMsg(String(e)))},[]);
 useEffect(()=>{if(userId)getJson<any>(`/access/users/${userId}`).then(setAccess).catch(e=>setMsg(String(e)))},[userId]);
 const user=useMemo(()=>catalog?.users?.find((x:any)=>x.id===userId),[catalog,userId]);
 if(!catalog||!access)return <div className="panel" style={{padding:20}}>{msg||'Loading permissions…'}</div>;
 const has=(m:string,a:string)=>(access.permissions?.[m]||[]).includes(a);
 const toggle=(m:string,a:string)=>{const x=new Set<string>(access.permissions?.[m]||[]);x.has(a)?x.delete(a):x.add(a);if(a==='FULL'&&x.has('FULL'))catalog.actions.forEach((v:string)=>x.add(v));setAccess({...access,permissions:{...access.permissions,[m]:Array.from(x)}})};
 const setScope=(m:string,mode:string)=>setAccess({...access,scopes:{...access.scopes,[m]:{mode,values:[]}}});
 const toggleValue=(module:string,value:string)=>{
   const current:Scope=access.scopes?.[module] ?? {mode:'OWN',values:[]};
   const values=new Set<string>(current.values ?? []);
   if(values.has(value)) values.delete(value);
   else values.add(value);

   setAccess({
     ...access,
     scopes:{
       ...access.scopes,
       [module]:{
         ...current,
         values:Array.from(values)
       }
     }
   });
 };
 const resetPassword=async()=>{
   if(!userId)return;
   if(!window.confirm(`Reset password for ${user?.name||'this user'} to Bitexco@123?`))return;
   setBusy(true);setMsg('');
   try{
     const r=await fetch(`/api/access/users/${userId}/reset-password`,{method:'POST',credentials:'include',headers:{'Content-Type':'application/json'},body:JSON.stringify({temporaryPassword:'Bitexco@123'})});
     const data=await r.json().catch(()=>({}));
     if(!r.ok)throw new Error(data?.message||`HTTP ${r.status}`);
     setMsg(`Password reset: ${data.email} / ${data.temporaryPassword}. User must change password at first login.`);
   }catch(e:any){setMsg(e?.message||String(e))}finally{setBusy(false)}
 };

 const initializeMissing=async()=>{
   if(!window.confirm('Create login accounts for all users missing AuthAccounts? Temporary password: Bitexco@123'))return;
   setBusy(true);setMsg('');
   try{
     const r=await fetch('/api/access/accounts/initialize-missing',{method:'POST',credentials:'include'});
     const data=await r.json().catch(()=>({}));
     if(!r.ok)throw new Error(data?.message||`HTTP ${r.status}`);
     setMsg(`Created ${data.created} missing account(s). Temporary password: ${data.temporaryPassword}.`);
   }catch(e:any){setMsg(e?.message||String(e))}finally{setBusy(false)}
 };

 const save=async()=>{setBusy(true);setMsg('');try{setAccess(await putJson<any>(`/access/users/${userId}`,{permissions:access.permissions,scopes:access.scopes}));setMsg('Permissions saved.')}catch(e){setMsg(String(e))}finally{setBusy(false)}};
 return <><div className="page-title"><div><h1>Access Control</h1><p>Functional permission + data scope by user.</p></div><div style={{display:'flex',gap:8,flexWrap:'wrap'}}><button type="button" disabled={busy||!userId} onClick={resetPassword}>Reset password</button><button type="button" disabled={busy} onClick={initializeMissing}>Initialize missing accounts</button><button className="budget-primary" disabled={busy} onClick={save}>{busy?'Saving…':'Save permissions'}</button></div></div>{msg&&<div className="budget-error">{msg}</div>}
 <div className="access-userbar"><label>User<select value={userId} onChange={e=>setUserId(Number(e.target.value))}>{catalog.users.map((u:any)=><option key={u.id} value={u.id}>{u.name} — {u.email}</option>)}</select></label><div><b>{user?.name}</b><span>{user?.role} · {user?.orgUnit||user?.department||'—'}</span></div></div>
 <div className="access-grid"><div className="access-perm panel"><table><thead><tr><th>Module</th>{catalog.actions.map((a:string)=><th key={a}>{a}</th>)}</tr></thead><tbody>{catalog.modules.map((m:string)=><tr key={m}><td><b>{m.replaceAll('_',' ')}</b></td>{catalog.actions.map((a:string)=><td key={a}><input type="checkbox" checked={has(m,a)} onChange={()=>toggle(m,a)}/></td>)}</tr>)}</tbody></table></div>
 <div className="access-scopes panel"><h3>Data Scope — Budget & KPI</h3>{['BUDGET','KPI'].map(m=>{const s:Scope=access.scopes?.[m]||{mode:'OWN',values:[]};return <div className="scope-card" key={m}><b>{m}</b><select value={s.mode} onChange={e=>setScope(m,e.target.value)}>{catalog.scopeModes.map((x:string)=><option key={x}>{x}</option>)}</select>{s.mode==='SELECTED_ORGS'&&<div className="scope-values">{catalog.orgUnits.map((o:any)=><label key={o.id}><input type="checkbox" checked={(s.values||[]).includes(o.code)} onChange={()=>toggleValue(m,o.code)}/>{o.code} — {o.name}</label>)}</div>}{s.mode==='SELECTED_USERS'&&<div className="scope-values">{catalog.users.filter((u:any)=>u.id!==userId).map((u:any)=><label key={u.id}><input type="checkbox" checked={(s.values||[]).includes(String(u.id))} onChange={()=>toggleValue(m,String(u.id))}/>{u.name}</label>)}</div>}</div>})}</div></div></>;
}
