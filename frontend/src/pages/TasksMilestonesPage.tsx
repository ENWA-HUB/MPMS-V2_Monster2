
import { useEffect, useMemo, useState } from 'react';
import { Edit3, Filter, Flag, ListTodo, Plus, Search, Trash2, X } from 'lucide-react';
import { deleteJson, getJson, postJson, putJson } from '../lib/api';

import {openTaskEditor,openMilestoneEditor} from '../components/OperationalEditDialogs';
import {RowMergeButton} from '../components/RowMergeButton';
type Task={id:number;projectId:number;assigneeId?:number;code:string;name:string;project:string;assignee?:string;status:string;priority:string;progressPct:number;startDate?:string;dueDate?:string};
type Milestone={id:number;projectId:number;name:string;project:string;status:string;dueDate?:string;weightPct:number};
type Project={id:number;code:string;name:string}; type User={id:number;name:string};
const nice=(s:string)=>s.toLowerCase().replaceAll('_',' ').replace(/\b\w/g,c=>c.toUpperCase());

export function TasksMilestonesPage(){
 const [canDownloadTasks,setCanDownloadTasks]=useState(false);
 useEffect(()=>{getJson<any>('/access/me').then(a=>{
   const p=a?.permissions?.TASKS||[];
   setCanDownloadTasks(p.includes('DOWNLOAD'));
 }).catch(()=>setCanDownloadTasks(false))},[]);

 const [tasks,setTasks]=useState<Task[]>([]),[milestones,setMilestones]=useState<Milestone[]>([]),[projects,setProjects]=useState<Project[]>([]),[users,setUsers]=useState<User[]>([]);
 const [tab,setTab]=useState<'tasks'|'milestones'>('tasks'),[q,setQ]=useState(''),[status,setStatus]=useState('ALL'),[open,setOpen]=useState(false),[saving,setSaving]=useState(false),[error,setError]=useState('');
 const [task,setTask]=useState({projectId:'',name:'',description:'',assigneeId:'',status:'TODO',priority:'MEDIUM',startDate:'',dueDate:'',progressPct:'0',estimatedHours:'0'});
 const [ms,setMs]=useState({projectId:'',name:'',description:'',dueDate:'',status:'OPEN',weightPct:'0'});
 const load=async()=>{const [a,b,c,d]=await Promise.all([getJson<Task[]>('/tasks'),getJson<Milestone[]>('/milestones'),getJson<Project[]>('/projects'),getJson<User[]>('/users')]);setTasks(a);setMilestones(b);setProjects(c);setUsers(d)};
 useEffect(()=>{load().catch(e=>setError(String(e)))},[]);
 const taskRows=useMemo(()=>tasks.filter(x=>(!q||`${x.code} ${x.name} ${x.project} ${x.assignee||''}`.toLowerCase().includes(q.toLowerCase()))&&(status==='ALL'||x.status===status)),[tasks,q,status]);
 const msRows=useMemo(()=>milestones.filter(x=>(!q||`${x.name} ${x.project}`.toLowerCase().includes(q.toLowerCase()))&&(status==='ALL'||x.status===status)),[milestones,q,status]);
 const editTask=(t:Task)=>openTaskEditor(t,load,e=>setError(String(e)));
 const editMilestone=(m:Milestone)=>openMilestoneEditor(m,load,e=>setError(String(e)));
 const remove=async(kind:'tasks'|'milestones',id:number)=>{if(!confirm(`Delete this ${kind==='tasks'?'task':'milestone'}?`))return;try{await deleteJson(`/${kind}/${id}`);await load()}catch(e){setError(String(e))}};
 const submit=async(e:React.FormEvent)=>{e.preventDefault();setSaving(true);setError('');try{
   if(tab==='tasks') await postJson('/tasks',{projectId:Number(task.projectId),name:task.name,description:task.description,assigneeId:task.assigneeId?Number(task.assigneeId):null,status:task.status,priority:task.priority,startDate:task.startDate||null,dueDate:task.dueDate||null,progressPct:Number(task.progressPct||0),estimatedHours:Number(task.estimatedHours||0)});
   else await postJson('/milestones',{projectId:Number(ms.projectId),name:ms.name,description:ms.description,dueDate:ms.dueDate||null,status:ms.status,weightPct:Number(ms.weightPct||0)});
   setOpen(false);await load();
 }catch(e){setError(String(e))}finally{setSaving(false)}};
 return <>
  <div className="page-title"><div><h1>Tasks & Milestones</h1><p>{tasks.length} tasks and {milestones.length} milestones in the current workspace.</p></div><button className="budget-primary" onClick={()=>setOpen(true)}><Plus size={16}/> Add {tab==='tasks'?'Task':'Milestone'}</button></div>
  {error&&<div className="budget-error">{error}</div>}
  <div className="module-tabs"><button className={tab==='tasks'?'active':''} onClick={()=>{setTab('tasks');setStatus('ALL')}}><ListTodo size={15}/> Tasks</button><button className={tab==='milestones'?'active':''} onClick={()=>{setTab('milestones');setStatus('ALL')}}><Flag size={15}/> Milestones</button></div>
  <section className="data-module-panel">
   <div className="data-module-toolbar"><label className="module-search"><Search/><input value={q} onChange={e=>setQ(e.target.value)} placeholder={`Search ${tab}...`}/></label><label className="module-filter"><Filter/><select value={status} onChange={e=>setStatus(e.target.value)}><option value="ALL">All status</option>{(tab==='tasks'?['TODO','IN_PROGRESS','BLOCKED','REVIEW','DONE','CANCELLED']:['OPEN','IN_PROGRESS','COMPLETED','DELAYED']).map(s=><option key={s}>{s}</option>)}</select></label></div>
   {tab==='tasks'?<div className="module-table-wrap"><table className="module-table"><thead><tr><th>Code</th><th>Task</th><th>Project</th><th>Assignee</th><th>Due</th><th>Progress</th><th>Status</th><th>Actions</th></tr></thead><tbody>{taskRows.map(t=><tr key={t.id}><td>{t.code}</td><td><b>{t.name}</b></td><td>{t.project}</td><td>{t.assignee||'—'}</td><td>{t.dueDate||'—'}</td><td><div className="inline-progress"><i><span style={{width:`${Math.min(100,t.progressPct)}%`}}/></i><em>{t.progressPct}%</em></div></td><td><span className={`module-pill ${t.status.toLowerCase()}`}>{nice(t.status)}</span></td><td><div className="row-actions"><button onClick={()=>editTask(t)}><Edit3/></button><RowMergeButton entity="TASKS" source={t}/><button className="danger" onClick={()=>remove('tasks',t.id)}><Trash2/></button></div></td></tr>)}</tbody></table></div>
   :<div className="module-table-wrap"><table className="module-table"><thead><tr><th>Milestone</th><th>Project</th><th>Due</th><th>Weight</th><th>Status</th><th>Actions</th></tr></thead><tbody>{msRows.map(m=><tr key={m.id}><td><b>{m.name}</b></td><td>{m.project}</td><td>{m.dueDate||'—'}</td><td>{m.weightPct}%</td><td><span className={`module-pill ${m.status.toLowerCase()}`}>{nice(m.status)}</span></td><td><div className="row-actions"><button onClick={()=>editMilestone(m)}><Edit3/></button><button className="danger" onClick={()=>remove('milestones',m.id)}><Trash2/></button></div></td></tr>)}</tbody></table></div>}
  
</section>
  {open&&<div className="budget-modal-backdrop" onMouseDown={()=>setOpen(false)}><div className="budget-modal" onMouseDown={e=>e.stopPropagation()}>
   <div className="budget-modal-head"><div><h3>Add {tab==='tasks'?'Task':'Milestone'}</h3><p>Create delivery work directly in the project database.</p></div><button onClick={()=>setOpen(false)}><X/></button></div>
   <form onSubmit={submit}>{tab==='tasks'?<>
    <label>Project<select required value={task.projectId} onChange={e=>setTask({...task,projectId:e.target.value})}><option value="">Select project</option>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label>
    <div className="budget-form-grid"><label>Task name<input required value={task.name} onChange={e=>setTask({...task,name:e.target.value})}/></label><label>Assignee<select value={task.assigneeId} onChange={e=>setTask({...task,assigneeId:e.target.value})}><option value="">Unassigned</option>{users.map(u=><option key={u.id} value={u.id}>{u.name}</option>)}</select></label><label>Priority<select value={task.priority} onChange={e=>setTask({...task,priority:e.target.value})}><option>LOW</option><option>MEDIUM</option><option>HIGH</option><option>CRITICAL</option></select></label><label>Start date<input type="date" value={task.startDate} onChange={e=>setTask({...task,startDate:e.target.value})}/></label><label>Due date<input type="date" value={task.dueDate} onChange={e=>setTask({...task,dueDate:e.target.value})}/></label><label>Progress %<input type="number" min="0" max="100" value={task.progressPct} onChange={e=>setTask({...task,progressPct:e.target.value})}/></label><label>Estimated hours<input type="number" min="0" value={task.estimatedHours} onChange={e=>setTask({...task,estimatedHours:e.target.value})}/></label></div>
    <label>Description<textarea rows={3} value={task.description} onChange={e=>setTask({...task,description:e.target.value})}/></label>
   </>:<>
    <label>Project<select required value={ms.projectId} onChange={e=>setMs({...ms,projectId:e.target.value})}><option value="">Select project</option>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label>
    <div className="budget-form-grid"><label>Milestone name<input required value={ms.name} onChange={e=>setMs({...ms,name:e.target.value})}/></label><label>Due date<input type="date" value={ms.dueDate} onChange={e=>setMs({...ms,dueDate:e.target.value})}/></label><label>Status<select value={ms.status} onChange={e=>setMs({...ms,status:e.target.value})}><option>OPEN</option><option>IN_PROGRESS</option><option>COMPLETED</option><option>DELAYED</option></select></label><label>Weight %<input type="number" min="0" max="100" value={ms.weightPct} onChange={e=>setMs({...ms,weightPct:e.target.value})}/></label></div><label>Description<textarea rows={3} value={ms.description} onChange={e=>setMs({...ms,description:e.target.value})}/></label>
   </>}<div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setOpen(false)}>Cancel</button><button className="budget-primary" disabled={saving}>{saving?'Saving...':'Save'}</button></div></form>
  </div></div>}
 </>;
}
