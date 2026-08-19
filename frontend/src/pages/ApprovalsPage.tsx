import { useEffect, useState } from 'react';
import { Check, X } from 'lucide-react';
import { getJson, postJson } from '../lib/api';
export function ApprovalsPage(){
 const[rows,setRows]=useState<any[]>([]),[error,setError]=useState('');
 const load=()=>getJson<any[]>('/kpi-approvals').then(setRows).catch(e=>setError(String(e))); useEffect(()=>{load()},[]);
 const act=async(id:number,d:'approved'|'rejected')=>{const comment=prompt(d==='approved'?'Approval comment (optional)':'Reason for rejection')??'';if(d==='rejected'&&!comment.trim()){alert('Please enter a reason for rejection.');return;}try{await postJson(`/kpi-approvals/${id}/${d}`,{comment});await load()}catch(e){setError(String(e))}};
 return <><div className="page-title"><div><h1>Approvals</h1><p>KPI requests assigned to you. All selected approvers must approve; any rejection returns the KPI to the member.</p></div></div>{error&&<div className="budget-error">{error}</div>}<div className="panel"><div className="approval-list">{rows.map(r=><div className="approval-row" key={r.id}><div><b>{r.employeeName||'KPI'} · {r.period||''}</b><span>{r.department||''} · requested {new Date(r.requestedAt).toLocaleString()}</span><small>{r.steps.map((s:any)=>`${s.approver}: ${s.status}${s.comment?` — ${s.comment}`:''}`).join(' · ')}</small></div><span className={`pill ${String(r.status).toLowerCase()}`}>{r.status}</span>{r.canAct&&<div className="approval-actions"><button className="approve" onClick={()=>act(r.id,'approved')}><Check size={16}/>Approve</button><button className="reject" onClick={()=>act(r.id,'rejected')}><X size={16}/>Reject</button></div>}</div>)}</div></div></>;
}
