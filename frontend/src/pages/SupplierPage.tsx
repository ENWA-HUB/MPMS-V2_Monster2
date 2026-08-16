import { useEffect, useMemo, useState } from 'react';
import { Award, Download, Edit3, Medal, MoreVertical, Plus, Trash2, Trophy, TrendingDown, TrendingUp, X } from 'lucide-react';
import {
  Legend, PolarAngleAxis, PolarGrid, PolarRadiusAxis, Radar, RadarChart,
  ResponsiveContainer, Tooltip
} from 'recharts';
import { deleteJson, getJson, postJson, putJson } from '../lib/api';

type SupplierRow={
  id:number; code:string; name:string; category:string; contactName:string; email:string;
  status:string; rating:number; contractValue:number; currency:string;
  kpiScore:number; trend:number; rank:number;
};
type RadarRow={criterion:string;[key:string]:string|number};
type SupplierOverview={
  suppliers:SupplierRow[];
  radar:{suppliers:string[];data:RadarRow[]};
};

const money=(n:number,c='VND')=>{
  const compact=n>=1_000_000_000?`${(n/1_000_000_000).toFixed(1)}B`:n>=1_000_000?`${(n/1_000_000).toFixed(1)}M`:n>=1_000?`${(n/1_000).toFixed(0)}K`:new Intl.NumberFormat('en-US').format(n);
  return c==='VND'?`${compact} ₫`:c==='USD'?`$${compact}`:`${compact} ${c}`;
};

