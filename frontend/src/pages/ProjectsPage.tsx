import {useEffect, useMemo, useState, useRef, cloneElement} from 'react'
import { CalendarDays, CircleDot, Clock3, Download, Edit3, Filter, FolderKanban, Plus, Search, Trash2, X } from 'lucide-react';
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { deleteJson, getJson, postJson, putJson } from '../lib/api';

import {RowMergeButton} from '../components/RowMergeButton';

import FormattedNumberInput from "../components/FormattedNumberInput";

function TypeableSelectWrapperV3({children,placeholder}:{children:any,placeholder:string}){
 const selectRef=useRef<HTMLSelectElement|null>(null)
 const [open,setOpen]=useState(false)
 const [query,setQuery]=useState('')
 const [selectedLabel,setSelectedLabel]=useState('')

 const syncSelected=()=>{
  const el=selectRef.current
  if(!el)return
  const opt=el.options[el.selectedIndex]
  setSelectedLabel(opt?.text||'')
 }

 useEffect(()=>{syncSelected()},[children])

 const getOptions=()=>{
  const el=selectRef.current
  if(!el)return [] as {value:string,label:string}[]
  return Array.from(el.options).map(o=>({value:o.value,label:o.text}))
 }

 const q=query.trim().toLowerCase()
 const matches=(q?getOptions().filter(x=>x.label.toLowerCase().includes(q)):getOptions()).slice(0,50)

 const choose=(value:string,label:string)=>{
  const el=selectRef.current
  if(!el)return
  el.value=value
  el.dispatchEvent(new Event('change',{bubbles:true}))
  setSelectedLabel(label)
  setQuery('')
  setOpen(false)
 }

 return <div className="project-typeable-v3">
  <input
   value={open?query:selectedLabel}
   placeholder={placeholder}
   autoComplete="off"
   onFocus={()=>{syncSelected();setQuery('');setOpen(true)}}
   onChange={e=>{setQuery(e.target.value);setOpen(true)}}
   onKeyDown={e=>{
    if(e.key==='Escape')setOpen(false)
    if(e.key==='Enter'&&matches.length){e.preventDefault();choose(matches[0].value,matches[0].label)}
   }}
  />
  <button type="button" className="project-typeable-v3-arrow" onClick={()=>{syncSelected();setQuery('');setOpen(v=>!v)}}>v</button>
  {cloneElement(children as any,{ref:selectRef,className:'project-typeable-v3-native'})}
  {open&&<div className="project-typeable-v3-results">
   {matches.length?matches.map(x=><button type="button" key={`${x.value}-${x.label}`} onMouseDown={e=>e.preventDefault()} onClick={()=>choose(x.value,x.label)}>{x.label}</button>):<div className="project-typeable-v3-empty">No matching result</div>}
  </div>}
 </div>
}

type Project={
  id:number;
  orgUnitId:number;
  portfolioId?:number|null;

  code:string;
  name:string;
  description:string;

  ownerId:number;
  sponsorId?:number|null;

  status:string;
  priority:string;

  progressPct:number;
  healthScore:number;
  healthStatus:string;

  startDate?:string|null;
  baselineEndDate?:string|null;
  endDate?:string|null;

  budgetAmount:number;
  currency:string;

  orgUnit?:string;
  portfolio?:string;
  owner?:string;
  sponsor?:string;

  createdByUserId?:number|null;
  createdByName?:string|null;
  createdAt?:string|null;
  updatedAt?:string|null;
};

type FormOptions={currentOrgUnitId?:number;allOrgUnits?:boolean;
  orgUnits:{id:number;code:string;name:string}[];
  portfolios:{id:number;code:string;name:string;status:string}[];
  users:{
    id:number;
    name:string;
    email:string;
    jobTitle:string;
    department:string;
    role:string;
  }[];
};
type Milestone={id:number;name:string;project:string;dueDate?:string;status:string;weightPct:number};
type ProjectOverview={
  totalProjects:number; activeProjects:number; completedProjects:number; onHoldProjects:number;
  greenProjects:number; amberProjects:number; redProjects:number;
  portfolios:{name:string;value:number}[];
  milestones:Milestone[];
};

