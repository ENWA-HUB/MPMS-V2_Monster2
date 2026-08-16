import { useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle, Banknote, CalendarClock, CheckCircle2, CircleDollarSign, Clock3,
  Gauge, Handshake, RefreshCw, ShieldAlert, Target, UsersRound, WalletCards
} from 'lucide-react';
import {
  Bar, BarChart, CartesianGrid, Cell, Legend, Pie, PieChart, ResponsiveContainer,
  Tooltip, XAxis, YAxis
} from 'recharts';
import { getJson } from '../lib/api';

type Summary={
  totalProjects:number;activeProjects:number;portfolioProgress:number;greenProjects:number;amberProjects:number;redProjects:number;
  annualBudgetPlan:number;annualBudgetLinked:number;revisedBudget:number;actualCost:number;forecastCost:number;
  openRisks:number;criticalRisks:number;openIssues:number;overdueTasks:number;overdueMilestones:number;
  supplierScore:number;teamPerformanceScore:number;teamPeriods:number;projectTeamMembers:number;
};
type Executive={
  generatedAt:string;selectedBudgetYear:number;availableBudgetYears:number[];performancePeriod:string;summary:Summary;
  projectHealth:any[];riskWatch:any[];issueWatch:any[];nextMilestones:any[];annualBudgetByUnit:any[];
  projectStatus:any[];health:any[];teamScores:any[];attention:any[];
};

const money=(n:number)=>n>=1e9?`${(n/1e9).toFixed(1)}B ₫`:n>=1e6?`${(n/1e6).toFixed(1)}M ₫`:`${new Intl.NumberFormat('en-US').format(n)} ₫`;
const nice=(s:string)=>String(s||'').toLowerCase().replaceAll('_',' ').replace(/\b\w/g,c=>c.toUpperCase());
const monthValue=()=>new Date().toISOString().slice(0,7);