export function SupplierPage(){
  const [data,setData]=useState<SupplierOverview|null>(null);
  const [open,setOpen]=useState(false);
  const [saving,setSaving]=useState(false);
  const [error,setError]=useState('');
  const [form,setForm]=useState({code:'',name:'',taxCode:'',category:'IT Services',contactName:'',email:'',phone:'',address:'',status:'ACTIVE',rating:'0'});

  const load=()=>getJson<SupplierOverview>('/suppliers/overview').then(setData).catch(e=>setError(String(e)));
  useEffect(()=>{load()},[]);

  const top=useMemo(()=>data?.suppliers.slice(0,6)||[],[data]);

  const submit=async(e:React.FormEvent)=>{
    e.preventDefault(); setSaving(true); setError('');
    try{
      await postJson('/suppliers',{
        orgUnitId:1, code:form.code.trim(), name:form.name.trim(), taxCode:form.taxCode.trim(),
        category:form.category.trim(), contactName:form.contactName.trim(), email:form.email.trim(),
        phone:form.phone.trim(), address:form.address.trim(), status:form.status, rating:Number(form.rating||0)
      });
      setOpen(false);
      setForm({code:'',name:'',taxCode:'',category:'IT Services',contactName:'',email:'',phone:'',address:'',status:'ACTIVE',rating:'0'});
      await load();
    }catch(e){setError(String(e))} finally{setSaving(false)}
  };


  const editSupplier=async(s:SupplierRow)=>{const name=prompt('Supplier name',s.name);if(name===null)return;const status=prompt('Status',s.status);if(status===null)return;const rating=prompt('Rating',String(s.rating||0));try{await putJson(`/suppliers/${s.id}`,{...s,name,status,rating:Number(rating||0),orgUnitId:1});await load()}catch(e){setError(String(e))}};
  const deleteSupplier=async(s:SupplierRow)=>{if(!confirm(`Delete supplier “${s.name}” and related contracts/evaluations?`))return;try{await deleteJson(`/suppliers/${s.id}`);await load()}catch(e){setError(String(e))}};

  const exportCsv=()=>{
    if(!data) return;
    const rows=[['Supplier','Category','Contact','Email','Contract Value','KPI Score','Rating','Status'],
      ...data.suppliers.map(x=>[x.name,x.category,x.contactName,x.email,x.contractValue,x.kpiScore,x.rating,x.status])];
    const csv=rows.map(r=>r.map(v=>`"${String(v).replaceAll('"','""')}"`).join(',')).join('\n');
    const blob=new Blob([csv],{type:'text/csv;charset=utf-8'}); const a=document.createElement('a');
    a.href=URL.createObjectURL(blob); a.download=`MAIPT-Supplier-Report-${new Date().toISOString().slice(0,10)}.csv`; a.click(); URL.revokeObjectURL(a.href);
  };

  if(!data) return <div className="loading">{error||'Loading supplier management...'}</div>;

  const radarColors=['#082d57','#dca310','#377dbb'];

  return <>
    <div className="page-title supplier-title">
      <div><h1>Supplier Management</h1><p>Manage suppliers, contracts, and performance metrics</p></div>
      <div className="supplier-actions">
        <button className="secondary" onClick={exportCsv}><Download size={16}/> Export Report</button>
        <button className="budget-primary" onClick={()=>setOpen(true)}><Plus size={17}/> Add Supplier</button>
      </div>
    </div>
    {error&&<div className="budget-error">{error}</div>}

    <section className="supplier-top-grid">
      <article className="supplier-panel">
        <h3>Supplier Performance Comparison</h3>
        <div className="supplier-radar">
          {data.radar.data.length?
          <ResponsiveContainer width="100%" height="100%">
            <RadarChart data={data.radar.data} outerRadius="66%">
              <PolarGrid/><PolarAngleAxis dataKey="criterion"/><PolarRadiusAxis domain={[0,100]} tickCount={5}/>
              <Tooltip/><Legend/>
              {data.radar.suppliers.slice(0,3).map((name,i)=><Radar key={name} name={name} dataKey={name} stroke={radarColors[i]} fill={radarColors[i]} fillOpacity={i===0?.28:.12}/>)}
            </RadarChart>
          </ResponsiveContainer>:<div className="empty-mini">No approved KPI score detail yet.</div>}
        </div>
      </article>

      <article className="supplier-panel supplier-ranking-panel">
        <h3>Supplier Rankings</h3>
        <div className="supplier-rankings">
          {top.map((s,i)=><div className="supplier-rank-row" key={s.id}>
            <div className={`rank-badge rank-${i+1}`}>{i===0?<Trophy/>:i===1?<Medal/>:i===2?<Award/>:`#${i+1}`}</div>
            <div className="rank-main"><b>{s.name}</b><span>{s.category||'General'}</span></div>
            <div className={s.trend>=0?'rank-change up':'rank-change down'}>{s.trend>=0?'+':''}{s.trend.toFixed(0)}</div>
            <div className="rank-score"><strong>{Math.round(s.kpiScore)}</strong><span>KPI Score</span></div>
          </div>)}
        </div>
      </article>
    </section>

    <section className="supplier-panel supplier-list-panel">
      <div className="supplier-panel-head"><h3>Active Suppliers</h3><span>{data.suppliers.filter(x=>x.status==='ACTIVE').length} active / {data.suppliers.length} total</span></div>
      <div className="supplier-table-wrap">
        <table className="supplier-table"><thead><tr>
          <th>Supplier Name</th><th>Category</th><th>Contact</th><th>Contract Value</th><th>Status</th><th>KPI Score</th><th>Rating</th><th>Trend</th><th></th>
        </tr></thead><tbody>
        {data.suppliers.map(s=><tr key={s.id}>
          <td><b>{s.name}</b><small>{s.code}</small></td>
          <td>{s.category||'—'}</td>
          <td><b className="contact-name">{s.contactName||'—'}</b><small>{s.email||'—'}</small></td>
          <td><b>{money(s.contractValue,s.currency)}</b></td>
          <td><span className={`supplier-status ${s.status.toLowerCase()}`}>{titleCase(s.status)}</span></td>
          <td><strong className={s.kpiScore>=80?'score-good':s.kpiScore>=60?'score-mid':'score-bad'}>{Math.round(s.kpiScore)}</strong></td>
          <td><span className="supplier-rating">★ {s.rating? s.rating.toFixed(1):'—'}</span></td>
          <td>{s.trend>=0?<TrendingUp className="trend-up" size={18}/>:<TrendingDown className="trend-down" size={18}/>}</td>
          <td><div className="row-actions"><button onClick={()=>editSupplier(s)}><Edit3/></button><button className="danger" onClick={()=>deleteSupplier(s)}><Trash2/></button></div></td>
        </tr>)}
        </tbody></table>
      </div>
    </section>

    {open&&<div className="budget-modal-backdrop" onMouseDown={()=>setOpen(false)}>
      <div className="budget-modal supplier-modal" onMouseDown={e=>e.stopPropagation()}>
        <div className="budget-modal-head"><div><h3>Add Supplier</h3><p>Create supplier master data. KPI and contract values are calculated from transactions.</p></div><button onClick={()=>setOpen(false)}><X/></button></div>
        <form onSubmit={submit}>
          <div className="budget-form-grid">
            <label>Supplier code<input required value={form.code} onChange={e=>setForm({...form,code:e.target.value})} placeholder="SUP-003"/></label>
            <label>Supplier name<input required value={form.name} onChange={e=>setForm({...form,name:e.target.value})}/></label>
            <label>Tax code<input value={form.taxCode} onChange={e=>setForm({...form,taxCode:e.target.value})}/></label>
            <label>Category<input value={form.category} onChange={e=>setForm({...form,category:e.target.value})}/></label>
            <label>Contact name<input value={form.contactName} onChange={e=>setForm({...form,contactName:e.target.value})}/></label>
            <label>Email<input type="email" value={form.email} onChange={e=>setForm({...form,email:e.target.value})}/></label>
            <label>Phone<input value={form.phone} onChange={e=>setForm({...form,phone:e.target.value})}/></label>
            <label>Status<select value={form.status} onChange={e=>setForm({...form,status:e.target.value})}><option>ACTIVE</option><option>QUALIFIED</option><option>SUSPENDED</option><option>INACTIVE</option></select></label>
          </div>
          <label>Address<input value={form.address} onChange={e=>setForm({...form,address:e.target.value})}/></label>
          <div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setOpen(false)}>Cancel</button><button className="budget-primary" disabled={saving}>{saving?'Saving...':'Add Supplier'}</button></div>
        </form>
      </div>
    </div>}
  </>;
}
function titleCase(s:string){return s.toLowerCase().replaceAll('_',' ').replace(/\b\w/g,c=>c.toUpperCase())}
