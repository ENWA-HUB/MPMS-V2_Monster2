import { useEffect, useMemo, useState } from 'react';
import { CalendarDays, CircleDot, Clock3, Download, Edit3, Filter, FolderKanban, Plus, Search, Trash2, X } from 'lucide-react';
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { deleteJson, getJson, postJson, putJson } from '../lib/api';

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
};

type FormOptions={
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
  const [projects,setProjects]=useState<Project[]>([]);
  const [overview,setOverview]=useState<ProjectOverview|null>(null);
  const [q,setQ]=useState('');
  const [status,setStatus]=useState('ALL');
  const [portfolio,setPortfolio]=useState('ALL');
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
  useEffect(()=>{load().catch(e=>setError(String(e)))},[]);

  const filtered=useMemo(()=>projects.filter(p=>{
    const hit=!q || `${p.code} ${p.name} ${p.owner||''} ${p.portfolio||''}`.toLowerCase().includes(q.toLowerCase());
    const s=status==='ALL'||p.status===status;
    const f=portfolio==='ALL'||(p.portfolio||'Unassigned')===portfolio;
    return hit&&s&&f;
  }),[projects,q,status,portfolio]);

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

      code:form.code.trim(),
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
      setError(String(e));
    }finally{
      setSaving(false);
    }
  };


  const openNew=()=>{
    setEditing(null);

    setForm({
      orgUnitId:options.orgUnits[0]
        ? String(options.orgUnits[0].id)
        : '',
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

  const removeProject=async(p:Project)=>{if(!confirm(`Delete project “${p.name}” and its dependent project-control data?`))return;try{await deleteJson(`/projects/${p.id}`);await load()}catch(e){setError(String(e))}};

  if(!overview) return <div className="loading">{error||'Loading projects...'}</div>;

  const pieColors=['#082d57','#2f79b9','#dca310','#6f879f','#b5c2cf'];

  return <>
   <div className="page-title projects-title">
    <div><h1>Projects</h1><p>Manage portfolio projects, milestones, health, schedule and delivery progress</p></div>
    <div className="project-actions">
      <button className="secondary" onClick={exportCsv}><Download size={16}/> Export</button>
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
    <article className="project-panel">
      <h3>Portfolio Distribution</h3>
      <div className="project-pie"><ResponsiveContainer width="100%" height="100%"><PieChart>
        <Pie data={overview.portfolios} dataKey="value" nameKey="name" innerRadius="46%" outerRadius="70%" paddingAngle={2} label={({name,percent})=>`${name}: ${Math.round((percent||0)*100)}%`}>
          {overview.portfolios.map((_,i)=><Cell key={i} fill={pieColors[i%pieColors.length]}/>)}
        </Pie><Tooltip/>
      </PieChart></ResponsiveContainer></div>
    </article>

    <article className="project-panel milestones-panel">
      <div className="project-panel-head"><h3>Upcoming Milestones</h3><span>Next 60 days</span></div>
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
      </div>
    </div>
    <div className="project-table-wrap">
     <table className="project-table"><thead><tr>
      <th>Project</th><th>Portfolio</th><th>Owner</th><th>Timeline</th><th>Progress</th><th>Budget</th><th>Health</th><th>Status</th><th>Actions</th>
     </tr></thead><tbody>
      {filtered.map(p=><tr key={p.id}>
       <td><b>{p.name}</b><small>{p.code}</small></td>
       <td>{p.portfolio||'Unassigned'}</td><td>{p.owner||'—'}</td>
       <td><span className="project-date">{p.startDate||'—'} → {p.endDate||'—'}</span></td>
       <td><div className="project-progress"><i><span style={{width:`${Math.min(100,p.progressPct)}%`}}/></i><em>{p.progressPct}%</em></div></td>
       <td><b>{money(p.budgetAmount,p.currency)}</b></td>
       <td><span className={`health-pill ${p.healthStatus.toLowerCase()}`}>{nice(p.healthStatus)}</span></td>
       <td><span className={`project-status ${p.status.toLowerCase()}`}>{nice(p.status)}</span></td><td><div className="row-actions"><button onClick={()=>openEdit(p)}><Edit3/></button><button className="danger" onClick={()=>removeProject(p)}><Trash2/></button></div></td>
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
       <label>Project code<input required value={form.code} onChange={e=>setForm({...form,code:e.target.value})} placeholder="PRJ-003"/></label>
       <label>Project name<input required value={form.name} onChange={e=>setForm({...form,name:e.target.value})}/></label>
       <label>Business Unit
<select
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
</select>
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
<select
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
</select>
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
       <label>Budget<input type="number" min="0" value={form.budgetAmount} onChange={e=>setForm({...form,budgetAmount:e.target.value})}/></label>
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
