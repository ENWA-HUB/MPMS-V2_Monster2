import {useEffect,useState} from 'react';
import {createRoot} from 'react-dom/client';
import {X} from 'lucide-react';
import {getJson,putJson} from '../lib/api';

import {SupplierProfileFiles} from './SupplierProfileFiles';

import FormattedNumberInput from "../components/FormattedNumberInput";
type Done=()=>void|Promise<void>;
type Project={id:number;code:string;name:string};
type User={id:number;name:string};
type Milestone={id:number;projectId:number;name:string};
type Task={id:number;projectId:number;code:string;name:string};
type Risk={id:number;projectId:number;code:string;title:string};
type Org={id:number;code:string;name:string};
const v=(x:any)=>x==null?'':String(x);
const n=(x:any)=>x===''||x==null?0:Number(x);
const nn=(x:any)=>x===''||x==null?null:Number(x);

function host(render:(close:()=>void)=>React.ReactNode){
 const el=document.createElement('div'); document.body.appendChild(el); const root=createRoot(el);
 const close=()=>{root.unmount();el.remove()}; root.render(<>{render(close)}</>);
}
function Modal({title,subtitle,children,onClose,onSave,busy}:{title:string;subtitle:string;children:React.ReactNode;onClose:()=>void;onSave:()=>void;busy:boolean}){
 return <div className="budget-modal-backdrop" onMouseDown={onClose}><div className="budget-modal" style={{maxWidth:920}} onMouseDown={e=>e.stopPropagation()}>
  <div className="budget-modal-head"><div><h3>{title}</h3><p>{subtitle}</p></div><button type="button" onClick={onClose}><X/></button></div>
  <form onSubmit={e=>{e.preventDefault();onSave()}}>{children}<div className="budget-modal-actions"><button type="button" className="secondary" onClick={onClose}>Cancel</button><button className="budget-primary" disabled={busy}>{busy?'Saving…':'Save changes'}</button></div></form>
 </div></div>
}

