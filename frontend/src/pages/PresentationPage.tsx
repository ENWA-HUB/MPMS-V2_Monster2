import { useEffect, useMemo, useState } from 'react';
import { CheckCircle2, Download, Maximize2, Minimize2, Presentation, TrendingUp, TriangleAlert, UsersRound, WalletCards } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { getJson } from '../lib/api';

type Dashboard={totalProjects:number;activeProjects:number;portfolioProgress:number;revisedBudget:number;actualCost:number;budgetVariance:number;openRisks:number;criticalRisks:number;openIssues:number;overdueTasks:number;greenProjects:number;amberProjects:number;redProjects:number;supplierAverageKpi:number;suppliersBelowThreshold:number};
type Project={id:number;name:string;status:string;progressPct:number;healthStatus:string;budgetAmount:number;currency:string;portfolio?:string};
type Budget={totalBudget:number;totalActual:number;projects:{project:string;category:string;totalBudget:number;spent:number;status:string}[]};

const money=(n:number)=>n>=1e9?`${(n/1e9).toFixed(1)}B`:n>=1e6?`${(n/1e6).toFixed(1)}M`:n>=1e3?`${(n/1e3).toFixed(0)}K`:String(Math.round(n));
const pct=(n:number)=>`${Math.round(n)}%`;

