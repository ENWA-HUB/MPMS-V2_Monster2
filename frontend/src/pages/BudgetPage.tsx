import { useEffect, useMemo, useState } from 'react';
import {
  ArrowDownRight, ArrowUpRight, CircleDollarSign, Download, Edit3,
  PieChart as PieIcon, Plus, Trash2, TrendingDown, TrendingUp, X
} from 'lucide-react';
import {
  CartesianGrid, Legend, Line, LineChart, ResponsiveContainer,
  Tooltip, XAxis, YAxis
} from 'recharts';
import { deleteJson, getJson, postJson, putJson } from '../lib/api';

type Overview = {
  totalBudget:number;
  totalActual:number;
  remaining:number;
  utilizationPct:number;
  baselineAmount:number;
  committedAmount:number;
  forecastAmount:number;
  budgetVariance:number;
  forecastVariance:number;
  budgetChangePct:number;
  spendChangePct:number;
  trend:{month:string;budget:number;actual:number}[];
  projects:{
    id:number;
    projectId:number;
    project:string;
    code:string;
    category:string;
    currency:string;
    totalBudget:number;
    committed:number;
    spent:number;
    remaining:number;
    forecast:number;
    utilizationPct:number;
    status:string;
    trend:string;
  }[];
};

type Project={id:number;code:string;name:string;currency:string};

const money=(n:number,currency='VND')=>{
  const symbol=currency==='VND'?'₫':currency==='USD'?'$':currency+' ';
  const abs=Math.abs(n);
  const compact=abs>=1_000_000_000 ? `${(n/1_000_000_000).toFixed(abs>=10_000_000_000?1:2)}B`
    : abs>=1_000_000 ? `${(n/1_000_000).toFixed(abs>=10_000_000?1:2)}M`
    : abs>=1_000 ? `${(n/1_000).toFixed(0)}K`
    : new Intl.NumberFormat('en-US').format(n);
  return currency==='VND'?`${compact} ${symbol}`:`${symbol}${compact}`;
};

function Trend({value,label,inverse=false}:{value:number;label:string;inverse?:boolean}){
  const positive=inverse?value<=0:value>=0;
  const Icon=positive?TrendingUp:TrendingDown;
  return <small className={positive?'budget-trend good':'budget-trend bad'}>
    <Icon size={13}/>{value>=0?'+':''}{value.toFixed(1)}% <span>{label}</span>
  </small>
}

