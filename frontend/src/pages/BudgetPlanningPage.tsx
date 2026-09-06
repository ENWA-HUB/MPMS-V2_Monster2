import { useEffect, useMemo, useRef, useState } from 'react';
import { Download, Edit3, Link2, Plus, RotateCcw, Search, Send, Trash2, Upload, X } from 'lucide-react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { deleteJson, getJson, postJson, putJson } from '../lib/api';
import { BusinessUnitSelect, CategorySelect } from '../components/MasterDataSelect';

import FormattedNumberInput from "../components/FormattedNumberInput";


async function budgetImportErrorV4(r:Response){
 const raw=await r.text();
 try{
  const j=JSON.parse(raw);
  if(j?.message){
   let message=String(j.message);
   if(Array.isArray(j.deniedBusinessUnits)&&j.deniedBusinessUnits.length)
     message+=` Business Unit không được phép: ${j.deniedBusinessUnits.join(', ')}.`;
   if(Array.isArray(j.missingProjects)&&j.missingProjects.length)
     message+=` Dự án chưa tồn tại: ${j.missingProjects.join(', ')}.`;
   return message;
  }
 }catch{}
 return raw||`HTTP ${r.status} ${r.statusText}`;
}

async function budgetApiError(r:Response){
 const raw=await r.text();
 try{
  const j=JSON.parse(raw);
  if(j?.message){
   const denied=Array.isArray(j.deniedBusinessUnits)&&j.deniedBusinessUnits.length
     ? ` Denied Business Unit(s): ${j.deniedBusinessUnits.join(', ')}`
     : '';
   return `${j.message}${denied}`;
  }
 }catch{}
 return raw||`HTTP ${r.status} ${r.statusText}`;
}


type Project={id:number;code:string;name:string};
type Item={id:number;budgetYear:number;orgUnit:string;sourceSheet:string;sourceRow:number;projectId?:number;project?:string;name:string;category:string;vendor:string;plannedAmount:number;quantity?:number;unitPrice?:number;plannedMonth:string;monthlyPlanJson:string;note:string;status:string; createdBy?: string | null; editedBy?: string | null; createdAt?: string | null; updatedAt?: string | null; createdByUserId?: number | null; updatedByUserId?: number | null;};
type Overview={year:number;totalPlanned:number;itemCount:number;linkedProjects:number;unlinkedItems:number;orgUnits:string[];byUnit:{orgUnit:string;amount:number;items:number}[];items:Item[]};
type Approver={id:number;name:string;email:string;jobTitle?:string;department?:string;status?:string};
type ApprovalLevels={level1:number|null;projectManager:number|null;level2:number|null;level3:number|null;level4:number[];level5:number|null;level6:number|null};
const emptyApprovalLevels:ApprovalLevels={level1:null,projectManager:null,level2:null,level3:null,level4:[],level5:null,level6:null};

