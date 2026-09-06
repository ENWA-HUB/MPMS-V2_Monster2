import { useEffect, useMemo, useRef, useState } from 'react';
import { CopyPlus, Download, Edit3, FileSpreadsheet, Lock, LockOpen, Plus, Search, Trash2, Upload, X } from 'lucide-react';
import { deleteJson, getJson, postJson, putJson } from '../lib/api';
import { KpiApprovalModal } from '../components/KpiApprovalModal';

type Project={id:number;code:string;name:string};
type User={id:number;orgUnitId?:number;name:string;email:string;jobTitle:string;department:string;status:string};
type OrgUnit={id:number;code:string;name:string};
type KpiScopeOptions={orgUnits:OrgUnit[]};
type Period={id:number;periodKey:string;period:string;periodType?:'MONTHLY'|'YEARLY';level:string;userId?:number;employeeName:string;department:string;status:string;isLocked:boolean;sourceSheet:string};
type Item={id:number;performancePeriodId:number;employeeName?:string;projectId?:number;project?:string;sourceRow:number;strategy:string;function:string;plan:string;actual:string;weight:number;selfScore:number;managerScore:number;hodScore:number;finalScore:number;nextPlan:string;note:string;allocationsJson:string;status:string};
type Overview={members?:{id:number;name:string}[];periods:Period[];selected?:Period;items:Item[];teamScore:number;selfScore:number;managerScore:number;totalWeight:number;canManage:boolean;currentUserId:number};
const blank={projectId:'',strategy:'',function:'',plan:'',actual:'',weight:'0',selfScore:'0',managerScore:'0',hodScore:'0',nextPlan:'',note:'',status:'OPEN'};
const pct=(v:number)=>`${Math.round(v*100)}%`;

