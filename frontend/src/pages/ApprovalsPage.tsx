import { useEffect, useMemo, useState } from 'react';
import { Check, Edit3, Save, Send, X } from 'lucide-react';
import { getJson, postJson } from '../lib/api';

type ApprovalRow={
  id:number;status:string;employeeName?:string;period?:string;department?:string;
  level?:string;approvalType?:'KPI'|'BUDGET_PLAN';requestedAt:string;canAct:boolean;steps:any[];
};

type Review={
  approvalType?:'KPI'|'BUDGET_PLAN';
  request:any;
  period:any;
  scoreField:'MANAGER'|'HOD'|'NONE';
  summary?:{budgetYear:number;orgUnit:string;itemCount:number;totalPlanned:number};
  items:any[];
};

const money=(n:number)=>new Intl.NumberFormat('vi-VN',{maximumFractionDigits:0}).format(Number(n||0))+' ₫';

export function ApprovalsPage(){
 const [rows,setRows]=useState<ApprovalRow[]>([]);
 const [error,setError]=useState('');
 const [permissionNotice,setPermissionNotice]=useState('');
 const [review,setReview]=useState<Review|null>(null);
 const [reviewId,setReviewId]=useState<number|null>(null);
 const [scores,setScores]=useState<Record<number,string>>({});
 const [saving,setSaving]=useState(false);
 const [scoresSaved,setScoresSaved]=useState(false);
 const [reviewComment,setReviewComment]=useState('');
 const today=new Date();
 const [filterYear,setFilterYear]=useState(String(today.getFullYear()));
 const [filterMonth,setFilterMonth]=useState(String(today.getMonth()+1).padStart(2,'0'));
 const [filterMember,setFilterMember]=useState('');
 const [filterUnit,setFilterUnit]=useState('');
 const [filterLevel,setFilterLevel]=useState('');
 const [filterStatus,setFilterStatus]=useState('');

 const showFailure=(e:unknown)=>{
   const message=String(e||'');
   if(/(?:403|permission denied|not the requester|not (?:an? |the )?assigned approver|forbidden)/i.test(message)){
     setError('');
     setPermissionNotice('You do not have permission to view or process this approval request.');
     return;
   }
   setPermissionNotice('');
   setError(message);
 };
 const load=()=>getJson<ApprovalRow[]>('/kpi-approvals').then(x=>{setRows(x);setPermissionNotice('')}).catch(showFailure);
 useEffect(()=>{load()},[]);

 const rowLevel=(r:ApprovalRow)=>{
   if(r.level)return String(r.level).toUpperCase();
   if(/^\[UPF\]/i.test(r.employeeName||''))return 'DEPARTMENT';
   if(/^\[(?:PRJ|PROJECT)\]/i.test(r.employeeName||''))return 'PROJECT';
   return 'INDIVIDUAL';
 };
 const memberOptions=useMemo(()=>Array.from(new Set(rows.map(r=>r.employeeName||'').filter(Boolean))).sort((a,b)=>a.localeCompare(b)),[rows]);
 const unitOptions=useMemo(()=>Array.from(new Set(rows.map(r=>r.department||'').filter(Boolean))).sort((a,b)=>a.localeCompare(b)),[rows]);
 const yearOptions=useMemo(()=>Array.from(new Set([String(today.getFullYear()),...rows.map(r=>(r.period||'').slice(0,4)).filter(y=>/^\d{4}$/.test(y))])).sort((a,b)=>Number(b)-Number(a)),[rows]);
 const filteredRows=useMemo(()=>rows.filter(r=>{
   const period=String(r.period||'');
   const member=String(r.employeeName||'');
   return (!filterYear||period.slice(0,4)===filterYear)
     &&(!filterMonth||r.approvalType==='BUDGET_PLAN'||period.slice(5,7)===filterMonth)
     &&(!filterMember||member.toLowerCase().includes(filterMember.toLowerCase()))
     &&(!filterUnit||String(r.department||'')===filterUnit)
     &&(!filterLevel||rowLevel(r)===filterLevel)
     &&(!filterStatus||String(r.status||'').toUpperCase()===filterStatus);
 }),[rows,filterYear,filterMonth,filterMember,filterUnit,filterLevel,filterStatus]);

 const openReview=async(id:number)=>{
   setError('');setPermissionNotice('');setScoresSaved(false);
   try{
     const d=await getJson<Review>(`/kpi-approvals/${id}/review`);
     setReview(d);setReviewId(id);setReviewComment('');
     const next:Record<number,string>={};
     for(const x of d.items||[]){
       const v=d.scoreField==='HOD'?x.hodScore:x.managerScore;
       next[x.id]=String(v??0);
     }
     setScores(next);
   }catch(e){showFailure(e)}
 };

 const saveScores=async()=>{
   if(!review||!reviewId)return false;
   if(!review.request?.canAct){
     setError('This KPI is already completed or you are not the current approver.');
     return false;
   }
   setSaving(true);setError('');
   try{
     const items=(review.items||[]).map((x:any)=>({
       id:x.id,
       score:Math.max(0,Math.min(5,Number(scores[x.id]||0)))
     }));
     await postJson(`/kpi-approvals/${reviewId}/scores`,{items});
     setScoresSaved(true);
     return true;
   }catch(e){showFailure(e);return false}
   finally{setSaving(false)}
 };

 const act=async(id:number,d:'approved'|'rejected',providedComment?:string)=>{
   const isBudget=reviewId===id&&review?.approvalType==='BUDGET_PLAN';
   if(d==='approved' && reviewId===id && review?.request?.canAct && !scoresSaved && !isBudget){
     const ok=await saveScores();
     if(!ok)return;
   }

   const comment=providedComment!==undefined?providedComment:(prompt(d==='approved'?'Approval comment (optional)':'Reason for rejection')??'');
   if(d==='rejected'&&!comment.trim()){
     alert('Please enter a reason for rejection.');
     return;
   }

   try{
     await postJson(`/kpi-approvals/${id}/${d}`,{comment});
     setReview(null);setReviewId(null);setScoresSaved(false);
     await load();
   }catch(e){showFailure(e)}
 };
 const resendEmail=async()=>{if(!reviewId)return;setSaving(true);setError('');setPermissionNotice('');try{const r:any=await postJson(`/kpi-approvals/${reviewId}/resend-email`,{});if(r.mailWarnings?.length)alert(`Email was not sent:\n${r.mailWarnings.join('\n')}`);else alert(`Email sent to ${r.sent} current approver(s).`)}catch(e){showFailure(e)}finally{setSaving(false)}};

 const canEditScores=review?.approvalType!=='BUDGET_PLAN'&&!!review?.request?.canAct&&review?.request?.status==='PENDING';
 const isBudgetReview=review?.approvalType==='BUDGET_PLAN';

 return <>
   <div className="page-title">
     <div>
       <h1>Approvals</h1>
       <p>Review KPI and Plan/Budget submissions, then Approve or Reject.</p>
     </div>
   </div>

   {permissionNotice&&<div className="budget-info" role="status">{permissionNotice}</div>}
   {error&&<div className="budget-error">{error}</div>}

   <section className="performance-period-filters approval-filters">
     <label className="kpi-filter-select-only"><select value={filterYear} onChange={e=>setFilterYear(e.target.value)}><option value="">All years</option>{yearOptions.map(y=><option key={y} value={y}>{y}</option>)}</select></label>
     <label className="kpi-filter-select-only"><select value={filterMonth} onChange={e=>setFilterMonth(e.target.value)}><option value="">All months</option>{Array.from({length:12},(_,i)=>String(i+1).padStart(2,'0')).map(m=><option key={m} value={m}>Month {m}</option>)}</select></label>
     <label className="kpi-filter-select-only"><select value={filterMember} onChange={e=>setFilterMember(e.target.value)}><option value="">All members</option>{memberOptions.map(name=><option key={name} value={name}>{name}</option>)}</select></label>
     <label className="kpi-filter-select-only"><select value={filterUnit} onChange={e=>setFilterUnit(e.target.value)}><option value="">All Business Units</option>{unitOptions.map(unit=><option key={unit} value={unit}>{unit}</option>)}</select></label>
     <label className="kpi-filter-select-only"><select value={filterLevel} onChange={e=>setFilterLevel(e.target.value)}><option value="">All KPI Levels</option><option value="INDIVIDUAL">Individual</option><option value="DEPARTMENT">Department</option><option value="PROJECT">Project</option></select></label>
     <label className="kpi-filter-select-only"><select value={filterStatus} onChange={e=>setFilterStatus(e.target.value)}><option value="">All Statuses</option><option value="PENDING">Pending</option><option value="APPROVED">Approved</option><option value="REJECTED">Rejected</option><option value="RECALLED">Recalled</option><option value="CANCELLED">Cancelled</option></select></label>
   </section>

   <div className="panel">
     <div className="approval-list">
       {filteredRows.length===0&&<div className="budget-info">No approvals match the selected filters.</div>}
       {filteredRows.map(r=>
         <div className="approval-row" key={r.id}>
           <div>
             <b>{r.employeeName||'KPI'} - {r.period||''}</b>
             <span>{r.department||''} - {r.approvalType==='BUDGET_PLAN'?'PLAN / BUDGET':rowLevel(r)} - requested {new Date(r.requestedAt).toLocaleString()}</span>
             <small>{(r.steps||[]).map((s:any)=>`${s.approver}: ${s.status}${s.comment?` - ${s.comment}`:''}`).join(' - ')}</small>
           </div>

           <span className={`pill ${String(r.status).toLowerCase()}`}>{r.status}</span>

           <div className="approval-actions">
             <button className="secondary" onClick={()=>openReview(r.id)}>
               <Edit3 size={16}/> {r.approvalType==='BUDGET_PLAN'?(r.canAct?'Review & Comment':'View Result'):(r.canAct?'Review & Score':'View Result')}
             </button>
             {r.canAct&&<>
               <button className="approve" onClick={()=>act(r.id,'approved')}>
                 <Check size={16}/> Approve
               </button>
               <button className="reject" onClick={()=>act(r.id,'rejected')}>
                 <X size={16}/> Reject
               </button>
             </>}
           </div>
         </div>
       )}
     </div>
   </div>

   {review&&reviewId&&
     <div className="budget-modal-backdrop" onMouseDown={()=>{setReview(null);setReviewId(null)}}>
       <div className="budget-modal kpi-review-modal" style={{width:'min(1160px,calc(100vw - 32px))',maxWidth:'1160px',height:'min(900px,calc(100vh - 32px))',maxHeight:'calc(100vh - 32px)',display:'flex',flexDirection:'column',overflow:'hidden'}} onMouseDown={e=>e.stopPropagation()}>
         <div className="budget-modal-head">
           <div>
             <h3>{isBudgetReview?(review.request?.canAct?'Review & Comment Plan/Budget':'Plan/Budget Result'):(canEditScores?'Review & Score KPI':'KPI Result')}</h3>
             <p>{review.period?.employeeName} - {review.period?.period} - {review.request?.status}</p>
           </div>
           <button onClick={()=>{setReview(null);setReviewId(null)}}><X/></button>
         </div>

         <div className="kpi-review-body" style={{flex:1,minHeight:0,overflow:'auto'}}>
           {isBudgetReview? <>
             <div className="kpi-review-summary">
               <span><small>Business Unit</small><b>{review.summary?.orgUnit}</b></span>
               <span><small>Budget Year</small><b>{review.summary?.budgetYear}</b></span>
               <span><small>Plan Items</small><b>{review.summary?.itemCount}</b></span>
               <span><small>Total Planned</small><b>{money(review.summary?.totalPlanned||0)}</b></span>
             </div>
             <div className="kpi-review-table-wrap">
               <table className="kpi-review-table"><thead><tr><th>Plan Item</th><th>Month</th><th>Category</th><th>Vendor</th><th>Project</th><th>Amount</th><th>Status</th></tr></thead><tbody>{(review.items||[]).map((x:any)=><tr key={x.id}><td><b>{x.name}</b>{x.note&&<small style={{display:'block'}}>{x.note}</small>}</td><td>{x.plannedMonth||'—'}</td><td>{x.category||'—'}</td><td>{x.vendor||'—'}</td><td>{x.project||'—'}</td><td><b>{money(x.plannedAmount)}</b></td><td>{x.status}</td></tr>)}</tbody></table>
             </div>
             {review.request?.canAct&&<label style={{display:'flex',flexDirection:'column',gap:7,fontSize:12,fontWeight:700}}>Review comment<textarea rows={3} value={reviewComment} onChange={e=>setReviewComment(e.target.value)} placeholder="Enter approval comment or reason for rejection..." style={{fontSize:12,fontWeight:400}}/></label>}
           </> : <>
           <div className="kpi-review-table-wrap">
             <table className="kpi-review-table kpi-score-result-table" style={{minWidth:0,width:'100%',tableLayout:'fixed'}}>
               <colgroup>
                 <col style={{width:'24%'}}/><col style={{width:'13%'}}/><col style={{width:'19%'}}/>
                 <col style={{width:'10%'}}/><col style={{width:'7%'}}/><col style={{width:'6%'}}/>
                 <col style={{width:'7%'}}/><col style={{width:'6%'}}/><col style={{width:'8%'}}/>
               </colgroup>
               <thead>
                 <tr>
                   <th>Plan / Deliverable</th><th>Actual Result</th><th>Next Month Plan</th><th>Note</th><th>Weight</th>
                   <th>Self</th><th>Manager</th><th>HOD</th><th>Your Score</th>
                 </tr>
               </thead>
               <tbody>
                 {(review.items||[]).map((x:any)=>
                   <tr key={x.id}>
                     <td>{x.plan}</td><td>{x.actual||'—'}</td>
                     <td>{x.nextPlan||'—'}</td><td>{x.note||'—'}</td>
                     <td>{Number(x.weight||0).toFixed(2)}</td>
                     <td>{Number(x.selfScore||0).toFixed(1)}</td>
                     <td>{Number(x.managerScore||0).toFixed(1)}</td>
                     <td>{Number(x.hodScore||0).toFixed(1)}</td>
                     <td>
                       {canEditScores
                         ? <input
                             className="kpi-score-input"
                             type="number"
                             min={0}
                             max={5}
                             step={0.1}
                             inputMode="decimal"
                             value={scores[x.id]??''}
                             onChange={e=>{
                               const raw=e.target.value;
                               setScoresSaved(false);
                               setScores(v=>({...v,[x.id]:raw}));
                             }}
                             onBlur={e=>{
                               const n=Number(e.target.value);
                               const safe=Number.isFinite(n)?Math.max(0,Math.min(5,n)):0;
                               setScores(v=>({...v,[x.id]:String(safe)}));
                             }}
                           />
                         : <b>{review.scoreField==='HOD'?Number(x.hodScore||0).toFixed(1):Number(x.managerScore||0).toFixed(1)}</b>
                       }
                     </td>
                   </tr>
                 )}
               </tbody>
             </table>
           </div>
           </>}
         </div>

         <div className="budget-modal-actions">
           <button className="secondary" onClick={()=>{setReview(null);setReviewId(null)}}>Close</button>
           {canEditScores&&
             <button className="budget-primary" disabled={saving} onClick={saveScores}>
               <Save size={16}/> {saving?'Saving...':'Save Scores'}
             </button>
           }
           {isBudgetReview&&review.request?.canAct&&<>
             <button className="secondary" disabled={saving} onClick={resendEmail}><Send size={16}/> Resend Email</button>
             <button className="approve" disabled={saving} onClick={()=>act(reviewId,'approved',reviewComment)}><Check size={16}/> Approve</button>
             <button className="reject" disabled={saving||!reviewComment.trim()} onClick={()=>act(reviewId,'rejected',reviewComment)}><X size={16}/> Reject</button>
           </>}
         </div>
       </div>
     </div>
   }
 </>;
}