const money=(n:number,c='VND')=>{
  const abs=Math.abs(n);
  const compact=abs>=1e9?`${(n/1e9).toFixed(1)}B`:abs>=1e6?`${(n/1e6).toFixed(1)}M`:abs>=1e3?`${(n/1e3).toFixed(0)}K`:new Intl.NumberFormat('en-US').format(n);
  return c==='VND'?`${compact} ₫`:c==='USD'?`$${compact}`:`${compact} ${c}`;
};
const nice=(s:string)=>s.toLowerCase().replaceAll('_',' ').replace(/\b\w/g,c=>c.toUpperCase());

export function ProjectsPage(){
 const [canDownloadProjects,setCanDownloadProjects]=useState(false);
 useEffect(()=>{getJson<any>('/access/me').then(a=>{
   const p=a?.permissions?.PROJECTS||[];
   setCanDownloadProjects(p.includes('DOWNLOAD'));
 }).catch(()=>setCanDownloadProjects(false))},[]);

  const [projects,setProjects]=useState<Project[]>([]);
  const [overview,setOverview]=useState<ProjectOverview|null>(null);
  const [q,setQ]=useState('');
  const [status,setStatus]=useState('ALL');
  const [portfolio,setPortfolio]=useState('ALL');
  const [businessUnit,setBusinessUnit]=useState('ALL');
  const [open,setOpen]=useState(false);
  const [editing,setEditing]=useState<Project|null>(null);
  const [options,setOptions]=useState<FormOptions>({
    orgUnits:[],
    portfolios:[],
    users:[]
  });
  const [saving,setSaving]=useState(false);
  const [error,setError]=useState('');
  const [form,setForm]=useState({
    orgUnitId:'1', portfolioId:'', code:'', name:'', description:'', ownerId:'1', sponsorId:'1',
    status:'PLANNING', priority:'MEDIUM', startDate:'', endDate:'', baselineEndDate:'',
    budgetAmount:'0', currency:'VND', progressPct:'0', healthScore:'0', healthStatus:'GREEN'
  });

  const load=async()=>{
    const [p,o,fo]=await Promise.all([
      getJson<Project[]>('/projects'),
      getJson<ProjectOverview>('/projects/overview'),
      getJson<FormOptions>('/projects/form-options')
    ]);
    setProjects(p);
    setOverview(o);
    setOptions(fo);
  };
  useEffect(()=>{load().catch(e=>setError(String((e as any)?.message||e||'System error.')))},[]);

  const filtered=useMemo(()=>projects.filter(p=>{
    const bu=projectBuLabel(p);
    const hit=!q || `${p.code} ${p.name} ${p.description||''} ${p.owner||''} ${p.portfolio||''} ${bu}`.toLowerCase().includes(q.toLowerCase());
    const s=status==='ALL'||p.status===status;
    const f=portfolio==='ALL'||(p.portfolio||'Unassigned')===portfolio;
    const b=businessUnit==='ALL'||String(p.orgUnitId||'')===businessUnit;
    return hit&&s&&f&&b;
  }),[projects,q,status,portfolio,businessUnit,options.orgUnits]);

  const portfolioOptions=useMemo(()=>Array.from(new Set(projects.map(p=>p.portfolio||'Unassigned'))),[projects]);

  const exportCsv=()=>{
    const rows=[['Code','Project','Portfolio','Owner','Status','Priority','Progress','Health','Start','End','Budget','Currency'],
      ...filtered.map(p=>[p.code,p.name,p.portfolio||'',p.owner||'',p.status,p.priority,p.progressPct,p.healthStatus,p.startDate||'',p.endDate||'',p.budgetAmount,p.currency])];
    const csv=rows.map(r=>r.map(v=>`"${String(v).replaceAll('"','""')}"`).join(',')).join('\n');
    const blob=new Blob([csv],{type:'text/csv;charset=utf-8'}); const a=document.createElement('a');
    a.href=URL.createObjectURL(blob);a.download=`MAIPT-Projects-${new Date().toISOString().slice(0,10)}.csv`;a.click();URL.revokeObjectURL(a.href);
  };

  const submit=async(e:React.FormEvent)=>{
    e.preventDefault();
    setSaving(true);
    setError('');

    const payload={
      orgUnitId:Number(form.orgUnitId),
      portfolioId:form.portfolioId
        ? Number(form.portfolioId)
        : null,

      
      name:form.name.trim(),
      description:form.description.trim(),

      ownerId:Number(form.ownerId),
      sponsorId:form.sponsorId
        ? Number(form.sponsorId)
        : null,

      status:form.status,
      priority:form.priority,

      startDate:form.startDate||null,
      baselineEndDate:form.baselineEndDate||null,
      endDate:form.endDate||null,

      budgetAmount:Number(form.budgetAmount||0),
      currency:form.currency,

      progressPct:Number(form.progressPct||0),
      healthScore:Number(form.healthScore||0),
      healthStatus:form.healthStatus
    };

    try{
      if(editing){
        await putJson(`/projects/${editing.id}`,payload);
      }else{
        await postJson('/projects',payload);
      }

      setOpen(false);
      setEditing(null);
      await load();

    }catch(e){
      setError(String((e as any)?.message||e||'System error.'));
    }finally{
      setSaving(false);
    }
  };


  const openNew=()=>{
    setEditing(null);

    setForm({
      orgUnitId:defaultProjectOrgUnitId(),
      portfolioId:'',
      code:'',
      name:'',
      description:'',
      ownerId:options.users[0]
        ? String(options.users[0].id)
        : '',
      sponsorId:'',
      status:'PLANNING',
      priority:'MEDIUM',
      startDate:'',
      baselineEndDate:'',
      endDate:'',
      budgetAmount:'0',
      currency:'VND',
      progressPct:'0',
      healthScore:'100',
      healthStatus:'GREEN'
    });

    setOpen(true);
  };


  const openEdit=(p:Project)=>{
    setEditing(p);

    setForm({
      orgUnitId:String(p.orgUnitId||''),
      portfolioId:p.portfolioId
        ? String(p.portfolioId)
        : '',

      code:p.code||'',
      name:p.name||'',
      description:p.description||'',

      ownerId:String(p.ownerId||''),
      sponsorId:p.sponsorId
        ? String(p.sponsorId)
        : '',

      status:p.status||'PLANNING',
      priority:p.priority||'MEDIUM',

      startDate:p.startDate||'',
      baselineEndDate:p.baselineEndDate||'',
      endDate:p.endDate||'',

      budgetAmount:String(p.budgetAmount||0),
      currency:p.currency||'VND',

      progressPct:String(p.progressPct||0),
      healthScore:String(p.healthScore??100),
      healthStatus:p.healthStatus||'GREEN'
    });

    setOpen(true);
  };

  const removeProject=async(p:Project)=>{if(!confirm(`Deactivate project “${p.name}”? The project will be hidden from active lists, but its history and related data will be retained.`))return;setError("");try{await deleteJson(`/projects/${p.id}`);await load()}catch(e:any){setError(String(e?.message||e||'Unable to deactivate this Project.'))}};

  if(!overview) return <div className="loading">{error||'Loading projects...'}</div>;

  const pieColors=['#082d57','#2f79b9','#dca310','#6f879f','#b5c2cf'];

  const portfolioPerformance=portfolioOptions.map(name=>{
    const rows=projects.filter(p=>(p.portfolio||'Unassigned')===name);
    const total=rows.length;
    const active=rows.filter(p=>p.status==='ACTIVE').length;
    const avgProgress=total?Math.round(rows.reduce((a,p)=>a+Number(p.progressPct||0),0)/total):0;
    const totalBudget=rows.reduce((a,p)=>a+Number(p.budgetAmount||0),0);
    const healthScore=total?Math.round(rows.reduce((a,p)=>{
      const h=p.healthStatus==='GREEN'?100:p.healthStatus==='AMBER'?65:p.healthStatus==='RED'?30:75;
      return a+h;
    },0)/total):0;
    return {name,shortName:name.length>22?name.slice(0,20)+'…':name,total,active,avgProgress,healthScore,totalBudget};
  }).sort((a,b)=>b.total-a.total);

    function projectBuLabel(project:any){
    const direct=(options.orgUnits??[]).find((x:any)=>Number(x.id)===Number(project.orgUnitId));
    return direct?`${direct.code} - ${direct.name}`:(project.orgUnit||'—');
  }

  function defaultProjectOrgUnitId(){
    const currentId=Number((options as any)?.currentOrgUnitId||0);
    if(currentId&&(options.orgUnits??[]).some((x:any)=>Number(x.id)===currentId))
      return String(currentId);
    return options.orgUnits?.length?String(options.orgUnits[0].id):'';
  }
return <>
   <div className="page-title projects-title">
    <div><h1>Projects</h1><p>Manage portfolio projects, milestones, health, schedule and delivery progress</p></div>
    <div className="project-actions">
      {canDownloadProjects&&<button className="secondary" onClick={exportCsv}><Download size={16}/> Export</button>}
      
      <button className="budget-primary" onClick={openNew}><Plus size={17}/> New Project</button>
    </div>
   </div>
   {error&&<div className="budget-error">{error}</div>}

   <section className="project-kpis">
    <div className="project-kpi"><span>Total Projects</span><strong>{overview.totalProjects}</strong><small>{overview.activeProjects} active</small><FolderKanban/></div>
    <div className="project-kpi"><span>Active Projects</span><strong>{overview.activeProjects}</strong><small>{overview.completedProjects} completed</small><CircleDot/></div>
    <div className="project-kpi"><span>On Hold</span><strong>{overview.onHoldProjects}</strong><small>Require attention</small><Clock3/></div>
    <div className="project-kpi"><span>Healthy</span><strong>{overview.greenProjects}</strong><small>{overview.amberProjects+overview.redProjects} at risk</small><CalendarDays/></div>
   
</section>

   <section className="projects-top-grid">
    <article className="project-panel project-chart-panel portfolio-performance-panel">
      <div className="project-panel-head">
        <div>
          <h3>Portfolio Performance Overview</h3>
          <span>Average delivery progress and portfolio health</span>
        </div>
      </div>

      <div className="portfolio-line-chart">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={portfolioPerformance} margin={{top:16,right:24,left:4,bottom:18}}>
            <CartesianGrid strokeDasharray="3 3" vertical={false}/>
            <XAxis dataKey="shortName" interval={0} angle={portfolioPerformance.length>4?-18:0} textAnchor={portfolioPerformance.length>4?'end':'middle'} height={portfolioPerformance.length>4?64:36} tick={{fontSize:10}}/>
            <YAxis domain={[0,100]} tickFormatter={v=>`${v}%`} tick={{fontSize:10}} width={42}/>
            <Tooltip content={({active,payload})=>{
              if(!active||!payload?.length)return null;
              const d=payload[0].payload;
              return <div className="portfolio-chart-tooltip"><b>{d.name}</b><span>Total Projects: {d.total}</span><span>Active Projects: {d.active}</span><span>Average Progress: {d.avgProgress}%</span><span>Health Score: {d.healthScore}%</span><span>Total Budget: {money(d.totalBudget)}</span></div>
            }}/>
            <Legend/>
            <Line type="monotone" dataKey="avgProgress" name="Average Progress" stroke="#0b3564" strokeWidth={3} dot={{r:4}} activeDot={{r:6}}/>
            <Line type="monotone" dataKey="healthScore" name="Health Score" stroke="#d89b0d" strokeWidth={3} dot={{r:4}} activeDot={{r:6}}/>
          </LineChart>
        </ResponsiveContainer>
      </div>
    </article>

    <article className="project-panel milestones-panel">
      <div className="project-panel-head"><h3>Upcoming Milestones</h3><span>Next 6 months</span></div>
      <div className="milestone-list">
        {overview.milestones.length?overview.milestones.map(m=><div className="milestone-row" key={m.id}>
          <div className={`milestone-dot ${m.status.toLowerCase()}`}/>
          <div><b>{m.name}</b><span>{m.project}</span></div>
          <div className="milestone-date"><strong>{m.dueDate||'—'}</strong><span>{nice(m.status)}</span></div>
        </div>):<div className="empty-mini">No upcoming milestones.</div>}
      </div>
    </article>
   </section>

   <section className="project-panel project-list-panel">
    <div className="project-list-head">
      <div><h3>All Projects</h3><span>{filtered.length} of {projects.length} projects</span></div>
      <div className="project-filters">
        <label className="project-search"><Search size={15}/><input value={q} onChange={e=>setQ(e.target.value)} placeholder="Search projects..."/></label>
        <label><Filter size={14}/><select value={status} onChange={e=>setStatus(e.target.value)}><option value="ALL">All status</option><option>PLANNING</option><option>ACTIVE</option><option>ON_HOLD</option><option>COMPLETED</option><option>CANCELLED</option></select></label>
        <select value={portfolio} onChange={e=>setPortfolio(e.target.value)}><option value="ALL">All portfolios</option>{portfolioOptions.map(x=><option key={x}>{x}</option>)}</select>
        <select value={businessUnit} onChange={e=>setBusinessUnit(e.target.value)}>
          <option value="ALL">All Business Units</option>
          {options.orgUnits.map(x=><option key={x.id} value={String(x.id)}>{x.code} - {x.name}</option>)}
        </select>
      </div>
    </div>
    <div className="project-table-wrap">
     <table className="project-table"><thead><tr>
      <th>Project</th><th>Portfolio</th><th>Business Unit</th><th>Owner</th><th>Description</th><th>Created By</th><th>Timeline</th><th>Progress</th><th>Budget</th><th>Health</th><th>Status</th><th>Actions</th>
     </tr></thead><tbody>
      {filtered.map(p=><tr key={p.id}>
       <td><b>{p.name}</b><small>{p.code}</small></td>
       <td>{p.portfolio||'Unassigned'}</td>
       <td><span className="project-cell-ellipsis" title={projectBuLabel(p)}>{projectBuLabel(p)}</span></td>
       <td>{p.owner||'—'}</td>
       <td><span className="project-desc-2line" title={p.description||''}>{p.description||'—'}</span></td>
       <td>{p.createdByName||"System Created"}</td><td><span className="project-date">{p.startDate||'—'} → {p.endDate||'—'}</span></td>
       <td><div className="project-progress"><i><span style={{width:`${Math.min(100,p.progressPct)}%`}}/></i><em>{p.progressPct}%</em></div></td>
       <td><b>{money(p.budgetAmount,p.currency)}</b></td>
       <td><span className={`health-pill ${p.healthStatus.toLowerCase()}`}>{nice(p.healthStatus)}</span></td>
       <td><span className={`project-status ${p.status.toLowerCase()}`}>{nice(p.status)}</span></td><td><div className="row-actions"><button onClick={()=>openEdit(p)}><Edit3/></button><RowMergeButton entity="PROJECTS" source={p}/><button className="danger" onClick={()=>removeProject(p)}><Trash2/></button></div></td>
      </tr>)}
     </tbody></table>
    </div>
   </section>

   {open&&<div className="budget-modal-backdrop" onMouseDown={()=>setOpen(false)}>
    <div className="budget-modal project-modal" onMouseDown={e=>e.stopPropagation()}>
     <div className="budget-modal-head"><div><h3>{editing?'Edit Project':'New Project'}</h3>
<p>
{editing
 ? 'Update complete project information.'
 : 'Create a project with ownership, schedule, budget and health.'}
</p></div><button onClick={()=>setOpen(false)}><X/></button></div>
     <form onSubmit={submit}>
      <div className="budget-form-grid">
       
       <label>Project name<input required value={form.name} onChange={e=>setForm({...form,name:e.target.value})}/></label>
       <label>Business Unit
<TypeableSelectWrapperV3 placeholder="Type BU code or name..."><select
  required
  value={form.orgUnitId}
  onChange={e=>setForm({...form,orgUnitId:e.target.value})}
>
<option value="">Select Business Unit</option>
{options.orgUnits.map(x=>
<option key={x.id} value={x.id}>
{x.code} — {x.name}
</option>
)}
</select></TypeableSelectWrapperV3>
</label>

<label>Portfolio
<select
  value={form.portfolioId}
  onChange={e=>setForm({...form,portfolioId:e.target.value})}
>
<option value="">Unassigned</option>
{options.portfolios.map(x=>
<option key={x.id} value={x.id}>
{x.code} — {x.name}
</option>
)}
</select>
</label>

<label>Project Manager / Owner
<TypeableSelectWrapperV3 placeholder="Type name or email..."><select
  required
  value={form.ownerId}
  onChange={e=>setForm({...form,ownerId:e.target.value})}
>
<option value="">Select Owner</option>
{options.users.map(x=>
<option key={x.id} value={x.id}>
{x.name} — {x.jobTitle||x.department||x.role}
</option>
)}
</select></TypeableSelectWrapperV3>
</label>

<label>Sponsor
<select
  value={form.sponsorId}
  onChange={e=>setForm({...form,sponsorId:e.target.value})}
>
<option value="">No Sponsor</option>
{options.users.map(x=>
<option key={x.id} value={x.id}>
{x.name} — {x.jobTitle||x.department||x.role}
</option>
)}
</select>
</label>

<label>Baseline End Date
<input
 type="date"
 value={form.baselineEndDate}
 onChange={e=>setForm({
   ...form,
   baselineEndDate:e.target.value
 })}
/>
</label>

<label>Health Score
<input
 type="number"
 min="0"
 max="100"
 step="0.1"
 value={form.healthScore}
 onChange={e=>setForm({
   ...form,
   healthScore:e.target.value
 })}
/>
</label>

<label>Status<select value={form.status} onChange={e=>setForm({...form,status:e.target.value})}><option>PLANNING</option><option>ACTIVE</option><option>ON_HOLD</option><option>COMPLETED</option></select></label>
       <label>Priority<select value={form.priority} onChange={e=>setForm({...form,priority:e.target.value})}><option>LOW</option><option>MEDIUM</option><option>HIGH</option><option>CRITICAL</option></select></label>
       <label>Start date<input type="date" value={form.startDate} onChange={e=>setForm({...form,startDate:e.target.value})}/></label>
       <label>End date<input type="date" value={form.endDate} onChange={e=>setForm({...form,endDate:e.target.value})}/></label>
       <label>Budget<FormattedNumberInput value={form.budgetAmount} decimals={0} onValueChange={v=>setForm({...form,budgetAmount:v})}/></label>
       <label>Currency<select value={form.currency} onChange={e=>setForm({...form,currency:e.target.value})}><option>VND</option><option>USD</option></select></label>
       <label>Progress %<input type="number" min="0" max="100" value={form.progressPct} onChange={e=>setForm({...form,progressPct:e.target.value})}/></label>
       <label>Health<select value={form.healthStatus} onChange={e=>setForm({...form,healthStatus:e.target.value})}><option>GREEN</option><option>AMBER</option><option>RED</option></select></label>
      </div>
      <label>Description<textarea rows={3} value={form.description} onChange={e=>setForm({...form,description:e.target.value})}/></label>
      <div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setOpen(false)}>Cancel</button><button className="budget-primary" disabled={saving}>{saving?'Saving...':editing?'Save Changes':'Create Project'}</button></div>
     </form>
    </div>
   </div>}
  </>;
}
