import {useEffect,useState} from 'react';
import {BriefcaseBusiness,Building2,UsersRound,Plus,Edit3,Trash2,X} from 'lucide-react';
import {getJson,postJson,putJson,deleteJson} from '../lib/api';
import { TeamManagementPage } from './TeamManagementPage';
type Tab='portfolios'|'orgUnits'|'members'|'projectTeam';
export function SettingsPage(){
 const[tab,setTab]=useState<Tab>('portfolios'),[rows,setRows]=useState<any[]>([]),[opts,setOpts]=useState<any>({orgUnits:[],users:[]}),[open,setOpen]=useState(false),[edit,setEdit]=useState<any>(null),[error,setError]=useState('');
 const path=tab==='portfolios'
   ?'portfolios'
   :tab==='orgUnits'
   ?'org-units'
   :'team-members';
 const load=async()=>{setRows(await getJson<any[]>('/settings/'+path));setOpts(await getJson<any>('/settings/options'))};
 useEffect(()=>{
   if(tab!=='projectTeam')
     load().catch(e=>setError(String(e)));
 },[tab]);
 const fresh=()=>tab==='portfolios'?{orgUnitId:opts.orgUnits[0]?.id||'',code:'',name:'',description:'',ownerId:null,status:'ACTIVE'}:tab==='orgUnits'?{code:'',name:'',description:'',status:'ACTIVE'}:{orgUnitId:opts.orgUnits[0]?.id||'',name:'',email:'',jobTitle:'',department:'',role:'MEMBER',status:'ACTIVE'};
 const start=(x?:any)=>{setEdit(x?{...x}:fresh());setOpen(true)};
 const save=async(e:any)=>{e.preventDefault();try{edit.id?await putJson(`/settings/${path}/${edit.id}`,edit):await postJson(`/settings/${path}`,edit);setOpen(false);await load()}catch(e){setError(String(e))}};
 const del=async(x:any)=>{if(!confirm(`Delete "${x.name}"?`))return;try{await deleteJson(`/settings/${path}/${x.id}`);await load()}catch(e){setError(String(e))}};
 return <><div className="page-title"><div><h1>Settings</h1><p>Portfolio, Business Unit and Team Member master data</p></div><button className="budget-primary" onClick={()=>start()}><Plus size={16}/> New</button></div>
 {error&&<div className="budget-error">{error}</div>}
 <div className="settings-tabs"><button className={tab==='portfolios'?'active':''} onClick={()=>setTab('portfolios')}><BriefcaseBusiness/>Portfolios</button><button className={tab==='orgUnits'?'active':''} onClick={()=>setTab('orgUnits')}><Building2/>Business Units</button><button className={tab==='members'?'active':''} onClick={()=>setTab('members')}><UsersRound/>Team Members</button>
 <button className={tab==='projectTeam'?'active':''} onClick={()=>setTab('projectTeam')}><UsersRound/>Project Team</button>
 </div>
 {tab==='projectTeam'
 ? <div className="settings-project-team"><TeamManagementPage/></div>
 : <div className="settings-panel"><table className="settings-table"><thead><tr>{tab!=='members'&&<th>Code</th>}<th>Name</th>{tab==='members'&&<th>Email</th>}<th>{tab==='portfolios'?'Business Unit':tab==='members'?'Department':'Description'}</th><th>Status</th><th>Actions</th></tr></thead><tbody>{rows.map(x=><tr key={x.id}>{tab!=='members'&&<td><b>{x.code}</b></td>}<td><b>{x.name}</b></td>{tab==='members'&&<td>{x.email}</td>}<td>{tab==='portfolios'?x.orgUnit:tab==='members'?x.department:x.description}</td><td>{x.status}</td><td><button onClick={()=>start(x)}><Edit3 size={15}/></button>{tab!=='members'&&<button className="danger" onClick={()=>del(x)}><Trash2 size={15}/></button>}</td></tr>)}</tbody></table></div>}
 {open&&tab!=='projectTeam'&&<div className="budget-modal-backdrop"><div className="budget-modal settings-modal"><div className="budget-modal-head"><h3>{edit.id?'Edit':'New'} {tab==='portfolios'?'Portfolio':tab==='orgUnits'?'Business Unit':'Team Member'}</h3><button onClick={()=>setOpen(false)}><X/></button></div><form onSubmit={save}><div className="budget-form-grid">
 {tab!=='members'&&<label>Code<input required value={edit.code||''} onChange={e=>setEdit({...edit,code:e.target.value})}/></label>}
 {tab!=='orgUnits'&&<label>Business Unit<select required value={edit.orgUnitId||''} onChange={e=>setEdit({...edit,orgUnitId:Number(e.target.value)})}>{opts.orgUnits.map((o:any)=><option key={o.id} value={o.id}>{o.code} — {o.name}</option>)}</select></label>}
 <label>Name<input required value={edit.name||''} onChange={e=>setEdit({...edit,name:e.target.value})}/></label>
 {tab==='members'&&<><label>Email<input required type="email" value={edit.email||''} onChange={e=>setEdit({...edit,email:e.target.value})}/></label><label>Job Title<input value={edit.jobTitle||''} onChange={e=>setEdit({...edit,jobTitle:e.target.value})}/></label><label>Department<input value={edit.department||''} onChange={e=>setEdit({...edit,department:e.target.value})}/></label><label>Role<select value={edit.role||'MEMBER'} onChange={e=>setEdit({...edit,role:e.target.value})}><option>MEMBER</option><option>PM</option><option>PROJECT_MANAGER</option><option>HOD</option><option>CIO</option><option>ADMIN</option></select></label></>}
 {tab==='portfolios'&&<label>Owner<select value={edit.ownerId||''} onChange={e=>setEdit({...edit,ownerId:e.target.value?Number(e.target.value):null})}><option value="">No Owner</option>{opts.users.map((u:any)=><option key={u.id} value={u.id}>{u.name}</option>)}</select></label>}
 <label>Status<select value={edit.status||'ACTIVE'} onChange={e=>setEdit({...edit,status:e.target.value})}><option>ACTIVE</option><option>INACTIVE</option></select></label>
 </div>{tab!=='members'&&<label>Description<textarea rows={4} value={edit.description||''} onChange={e=>setEdit({...edit,description:e.target.value})}/></label>}<div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setOpen(false)}>Cancel</button><button className="budget-primary">Save</button></div></form></div></div>}</>;
}