function ApproverLookup({label,approvers,value,onChange,multiple=false}:{label:string;approvers:Approver[];value:number|null|number[];onChange:(v:any)=>void;multiple?:boolean}){
 const selected=multiple?(Array.isArray(value)?value:[]):value?[value as number]:[];
 const [query,setQuery]=useState('');
 const matches=query.trim()?approvers.filter(a=>`${a.name} ${a.email}`.toLowerCase().includes(query.toLowerCase())).slice(0,8):[];
 return <div style={{display:'flex',flexDirection:'column',gap:7,minWidth:0}}>
   <b style={{fontSize:12,lineHeight:1.25,color:'#34445b'}}>{label}</b>
   <div style={{position:'relative',minWidth:0}}>
     <Search size={17} style={{position:'absolute',left:13,top:13,zIndex:2,color:'#6f8196',pointerEvents:'none'}}/>
     <input value={query} placeholder="Type name or email..." onChange={e=>setQuery(e.target.value)} style={{display:'block',width:'100%',height:42,boxSizing:'border-box',margin:0,padding:'0 12px 0 40px',border:'1px solid #d5dfeb',borderRadius:9,background:'#fff',fontSize:12,lineHeight:'42px',color:'#17263d',outline:'none'}}/>
     {matches.length>0&&<div style={{position:'absolute',top:46,left:0,right:0,zIndex:30,maxHeight:210,overflowY:'auto',padding:5,border:'1px solid #d5dfeb',borderRadius:9,background:'#fff',boxShadow:'0 10px 28px rgba(15,35,60,.16)'}}>{matches.map(a=><button type="button" key={a.id} onClick={()=>{onChange(multiple?Array.from(new Set([...selected,a.id])):a.id);setQuery('')}} style={{display:'flex',width:'100%',flexDirection:'column',alignItems:'flex-start',gap:2,padding:'8px 10px',border:0,borderRadius:7,background:'transparent',textAlign:'left',cursor:'pointer'}}><b style={{fontSize:12}}>{a.name}</b><small style={{fontSize:10,color:'#718096'}}>{a.email}</small></button>)}</div>}
   </div>
   {selected.length>0&&<div style={{display:'flex',flexWrap:'wrap',gap:6}}>{selected.map(id=>{const a=approvers.find(x=>x.id===id);return a?<span key={id} style={{display:'inline-flex',alignItems:'center',gap:6,maxWidth:'100%',padding:'5px 8px',borderRadius:7,background:'#edf4fb',fontSize:11,color:'#183b63'}}><span style={{overflow:'hidden',textOverflow:'ellipsis',whiteSpace:'nowrap'}}>{a.name}</span><button type="button" aria-label={`Remove ${a.name}`} onClick={()=>onChange(multiple?selected.filter(x=>x!==id):null)} style={{padding:0,border:0,background:'transparent',fontSize:16,lineHeight:1,cursor:'pointer'}}>×</button></span>:null})}</div>}
   {multiple&&<small style={{display:'block',margin:0,fontSize:10,lineHeight:1.35,color:'#718096'}}>Có thể chọn nhiều người tại Level 4; tất cả đều phải duyệt.</small>}
 </div>;
}
const money=(n:number)=>n>=1e9?`${(n/1e9).toFixed(2)}B ₫`:n>=1e6?`${(n/1e6).toFixed(1)}M ₫`:`${new Intl.NumberFormat('en-US').format(n)} ₫`;
const displayPlannedMonth=(value:string|undefined|null)=>{
 const v=String(value||'').trim();
 const iso=v.match(/^(\d{4})-(\d{2})$/);
 return iso?`${iso[2]}/${iso[1]}`:v;
};
const normalizePlannedMonth=(value:string|undefined|null)=>{
 const v=String(value||'').trim();
 if(!v)return '';
 const match=v.match(/^(\d{1,2})\s*\/\s*(\d{4})$/);
 return match?`${match[1].padStart(2,'0')}/${match[2]}`:v;
};
const budgetMetadata=(raw:string|undefined|null)=>{try{const value=JSON.parse(String(raw||'{}'));return value&&typeof value==='object'&&!Array.isArray(value)?value:{}}catch{return {}}};
const personInChargeId=(raw:string|undefined|null)=>{const id=Number(budgetMetadata(raw).personInChargeUserId||0);return id>0?id:null};
const budgetMetadataWithPerson=(raw:string|undefined|null,userId:number|null)=>{const value=budgetMetadata(raw);if(userId)value.personInChargeUserId=userId;else delete value.personInChargeUserId;return JSON.stringify(value)};
const blank={budgetYear:new Date().getFullYear(),orgUnit:'',sourceSheet:'MANUAL',sourceRow:0,projectId:'',name:'',category:'',vendor:'',plannedAmount:'0',quantity:'',unitPrice:'',plannedMonth:'',personInChargeUserId:null,note:'',status:'PLANNED'};