export function openTaskEditor(row:any,onDone:Done,onError?:(e:any)=>void){
  getJson<any>(`/tasks/${row.id}`).then(full=>host(close=><TaskEditor row={full} close={close} done={onDone} fail={onError}/>)).catch(e=>{onError?.(e);alert(String(e))});
}
function TaskEditor({row,close,done,fail}:{row:any;close:()=>void;done:Done;fail?:(e:any)=>void}){
 const [projects,setProjects]=useState<Project[]>([]),[users,setUsers]=useState<User[]>([]),[milestones,setMilestones]=useState<Milestone[]>([]),[tasks,setTasks]=useState<Task[]>([]),[busy,setBusy]=useState(false);
 const [f,setF]=useState({projectId:v(row.projectId),parentTaskId:v(row.parentTaskId),milestoneId:v(row.milestoneId),code:row.code||'',name:row.name||'',description:row.description||'',assigneeId:v(row.assigneeId),status:row.status||'TODO',priority:row.priority||'MEDIUM',startDate:row.startDate||'',dueDate:row.dueDate||'',completedDate:row.completedDate||'',progressPct:v(row.progressPct??0),estimatedHours:v(row.estimatedHours??0),actualHours:v(row.actualHours??0),sortOrder:v(row.sortOrder??0)});
 useEffect(()=>{Promise.all([getJson<Project[]>('/projects'),getJson<User[]>('/users'),getJson<Milestone[]>('/milestones'),getJson<Task[]>('/tasks')]).then(([p,u,m,t])=>{setProjects(p);setUsers(u);setMilestones(m);setTasks(t)}).catch(fail)},[]);
 const save=async()=>{setBusy(true);try{await putJson(`/tasks/${row.id}`,{projectId:n(f.projectId),parentTaskId:nn(f.parentTaskId),milestoneId:nn(f.milestoneId),code:f.code,name:f.name,description:f.description,assigneeId:nn(f.assigneeId),status:f.status,priority:f.priority,startDate:f.startDate||null,dueDate:f.dueDate||null,completedDate:f.completedDate||null,progressPct:n(f.progressPct),estimatedHours:n(f.estimatedHours),actualHours:n(f.actualHours),sortOrder:n(f.sortOrder)});await done();close()}catch(e){fail?.(e);alert(String(e))}finally{setBusy(false)}};
 const pid=n(f.projectId);
 return <Modal title="Edit Task" subtitle="Edit assignment, hierarchy, schedule, effort and progress." onClose={close} onSave={save} busy={busy}>
  <div className="budget-form-grid">
   <label>Project<select required value={f.projectId} onChange={e=>setF({...f,projectId:e.target.value})}><option value="">Select project</option>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label>
   <label>Code<input required value={f.code} onChange={e=>setF({...f,code:e.target.value})}/></label>
   <label>Parent Task<select value={f.parentTaskId} onChange={e=>setF({...f,parentTaskId:e.target.value})}><option value="">None</option>{tasks.filter(t=>t.id!==row.id&&(!pid||t.projectId===pid)).map(t=><option key={t.id} value={t.id}>{t.code} — {t.name}</option>)}</select></label>
   <label>Milestone<select value={f.milestoneId} onChange={e=>setF({...f,milestoneId:e.target.value})}><option value="">None</option>{milestones.filter(m=>!pid||m.projectId===pid).map(m=><option key={m.id} value={m.id}>{m.name}</option>)}</select></label>
   <label>Assignee<select value={f.assigneeId} onChange={e=>setF({...f,assigneeId:e.target.value})}><option value="">Unassigned</option>{users.map(u=><option key={u.id} value={u.id}>{u.name}</option>)}</select></label>
   <label>Priority<select value={f.priority} onChange={e=>setF({...f,priority:e.target.value})}><option>LOW</option><option>MEDIUM</option><option>HIGH</option><option>CRITICAL</option></select></label>
   <label>Status<select value={f.status} onChange={e=>setF({...f,status:e.target.value})}><option>TODO</option><option>IN_PROGRESS</option><option>BLOCKED</option><option>REVIEW</option><option>DONE</option><option>CANCELLED</option></select></label>
   <label>Progress %<input type="number" min="0" max="100" value={f.progressPct} onChange={e=>setF({...f,progressPct:e.target.value})}/></label>
   <label>Start Date<input type="date" value={f.startDate} onChange={e=>setF({...f,startDate:e.target.value})}/></label>
   <label>Due Date<input type="date" value={f.dueDate} onChange={e=>setF({...f,dueDate:e.target.value})}/></label>
   <label>Completed Date<input type="date" value={f.completedDate} onChange={e=>setF({...f,completedDate:e.target.value})}/></label>
   <label>Sort Order<input type="number" value={f.sortOrder} onChange={e=>setF({...f,sortOrder:e.target.value})}/></label>
   <label>Estimated Hours<input type="number" min="0" step="0.25" value={f.estimatedHours} onChange={e=>setF({...f,estimatedHours:e.target.value})}/></label>
   <label>Actual Hours<input type="number" min="0" step="0.25" value={f.actualHours} onChange={e=>setF({...f,actualHours:e.target.value})}/></label>
  </div>
  <label>Task Name<input required value={f.name} onChange={e=>setF({...f,name:e.target.value})}/></label>
  <label>Description<textarea rows={4} value={f.description} onChange={e=>setF({...f,description:e.target.value})}/></label>
 </Modal>
}