export function TeamPerformancePage(){
 const [canDownloadKpiExport,setCanDownloadKpiExport]=useState(false);
 useEffect(()=>{
   fetch('/api/access/me',{credentials:'same-origin'})
    .then(r=>r.ok?r.json():null)
    .then(a=>setCanDownloadKpiExport((a?.permissions?.KPI||[]).includes('DOWNLOAD')))
    .catch(()=>setCanDownloadKpiExport(false));
 },[]);

 const [approvalModalOpen,setApprovalModalOpen]=useState(false);
 const kpiImportRef=useRef<HTMLInputElement|null>(null);
 const memberDefaultInitialized=useRef(false);
 const [data,setData]=useState<Overview|null>(null);const [projects,setProjects]=useState<Project[]>([]);const [users,setUsers]=useState<User[]>([]);const [orgUnits,setOrgUnits]=useState<OrgUnit[]>([]);const [periodForm,setPeriodForm]=useState<any>(null);const [editingPeriod,setEditingPeriod]=useState(false);const [periodType,setPeriodType]=useState<'MONTHLY'|'YEARLY'>('MONTHLY');const [filterYear,setFilterYear]=useState(new Date().getFullYear());const [filterMonth,setFilterMonth]=useState<number|''>(new Date().getMonth()+1);const [memberFilter,setMemberFilter]=useState<number|''>('');const [filterLevel,setFilterLevel]=useState('');const [filterUnit,setFilterUnit]=useState('');const [filterUnitQuery,setFilterUnitQuery]=useState('All Business Units');const [selectedId,setSelectedId]=useState<number|undefined>();const [q,setQ]=useState('');const [form,setForm]=useState<any>(null);const [editing,setEditing]=useState<Item|null>(null);const [error,setError]=useState('');const [submitOpen,setSubmitOpen]=useState(false);const [approvers,setApprovers]=useState<any[]>([]);const [approverIds,setApproverIds]=useState<number[]>([]);const [submitMessage,setSubmitMessage]=useState('');const [submitting,setSubmitting]=useState(false);
 const departmentForUser=(u?:User)=>orgUnits.find(o=>o.id===u?.orgUnitId)?.name||u?.department||'';
 const load=async(id?:number,pt=periodType,y=filterYear,m=filterMonth,member=memberFilter,level=filterLevel,unit=filterUnit)=>{const qs=new URLSearchParams();if(id)qs.set('periodId',String(id));if(member!=='')qs.set('userId',String(member));if(level)qs.set('level',level);if(unit)qs.set('department',unit);qs.set('periodType',pt);qs.set('year',String(y));if(pt==='MONTHLY'&&m!=='')qs.set('month',String(m));const [d,p,u,s]=await Promise.all([getJson<Overview>(`/performance/overview?${qs.toString()}`),getJson<Project[]>('/projects'),getJson<User[]>('/team/users'),getJson<KpiScopeOptions>('/scope/options/KPI')]);setProjects(p);setUsers(u);setOrgUnits(s.orgUnits||[]);if(!memberDefaultInitialized.current&&member===''&&d.members?.length){memberDefaultInitialized.current=true;setMemberFilter(d.members[0].id);return}setData(d);if(d.selected)setSelectedId(d.selected.id);else setSelectedId(undefined)};
 useEffect(()=>{load(undefined,periodType,filterYear,filterMonth,memberFilter,filterLevel,filterUnit).catch(e=>setError(String(e)))},[periodType,filterYear,filterMonth,memberFilter,filterLevel,filterUnit]);
 useEffect(()=>{if(!periodForm||editingPeriod||periodForm.userId)return;const creator=users.find(u=>u.id===data?.currentUserId);if(!creator)return;setPeriodForm((current:any)=>current&&!current.userId?{...current,userId:String(creator.id),memberQuery:creator.name,department:departmentForUser(creator)}:current)},[periodForm,editingPeriod,users,data?.currentUserId]);
 const rows=useMemo(()=>data?.items.filter(x=>!q||`${x.plan} ${x.actual} ${x.nextPlan||''} ${x.note||''} ${x.project||''} ${x.function}`.toLowerCase().includes(q.toLowerCase()))||[],[data,q]);
 const updateUnitFilter=(raw:string)=>{setFilterUnitQuery(raw);const value=raw.trim();if(!value||value.toLowerCase()==='all business units'){setFilterUnit('');return}const normalized=value.toLowerCase();const matched=orgUnits.find(o=>o.name.trim().toLowerCase()===normalized||o.code.trim().toLowerCase()===normalized||`${o.code} — ${o.name}`.trim().toLowerCase()===normalized);if(matched)setFilterUnit(matched.name)};
 const normalizeUnitFilter=()=>{if(!filterUnit){setFilterUnitQuery('All Business Units');return}const selected=orgUnits.find(o=>o.name===filterUnit||o.code===filterUnit);setFilterUnitQuery(selected?`${selected.code} — ${selected.name}`:'All Business Units');if(!selected)setFilterUnit('')};
 const periodEmployeeName=(name:string,level:string,department:string,projectName?:string)=>{const clean=name.replace(/^\[(?:UPF|Project|PRJ)\]\[[^\]]+\]\s*/i,'').trim();const matchedUnit=orgUnits.find(o=>o.name.trim().toLowerCase()===department.trim().toLowerCase()||o.code.trim().toLowerCase()===department.trim().toLowerCase());const unitCode=matchedUnit?.code||department||'Unit';return level==='DEPARTMENT'?`[UPF][${unitCode}] ${clean}`:level==='PROJECT'?`[PRJ][${projectName||'Project'}] ${clean}`:clean};
 const savePeriod=async(e:React.FormEvent)=>{e.preventDefault();const u=users.find(x=>x.id===Number(periodForm.userId));if(!u)return;const pt=periodForm.periodType||'MONTHLY';const period=pt==='YEARLY'?String(periodForm.year):`${periodForm.year}-${String(periodForm.month).padStart(2,'0')}`;const level=periodForm.level||'INDIVIDUAL';const department=periodForm.department||departmentForUser(u)||'IT';const selectedProject=level==='PROJECT'?projects.find(p=>p.id===Number(periodForm.projectId)||p.name.trim().toLowerCase()===String(periodForm.projectQuery||'').trim().toLowerCase()):undefined;if(level==='PROJECT'&&!selectedProject){alert('Vui lòng chọn Project hợp lệ trong danh mục Projects.');return}const employeeName=periodEmployeeName(u.name,level,department,selectedProject?.name);try{
   const duplicateQs=new URLSearchParams({userId:String(u.id),periodType:pt,year:String(periodForm.year),level,department});
   if(pt==='MONTHLY')duplicateQs.set('month',String(periodForm.month));
   const duplicateData=await getJson<Overview>(`/performance/overview?${duplicateQs.toString()}`);
   const duplicate=duplicateData.periods.some(p=>(!editingPeriod||p.id!==data?.selected?.id)&&p.userId===u.id&&p.level===level&&p.period===period);
   if(duplicate){alert(`KPI đã tồn tại cho ${u.name} · ${level} · ${period} · ${department}. Không thể ${editingPeriod?'lưu thay đổi':'tạo mới'}.`);return}
   if(editingPeriod&&data?.selected){
     await putJson(`/performance/periods/${data.selected.id}`,{...data.selected,userId:u.id,employeeName,department,period,level,status:periodForm.status||'OPEN'});
     setPeriodForm(null);setEditingPeriod(false);setPeriodType(pt);setFilterYear(Number(periodForm.year));setFilterMonth(pt==='MONTHLY'?Number(periodForm.month):'');await load(data.selected.id,pt,Number(periodForm.year),pt==='MONTHLY'?Number(periodForm.month):'');
   }else{
     const payload={periodKey:`${employeeName}-${period}-${Date.now()}`,period,level,userId:u.id,employeeName,department,sourceSheet:'MPMS',status:'OPEN',isLocked:false};
     const r=await postJson<{id:number}>('/performance/periods',payload);setPeriodForm(null);setEditingPeriod(false);setPeriodType(pt);setFilterYear(Number(periodForm.year));setFilterMonth(pt==='MONTHLY'?Number(periodForm.month):'');await load(r.id,pt,Number(periodForm.year),pt==='MONTHLY'?Number(periodForm.month):'');
   }
 }catch(e){const message=String(e);if(/already exists|409|conflict/i.test(message)){alert(`KPI đã tồn tại cho ${u.name} · ${level} · ${period} · ${department}. Không thể ${editingPeriod?'lưu thay đổi':'tạo mới'}.`);return}setError(message)}};
 const openEditPeriod=()=>{if(!data?.selected)return;const matched=users.find(u=>u.id===data.selected?.userId)||users.find(u=>u.name===data.selected?.employeeName);const pt=(data.selected.periodType||(data.selected.period.length===4?'YEARLY':'MONTHLY')) as 'MONTHLY'|'YEARLY';const [yy,mm]=data.selected.period.split('-');const projectName=data.selected.employeeName?.match(/^\[PRJ\]\[([^\]]+)\]/i)?.[1]||'';const matchedProject=projects.find(p=>p.name.trim().toLowerCase()===projectName.trim().toLowerCase());setEditingPeriod(true);setPeriodForm({userId:String(data.selected.userId||matched?.id||''),memberQuery:matched?.name||data.selected.employeeName.replace(/^\[(?:UPF|Project|PRJ)\]\[[^\]]+\]\s*/i,'')||'',periodType:pt,year:Number(yy),month:mm?Number(mm):1,level:data.selected.level||'INDIVIDUAL',projectId:matchedProject?String(matchedProject.id):'',projectQuery:matchedProject?.name||projectName,department:data.selected.department||departmentForUser(matched),status:data.selected.status||'OPEN'})};
 const openNew=()=>{if(!data?.selected)return;setEditing(null);setForm({...blank,performancePeriodId:data.selected.id})};
 const openEdit=(x:Item)=>{setEditing(x);setForm({...x,projectId:x.projectId?String(x.projectId):'',weight:String(x.weight),selfScore:String(x.selfScore),managerScore:String(x.managerScore),hodScore:String(x.hodScore)})};
 const save=async(e:React.FormEvent)=>{e.preventDefault();
   const payload={
     performancePeriodId:Number(data?.selected?.id||form.performancePeriodId||0),
     projectId:form.projectId?Number(form.projectId):null,
     sourceRow:Number(form.sourceRow||0),
     strategy:String(form.strategy||''),
     function:String(form.function||''),
     plan:String(form.plan||'').replace(/^\[(?:UPF|Project)\]\[[^\]]+\]\s*/i,'').trim(),
     actual:String(form.actual||''),
     weight:Number(form.weight||0),
     selfScore:Number(form.selfScore||0),
     managerScore:Number(form.managerScore||0),
     hodScore:Number(form.hodScore||0),
     finalScore:Number(form.finalScore||0),
     nextPlan:String(form.nextPlan||''),
     note:String(form.note||''),
     allocationsJson:String(form.allocationsJson||'{}'),
     status:String(form.status||'OPEN')
   };
   try{
     editing?await putJson(`/performance/items/${editing.id}`,payload):await postJson('/performance/items',payload);
     setForm(null);setEditing(null);await load(selectedId);
   }catch(e){setError(String(e))}
 };
 const del=async(x:Item)=>{if(!confirm('Delete this performance item?'))return;try{await deleteJson(`/performance/items/${x.id}`);await load(selectedId)}catch(e){setError(String(e))}};

 const clonePeriod=async()=>{if(!data?.selected)return;const next=prompt('New period (YYYY-MM)',data.selected.period);if(!next)return;try{const r=await postJson<{id:number}>(`/performance/periods/${data.selected.id}/clone?period=${encodeURIComponent(next)}`);await load(r.id)}catch(e){setError(String(e))}};
 const deletePeriod=async()=>{if(!data?.selected)return;if(!confirm(`Delete period ${data.selected.period} for ${data.selected.employeeName||data.selected.department}?`))return;try{await deleteJson(`/performance/periods/${data.selected.id}`);setSelectedId(undefined);await load()}catch(e){setError(String(e))}};

 const lock=async()=>{if(!data?.selected)return;try{await postJson(`/performance/periods/${data.selected.id}/lock`);await load(data.selected.id)}catch(e){setError(String(e))}};
 const exportExcel=async()=>{if(!data?.selected)return;setError('');try{const r=await fetch(`/api/excel/kpi/export/${data.selected.id}`,{credentials:'same-origin'});if(!r.ok)throw new Error(await r.text());const blob=await r.blob();const cd=r.headers.get('content-disposition')||'';const m=cd.match(/filename\*?=(?:UTF-8''|\")?([^\";]+)/i);const name=m?decodeURIComponent(m[1].replace(/\"/g,'')):`KPI_${data.selected.period}_${data.selected.employeeName}.xlsx`;const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=name;a.click();URL.revokeObjectURL(url)}catch(e){setError(String(e))}};
 const importExcel=async(e:React.ChangeEvent<HTMLInputElement>)=>{const file=e.target.files?.[0];e.target.value='';if(!file)return;setError('');try{const formData=new FormData();formData.append('file',file);let r=await fetch('/api/excel/kpi/import?dryRun=true&mode=replace',{method:'POST',body:formData,credentials:'same-origin'});if(!r.ok)throw new Error(await r.text());const preview=await r.json();const sheets=(preview.sheets||[]).map((x:any)=>`${x.employeeName} · ${x.period}: ${x.itemCount} KPI, weight ${Math.round((x.totalWeight||0)*100)}%`).join('\n');const warnings=(preview.warnings||[]).join('\n');if(!confirm(`KPI Excel preview\n\n${sheets||'No valid KPI sheets found.'}${warnings?`\n\nWarnings:\n${warnings}`:''}\n\nImport and replace matching open periods?`))return;const formData2=new FormData();formData2.append('file',file);r=await fetch('/api/excel/kpi/import?dryRun=false&mode=replace',{method:'POST',body:formData2,credentials:'same-origin'});if(!r.ok)throw new Error(await r.text());const result=await r.json();alert(`Imported ${result.importedPeriods||0} period(s), ${result.importedItems||0} KPI item(s).${result.warnings?.length?`\nWarnings: ${result.warnings.join('; ')}`:''}`);await load(undefined,periodType,filterYear,filterMonth)}catch(e){setError(String(e))}};

 if(!data)return <div className="loading">{error||'Loading team performance...'}</div>;

 const openSubmitApproval=async()=>{
   if(!data?.selected)return;

   setError('');

   try{
     const a=await getJson<any[]>('/performance/approvers');

     setApprovers(
       a.filter((x:any)=>x.id!==data.currentUserId)
     );

     setApproverIds([]);
     setSubmitMessage('');
     setSubmitOpen(true);

   }catch(e){
     setError(String(e));
   }
 };

 const toggleApprover=(id:number)=>{
   setApproverIds(current=>
     current.includes(id)
       ? current.filter(x=>x!==id)
       : [...current,id]
   );
 };

 const submitForApproval=async()=>{
   if(!data?.selected)return;

   if(!approverIds.length){
     setError('Select at least one approver.');
     return;
   }

   setSubmitting(true);
   setError('');

   try{
     const r=await postJson<any>(
       `/performance/periods/${data.selected.id}/submit`,
       {
         approverIds,
         message:submitMessage
       }
     );

     setSubmitOpen(false);

     // Reload current KPI data using existing load()
     await load(data.selected.id);

     const mailSent=Number(r?.mailSent||0);
     const warnings=Array.isArray(r?.mailWarnings)
       ? r.mailWarnings
       : [];

     if(warnings.length){
       alert(
         `KPI submitted successfully.\n` +
         `Email sent to ${mailSent} approver(s).\n\n` +
         `Mail warnings:\n${warnings.join('\n')}`
       );
     }else{
       alert(
         `KPI submitted successfully.\n` +
         `Email sent to ${mailSent} approver(s).`
       );
     }

   }catch(e){
     setError(String(e));

   }finally{
     setSubmitting(false);
   }
 };
 const totalWeightRaw=Number(data.totalWeight??0);
 const totalWeightPercent=totalWeightRaw<=1.000001 ? totalWeightRaw*100 : totalWeightRaw;
 const isTotalWeightValid=Math.abs(totalWeightPercent-100)<0.01;
 const allMembers=memberFilter==='';
 const memberOptions=(data.members||[]).map(x=>[x.id,x.name] as [number,string]);
 const visibleMemberCount=new Set(data.periods.map(p=>p.userId).filter(Boolean)).size;


 return <>
  <div className="page-title"><div><h1>Performance & KPI</h1><p>UPF-based periodic review: Plan → Actual → Self → Manager → HOD/Final → Lock.</p></div><div className="project-actions performance-actions"><input ref={kpiImportRef} hidden type="file" accept=".xlsx" onChange={importExcel}/><button className="secondary" onClick={()=>kpiImportRef.current?.click()}><Upload size={16}/> Import Excel</button>{canDownloadKpiExport&&<button className="secondary" disabled={!data.selected||data.selected.status==="APPROVED"||data.selected.status==="REJECTED"} onClick={exportExcel}><Download size={16}/> Export Excel</button>}<button className="secondary" onClick={()=>{setEditingPeriod(false);setPeriodForm({userId:data.canManage?'':String(data.currentUserId),periodType,year:filterYear,month:filterMonth===''?new Date().getMonth()+1:filterMonth,level:'INDIVIDUAL',projectId:'',projectQuery:'',department:users.find(u=>u.id===data.currentUserId)?.department||'IT',status:'OPEN'})}}><Plus size={16}/> New Period</button><button className="secondary" onClick={openEditPeriod} disabled={!data.selected}><Edit3 size={16}/> Edit Period</button><button className="secondary" onClick={clonePeriod}><CopyPlus size={16}/> Clone Period</button>{data.canManage&&<button className="secondary danger-text" onClick={deletePeriod}><Trash2 size={16}/> Delete Period</button>}{data.selected
  &&data.selected.status!=="APPROVED"
  &&data.selected.status!=="REJECTED"
  &&(
      data.canManage
      ||(data.selected.userId===data.currentUserId&&data.selected.isLocked)
    )
  &&<button className="secondary" onClick={lock}>
      {data.selected.isLocked?<LockOpen size={16}/>:<Lock size={16}/>}
      {' '}{data.selected.isLocked?'Unlock Period':'Lock Period'}
    </button>
}<button className="secondary" onClick={()=>setApprovalModalOpen(true)} disabled={!data.selected||data.selected.isLocked||data.selected.status==="SUBMITTED"||data.selected.status==="APPROVED"}>Submit for Approval</button><button className="budget-primary" onClick={openNew} disabled={!data.selected||data.selected.isLocked||data.selected.status==="APPROVED"||data.selected.status==="REJECTED"}><Plus size={16}/> Add KPI Item</button></div></div>
  {error&&<div className="budget-error">{error}</div>}

{data?.selected&&!isTotalWeightValid&&
  <div className="kpi-weight-warning">
    <b>Total Weight is currently {totalWeightPercent.toFixed(2)}%.</b>
    <span>Total Weight must equal 100%. Please check and adjust KPI weights before submitting.</span>
  </div>
}

  <section className="performance-period-filters">
    <label className="kpi-filter-select-only"><select value={periodType} onChange={e=>{const v=e.target.value as 'MONTHLY'|'YEARLY';setPeriodType(v);if(v==='YEARLY')setFilterMonth('')}}><option value="MONTHLY">Monthly</option><option value="YEARLY">Yearly</option></select></label>
    <label className="kpi-filter-select-only"><select value={filterYear} onChange={e=>setFilterYear(Number(e.target.value))}>{Array.from({length:9},(_,i)=>new Date().getFullYear()-4+i).map(y=><option key={y} value={y}>{y}</option>)}</select></label>
    {periodType==='MONTHLY'&&<label className="kpi-filter-select-only"><select value={filterMonth} onChange={e=>setFilterMonth(e.target.value===''?'':Number(e.target.value))}><option value="">All months</option>{Array.from({length:12},(_,i)=>i+1).map(m=><option key={m} value={m}>Month {String(m).padStart(2,'0')}</option>)}</select></label>}
    <label className="kpi-filter-select-only"><select value={memberFilter} onChange={e=>setMemberFilter(e.target.value===''?'':Number(e.target.value))}><option value="">All members</option>{memberOptions.map(([id,name])=><option key={id} value={id}>{name}</option>)}</select></label>
    <label className="kpi-filter-select-only"><select value={filterLevel} onChange={e=>setFilterLevel(e.target.value)}><option value="">All KPI Levels</option><option value="INDIVIDUAL">Individual</option><option value="DEPARTMENT">Department</option><option value="PROJECT">Project</option></select></label>
    <label className="kpi-filter-select-only"><input list="performance-unit-filter-options" value={filterUnitQuery} placeholder="Type to search Business Unit..." onFocus={e=>{if(!filterUnit)e.currentTarget.select()}} onChange={e=>updateUnitFilter(e.target.value)} onBlur={normalizeUnitFilter} style={{height:42,minWidth:210,width:'100%',boxSizing:'border-box',border:'1px solid #d7e0ea',borderRadius:8,padding:'0 14px',background:'#fff',font:'inherit'}}/><datalist id="performance-unit-filter-options"><option value="All Business Units"/>{orgUnits.map(unit=><option key={unit.id} value={`${unit.code} — ${unit.name}`}/>)}</datalist></label>
    <div className="filter-summary">{periodType==='YEARLY'?`Annual periods · ${filterYear}`:`Monthly periods · ${filterYear}${filterMonth!==''?` / ${String(filterMonth).padStart(2,'0')}`:' / All months'}`} · {allMembers?'All members':data.selected?.employeeName||'Selected member'} · {filterLevel||'All levels'} · {filterUnit||'All Business Units'}</div>
  </section>
  {(data.selected||rows.length>0)&&<><section className="performance-kpis"><div><span>{allMembers?'Average Final Score':'Final Score'}</span><b>{data.teamScore.toFixed(2)}</b></div><div><span>{allMembers?'Average Self Score':'Self Score'}</span><b>{data.selfScore.toFixed(2)}</b></div><div><span>{allMembers?'Average Manager Score':'Manager Score'}</span><b>{data.managerScore.toFixed(2)}</b></div><div><span>{allMembers?'Members':'Total Weight'}</span><b>{allMembers?visibleMemberCount:pct(data.totalWeight)}</b></div></section>
  <section className="data-module-panel"><div className="data-module-toolbar"><label className="module-search"><Search/><input value={q} onChange={e=>setQ(e.target.value)} placeholder="Search plan, actual, next month plan or note..."/></label><div className="period-source">Source: {allMembers?'All permitted KPI periods':data.selected?.sourceSheet}</div></div><div className="module-table-wrap"><table className="module-table performance-table"><thead><tr>{allMembers&&<th>Member</th>}<th>Plan / Deliverable</th><th>Project</th><th>Actual Result</th><th>Next Month Plan</th><th>Note</th><th>Weight</th><th>Self</th><th>Manager</th><th>HOD</th><th>Final</th><th>Actions</th></tr></thead><tbody>{rows.map(x=><tr key={x.id}>{allMembers&&<td><b>{x.employeeName||'—'}</b></td>}<td><b>{x.plan}</b><small>{x.function||x.strategy}</small></td><td>{x.project||<span className="unlinked">Unlinked</span>}</td><td>{x.actual||'—'}</td><td>{x.nextPlan||'—'}</td><td>{x.note||'—'}</td><td>{pct(x.weight)}</td><td>{x.selfScore||'—'}</td><td>{x.managerScore||'—'}</td><td>{x.hodScore||'—'}</td><td><strong className={x.finalScore>=4?'score-good':x.finalScore>=3?'score-mid':'score-bad'}>{x.finalScore.toFixed(1)}</strong></td><td><div className="row-actions"><button disabled={allMembers||data.selected?.isLocked||data.selected?.status==="APPROVED"||data.selected?.status==="REJECTED"} onClick={()=>openEdit(x)}><Edit3/></button><button disabled={allMembers||data.selected?.isLocked||data.selected?.status==="APPROVED"||data.selected?.status==="REJECTED"} className="danger" onClick={()=>del(x)}><Trash2/></button></div></td></tr>)}</tbody></table></div></section></>}
  {submitOpen&&data.selected&&<div className="budget-modal-backdrop" onMouseDown={()=>setSubmitOpen(false)}><div className="budget-modal approval-submit-modal" onMouseDown={e=>e.stopPropagation()}><div className="budget-modal-head"><div><h3>Submit KPI for Approval</h3><p>{data.selected.employeeName} · {data.selected.period} · select one or more approvers.</p></div><button onClick={()=>setSubmitOpen(false)}><X/></button></div><div className="approver-picker">{approvers.map(a=><label key={a.id} className={approverIds.includes(a.id)?'selected':''}><input type="checkbox" checked={approverIds.includes(a.id)} onChange={()=>toggleApprover(a.id)}/><span><b>{a.name}</b><small>{a.jobTitle||a.department||a.role} · {a.email}</small></span></label>)}</div><label>Message to approvers<textarea rows={3} value={submitMessage} onChange={e=>setSubmitMessage(e.target.value)} placeholder="Please review my KPI for this period."/></label><div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setSubmitOpen(false)}>Cancel</button><button type="button" className="budget-primary" disabled={!approverIds.length||submitting} onClick={submitForApproval}>{submitting?'Submitting...':`Submit to ${approverIds.length||0} Approver(s)`}</button></div></div></div>}
  {periodForm&&<div className="budget-modal-backdrop" onMouseDown={()=>{setPeriodForm(null);setEditingPeriod(false)}}><div className="budget-modal" onMouseDown={e=>e.stopPropagation()}><div className="budget-modal-head"><div><h3>{editingPeriod?'Edit Period':'New Team Performance Period'}</h3><p>{editingPeriod?'Change member, month and period metadata. KPI items remain attached to this period.':'Create one monthly KPI period per team member. A member cannot have more than one KPI in the same month.'}</p></div><button onClick={()=>{setPeriodForm(null);setEditingPeriod(false)}}><X/></button></div><form onSubmit={savePeriod}>
<label>Team member<input required list="kpi-period-member-options" value={periodForm.memberQuery||''} placeholder="Type to search member..." onChange={e=>{const query=e.target.value;const u=users.find(x=>x.name.trim().toLowerCase()===query.trim().toLowerCase());setPeriodForm({...periodForm,memberQuery:query,userId:u?String(u.id):'',department:u?departmentForUser(u):periodForm.department})}}/><datalist id="kpi-period-member-options">{users.filter(u=>(data.canManage||(u.id===data.currentUserId))&&(u.status==='ACTIVE'||String(u.id)===String(periodForm.userId))).map(u=><option key={u.id} value={u.name}>{u.jobTitle||departmentForUser(u)||u.email}</option>)}</datalist></label>
<div className="budget-form-grid"><label>Period Type<select value={periodForm.periodType} onChange={e=>setPeriodForm({...periodForm,periodType:e.target.value})}><option value="MONTHLY">Monthly</option><option value="YEARLY">Yearly</option></select></label><label>Year<input required type="number" min="2020" max="2100" value={periodForm.year} onChange={e=>setPeriodForm({...periodForm,year:Number(e.target.value)})}/></label>{periodForm.periodType==="MONTHLY"&&<label>Month<select value={periodForm.month} onChange={e=>setPeriodForm({...periodForm,month:Number(e.target.value)})}>{Array.from({length:12},(_,i)=>i+1).map(m=><option key={m} value={m}>{String(m).padStart(2,"0")}</option>)}</select></label>}<label>Level<select value={periodForm.level} onChange={e=>{const level=e.target.value;setPeriodForm({...periodForm,level,projectId:level==='PROJECT'?periodForm.projectId:'',projectQuery:level==='PROJECT'?periodForm.projectQuery:''})}}><option>INDIVIDUAL</option><option>DEPARTMENT</option><option>PROJECT</option></select></label>{periodForm.level==='PROJECT'&&<label>Project<input required list="kpi-period-project-options" value={periodForm.projectQuery||''} placeholder="Type to search Project..." onChange={e=>{const query=e.target.value;const p=projects.find(x=>x.name.trim().toLowerCase()===query.trim().toLowerCase());setPeriodForm({...periodForm,projectQuery:query,projectId:p?String(p.id):''})}}/><datalist id="kpi-period-project-options">{projects.map(p=><option key={p.id} value={p.name}>{p.code}</option>)}</datalist></label>}<label>Department<input required list="kpi-period-department-options" value={periodForm.department||''} placeholder="Type to search Business Unit..." onChange={e=>setPeriodForm({...periodForm,department:e.target.value})}/><datalist id="kpi-period-department-options">{orgUnits.map(o=><option key={o.id} value={o.name}>{o.code}</option>)}</datalist></label><label>Workflow Status<input readOnly value={periodForm.status||"OPEN"}/></label></div>
<div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>{setPeriodForm(null);setEditingPeriod(false)}}>Cancel</button><button className="budget-primary">{editingPeriod?'Save Changes':'Create Period'}</button></div></form></div></div>}
  {form&&<div className="budget-modal-backdrop" onMouseDown={()=>setForm(null)}><div className="budget-modal performance-modal" onMouseDown={e=>e.stopPropagation()}><div className="budget-modal-head"><div><h3>{editing?'Edit':'Add'} Performance Item</h3><p>Final score follows HOD → Manager → Self in that priority when a score is entered.</p></div><button onClick={()=>setForm(null)}><X/></button></div><form onSubmit={save}><label>Project<select value={form.projectId||''} onChange={e=>setForm({...form,projectId:e.target.value})}><option value="">Unlinked / General</option>{projects.map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label><label>Plan / Deliverable<textarea required rows={3} value={form.plan} onChange={e=>setForm({...form,plan:e.target.value})}/></label><label>Actual Result<textarea rows={3} value={form.actual} onChange={e=>setForm({...form,actual:e.target.value})}/></label><div className="performance-score-grid"><label>Weight<input type="number" min="0" max="1" step="0.01" value={form.weight} onChange={e=>setForm({...form,weight:e.target.value})}/></label><label>Self<input type="number" min="0" max="5" step="0.1" value={form.selfScore} onChange={e=>setForm({...form,selfScore:e.target.value})}/></label>{data.canManage&&<label>Manager<input type="number" min="0" max="5" step="0.1" value={form.managerScore} onChange={e=>setForm({...form,managerScore:e.target.value})}/></label>}{data.canManage&&<label>HOD<input type="number" min="0" max="5" step="0.1" value={form.hodScore} onChange={e=>setForm({...form,hodScore:e.target.value})}/></label>}</div><label>Next Month Plan<textarea rows={2} value={form.nextPlan||''} onChange={e=>setForm({...form,nextPlan:e.target.value})}/></label><label>Note<textarea rows={2} value={form.note||''} onChange={e=>setForm({...form,note:e.target.value})}/></label><div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setForm(null)}>Cancel</button><button className="budget-primary">Save</button></div></form></div></div>}
 {data.selected&&<KpiApprovalModal open={approvalModalOpen} period={data.selected} onClose={()=>setApprovalModalOpen(false)} onSubmitted={()=>{setApprovalModalOpen(false);window.location.reload()}}/>}
 </>;
}