export function BudgetPage(){
  const [data,setData]=useState<Overview|null>(null);
  const [projects,setProjects]=useState<Project[]>([]);
  const [open,setOpen]=useState(false);
  const [saving,setSaving]=useState(false);
  const [error,setError]=useState('');
  const [form,setForm]=useState({
    projectId:'', code:'', category:'IMPLEMENTATION', description:'',
    baselineAmount:'', revisedAmount:'', committedAmount:'0',
    actualAmount:'0', forecastAmount:'', currency:'VND'
  });

  const load=async()=>{
    const [o,p]=await Promise.all([
      getJson<Overview>('/budget/overview'),
      getJson<Project[]>('/projects')
    ]);
    setData(o); setProjects(p);
  };

  useEffect(()=>{load().catch(e=>setError(String(e)))},[]);

  const currency=data?.projects[0]?.currency || 'VND';
  const maxUtil=useMemo(()=>Math.max(100,...(data?.projects.map(x=>x.utilizationPct)||[100])),[data]);

  const submit=async(e:React.FormEvent)=>{
    e.preventDefault(); setSaving(true); setError('');
    try{
      const revised=Number(form.revisedAmount||form.baselineAmount||0);
      await postJson('/budgets',{
        projectId:Number(form.projectId),
        code:form.code.trim(),
        category:form.category.trim(),
        description:form.description.trim(),
        baselineAmount:Number(form.baselineAmount||0),
        revisedAmount:revised,
        committedAmount:Number(form.committedAmount||0),
        actualAmount:Number(form.actualAmount||0),
        forecastAmount:Number(form.forecastAmount||revised),
        currency:form.currency
      });
      setOpen(false);
      setForm({projectId:'',code:'',category:'IMPLEMENTATION',description:'',baselineAmount:'',revisedAmount:'',committedAmount:'0',actualAmount:'0',forecastAmount:'',currency:'VND'});
      await load();
    }catch(e){setError(String(e))}
    finally{setSaving(false)}
  };


  const editBudget=async(row:Overview['projects'][number])=>{const revised=prompt('Revised budget',String(row.totalBudget));if(revised===null)return;const actual=prompt('Actual spent',String(row.spent));if(actual===null)return;const forecast=prompt('Forecast',String(row.forecast));if(forecast===null)return;try{await putJson(`/budgets/${row.id}`,{projectId:row.projectId,code:row.code,category:row.category,description:'',baselineAmount:Number(revised),revisedAmount:Number(revised),committedAmount:row.committed,actualAmount:Number(actual),forecastAmount:Number(forecast),currency:row.currency});await load()}catch(e){setError(String(e))}};
  const deleteBudget=async(row:Overview['projects'][number])=>{if(!confirm(`Delete budget line ${row.code}?`))return;try{await deleteJson(`/budgets/${row.id}`);await load()}catch(e){setError(String(e))}};

  const exportCsv=()=>{
    if(!data) return;
    const headers=['Project','Code','Category','Budget','Committed','Spent','Remaining','Forecast','Utilization','Status'];
    const rows=data.projects.map(x=>[
      x.project,x.code,x.category,x.totalBudget,x.committed,x.spent,x.remaining,x.forecast,
      `${x.utilizationPct.toFixed(1)}%`,x.status
    ]);
    const csv=[headers,...rows].map(r=>r.map(v=>`"${String(v).replaceAll('"','""')}"`).join(',')).join('\n');
    const blob=new Blob([csv],{type:'text/csv;charset=utf-8'});
    const a=document.createElement('a');
    a.href=URL.createObjectURL(blob);
    a.download=`MAIPT-Budget-Report-${new Date().toISOString().slice(0,10)}.csv`;
    a.click(); URL.revokeObjectURL(a.href);
  };

  if(!data) return <div className="loading">{error||'Loading budget management...'}</div>;

  return <>
    <div className="page-title budget-title">
      <div>
        <h1>Budget Management</h1>
        <p>Track and manage project budgets, commitments, actual spending and forecast.</p>
      </div>
      <div className="budget-actions">
        <button className="secondary" onClick={exportCsv}><Download size={16}/> Export Report</button>
        <button className="budget-primary" onClick={()=>setOpen(true)}><Plus size={17}/> New Budget</button>
      </div>
    </div>

    {error&&<div className="budget-error">{error}</div>}

    <section className="budget-kpis">
      <div className="budget-kpi-card">
        <div><span>Total Budget</span><strong>{money(data.totalBudget,currency)}</strong>
          <Trend value={data.budgetChangePct} label="vs baseline"/>
        </div><CircleDollarSign/>
      </div>
      <div className="budget-kpi-card">
        <div><span>Total Spent</span><strong>{money(data.totalActual,currency)}</strong>
          <Trend value={data.spendChangePct} label="vs prior month"/>
        </div><TrendingUp/>
      </div>
      <div className="budget-kpi-card">
        <div><span>Remaining</span><strong>{money(data.remaining,currency)}</strong>
          <Trend value={data.totalBudget?-(data.totalActual/data.totalBudget*100):0} label="consumed" inverse/>
        </div><ArrowDownRight/>
      </div>
      <div className="budget-kpi-card">
        <div><span>Utilization</span><strong>{data.utilizationPct.toFixed(0)}%</strong>
          <small className="budget-trend neutral"><PieIcon size={13}/>{money(data.committedAmount,currency)} committed</small>
        </div><PieIcon/>
      </div>
    </section>

    <section className="budget-panel budget-chart-panel">
      <div className="budget-panel-head">
        <div><h3>Budget vs Actual Spending</h3><p>Cumulative portfolio budget and posted actuals — last 6 months</p></div>
        <div className="budget-variance">
          <span>Forecast variance</span>
          <b className={data.forecastVariance>=0?'positive':'negative'}>{money(data.forecastVariance,currency)}</b>
        </div>
      </div>
      <div className="budget-chart">
        <ResponsiveContainer width="100%" height={310}>
          <LineChart data={data.trend} margin={{top:12,right:20,left:8,bottom:0}}>
            <CartesianGrid strokeDasharray="3 3" vertical={true} stroke="#dbe1e8"/>
            <XAxis dataKey="month" tickLine={false} axisLine={{stroke:'#9aa5b1'}}/>
            <YAxis tickLine={false} axisLine={{stroke:'#9aa5b1'}} tickFormatter={(v)=>money(Number(v),currency).replace(' ₫','')}/>
            <Tooltip formatter={(v:any,n:any)=>[money(Number(v),currency),n==='budget'?'Budget':'Actual']} contentStyle={{border:'1px solid #d8dee6',borderRadius:4}}/>
            <Legend formatter={(v)=>v==='budget'?'Budget':'Actual'}/>
            <Line type="monotone" dataKey="budget" stroke="#082d57" strokeWidth={2.2} dot={{r:3,fill:'#fff',strokeWidth:2}} activeDot={{r:5}}/>
            <Line type="monotone" dataKey="actual" stroke="#d79a00" strokeWidth={2.2} dot={{r:3,fill:'#fff',strokeWidth:2}} activeDot={{r:5}}/>
          </LineChart>
        </ResponsiveContainer>
      </div>
    </section>

    <section className="budget-panel">
      <div className="budget-panel-head">
        <div><h3>Budget Breakdown by Project</h3><p>Revised budget, actual spending and utilization by project budget line.</p></div>
      </div>
      <div className="budget-table-wrap">
        <table className="budget-table">
          <thead><tr>
            <th>Project</th><th>Category</th><th>Total Budget</th><th>Spent</th>
            <th>Remaining</th><th>Utilization</th><th>Status</th><th>Trend</th><th>Actions</th>
          </tr></thead>
          <tbody>
          {data.projects.map(row=><tr key={row.id}>
            <td><b className="budget-project">{row.project}</b><small>{row.code}</small></td>
            <td>{titleCase(row.category)}</td>
            <td><b>{money(row.totalBudget,row.currency)}</b></td>
            <td>{money(row.spent,row.currency)}</td>
            <td>{money(row.remaining,row.currency)}</td>
            <td>
              <div className="budget-util">
                <i><span style={{width:`${Math.min(100,row.utilizationPct)}%`}}/></i>
                <em>{row.utilizationPct.toFixed(0)}%</em>
              </div>
            </td>
            <td><span className={`budget-status ${row.status.toLowerCase().replaceAll(' ','-')}`}>{row.status}</span></td>
            <td>{row.trend==='UP'?<ArrowUpRight className="trend-up" size={18}/>:<ArrowDownRight className="trend-down" size={18}/>}</td><td><div className="row-actions"><button onClick={()=>editBudget(row)}><Edit3/></button><button className="danger" onClick={()=>deleteBudget(row)}><Trash2/></button></div></td>
          </tr>)}
          </tbody>
        </table>
      </div>
    </section>

    {open&&<div className="budget-modal-backdrop" onMouseDown={()=>setOpen(false)}>
      <div className="budget-modal" onMouseDown={e=>e.stopPropagation()}>
        <div className="budget-modal-head">
          <div><h3>New Budget Line</h3><p>Create a project budget line with baseline, revised and forecast values.</p></div>
          <button onClick={()=>setOpen(false)}><X size={20}/></button>
        </div>
        <form onSubmit={submit}>
          <label>Project<select required value={form.projectId} onChange={e=>setForm({...form,projectId:e.target.value,currency:projects.find(p=>p.id===Number(e.target.value))?.currency||'VND'})}>
            <option value="">Select project</option>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}
          </select></label>
          <div className="budget-form-grid">
            <label>Budget code<input required placeholder="BL-003" value={form.code} onChange={e=>setForm({...form,code:e.target.value})}/></label>
            <label>Category<input required placeholder="IMPLEMENTATION" value={form.category} onChange={e=>setForm({...form,category:e.target.value})}/></label>
          </div>
          <label>Description<input placeholder="Budget description" value={form.description} onChange={e=>setForm({...form,description:e.target.value})}/></label>
          <div className="budget-form-grid">
            <label>Baseline amount<input required type="number" min="0" value={form.baselineAmount} onChange={e=>setForm({...form,baselineAmount:e.target.value})}/></label>
            <label>Revised amount<input type="number" min="0" value={form.revisedAmount} onChange={e=>setForm({...form,revisedAmount:e.target.value})}/></label>
            <label>Committed<input type="number" min="0" value={form.committedAmount} onChange={e=>setForm({...form,committedAmount:e.target.value})}/></label>
            <label>Actual<input type="number" min="0" value={form.actualAmount} onChange={e=>setForm({...form,actualAmount:e.target.value})}/></label>
            <label>Forecast<input type="number" min="0" value={form.forecastAmount} onChange={e=>setForm({...form,forecastAmount:e.target.value})}/></label>
            <label>Currency<select value={form.currency} onChange={e=>setForm({...form,currency:e.target.value})}><option>VND</option><option>USD</option></select></label>
          </div>
          <div className="budget-modal-actions">
            <button type="button" className="secondary" onClick={()=>setOpen(false)}>Cancel</button>
            <button type="submit" className="budget-primary" disabled={saving}>{saving?'Saving...':'Create Budget'}</button>
          </div>
        </form>
      </div>
    </div>}
  </>;
}

function titleCase(s:string){return s.toLowerCase().replaceAll('_',' ').replace(/\b\w/g,c=>c.toUpperCase())}