export function openMilestoneEditor(row:any,onDone:Done,onError?:(e:any)=>void){
  getJson<any>(`/milestones/${row.id}`).then(full=>host(close=><MilestoneEditor row={full} close={close} done={onDone} fail={onError}/>)).catch(e=>{onError?.(e);alert(String(e))});
}
function MilestoneEditor({row,close,done,fail}:{row:any;close:()=>void;done:Done;fail?:(e:any)=>void}){
 const [projects,setProjects]=useState<Project[]>([]),[busy,setBusy]=useState(false);
 const [f,setF]=useState({projectId:v(row.projectId),name:row.name||'',description:row.description||'',dueDate:row.dueDate||'',actualDate:row.actualDate||'',status:row.status||'OPEN',weightPct:v(row.weightPct??0)});
 useEffect(()=>{getJson<Project[]>('/projects').then(setProjects).catch(fail)},[]);
 const save=async()=>{setBusy(true);try{await putJson(`/milestones/${row.id}`,{projectId:n(f.projectId),name:f.name,description:f.description,dueDate:f.dueDate||null,actualDate:f.actualDate||null,status:f.status,weightPct:n(f.weightPct)});await done();close()}catch(e){fail?.(e);alert(String(e))}finally{setBusy(false)}};
 return <Modal title="Edit Milestone" subtitle="Edit complete milestone information." onClose={close} onSave={save} busy={busy}>
  <div className="budget-form-grid"><label>Project<select required value={f.projectId} onChange={e=>setF({...f,projectId:e.target.value})}>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label><label>Status<select value={f.status} onChange={e=>setF({...f,status:e.target.value})}><option>OPEN</option><option>IN_PROGRESS</option><option>COMPLETED</option><option>DELAYED</option></select></label><label>Due Date<input type="date" value={f.dueDate} onChange={e=>setF({...f,dueDate:e.target.value})}/></label><label>Actual Date<input type="date" value={f.actualDate} onChange={e=>setF({...f,actualDate:e.target.value})}/></label><label>Weight %<input type="number" min="0" max="100" step="0.1" value={f.weightPct} onChange={e=>setF({...f,weightPct:e.target.value})}/></label></div>
  <label>Name<input required value={f.name} onChange={e=>setF({...f,name:e.target.value})}/></label><label>Description<textarea rows={4} value={f.description} onChange={e=>setF({...f,description:e.target.value})}/></label>
 </Modal>
}

export function openRiskEditor(row:any,onDone:Done,onError?:(e:any)=>void){
  getJson<any>(`/risks/${row.id}`).then(full=>host(close=><RiskEditor row={full} close={close} done={onDone} fail={onError}/>)).catch(e=>{onError?.(e);alert(String(e))});
}
function RiskEditor({row,close,done,fail}:{row:any;close:()=>void;done:Done;fail?:(e:any)=>void}){
 const [projects,setProjects]=useState<Project[]>([]),[users,setUsers]=useState<User[]>([]),[busy,setBusy]=useState(false);
 const [f,setF]=useState({projectId:v(row.projectId),code:row.code||'',category:row.category||'GENERAL',title:row.title||'',description:row.description||'',probability:v(row.probability??1),impact:v(row.impact??1),ownerId:v(row.ownerId),responseStrategy:row.responseStrategy||'MITIGATE',mitigationPlan:row.mitigationPlan||'',contingencyPlan:row.contingencyPlan||'',targetDate:row.targetDate||'',status:row.status||'OPEN'});
 useEffect(()=>{Promise.all([getJson<Project[]>('/projects'),getJson<User[]>('/users')]).then(([p,u])=>{setProjects(p);setUsers(u)}).catch(fail)},[]);
 const save=async()=>{setBusy(true);try{await putJson(`/risks/${row.id}`,{projectId:n(f.projectId),code:f.code,category:f.category,title:f.title,description:f.description,probability:n(f.probability),impact:n(f.impact),ownerId:nn(f.ownerId),responseStrategy:f.responseStrategy,mitigationPlan:f.mitigationPlan,contingencyPlan:f.contingencyPlan,targetDate:f.targetDate||null,status:f.status});await done();close()}catch(e){fail?.(e);alert(String(e))}finally{setBusy(false)}};
 return <Modal title="Edit Risk" subtitle="Edit assessment, owner and response plans." onClose={close} onSave={save} busy={busy}>
  <div className="budget-form-grid">
   <label>Project<select required value={f.projectId} onChange={e=>setF({...f,projectId:e.target.value})}>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label><label>Code<input required value={f.code} onChange={e=>setF({...f,code:e.target.value})}/></label><label>Category<input value={f.category} onChange={e=>setF({...f,category:e.target.value})}/></label><label>Owner<select value={f.ownerId} onChange={e=>setF({...f,ownerId:e.target.value})}><option value="">Unassigned</option>{users.map(u=><option key={u.id} value={u.id}>{u.name}</option>)}</select></label><label>Probability<input type="number" min="1" max="5" value={f.probability} onChange={e=>setF({...f,probability:e.target.value})}/></label><label>Impact<input type="number" min="1" max="5" value={f.impact} onChange={e=>setF({...f,impact:e.target.value})}/></label><label>Severity Score<input disabled value={n(f.probability)*n(f.impact)}/></label><label>Status<select value={f.status} onChange={e=>setF({...f,status:e.target.value})}><option>OPEN</option><option>MONITORING</option><option>MITIGATING</option><option>CLOSED</option></select></label><label>Response Strategy<select value={f.responseStrategy} onChange={e=>setF({...f,responseStrategy:e.target.value})}><option>AVOID</option><option>MITIGATE</option><option>TRANSFER</option><option>ACCEPT</option></select></label><label>Target Date<input type="date" value={f.targetDate} onChange={e=>setF({...f,targetDate:e.target.value})}/></label>
  </div>
  <label>Title<input required value={f.title} onChange={e=>setF({...f,title:e.target.value})}/></label><label>Description<textarea rows={3} value={f.description} onChange={e=>setF({...f,description:e.target.value})}/></label><label>Mitigation Plan<textarea rows={3} value={f.mitigationPlan} onChange={e=>setF({...f,mitigationPlan:e.target.value})}/></label><label>Contingency Plan<textarea rows={3} value={f.contingencyPlan} onChange={e=>setF({...f,contingencyPlan:e.target.value})}/></label>
 </Modal>
}