export function Dashboard(){
 const [data,setData]=useState<Executive|null>(null);
 const [year,setYear]=useState<number>(new Date().getFullYear());
 const [period,setPeriod]=useState(monthValue());
 const [error,setError]=useState('');
 const [loading,setLoading]=useState(true);

 const load=async(y=year,p=period)=>{
  setLoading(true);setError('');
  try{
   const d=await getJson<Executive>(`/dashboard/executive?budgetYear=${y}&performancePeriod=${encodeURIComponent(p)}`);
   setData(d);
   if(!d.availableBudgetYears.includes(y)&&d.selectedBudgetYear)setYear(d.selectedBudgetYear);
  }catch(e){setError(String(e))}finally{setLoading(false)}
 };
 useEffect(()=>{load(year,period)},[year,period]);

 const s=data?.summary;
 const utilization=s&&s.annualBudgetPlan>0?s.actualCost/s.annualBudgetPlan*100:0;
 const forecastVsPlan=s&&s.annualBudgetPlan>0?s.forecastCost/s.annualBudgetPlan*100:0;
 const healthColors=['#16a34a','#d6a125','#dc2626'];

 if(loading&&!data)return <div className="loading">Loading executive dashboard…</div>;
 if(!data||!s)return <div className="loading">{error||'Dashboard data is unavailable.'}</div>;

 return <>
  <div className="page-title dashboard-title">
   <div><h1>Executive Dashboard</h1><p>Portfolio delivery, annual budget, risk, suppliers and team performance in one management view.</p></div>
   <div className="dashboard-filters">
    <label>Budget Year<select value={year} onChange={e=>setYear(Number(e.target.value))}>{Array.from(new Set([year,...data.availableBudgetYears])).sort((a,b)=>b-a).map(y=><option key={y}>{y}</option>)}</select></label>
    <label>KPI Period<input type="month" value={period} onChange={e=>setPeriod(e.target.value)}/></label>
    <button className="secondary" onClick={()=>load()}><RefreshCw size={15}/> Refresh</button>
   </div>
  </div>
  {error&&<div className="budget-error">{error}</div>}

  <section className="exec-kpi-grid">
   <Kpi icon={<Target/>} label="Active Projects" value={`${s.activeProjects}/${s.totalProjects}`} sub={`${s.portfolioProgress}% portfolio completion`} tone="navy"/>
   <Kpi icon={<WalletCards/>} label={`${data.selectedBudgetYear} Budget Plan`} value={money(s.annualBudgetPlan)} sub={`${money(s.annualBudgetLinked)} mapped to projects`} tone="gold"/>
   <Kpi icon={<CircleDollarSign/>} label="Actual Cost" value={money(s.actualCost)} sub={`${utilization.toFixed(1)}% of annual plan`} tone={utilization>100?'red':'navy'}/>
   <Kpi icon={<Banknote/>} label="Forecast" value={money(s.forecastCost)} sub={`${forecastVsPlan.toFixed(1)}% of annual plan`} tone={forecastVsPlan>100?'red':'gold'}/>
   <Kpi icon={<ShieldAlert/>} label="Critical Risks" value={`${s.criticalRisks}`} sub={`${s.openRisks} open risks · ${s.openIssues} issues`} tone={s.criticalRisks?'red':'green'}/>
   <Kpi icon={<Gauge/>} label="Team Performance" value={s.teamPerformanceScore?`${s.teamPerformanceScore.toFixed(2)}/5`:'—'} sub={`${s.teamPeriods} period(s) in ${data.performancePeriod}`} tone="green"/>
   <Kpi icon={<Handshake/>} label="Supplier KPI" value={s.supplierScore?`${s.supplierScore.toFixed(2)}/5`:'—'} sub="Average approved evaluation" tone="navy"/>
   <Kpi icon={<UsersRound/>} label="Project Team" value={`${s.projectTeamMembers}`} sub="Active members allocated to projects" tone="gold"/>
  </section>

  <section className="exec-dashboard-grid two">
   <article className="exec-panel">
    <div className="exec-panel-head"><div><h3>Portfolio Health</h3><p>Project health distribution</p></div><div className="health-summary"><span className="green">{s.greenProjects} Green</span><span className="amber">{s.amberProjects} Amber</span><span className="red">{s.redProjects} Red</span></div></div>
    <div className="exec-chart pie"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={data.health} dataKey="value" nameKey="name" innerRadius="50%" outerRadius="72%" paddingAngle={3}>{data.health.map((_:any,i:number)=><Cell key={i} fill={healthColors[i]}/>)}</Pie><Tooltip/><Legend/></PieChart></ResponsiveContainer></div>
   </article>
   <article className="exec-panel">
    <div className="exec-panel-head"><div><h3>Annual Budget by Business Unit</h3><p>Top units · {data.selectedBudgetYear}</p></div></div>
    <div className="exec-chart"><ResponsiveContainer width="100%" height="100%"><BarChart data={data.annualBudgetByUnit} layout="vertical" margin={{left:12,right:20}}><CartesianGrid strokeDasharray="3 3"/><XAxis type="number" tickFormatter={v=>`${(Number(v)/1e9).toFixed(0)}B`}/><YAxis type="category" dataKey="name" width={105} tick={{fontSize:9}}/><Tooltip formatter={(v:any)=>money(Number(v))}/><Bar dataKey="amount" name="Plan" fill="#0b3158" radius={[0,4,4,0]}/></BarChart></ResponsiveContainer></div>
   </article>
  </section>

  <section className="exec-dashboard-grid main">
   <article className="exec-panel project-health-panel">
    <div className="exec-panel-head"><div><h3>Project Delivery Watch</h3><p>Projects requiring management visibility</p></div></div>
    <div className="exec-project-table">
     <div className="exec-project-head"><span>Project</span><span>Owner</span><span>Team</span><span>Progress</span><span>Health</span></div>
     {data.projectHealth.map((p:any)=><div className="exec-project-row" key={p.id}>
      <div><b>{p.name}</b><small>{p.code} · {p.portfolio}</small></div><span>{p.owner}</span><span>{p.teamSize}</span>
      <div className="exec-progress"><i><em style={{width:`${Math.min(100,p.progressPct)}%`}}/></i><b>{p.progressPct}%</b></div>
      <span className={`health-chip ${String(p.healthStatus).toLowerCase()}`}>{nice(p.healthStatus)}</span>
     </div>)}
    </div>
   </article>
   <article className="exec-panel attention-panel">
    <div className="exec-panel-head"><div><h3>Management Attention</h3><p>Items requiring action</p></div></div>
    <div className="attention-list">
     {data.attention.length?data.attention.map((a:any,i:number)=><div className={`attention-item ${String(a.severity).toLowerCase()}`} key={i}><AlertTriangle/><div><b>{a.title}</b><span>{a.detail}</span></div></div>):<div className="all-clear"><CheckCircle2/><b>No critical management alerts</b></div>}
    </div>
   </article>
  </section>

  <section className="exec-dashboard-grid three">
   <article className="exec-panel compact">
    <div className="exec-panel-head"><div><h3>Risk Watch</h3><p>Highest exposure first</p></div></div>
    <div className="watch-list">{data.riskWatch.map((r:any)=><div className="watch-row" key={r.id}><strong className={r.severityScore>=17?'critical':r.severityScore>=10?'high':'medium'}>{r.severityScore}</strong><div><b>{r.title}</b><span>{r.project} · {r.category}</span></div><em>{nice(r.status)}</em></div>)}</div>
   </article>
   <article className="exec-panel compact">
    <div className="exec-panel-head"><div><h3>Upcoming Milestones</h3><p>Schedule watch</p></div></div>
    <div className="watch-list">{data.nextMilestones.map((m:any)=><div className="watch-row milestone" key={m.id}><CalendarClock/><div><b>{m.name}</b><span>{m.project}</span></div><em className={m.overdue?'overdue':''}>{m.dueDate||'TBD'}</em></div>)}</div>
   </article>
   <article className="exec-panel compact">
    <div className="exec-panel-head"><div><h3>Team Performance</h3><p>{data.performancePeriod}</p></div></div>
    <div className="watch-list">{data.teamScores.length?data.teamScores.slice(0,6).map((t:any)=><div className="watch-row team" key={t.id}><div className="team-score">{t.score? t.score.toFixed(1):'—'}</div><div><b>{t.employeeName||t.department}</b><span>{t.level} · {Math.round(t.weight*100)}% weight</span></div><em>{t.isLocked?'Locked':'Open'}</em></div>):<div className="empty-dashboard">No performance period for this month.</div>}</div>
   </article>
  </section>

  <section className="exec-finance-strip">
   <div><span>Annual Plan</span><b>{money(s.annualBudgetPlan)}</b></div>
   <div><span>Revised Control Budget</span><b>{money(s.revisedBudget)}</b></div>
   <div><span>Actual Cost</span><b>{money(s.actualCost)}</b></div>
   <div><span>Forecast</span><b>{money(s.forecastCost)}</b></div>
   <div><span>Overdue Tasks / Milestones</span><b>{s.overdueTasks} / {s.overdueMilestones}</b></div>
  </section>
 </>;
}

function Kpi({icon,label,value,sub,tone}:{icon:any;label:string;value:string;sub:string;tone:'navy'|'gold'|'green'|'red'}){
 return <div className={`exec-kpi ${tone}`}><div className="exec-kpi-icon">{icon}</div><div><span>{label}</span><b>{value}</b><small>{sub}</small></div></div>
}