export function BudgetPlanningPage(){
 const [activeMembers,setActiveMembers]=useState<Approver[]>([]);
 useEffect(()=>{getJson<Approver[]>('/team/users').then(rows=>setActiveMembers(rows.filter(x=>!x.status||x.status==='ACTIVE').sort((a,b)=>a.name.localeCompare(b.name)))).catch(()=>setActiveMembers([]))},[]);
 const [canDownloadBudgetExport,setCanDownloadBudgetExport]=useState(false);
 useEffect(()=>{
   fetch('/api/access/me',{credentials:'same-origin'})
    .then(r=>r.ok?r.json():null)
    .then(a=>setCanDownloadBudgetExport((a?.permissions?.BUDGET||[]).includes('DOWNLOAD')))
    .catch(()=>setCanDownloadBudgetExport(false));
 },[]);

 const [budgetMasterOptions,setBudgetMasterOptions]=useState<any>({orgUnits:[]});
 const budgetImportRef=useRef<HTMLInputElement|null>(null);
 const [data,setData]=useState<Overview|null>(null); const [projects,setProjects]=useState<Project[]>([]); const [years,setYears]=useState<number[]>([]); const [year,setYear]=useState(new Date().getFullYear()); const [unit,setUnit]=useState('ALL'); const [q,setQ]=useState(''); const [projectFilter,setProjectFilter]=useState('ALL'); const [creatorFilter,setCreatorFilter]=useState('ALL'); const [monthFilter,setMonthFilter]=useState('ALL'); const [editing,setEditing]=useState<Item|null>(null); const [form,setForm]=useState<any>(blank); const [error,setError]=useState('');
 const [submitOpen,setSubmitOpen]=useState(false); const [approvers,setApprovers]=useState<Approver[]>([]); const [submitting,setSubmitting]=useState(false); const [recalling,setRecalling]=useState(false); const [submitForm,setSubmitForm]=useState<any>({budgetYear:new Date().getFullYear(),orgUnit:'',levels:{...emptyApprovalLevels},message:''});
 const load=async()=>{const [d,p,y]=await Promise.all([getJson<Overview>(`/budget-plan/overview?year=${year}&orgUnit=${encodeURIComponent(unit)}`),getJson<any>('/scope/options/BUDGET'),getJson<number[]>('/budget-plan/years')]);setData(d);setProjects(p.projects||[]);setBudgetMasterOptions(p);setYears(y)};
 useEffect(()=>{getJson<any>('/scope/options/BUDGET').then(setBudgetMasterOptions).catch(()=>{})},[]);
 useEffect(()=>{load().catch(e=>setError(String(e)))},[unit,year]);
 const creatorOptions=useMemo(()=>Array.from(new Set((data?.items||[]).map((x:any)=>x.createdBy||x.createdByName||'System Created').filter(Boolean))).sort((a,b)=>String(a).localeCompare(String(b))),[data]);
 const monthOptions=useMemo(()=>Array.from(new Set((data?.items||[]).map(x=>x.plannedMonth).filter((m):m is string=>Boolean(m)))).sort(),[data]);
 const rows=useMemo(()=>data?.items.filter((x:any)=>{
   const pic=activeMembers.find(m=>m.id===personInChargeId(x.monthlyPlanJson));
   const searchOk=!q||`${x.name} ${x.orgUnit} ${x.category} ${x.vendor} ${x.project||''} ${x.createdBy||x.createdByName||'System Created'} ${x.plannedMonth||''} ${pic?.name||''} ${pic?.email||''}`.toLowerCase().includes(q.toLowerCase());
   const projectOk=projectFilter==='ALL'||String(x.projectId||'')===projectFilter;
   const creator=String(x.createdBy||x.createdByName||'System Created');
   const creatorOk=creatorFilter==='ALL'||creator===creatorFilter;
   const monthOk=monthFilter==='ALL'||x.plannedMonth===monthFilter;
   return searchOk&&projectOk&&creatorOk&&monthOk;
 })||[],[data,q,projectFilter,creatorFilter,monthFilter,activeMembers]);
 const openNew=()=>{setError('');setEditing(null);setForm({...blank,budgetYear:year,orgUnit:unit==='ALL'?'':unit})};
 const openEdit=(x:Item)=>{setError('');setEditing(x);setForm({...x,projectId:x.projectId?String(x.projectId):'',plannedAmount:String(x.plannedAmount),quantity:x.quantity??'',unitPrice:x.unitPrice??'',plannedMonth:displayPlannedMonth(x.plannedMonth),personInChargeUserId:personInChargeId(x.monthlyPlanJson)})};
 const save=async(e:React.FormEvent)=>{
  e.preventDefault();
  setError('');
  if(form.plannedMonth){
    const normalizedMonth=normalizePlannedMonth(form.plannedMonth);
    const match=normalizedMonth.match(/^(0[1-9]|1[0-2])\/(\d{4})$/);
    if(!match){
      setError('Planned Month must use mm/yyyy format, for example 09/2026.');
      return;
    }
    if(Number(match[2])!==Number(form.budgetYear)){
      setError(`Planned Month must belong to Budget Year ${form.budgetYear}. Example: 09/${form.budgetYear}.`);
      return;
    }
  }

  // Do not spread overview/display fields into BudgetPlanItem.
  // `project` in the overview is a string, while BudgetPlanItem.Project is an object.
  // Sending it causes ASP.NET JSON model binding to return HTTP 400 before PUT runs.
  const payload={
    budgetYear:Number(form.budgetYear),
    orgUnit:String(form.orgUnit||''),
    sourceSheet:String(form.sourceSheet||'MANUAL'),
    sourceRow:Number(form.sourceRow||0),
    projectId:form.projectId?Number(form.projectId):null,
    name:String(form.name||''),
    category:String(form.category||''),
    vendor:String(form.vendor||''),
    plannedAmount:Number(form.plannedAmount||0),
    quantity:form.quantity===''||form.quantity==null?null:Number(form.quantity),
    unitPrice:form.unitPrice===''||form.unitPrice==null?null:Number(form.unitPrice),
    plannedMonth:normalizePlannedMonth(form.plannedMonth),
    monthlyPlanJson:budgetMetadataWithPerson(form.monthlyPlanJson,form.personInChargeUserId?Number(form.personInChargeUserId):null),
    note:String(form.note||''),
    status:String(form.status||'PLANNED')
  };

  try{
    editing
      ? await putJson(`/budget-plan/${editing.id}`,payload)
      : await postJson('/budget-plan',payload);
    setEditing(null);
    setForm(null);
    await load();
  }catch(e:any){
    setError(String(e?.message||e||'Unable to save Budget Plan Item.'));
  }
};
 const del=async(x:Item)=>{if(!confirm(`Delete budget plan item “${x.name}”?`))return;try{await deleteJson(`/budget-plan/${x.id}`);await load()}catch(e){setError(String(e))}};
 const locked=(x:Item)=>['SUBMITTED','APPROVED'].includes(String(x.status||'').toUpperCase());
 const openSubmit=async()=>{setError('');try{const a=await getJson<Approver[]>('/budget-plan/approval-options');setApprovers(a);setSubmitForm({budgetYear:new Date().getFullYear(),orgUnit:unit==='ALL'?'':unit,levels:{...emptyApprovalLevels,level4:[]},message:''});setSubmitOpen(true)}catch(e){setError(String(e))}};
 const submitApproval=async(e:React.FormEvent)=>{e.preventDefault();if(!submitForm.orgUnit){setError('Select one Business Unit.');return}const l=submitForm.levels as ApprovalLevels;const count=[l.level1,l.projectManager,l.level2,l.level3,...l.level4,l.level5,l.level6].filter(Boolean).length;if(!count){setError('Select at least one approval level.');return}setSubmitting(true);setError('');try{const r:any=await postJson('/budget-plan/submit',submitForm);setSubmitOpen(false);await load();if(r.alreadySubmitted||r.alreadyApproved){alert(r.message);return}alert(`Plan/Budget ${r.orgUnit} / ${r.budgetYear} submitted. Email sent to the first approval level.${r.mailWarnings?.length?'\nEmail warning: '+r.mailWarnings.join('; '):''}`)}catch(e:any){setError(String(e?.message||e))}finally{setSubmitting(false)}};
 const recallApproval=async()=>{if(unit==='ALL'){setError('Select one Business Unit before recalling.');return}if(!confirm(`Recall Plan/Budget ${unit} / ${year} for adjustment?`))return;setRecalling(true);setError('');try{const r:any=await postJson('/budget-plan/recall',{budgetYear:year,orgUnit:unit});await load();alert(r.message||`Plan/Budget ${unit} / ${year} recalled.`)}catch(e:any){setError(String(e?.message||e))}finally{setRecalling(false)}};
 const selectedPlanStatus=useMemo(()=>{if(unit==='ALL'||!data?.items?.length)return '';const statuses=new Set(data.items.map(x=>String(x.status||'').toUpperCase()));if(statuses.has('SUBMITTED'))return 'SUBMITTED';if(statuses.has('APPROVED'))return 'APPROVED';if(statuses.has('REJECTED'))return 'REJECTED';return 'EDITABLE'},[data,unit]);
 const setApprovalLevel=(key:keyof ApprovalLevels,value:any)=>setSubmitForm((f:any)=>({...f,levels:{...f.levels,[key]:value}}));
 const cloneYear=async()=>{const target=Number(prompt('Create budget plan for year:',String(year+1)));if(!target||target===year)return;try{await postJson(`/budget-plan/years/${year}/clone?targetYear=${target}`);setYear(target);setUnit('ALL')}catch(e){setError(String(e))}};
 const exportBudgetExcel=async()=>{setError('');try{const r=await fetch(`/api/excel/budget/export/${year}`,{credentials:'same-origin'});if(!r.ok)throw new Error(await budgetImportErrorV4(r));const blob=await r.blob();const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=`MPMS_Budget_Plan_${year}.xlsx`;a.click();URL.revokeObjectURL(url)}catch(e){setError(String(e))}};
 const importBudgetExcel=async(e:React.ChangeEvent<HTMLInputElement>)=>{const file=e.target.files?.[0];e.target.value='';if(!file)return;setError('');try{const f1=new FormData();f1.append('file',file);let r=await fetch('/api/excel/budget/import?dryRun=true&mode=merge',{method:'POST',body:f1,credentials:'same-origin'});if(!r.ok)throw new Error(await budgetApiError(r));const preview=await r.json();const warnings=(preview.warnings||[]).join('\n');if(!confirm(`Budget Excel preview\n\nFormat: ${preview.format||'unknown'}\nItems: ${preview.itemCount||0}\nYears: ${(preview.years||[]).join(', ')}\nTotal planned: ${money(preview.totalPlanned||0)}${warnings?`\n\nWarnings:\n${warnings}`:''}\n\nMerge into MPMS? Existing rows matched by Year + Business Unit + Plan Item will be updated.`))return;const f2=new FormData();f2.append('file',file);r=await fetch('/api/excel/budget/import?dryRun=false&mode=merge',{method:'POST',body:f2,credentials:'same-origin'});if(!r.ok)throw new Error(await budgetApiError(r));const result=await r.json();alert(`Budget import completed. Inserted: ${result.inserted||0}; Updated: ${result.updated||0}.${result.warnings?.length?`\nWarnings: ${result.warnings.join('; ')}`:''}`);await load()}catch(e){setError(String(e))}};

 if(!data)return <div className="loading">{error||'Loading annual budget plan...'}</div>;
 return <>
  <div className="page-title"><div><h1>Annual Work & Budget Plan</h1><p>Annual planning by business unit and project. The 2026 workbook is the first imported baseline; future years use the same structure.</p></div><div className="project-actions"><input ref={budgetImportRef} hidden type="file" accept=".xlsx" onChange={importBudgetExcel}/><button className="secondary" onClick={()=>budgetImportRef.current?.click()}><Upload size={16}/> Import</button>{canDownloadBudgetExport&&<button className="secondary" onClick={exportBudgetExcel}><Download size={16}/> Export</button>}<button className="secondary" onClick={cloneYear}>Clone Plan</button>{selectedPlanStatus==='SUBMITTED'?<button className="secondary" disabled={recalling} onClick={recallApproval}><RotateCcw size={16}/> {recalling?'Recalling...':'Recall Submission'}</button>:selectedPlanStatus==='APPROVED'?<button className="secondary" disabled>Approved</button>:<button className="secondary" onClick={openSubmit}><Send size={16}/> {selectedPlanStatus==='REJECTED'?'Submit Again':'Submit for Approval'}</button>}<button className="budget-primary" onClick={openNew}><Plus size={16}/> New</button></div></div>
  {error&&<div className="budget-error">{error}</div>}
  <section className="plan-kpis"><div><span>Total Planned</span><b>{money(data.totalPlanned)}</b></div><div><span>Plan Items</span><b>{data.itemCount}</b></div><div><span>Linked to Projects</span><b>{data.linkedProjects}</b></div><div><span>Need Project Mapping</span><b>{data.unlinkedItems}</b></div></section>
  <section className="plan-grid"><article className="project-panel"><h3>Budget by Business Unit</h3><div className="plan-chart"><ResponsiveContainer width="100%" height="100%"><BarChart data={data.byUnit}><CartesianGrid strokeDasharray="3 3"/><XAxis dataKey="orgUnit" interval={0} angle={-18} textAnchor="end" height={70}/><YAxis tickFormatter={v=>`${(Number(v)/1e9).toFixed(0)}B`}/><Tooltip formatter={(v:any)=>money(Number(v))}/><Bar dataKey="amount" fill="#082d57" radius={[4,4,0,0]}/></BarChart></ResponsiveContainer></div></article><article className="project-panel"><h3>Source Coverage</h3><div className="source-coverage">{data.byUnit.map(x=><div key={x.orgUnit}><span>{x.orgUnit}</span><b>{x.items} items</b><em>{money(x.amount)}</em></div>)}</div></article></section>
  <section className="data-module-panel budget-plan-data-panel"><div className="data-module-toolbar budget-plan-filter-toolbar budget-plan-filter-toolbar-v5" style={{display:'flex',flexDirection:'row',flexWrap:'nowrap',alignItems:'center',gap:12,width:'100%'}}><label className="module-search" style={{flex:'1.45 1 280px',minWidth:220,height:42,margin:0}}><Search/><input value={q} onChange={e=>setQ(e.target.value)} placeholder="Search budget plan..."/></label><div className="budget-plan-filter-group budget-plan-filter-group-v5" style={{display:'flex',flexDirection:'row',flexWrap:'nowrap',alignItems:'center',gap:12,flex:'4 1 900px',minWidth:0,width:'auto'}}><select style={{height:42,minHeight:42,flex:'1 1 0',minWidth:0,width:0,margin:0}} value={unit} onChange={e=>setUnit(e.target.value)} title="Business Unit"><option value="ALL">All business units</option>{budgetMasterOptions.orgUnits?.filter((x:any)=>x.status==="ACTIVE").map((x:any)=><option key={x.id} value={x.code}>{x.code} — {x.name}</option>)}</select><select style={{height:42,minHeight:42,flex:'1 1 0',minWidth:0,width:0,margin:0}} value={projectFilter} onChange={e=>setProjectFilter(e.target.value)} title="Project"><option value="ALL">All projects</option>{projects.map(p=><option key={p.id} value={String(p.id)}>{p.code} — {p.name}</option>)}</select><select style={{height:42,minHeight:42,flex:'1 1 0',minWidth:0,width:0,margin:0}} value={creatorFilter} onChange={e=>setCreatorFilter(e.target.value)} title="Created By"><option value="ALL">All creators</option>{creatorOptions.map(x=><option key={String(x)} value={String(x)}>{String(x)}</option>)}</select><select style={{height:42,minHeight:42,flex:'1 1 0',minWidth:0,width:0,margin:0}} value={monthFilter} onChange={e=>setMonthFilter(e.target.value)} title="Planned Month"><option value="ALL">All months</option>{monthOptions.map(m=><option key={m} value={m}>{displayPlannedMonth(m)}</option>)}</select><select style={{height:42,minHeight:42,flex:'1 1 0',minWidth:0,width:0,margin:0}} value={year} onChange={e=>{setYear(Number(e.target.value));setUnit('ALL');setProjectFilter('ALL');setCreatorFilter('ALL');setMonthFilter('ALL')}} title="Budget Year">{Array.from(new Set([year,...years])).sort((a,b)=>b-a).map(y=><option key={y} value={y}>{y}</option>)}</select>{(unit!=='ALL'||projectFilter!=='ALL'||creatorFilter!=='ALL'||monthFilter!=='ALL'||q)&&<button type="button" className="secondary budget-filter-clear" onClick={()=>{setQ('');setUnit('ALL');setProjectFilter('ALL');setCreatorFilter('ALL');setMonthFilter('ALL')}}>Clear filters</button>}</div></div><div className="module-table-wrap"><table className="module-table budget-plan-table"><thead><tr><th>Business Unit</th><th>Plan Item</th><th>Planned Month</th><th>Category</th><th>Vendor</th><th>Planned</th><th>Project</th><th>Created By</th><th>Status</th><th>Actions</th></tr></thead><tbody>{rows.map(x=><tr key={x.id}><td>{x.orgUnit}</td><td><b>{x.name}</b><small>{x.note||'—'}</small></td><td>{displayPlannedMonth(x.plannedMonth)||'—'}</td><td>{x.category||'—'}</td><td>{x.vendor||'—'}</td><td><b>{money(x.plannedAmount)}</b></td><td>{x.project?<span className="linked-project"><Link2/> {x.project}</span>:<span className="unlinked">Unlinked</span>}</td><td>{(x as any).createdBy||(x as any).createdByName||"System Created"}</td><td><span className={`pill ${String(x.status||'').toLowerCase()}`}>{x.status}</span></td><td><div className="row-actions"><button disabled={locked(x)} title={locked(x)?'Submitted/approved items are read-only':'Edit'} onClick={()=>openEdit(x)}><Edit3/></button><button disabled={locked(x)} title={locked(x)?'Submitted/approved items cannot be deleted':'Delete'} className="danger" onClick={()=>del(x)}><Trash2/></button></div></td></tr>)}</tbody></table></div></section>
  {submitOpen&&<div className="budget-modal-backdrop" onMouseDown={()=>setSubmitOpen(false)}><div className="budget-modal approval-submit-modal" style={{width:'min(980px,calc(100vw - 32px))',maxHeight:'calc(100vh - 32px)',overflowY:'auto'}} onMouseDown={e=>e.stopPropagation()}><div className="budget-modal-head"><div><h3>Submit Plan/Budget for Approval</h3><p>Emails are sent sequentially from the first selected level to Final.</p></div><button onClick={()=>setSubmitOpen(false)}><X/></button></div><form onSubmit={submitApproval} style={{display:'flex',flexDirection:'column',gap:18}}><div className="budget-form-grid"><label>Budget Year<input type="number" min="2020" max="2100" value={submitForm.budgetYear} onChange={e=>setSubmitForm({...submitForm,budgetYear:Number(e.target.value)})}/></label><label>Business Unit<input required list="budget-submit-unit-options" value={submitForm.orgUnit} placeholder="Type to search Business Unit..." onChange={e=>setSubmitForm({...submitForm,orgUnit:e.target.value})}/><datalist id="budget-submit-unit-options">{budgetMasterOptions.orgUnits?.filter((x:any)=>x.status==='ACTIVE').map((x:any)=><option key={x.id} value={x.code}>{x.name}</option>)}</datalist></label></div><div style={{display:'grid',gridTemplateColumns:'repeat(2,minmax(0,1fr))',gap:'16px 20px',padding:'16px',border:'1px solid #e1e8f0',borderRadius:12,background:'#fbfcfe'}}><ApproverLookup label="Level 1" approvers={approvers} value={submitForm.levels.level1} onChange={v=>setApprovalLevel('level1',v)}/><ApproverLookup label="Project Manager" approvers={approvers} value={submitForm.levels.projectManager} onChange={v=>setApprovalLevel('projectManager',v)}/><ApproverLookup label="Level 2" approvers={approvers} value={submitForm.levels.level2} onChange={v=>setApprovalLevel('level2',v)}/><ApproverLookup label="Level 3" approvers={approvers} value={submitForm.levels.level3} onChange={v=>setApprovalLevel('level3',v)}/><ApproverLookup label="Level 4" multiple approvers={approvers} value={submitForm.levels.level4} onChange={v=>setApprovalLevel('level4',v)}/><ApproverLookup label="Level 5" approvers={approvers} value={submitForm.levels.level5} onChange={v=>setApprovalLevel('level5',v)}/><ApproverLookup label="Level 6 / Final" approvers={approvers} value={submitForm.levels.level6} onChange={v=>setApprovalLevel('level6',v)}/></div><label style={{fontSize:12}}>Message to approvers<textarea rows={3} value={submitForm.message} onChange={e=>setSubmitForm({...submitForm,message:e.target.value})} placeholder="Please review this Plan/Budget submission." style={{fontSize:12}}/></label><div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setSubmitOpen(false)}>Cancel</button><button className="budget-primary" disabled={submitting}><Send size={16}/> {submitting?'Submitting...':'Submit for Approval'}</button></div></form></div></div>}
  {(form!==null)&&(editing!==null||form!==blank)&&<div className="budget-modal-backdrop" onMouseDown={()=>setForm(null)}><div className="budget-modal" onMouseDown={e=>e.stopPropagation()}><div className="budget-modal-head"><div><h3>{editing?'Edit':'New'} Budget Plan Item</h3><p>Map annual budget planning to projects and keep source traceability.</p></div><button onClick={()=>setForm(null)}><X/></button></div><form onSubmit={save}><div className="budget-form-grid"><label>Budget year<input required type="number" min="2020" max="2100" value={form.budgetYear} onChange={e=>setForm({...form,budgetYear:e.target.value})}/></label><label>Business Unit<BusinessUnitSelect module="BUDGET" value={form.orgUnit||''} onChange={value=>setForm({...form,orgUnit:value,projectId:null})}/></label><label>Project<select value={form.projectId||''} onChange={e=>setForm({...form,projectId:e.target.value})}><option value="">Unlinked</option>{projects.filter((p:any)=>!form.orgUnit||(p.orgUnitCodes||[]).includes(form.orgUnit)).map(p=><option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label><label>Plan item<input required value={form.name} onChange={e=>setForm({...form,name:e.target.value})}/></label><label>Category<CategorySelect scope="BUDGET" required value={form.category||''} onChange={value=>setForm({...form,category:value})}/></label><label>Vendor<input value={form.vendor||''} onChange={e=>setForm({...form,vendor:e.target.value})}/></label><label>Planned amount<FormattedNumberInput value={form.plannedAmount} decimals={0} onValueChange={v=>setForm({...form,plannedAmount:v})}/></label><label>Quantity<FormattedNumberInput value={form.quantity??''} decimals={0} onValueChange={v=>setForm({...form,quantity:v})}/></label><label>Unit price<FormattedNumberInput value={form.unitPrice??''} decimals={0} onValueChange={v=>setForm({...form,unitPrice:v})}/></label><label>Planned Month<input type="text" inputMode="numeric" maxLength={7} placeholder="mm/yyyy" value={form.plannedMonth||''} onChange={e=>setForm({...form,plannedMonth:e.target.value})} onBlur={e=>setForm({...form,plannedMonth:normalizePlannedMonth(e.target.value)})}/></label><ApproverLookup label="Person in charge" approvers={activeMembers} value={form.personInChargeUserId||null} onChange={v=>setForm({...form,personInChargeUserId:v})}/></div><label>Note<textarea rows={3} value={form.note||''} onChange={e=>setForm({...form,note:e.target.value})}/></label><div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setForm(null)}>Cancel</button><button className="budget-primary">Save</button></div></form></div></div>}
 </>;
}
