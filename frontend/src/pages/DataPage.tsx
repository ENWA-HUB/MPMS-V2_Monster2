import { useEffect, useMemo, useState } from 'react';
import { Search, SlidersHorizontal } from 'lucide-react';
import { getJson } from '../lib/api';
type Col={key:string;label:string;format?:'money'|'pct'|'date'|'status'};
export function DataPage({title,path,columns}:{title:string;path:string;columns:Col[]}){
 const [rows,setRows]=useState<any[]>([]); const [q,setQ]=useState('');
 useEffect(()=>{getJson<any[]>(path).then(setRows).catch(()=>setRows([]))},[path]);
 const filtered=useMemo(()=>rows.filter(r=>JSON.stringify(r).toLowerCase().includes(q.toLowerCase())),[rows,q]);
 return <><div className="page-title"><div><h1>{title}</h1><p>{filtered.length} records in the current workspace.</p></div><button className="primary">+ Add</button></div>
  <div className="panel"><div className="toolbar"><div className="table-search"><Search size={17}/><input value={q} onChange={e=>setQ(e.target.value)} placeholder={`Search ${title.toLowerCase()}...`}/></div><button className="secondary"><SlidersHorizontal size={17}/> Filter</button></div>
  <div className="table-wrap"><table><thead><tr>{columns.map(c=><th key={c.key}>{c.label}</th>)}</tr></thead><tbody>{filtered.map((r,i)=><tr key={r.id??i}>{columns.map(c=><td key={c.key}>{render(r[c.key],c.format)}</td>)}</tr>)}</tbody></table></div></div></>
}
function render(v:any,f?:Col['format']){if(v==null||v==='') return '—'; if(f==='money') return new Intl.NumberFormat('en-US',{notation:'compact',maximumFractionDigits:1}).format(Number(v)); if(f==='pct') return <><div className="mini-progress"><i style={{width:`${Number(v)}%`}}/></div><span>{v}%</span></>; if(f==='status') return <span className={`pill ${String(v).toLowerCase()}`}>{String(v).replaceAll('_',' ')}</span>; return String(v)}