export function PresentationPage(){
 const [canDownloadPresentation,setCanDownloadPresentation]=useState(false);
 useEffect(()=>{
   fetch('/api/access/me',{credentials:'same-origin'})
    .then(r=>r.ok?r.json():null)
    .then(a=>setCanDownloadPresentation((a?.permissions?.REPORTS||[]).includes('DOWNLOAD')))
    .catch(()=>setCanDownloadPresentation(false));
 },[]);

 const [dash,setDash]=useState<Dashboard|null>(null);
 const [projects,setProjects]=useState<Project[]>([]);
 const [budget,setBudget]=useState<Budget|null>(null);
 const [full,setFull]=useState(false);
 useEffect(()=>{Promise.all([getJson<Dashboard>('/dashboard'),getJson<Project[]>('/projects'),getJson<Budget>('/budget/overview')]).then(([d,p,b])=>{setDash(d);setProjects(p);setBudget(b)}).catch(console.error)},[]);
 useEffect(()=>{const h=()=>setFull(!!document.fullscreenElement);document.addEventListener('fullscreenchange',h);return()=>document.removeEventListener('fullscreenchange',h)},[]);
 const rootId='executive-presentation';
 const toggle=async()=>{if(!document.fullscreenElement) await document.getElementById(rootId)?.requestFullscreen(); else await document.exitFullscreen()};
 const distribution=useMemo(()=>{
   const m=new Map<string,number>(); projects.forEach(p=>m.set(p.portfolio||'Other',(m.get(p.portfolio||'Other')||0)+1));
   return [...m].map(([name,value])=>({name,value}));
 },[projects]);
 const perf=useMemo(()=>{
   const avg=projects.length?projects.reduce((a,p)=>a+p.progressPct,0)/projects.length:0;
   return ['Mar','Apr','May','Jun','Jul','Aug'].map((month,i)=>({month,actual:Math.max(0,Math.round(avg-(5-i)*3)),target:Math.max(0,Math.round(avg-(5-i)*2.2))}));
 },[projects]);
 const byCat=useMemo(()=>{
   const m=new Map<string,{category:string,budget:number,spent:number}>();
   budget?.projects.forEach(x=>{const k=x.category||'Other';const r=m.get(k)||{category:k,budget:0,spent:0};r.budget+=x.totalBudget;r.spent+=x.spent;m.set(k,r)});
   return [...m.values()].slice(0,6);
 },[budget]);
 const onTrack=projects.filter(p=>p.healthStatus==='GREEN'||p.healthStatus==='ON_TRACK').length;
 const atRisk=projects.filter(p=>p.healthStatus==='AMBER'||p.healthStatus==='RED'||p.healthStatus==='AT_RISK').length;
 const exportPdf=()=>window.print();
 if(!dash||!budget) return <div className="loading">Loading executive presentation...</div>;
 return <div id={rootId} className="presentation-view">
   <div className="page-title presentation-heading"><div><h1>Presentation Mode</h1><p>Full-screen executive portfolio view for management and BOD meetings</p></div>
    <div className="presentation-actions">{canDownloadPresentation&&<button className="secondary" onClick={exportPdf}><Download size={16}/> Export PDF</button>}<button className="budget-primary" onClick={toggle}>{full?<Minimize2 size={16}/>:<Maximize2 size={16}/>} {full?'Exit Presentation':'Start Presentation'}</button></div>
   </div>
   <section className="presentation-kpis">
    <Kpi label="Portfolio Budget" value={money(budget.totalBudget)} delta={`${money(budget.totalActual)} spent`} icon={<WalletCards/>}/>
    <Kpi label="Active Projects" value={String(dash.activeProjects)} delta={`${dash.totalProjects} total`} icon={<Presentation/>}/>
    <Kpi label="Avg. Completion" value={pct(dash.portfolioProgress)} delta={`${onTrack} on track`} icon={<CheckCircle2/>}/>
    <Kpi label="Supplier KPI" value={`${Math.round(dash.supplierAverageKpi||0)}/100`} delta="Portfolio average" icon={<UsersRound/>}/>
   </section>
   <section className="presentation-grid">
    <article className="presentation-card"><h3>Portfolio Distribution</h3><div className="presentation-chart"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={distribution} dataKey="value" nameKey="name" innerRadius="43%" outerRadius="68%" paddingAngle={2} label={({name,percent})=>`${name}: ${Math.round((percent||0)*100)}%`}><Cell fill="#082d57"/><Cell fill="#e0a000"/><Cell fill="#377dbb"/><Cell fill="#ca8800"/><Cell fill="#2d6598"/></Pie><Tooltip/></PieChart></ResponsiveContainer></div></article>
    <article className="presentation-card"><h3>Performance Trend</h3><div className="presentation-chart"><ResponsiveContainer width="100%" height="100%"><LineChart data={perf} margin={{top:20,right:20,left:0,bottom:0}}><CartesianGrid strokeDasharray="3 3"/><XAxis dataKey="month"/><YAxis domain={[0,100]}/><Tooltip/><Legend/><Line dataKey="actual" name="Actual" stroke="#082d57" strokeWidth={3}/><Line dataKey="target" name="Target" stroke="#d99a00" strokeWidth={2} strokeDasharray="6 4"/></LineChart></ResponsiveContainer></div></article>
   </section>
   <article className="presentation-card presentation-wide"><h3>Budget Allocation vs Spending</h3><div className="presentation-bar"><ResponsiveContainer width="100%" height="100%"><BarChart data={byCat} margin={{top:15,right:20,left:10,bottom:0}}><CartesianGrid strokeDasharray="3 3"/><XAxis dataKey="category"/><YAxis tickFormatter={money}/><Tooltip formatter={(v:any)=>money(Number(v))}/><Legend/><Bar dataKey="budget" name="Budget" fill="#082d57" radius={[3,3,0,0]}/><Bar dataKey="spent" name="Spent" fill="#dca310" radius={[3,3,0,0]}/></BarChart></ResponsiveContainer></div></article>
   <section className="presentation-alerts">
    <div className="exec-callout good"><CheckCircle2/><div><b>{onTrack} Projects On Track</b><span>{dash.totalProjects?Math.round(onTrack/dash.totalProjects*100):0}% of portfolio meeting health targets</span></div></div>
    <div className="exec-callout warn"><TriangleAlert/><div><b>{atRisk} Projects At Risk</b><span>{dash.criticalRisks} critical risks require attention</span></div></div>
    <div className="exec-callout info"><TrendingUp/><div><b>Supplier KPI: {Math.round(dash.supplierAverageKpi||0)}/100</b><span>{dash.suppliersBelowThreshold} supplier(s) below KPI threshold</span></div></div>
   </section>
 </div>
}
function Kpi({label,value,delta,icon}:{label:string;value:string;delta:string;icon:any}){return <div className="presentation-kpi"><div><span>{label}</span><strong>{value}</strong><small><TrendingUp size={13}/>{delta}</small></div>{icon}</div>}