export function openIssueEditor(row:any,onDone:Done,onError?:(e:any)=>void){
  getJson<any>(`/issues/${row.id}`).then(full=>host(close=><IssueEditor row={full} close={close} done={onDone} fail={onError}/>)).catch(e=>{onError?.(e);alert(String(e))});
}
function IssueEditor({row,close,done,fail}:{row:any;close:()=>void;done:Done;fail?:(e:any)=>void}){
 const [projects,setProjects]=useState<Project[]>([]),[users,setUsers]=useState<User[]>([]),[risks,setRisks]=useState<Risk[]>([]),[busy,setBusy]=useState(false);
 const [f,setF]=useState({projectId:v(row.projectId),riskId:v(row.riskId),code:row.code||'',title:row.title||'',description:row.description||'',category:row.category||'GENERAL',severity:row.severity||'MEDIUM',status:row.status||'OPEN',reportedBy:v(row.reportedBy),assignedTo:v(row.assignedTo),reportedDate:row.reportedDate||'',targetResolutionDate:row.targetResolutionDate||'',resolvedDate:row.resolvedDate||'',resolution:row.resolution||''});
 useEffect(()=>{Promise.all([getJson<Project[]>('/projects'),getJson<User[]>('/users'),getJson<Risk[]>('/risks')]).then(([p,u,r])=>{setProjects(p);setUsers(u);setRisks(r)}).catch(fail)},[]);
 const pid=n(f.projectId); const save=async()=>{setBusy(true);try{await putJson(`/issues/${row.id}`,{projectId:n(f.projectId),riskId:nn(f.riskId),code:f.code,title:f.title,description:f.description,category:f.category,severity:f.severity,status:f.status,reportedBy:nn(f.reportedBy),assignedTo:nn(f.assignedTo),reportedDate:f.reportedDate||null,targetResolutionDate:f.targetResolutionDate||null,resolvedDate:f.resolvedDate||null,resolution:f.resolution});await done();close()}catch(e){fail?.(e);alert(String(e))}finally{setBusy(false)}};
 return <Modal title="Edit Issue" subtitle="Edit linkage, ownership, dates and resolution." onClose={close} onSave={save} busy={busy}>
  <div className="budget-form-grid"><label>Project<select required value={f.projectId} onChange={e=>setF({...f,projectId:e.target.value})}>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label><label>Linked Risk<select value={f.riskId} onChange={e=>setF({...f,riskId:e.target.value})}><option value="">None</option>{risks.filter(r=>!pid||r.projectId===pid).map(r=><option key={r.id} value={r.id}>{r.code} — {r.title}</option>)}</select></label><label>Code<input required value={f.code} onChange={e=>setF({...f,code:e.target.value})}/></label><label>Category<input value={f.category} onChange={e=>setF({...f,category:e.target.value})}/></label><label>Severity<select value={f.severity} onChange={e=>setF({...f,severity:e.target.value})}><option>LOW</option><option>MEDIUM</option><option>HIGH</option><option>CRITICAL</option></select></label><label>Status<select value={f.status} onChange={e=>setF({...f,status:e.target.value})}><option>OPEN</option><option>INVESTIGATING</option><option>ACTION_REQUIRED</option><option>RESOLVED</option><option>CLOSED</option></select></label><label>Reported By<select value={f.reportedBy} onChange={e=>setF({...f,reportedBy:e.target.value})}><option value="">Unassigned</option>{users.map(u=><option key={u.id} value={u.id}>{u.name}</option>)}</select></label><label>Assigned To<select value={f.assignedTo} onChange={e=>setF({...f,assignedTo:e.target.value})}><option value="">Unassigned</option>{users.map(u=><option key={u.id} value={u.id}>{u.name}</option>)}</select></label><label>Reported Date<input type="date" value={f.reportedDate} onChange={e=>setF({...f,reportedDate:e.target.value})}/></label><label>Target Resolution<input type="date" value={f.targetResolutionDate} onChange={e=>setF({...f,targetResolutionDate:e.target.value})}/></label><label>Resolved Date<input type="date" value={f.resolvedDate} onChange={e=>setF({...f,resolvedDate:e.target.value})}/></label></div>
  <label>Title<input required value={f.title} onChange={e=>setF({...f,title:e.target.value})}/></label><label>Description<textarea rows={3} value={f.description} onChange={e=>setF({...f,description:e.target.value})}/></label><label>Resolution<textarea rows={3} value={f.resolution} onChange={e=>setF({...f,resolution:e.target.value})}/></label>
 </Modal>
}

