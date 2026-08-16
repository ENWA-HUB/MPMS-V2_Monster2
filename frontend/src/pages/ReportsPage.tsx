import { useEffect, useMemo, useState } from 'react';
import { BarChart3, CalendarDays, Download, FileText, Presentation, ShieldAlert } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Legend, Line, LineChart, PolarAngleAxis, PolarGrid, PolarRadiusAxis, Radar, RadarChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { getJson } from '../lib/api';

type Task={id:number;name:string;project:string;status:string;progressPct:number;dueDate?:string;startDate?:string};
type Project={id:number;name:string;status:string;progressPct:number;healthStatus:string};
type Budget={trend:{month:string;budget:number;actual:number}[]};
type SupplierOverview={radar:{suppliers:string[];data:any[]}};
type Dashboard={totalProjects:number;activeProjects:number;greenProjects:number;amberProjects:number;redProjects:number};

const reportCards=[
  {title:'Monthly Project Status Report',type:'Status Report',size:'2.4 MB',icon:FileText},
  {title:'Portfolio Financial Summary',type:'Financial Report',size:'1.8 MB',icon:BarChart3},
  {title:'Supplier Performance Analysis',type:'Performance Report',size:'3.2 MB',icon:Presentation},
  {title:'Risk Assessment Report',type:'Risk Report',size:'1.5 MB',icon:ShieldAlert},
];

export function ReportsPage(){
 const [tasks,setTasks]=useState<Task[]>([]); const [projects,setProjects]=useState<Project[]>([]);
 const [budget,setBudget]=useState<Budget|null>(null); const [supplier,setSupplier]=useState<SupplierOverview|null>(null);
 const [dash,setDash]=useState<Dashboard|null>(null);
 useEffect(()=>{Promise.all([
   getJson<Task[]>('/tasks'),getJson<Project[]>('/projects'),getJson<Budget>('/budget/overview'),
   getJson<SupplierOverview>('/suppliers/overview'),getJson<Dashboard>('/dashboard')
 ]).then(([t,p,b,s,d])=>{setTasks(t);setProjects(p);setBudget(b);setSupplier(s);setDash(d)}).catch(console.error)},[]);
 const portfolio=useMemo(()=>{
   const statuses=['COMPLETED','ACTIVE','PLANNING'];
   return statuses.map(status=>({status,count:projects.filter(p=>p.status===status).length}));
 },[projects]);
 const gantt=tasks.slice(0,6);
 const exportPdf=()=>window.print();
 const exportJson=()=>{
   const blob=new Blob([JSON.stringify({generatedAt:new Date().toISOString(),dashboard:dash,projects,tasks},null,2)],{type:'application/json'});
   const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download=`MAIPT-Executive-Report-${new Date().toISOString().slice(0,10)}.json`;a.click();URL.revokeObjectURL(a.href);
 };
 const radarColors=['#082d57','#dca310','#377dbb'];
 return <>
  <div className="page-title reports-title">
   <div><h1>Reports & Analytics</h1><p>Comprehensive reports, executive analytics and portfolio visualization</p></div>
   <div className="supplier-actions"><button className="secondary" onClick={exportPdf}>Export PDF</button><button className="budget-primary" onClick={exportJson}>Generate Report</button></div>
  </div>

  <section className="report-cards">
   {reportCards.map((r,i)=>{const Icon=r.icon;return <article className="report-card" key={r.title}>
    <div className="report-card-icon"><Icon/></div><button className="report-download" onClick={exportJson}><Download/></button>
    <b>{r.title}</b><span>{r.type}</span><small><CalendarDays size={11}/> Aug 2026 · {i===0?'145':i===1?'89':i===2?'67':'112'} downloads</small><em>{r.size}</em>
   </article>})}
  </section>

  <section className="report-panel">
   <h3>Project Timeline - Gantt Chart</h3>
   <div className="gantt">
    <div className="gantt-head"><b>Task</b>{['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'].map(m=><span key={m}>{m}</span>)}</div>
    {gantt.map((t,i)=><div className="gantt-row" key={t.id}><b>{t.name}</b><div className="gantt-track">
      <i className={t.progressPct>=100?'done':t.progressPct>0?'doing':'planned'} style={{left:`${Math.min(75,i*10+8)}%`,width:`${Math.max(9,28-i*2)}%`}}><span>{t.progressPct}%</span></i>
    </div></div>)}
    <div className="gantt-legend"><span><i className="done"/> Completed</span><span><i className="doing"/> In Progress</span><span><i className="planned"/> Planned</span></div>
   </div>
  </section>

  <section className="reports-two-grid">
    <article className="report-panel"><h3>Project Portfolio Overview</h3><div className="report-chart">
      <ResponsiveContainer width="100%" height="100%"><BarChart data={portfolio}><CartesianGrid strokeDasharray="3 3"/><XAxis dataKey="status"/><YAxis allowDecimals={false}/><Tooltip/><Bar dataKey="count" name="Projects" fill="#377dbb"/></BarChart></ResponsiveContainer>
    </div></article>
    <article className="report-panel"><h3>Budget vs Actual Spending</h3><div className="report-chart">
      <ResponsiveContainer width="100%" height="100%"><LineChart data={budget?.trend||[]}><CartesianGrid strokeDasharray="3 3"/><XAxis dataKey="month"/><YAxis/><Tooltip/><Legend/><Line dataKey="budget" stroke="#082d57" strokeWidth={2}/><Line dataKey="actual" stroke="#dca310" strokeWidth={2}/></LineChart></ResponsiveContainer>
    </div></article>
  </section>

  <section className="report-panel">
   <h3>Supplier Performance Comparison</h3>
   <div className="report-radar">
    {supplier?.radar.data?.length?<ResponsiveContainer width="100%" height="100%"><RadarChart data={supplier.radar.data} outerRadius="66%">
      <PolarGrid/><PolarAngleAxis dataKey="criterion"/><PolarRadiusAxis domain={[0,100]}/><Legend/><Tooltip/>
      {supplier.radar.suppliers.slice(0,3).map((n,i)=><Radar key={n} name={n} dataKey={n} stroke={radarColors[i]} fill={radarColors[i]} fillOpacity={i===0?.25:.1}/>)}
    </RadarChart></ResponsiveContainer>:<div className="empty-mini">No detailed supplier KPI scores yet.</div>}
   </div>
  </section>

  {dash&&<section className="report-executive-strip">
    <div><span>Total Projects</span><b>{dash.totalProjects}</b></div><div><span>Active</span><b>{dash.activeProjects}</b></div><div><span>Green</span><b>{dash.greenProjects}</b></div><div><span>Amber</span><b>{dash.amberProjects}</b></div><div><span>Red</span><b>{dash.redProjects}</b></div>
  </section>}
 </>;
}