export function openBudgetEditor(row:any,onDone:Done,onError?:(e:any)=>void){
  getJson<any>(`/budgets/${row.id}`).then(full=>host(close=><BudgetEditor row={full} close={close} done={onDone} fail={onError}/>)).catch(e=>{onError?.(e);alert(String(e))});
}
function BudgetEditor({row,close,done,fail}:{row:any;close:()=>void;done:Done;fail?:(e:any)=>void}){
 const [projects,setProjects]=useState<Project[]>([]),[busy,setBusy]=useState(false);
 const [f,setF]=useState({projectId:v(row.projectId),code:row.code||'',category:row.category||'',description:row.description||'',baselineAmount:v(row.baselineAmount??0),revisedAmount:v(row.totalBudget??0),committedAmount:v(row.committed??0),actualAmount:v(row.spent??0),forecastAmount:v(row.forecast??0),currency:row.currency||'VND'});
 useEffect(()=>{getJson<Project[]>('/projects').then(setProjects).catch(fail)},[]);
 const save=async()=>{setBusy(true);try{await putJson(`/budgets/${row.id}`,{projectId:n(f.projectId),code:f.code,category:f.category,description:f.description,baselineAmount:n(f.baselineAmount),revisedAmount:n(f.revisedAmount),committedAmount:n(f.committedAmount),actualAmount:n(f.actualAmount),forecastAmount:n(f.forecastAmount),currency:f.currency});await done();close()}catch(e){fail?.(e);alert(String(e))}finally{setBusy(false)}};
 return <Modal title="Edit Budget Control" subtitle="Edit complete budget line information." onClose={close} onSave={save} busy={busy}>
  <div className="budget-form-grid"><label>Project<select required value={f.projectId} onChange={e=>setF({...f,projectId:e.target.value})}>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label><label>Code<input required value={f.code} onChange={e=>setF({...f,code:e.target.value})}/></label><label>Category<input value={f.category} onChange={e=>setF({...f,category:e.target.value})}/></label><label>Currency<select value={f.currency} onChange={e=>setF({...f,currency:e.target.value})}><option>VND</option><option>USD</option><option>EUR</option></select></label><label>Baseline Amount<FormattedNumberInput value={f.baselineAmount} decimals={(f.currency||"VND").toUpperCase()==="VND"?0:2} onValueChange={v=>setF({...f,baselineAmount:v})}/></label><label>Revised Amount<FormattedNumberInput value={f.revisedAmount} decimals={(f.currency||"VND").toUpperCase()==="VND"?0:2} onValueChange={v=>setF({...f,revisedAmount:v})}/></label><label>Committed Amount<FormattedNumberInput value={f.committedAmount} decimals={(f.currency||"VND").toUpperCase()==="VND"?0:2} onValueChange={v=>setF({...f,committedAmount:v})}/></label><label>Actual Amount<FormattedNumberInput value={f.actualAmount} decimals={(f.currency||"VND").toUpperCase()==="VND"?0:2} onValueChange={v=>setF({...f,actualAmount:v})}/></label><label>Forecast Amount<FormattedNumberInput value={f.forecastAmount} decimals={(f.currency||"VND").toUpperCase()==="VND"?0:2} onValueChange={v=>setF({...f,forecastAmount:v})}/></label></div><label>Description<textarea rows={4} value={f.description} onChange={e=>setF({...f,description:e.target.value})}/></label>
 </Modal>
}

export function openSupplierEditor(row:any,onDone:Done,onError?:(e:any)=>void){
  getJson<any>(`/suppliers/${row.id}`).then(full=>host(close=><SupplierEditor row={full} close={close} done={onDone} fail={onError}/>)).catch(e=>{onError?.(e);alert(String(e))});
}
function SupplierEditor({row,close,done,fail}:{row:any;close:()=>void;done:Done;fail?:(e:any)=>void}){
 const [orgs,setOrgs]=useState<Org[]>([]),[busy,setBusy]=useState(false);
 const [f,setF]=useState({orgUnitId:v(row.orgUnitId||1),code:row.code||'',name:row.name||'',taxCode:row.taxCode||'',category:row.category||'',contactName:row.contactName||'',email:row.email||'',phone:row.phone||'',address:row.address||'',status:row.status||'ACTIVE',rating:v(row.rating??0),companyName:row.companyName||'',shortName:row.shortName||'',website:row.website||'',businessRegistrationNo:row.businessRegistrationNo||'',legalRepresentative:row.legalRepresentative||'',registrationDate:row.registrationDate?String(row.registrationDate).slice(0,10):'',country:row.country||'Vietnam',contactPosition:row.contactPosition||'',alternativePhone:row.alternativePhone||'',provinceCity:row.provinceCity||'',paymentTerms:row.paymentTerms||'',currency:row.currency||'VND',bankName:row.bankName||'',bankAccountNo:row.bankAccountNo||'',bankAccountName:row.bankAccountName||'',bankBranch:row.bankBranch||'',internalOwnerId:v(row.internalOwnerId||''),notes:row.notes||''});
 useEffect(()=>{getJson<any>('/projects/form-options').then(x=>setOrgs(x.orgUnits||[])).catch(fail)},[]);
 const save=async()=>{setBusy(true);try{await putJson(`/suppliers/${row.id}`,{orgUnitId:n(f.orgUnitId),code:f.code,name:f.name,taxCode:f.taxCode,category:f.category,contactName:f.contactName,email:f.email,phone:f.phone,address:f.address,status:f.status,rating:n(f.rating),companyName:f.companyName,shortName:f.shortName,website:f.website,businessRegistrationNo:f.businessRegistrationNo,legalRepresentative:f.legalRepresentative,registrationDate:f.registrationDate||null,country:f.country,contactPosition:f.contactPosition,alternativePhone:f.alternativePhone,provinceCity:f.provinceCity,paymentTerms:f.paymentTerms,currency:f.currency,bankName:f.bankName,bankAccountNo:f.bankAccountNo,bankAccountName:f.bankAccountName,bankBranch:f.bankBranch,internalOwnerId:f.internalOwnerId?n(f.internalOwnerId):null,notes:f.notes});await done();close()}catch(e){fail?.(e);alert(String(e))}finally{setBusy(false)}};
 return <Modal title="Edit Active Supplier" subtitle="Edit complete supplier master data." onClose={close} onSave={save} busy={busy}>
  <div className="budget-form-grid"><label>Business Unit<select required value={f.orgUnitId} onChange={e=>setF({...f,orgUnitId:e.target.value})}>{orgs.map(o=><option key={o.id} value={o.id}>{o.code} — {o.name}</option>)}</select></label><label>Supplier Code<input required value={f.code} onChange={e=>setF({...f,code:e.target.value})}/></label><label>Supplier Name<input required value={f.name} onChange={e=>setF({...f,name:e.target.value})}/></label><label>Tax Code<input value={f.taxCode} onChange={e=>setF({...f,taxCode:e.target.value})}/></label><label>Category<input value={f.category} onChange={e=>setF({...f,category:e.target.value})}/></label><label>Contact Name<input value={f.contactName} onChange={e=>setF({...f,contactName:e.target.value})}/></label><label>Email<input type="email" value={f.email} onChange={e=>setF({...f,email:e.target.value})}/></label><label>Phone<input value={f.phone} onChange={e=>setF({...f,phone:e.target.value})}/></label><label>Status<select value={f.status} onChange={e=>setF({...f,status:e.target.value})}><option>ACTIVE</option><option>INACTIVE</option><option>SUSPENDED</option></select></label><label>Rating<input type="number" min="0" max="5" step="0.1" value={f.rating} onChange={e=>setF({...f,rating:e.target.value})}/></label></div><label>Address<textarea rows={3} value={f.address} onChange={e=>setF({...f,address:e.target.value})}/></label>
 <div className="supplier-profile-sections">
<h4>Company Information</h4><div className="budget-form-grid">
<label>Company / Legal Name<input value={f.companyName} onChange={e=>setF({...f,companyName:e.target.value})}/></label>
<label>Short Name<input value={f.shortName} onChange={e=>setF({...f,shortName:e.target.value})}/></label>
<label>Website<input value={f.website} onChange={e=>setF({...f,website:e.target.value})}/></label>
<label>Business Registration No.<input value={f.businessRegistrationNo} onChange={e=>setF({...f,businessRegistrationNo:e.target.value})}/></label>
<label>Legal Representative<input value={f.legalRepresentative} onChange={e=>setF({...f,legalRepresentative:e.target.value})}/></label>
<label>Registration Date<input type="date" value={f.registrationDate} onChange={e=>setF({...f,registrationDate:e.target.value})}/></label>
<label>Country<input value={f.country} onChange={e=>setF({...f,country:e.target.value})}/></label>
<label>Province / City<input value={f.provinceCity} onChange={e=>setF({...f,provinceCity:e.target.value})}/></label>
</div><h4>Contact Information</h4><div className="budget-form-grid">
<label>Contact Position<input value={f.contactPosition} onChange={e=>setF({...f,contactPosition:e.target.value})}/></label>
<label>Alternative Phone<input value={f.alternativePhone} onChange={e=>setF({...f,alternativePhone:e.target.value})}/></label>
</div><h4>Commercial & Banking</h4><div className="budget-form-grid">
<label>Payment Terms<input value={f.paymentTerms} onChange={e=>setF({...f,paymentTerms:e.target.value})}/></label>
<label>Currency<select value={f.currency} onChange={e=>setF({...f,currency:e.target.value})}><option>VND</option><option>USD</option><option>EUR</option></select></label>
<label>Bank Name<input value={f.bankName} onChange={e=>setF({...f,bankName:e.target.value})}/></label>
<label>Bank Account No.<input value={f.bankAccountNo} onChange={e=>setF({...f,bankAccountNo:e.target.value})}/></label>
<label>Bank Account Name<input value={f.bankAccountName} onChange={e=>setF({...f,bankAccountName:e.target.value})}/></label>
<label>Bank Branch<input value={f.bankBranch} onChange={e=>setF({...f,bankBranch:e.target.value})}/></label>
<label className="supplier-span-2">Notes<textarea rows={3} value={f.notes} onChange={e=>setF({...f,notes:e.target.value})}/></label>
</div></div><SupplierProfileFiles supplierId={row.id}/>
 </Modal>
}
