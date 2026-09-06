import {useEffect,useState} from 'react';
import {Archive,Copy,Edit3,GitMerge,Plus,RefreshCw,Trash2,WalletCards,UsersRound,X,Search} from 'lucide-react';

import {ClubTournamentPanel} from './ClubTournamentPanel';
import {ClubFinancePanel} from './ClubFinancePanel';
import {ClubProfilePanel} from './ClubProfilePanel';
const api=async(u:string,o?:RequestInit)=>{
 const r=await fetch(u,{credentials:'same-origin',...o,headers:{...(o?.body?{'Content-Type':'application/json'}:{}),...(o?.headers||{})}});
 const d=await r.json().catch(()=>null); if(!r.ok)throw new Error(d?.message||d?.detail||`HTTP ${r.status}`); return d
};
const money=(n:any)=>`${Number(n||0).toLocaleString('vi-VN')} ₫`;
const today=()=>new Date().toISOString().slice(0,10);

const monthNames=['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
function TrendLine({values}:{values:number[]}){const w=260,h=80,p=8,max=Math.max(...values,1),min=Math.min(...values,0),range=Math.max(1,max-min);const pts=values.map((v,i)=>`${p+i*((w-p*2)/Math.max(1,values.length-1))},${h-p-(v-min)/range*(h-p*2)}`).join(' ');return <svg className="pfc-trend-line" viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none"><polyline points={pts}/></svg>}
function MiniBars({values}:{values:number[]}){const max=Math.max(1,...values.map(x=>Math.abs(Number(x||0))));return <div className="pfc-mini-bars">{values.map((x,i)=><i key={i} style={{height:`${Math.max(7,Math.abs(Number(x||0))/max*38)}px`}}/>)}</div>}
function DualLineChart({rows}:{rows:any[]}){const w=720,h=245,p=30,max=Math.max(1,...rows.flatMap(r=>[Number(r.income||0),Number(r.expense||0)]));const pts=(k:string)=>rows.map((r,i)=>`${p+i*((w-p*2)/11)},${h-p-Number(r[k]||0)/max*(h-p*2)}`).join(' ');return <div className="pfc-chart-wrap"><svg viewBox={`0 0 ${w} ${h}`} className="pfc-line-chart" preserveAspectRatio="none">{[0,1,2,3,4].map(i=><line key={i} x1={p} x2={w-p} y1={p+i*((h-p*2)/4)} y2={p+i*((h-p*2)/4)}/>)}<polyline points={pts('income')} className="income"/><polyline points={pts('expense')} className="expense"/></svg><div className="pfc-month-labels">{monthNames.map(x=><span key={x}>{x}</span>)}</div><div className="pfc-legend"><span className="income">Income</span><span className="expense">Expense</span></div></div>}
function BalanceBars({rows}:{rows:any[]}){const max=Math.max(1,...rows.map(r=>Number(r.balance||0)));return <div className="pfc-balance-bars">{rows.map((r:any)=><div key={r.month}><i style={{height:`${Math.max(5,Number(r.balance||0)/max*160)}px`}}/><span>{monthNames[r.month-1]}</span></div>)}</div>}
function ExpenseCategory({rows}:{rows:any[]}){return <div className="pfc-expense-cat"><div className="pfc-expense-list">{rows.slice(0,6).map((r:any,i:number)=><div key={i}><b>{money(r.amount)}</b><span>{r.category}</span></div>)}</div><div className="pfc-donut">{rows.slice(0,6).map((r:any,i:number)=><i key={i} className={`r${i}`} style={{width:`${70-i*8}%`,height:`${70-i*8}%`,borderWidth:10}}/>)}</div></div>}
// PFC_PENDING_ACCOUNT_LINE_V1
function PendingAccountLine({rows}:{rows:any[]}){
 const w=720,h=245,p=34;
 const data=rows.length?rows:[{accountName:'No pending data',income:0,expense:0}];
 const max=Math.max(1,...data.flatMap((r:any)=>[Number(r.income||0),Number(r.expense||0)]));
 const x=(i:number)=>p+i*((w-p*2)/Math.max(1,data.length-1));
 const y=(v:any)=>h-p-Number(v||0)/max*(h-p*2);
 const pts=(k:'income'|'expense')=>data.map((r:any,i:number)=>`${x(i)},${y(r[k])}`).join(' ');
 return <div className="pfc-chart-wrap pfc-pending-account-chart">
  <svg viewBox={`0 0 ${w} ${h}`} className="pfc-line-chart" preserveAspectRatio="none">
   {[0,1,2,3,4].map(i=><line key={i} x1={p} x2={w-p} y1={p+i*((h-p*2)/4)} y2={p+i*((h-p*2)/4)}/>)}
   <polyline points={pts('income')} className="income"/>
   <polyline points={pts('expense')} className="expense"/>
   {data.map((r:any,i:number)=><g key={r.accountId||i}><circle cx={x(i)} cy={y(r.income)} r="4" style={{fill:'#16a34a',stroke:'#fff',strokeWidth:2}}><title>{`${r.accountName} · Pending Income: ${money(r.income)}`}</title></circle><circle cx={x(i)} cy={y(r.expense)} r="4" style={{fill:'#ef4444',stroke:'#fff',strokeWidth:2}}><title>{`${r.accountName} · Pending Expense: ${money(r.expense)}`}</title></circle></g>)}
  </svg>
  <div className="pfc-month-labels" style={{gridTemplateColumns:`repeat(${data.length},minmax(0,1fr))`}}>{data.map((r:any,i:number)=><span key={r.accountId||i} title={r.accountName}>{r.accountName}</span>)}</div>
  <div className="pfc-legend"><span className="income">Pending Income</span><span className="expense">Pending Expense</span></div>
 </div>
}

type K='account'|'transaction'|'asset'|'debt'|'club'|'member'|'fund'|'sponsor'|'event'|'category'|null;

function MoneyInput({value,onChange}:{value:any;onChange:(v:number)=>void}){
 const text=Number(value||0)?Math.trunc(Number(value||0)).toLocaleString('vi-VN'):'';
 return <div className="pfc-number-input"><input inputMode="numeric" value={text} placeholder="0" onChange={e=>{const d=e.target.value.replace(/[^\d]/g,'');onChange(d?Number(d):0)}}/><span>₫</span></div>
}
function IntegerInput({value,onChange,min,max}:{value:any;onChange:(v:number|null)=>void;min?:number;max?:number}){
 return <input inputMode="numeric" value={value??''} onChange={e=>{const d=e.target.value.replace(/[^\d]/g,'');if(!d){onChange(null);return}let n=Number(d);if(min!==undefined)n=Math.max(min,n);if(max!==undefined)n=Math.min(max,n);onChange(n)}}/>
}
function RateInput({value,onChange}:{value:any;onChange:(v:number)=>void}){
 return <div className="pfc-number-input"><input inputMode="decimal" value={value??''} onChange={e=>{let x=e.target.value.replace(',','.').replace(/[^\d.]/g,'');let a=x.split('.');if(a.length>2)x=a[0]+'.'+a.slice(1).join('');if(x.includes('.')){let z=x.split('.');x=z[0]+'.'+z[1].slice(0,2)}const n=Number(x);onChange(Number.isFinite(n)?n:0)}}/><span>%</span></div>
}

function Field({label,children}:{label:string;children:any}){return <label className="pfc-field"><span>{label}</span>{children}</label>}
function Modal({title,onClose,onSave,children}:{title:string;onClose:()=>void;onSave:()=>void;children:any}){return <div className="pfc-modal-backdrop"><div className="pfc-modal"><div className="pfc-modal-head"><h3>{title}</h3><button onClick={onClose}><X/></button></div><div className="pfc-form">{children}</div><div className="pfc-modal-actions"><button onClick={onClose}>Cancel</button><button className="primary" onClick={onSave}>Save</button></div></div></div>}

export function PersonalFcPage(){
 const[sectionSearch,setSectionSearch]=useState('');

 const sectionHit=(...v:any[])=>{
  const q=sectionSearch.trim().toLowerCase();
  return !q||v.filter(x=>x!==null&&x!==undefined).join(' ').toLowerCase().includes(q);
 };

 const[pfcSearch,setPfcSearch]=useState('');
 const[pfcSearchRows,setPfcSearchRows]=useState<any[]>([]);
 const[pfcSearchOpen,setPfcSearchOpen]=useState(false);
 const[pfcSearchBusy,setPfcSearchBusy]=useState(false);
 const[pfcSearchExtra,setPfcSearchExtra]=useState<any>({members:[],tournaments:[]});

 const[clubLogoMap,setClubLogoMap]=useState<Record<number,string>>({});
 const loadClubLogo=async(id:number)=>{
  if(!id||clubLogoMap[id])return;
  try{
   const r=await fetch(`/api/personal-fc/clubs/${id}/documents`,{credentials:'same-origin'});
   if(!r.ok)return;
   const d=await r.json();
   const logo=(d?.items||[]).find((x:any)=>x.category==='CLUB_LOGO');
   if(logo?.viewUrl)setClubLogoMap((m:any)=>({...m,[id]:logo.viewUrl}));
  }catch{}
 };

 const[clubImageFile,setClubImageFile]=useState<File|null>(null);const[clubImagePreview,setClubImagePreview]=useState('');

 const[clubSubTab,setClubSubTab]=useState<'overview'|'members'|'funds'|'transactions'>('overview');
 const[tab,setTab]=useState('overview'),[o,setO]=useState<any>({}),[accounts,setAccounts]=useState<any[]>([]),[categories,setCategories]=useState<any[]>([]),[tx,setTx]=useState<any[]>([]),[assets,setAssets]=useState<any[]>([]),[debts,setDebts]=useState<any[]>([]),[clubs,setClubs]=useState<any[]>([]),[clubId,setClubId]=useState(0),[club,setClub]=useState<any>(null),[err,setErr]=useState('');
 // PFC_TOP_SEARCH_BRIDGE_V4_1
 useEffect(()=>{
  const handler=(ev:any)=>{
   const q=String(ev?.detail?.query??'');
   setSectionSearch(q);
   setPfcSearch(q);
  };
  window.addEventListener('mpms-global-search',handler as EventListener);
  return()=>window.removeEventListener('mpms-global-search',handler as EventListener);
 },[]);

 useEffect(()=>setSectionSearch(''),[tab]);
 const[modal,setModal]=useState<K>(null),[form,setForm]=useState<any>({}),[editId,setEditId]=useState<number|null>(null);
 const[clubProfileRefreshKey,setClubProfileRefreshKey]=useState(0);

 const [overviewDetail,setOverviewDetail]=useState<any>(null);
 const [pendingAccountTx,setPendingAccountTx]=useState<any[]>([]);
 const [ovYear,setOvYear]=useState(new Date().getFullYear());
 const [ovMonth,setOvMonth]=useState(new Date().getMonth()+1);
 const [ovAccount,setOvAccount]=useState('');
 // AUTO_SELECT_FIRST_CLUB_V1
 
 // LOAD_CLUB_LOGOS_V14
 useEffect(()=>{(clubs||[]).forEach((c:any)=>loadClubLogo(c.id))},[clubs]);

useEffect(()=>{
   if(!clubId && clubs.length>0){
     setClubId(clubs[0].id);
     setClubSubTab('overview');
   }
 },[clubs,clubId]);



 const loadBase=async()=>{setErr('');try{const[x1,x2,x3]=await Promise.all([api('/api/personal-fc/overview'),api('/api/personal-fc/accounts'),api('/api/personal-fc/clubs')]);setO(x1);setAccounts(x2);setClubs(x3)}catch(e:any){setErr(e.message)}};
 const loadTab=async(k:string,force=false)=>{try{if(k==='transactions'&&(force||tx.length===0))setTx(await api('/api/personal-fc/transactions'));else if(k==='assets'&&(force||assets.length===0))setAssets(await api('/api/personal-fc/assets'));else if(k==='debts'&&(force||debts.length===0))setDebts(await api('/api/personal-fc/debts'));else if(k==='settings'&&(force||categories.length===0))setCategories(await api('/api/personal-fc/categories'))}catch(e:any){setErr(e.message)}};
 const load=async()=>{await loadBase();await loadTab(tab)};
 const refreshClub=async()=>{if(clubId)setClub(await api(`/api/personal-fc/clubs/${clubId}/detail`))};
 useEffect(()=>{loadBase()},[]);
 useEffect(()=>{loadTab(tab)},[tab]);
 useEffect(()=>{if(tab!=='overview')return;Promise.all([api(`/api/personal-fc/overview-detail?year=${ovYear}&month=${ovMonth}${ovAccount?`&accountId=${ovAccount}`:''}`),api(`/api/personal-fc/transactions?_=${Date.now()}`)]).then(([detail,transactions])=>{setOverviewDetail(detail);setTx(Array.isArray(transactions)?transactions:[])}).catch((e:any)=>setErr(e.message))},[tab,ovYear,ovMonth,ovAccount]); useEffect(()=>{if(clubId)refreshClub()},[clubId]);
 useEffect(()=>{if(tab!=='overview'){return}if(!clubs.length){setPendingAccountTx([]);return}let cancelled=false;(async()=>{const stamp=Date.now();const batches=await Promise.all(clubs.map(async(c:any)=>{try{return await api(`/api/personal-fc/clubs/${c.id}/finance/transactions?year=${ovYear}&month=${ovMonth}&_=${stamp}`)}catch{return[]}}));if(!cancelled)setPendingAccountTx(batches.flat().filter((x:any)=>String(x.status||x.approvalStatus||'').toUpperCase()==='PENDING'))})();return()=>{cancelled=true}},[tab,clubs,ovYear,ovMonth]);

 const pendingByAccount=accounts.filter((a:any)=>!ovAccount||Number(a.id)===Number(ovAccount)).map((a:any)=>{
  const rows=pendingAccountTx.filter((x:any)=>Number(x.accountId)===Number(a.id));
  return {accountId:a.id,accountName:a.name||a.accountName||a.code||`Account ${a.id}`,income:rows.filter((x:any)=>String(x.transactionType).toUpperCase()==='INCOME').reduce((s:number,x:any)=>s+Number(x.amount||0),0),expense:rows.filter((x:any)=>String(x.transactionType).toUpperCase()==='EXPENSE').reduce((s:number,x:any)=>s+Number(x.amount||0),0)};
 });

 // PFC_GLOBAL_SEARCH_V1
 useEffect(()=>{
  let cancelled=false;
  (async()=>{
   const memberRows:any[]=[];
   const tourRows:any[]=[];
   for(const c of (clubs||[])){
    const [mr,tr]=await Promise.all([
     api(`/api/personal-fc/clubs/${c.id}/directory`).catch(()=>[]),
     api(`/api/personal-fc/clubs/${c.id}/tournaments`).catch(()=>[])
    ]);
    const members=Array.isArray(mr)?mr:(mr?.members||mr?.directory||mr?.items||mr?.data||[]);
    const tours=Array.isArray(tr)?tr:(tr?.tournaments||tr?.items||tr?.data||[]);
    for(const x of members)memberRows.push({...x,clubId:c.id,clubName:c.name,clubCode:c.code});
    for(const x of tours)tourRows.push({...x,clubId:c.id,clubName:c.name,clubCode:c.code});
   }
   if(!cancelled)setPfcSearchExtra({members:memberRows,tournaments:tourRows});
  })();
  return()=>{cancelled=true};
 },[clubs]);

 useEffect(()=>{
  const q=pfcSearch.trim().toLowerCase();
  if(q.length<2){setPfcSearchRows([]);setPfcSearchBusy(false);return}
  setPfcSearchBusy(true);

  const hit=(...v:any[])=>v.filter(x=>x!==null&&x!==undefined).join(' ').toLowerCase().includes(q);
  const rows:any[]=[];

  for(const x of (accounts||[]))if(hit(x.name,x.accountType,x.institution,x.currency,x.status,x.notes))
   rows.push({kind:'Account',title:x.name||'Account',meta:[x.accountType,x.institution,x.currency].filter(Boolean).join(' · '),tab:'accounts',id:x.id});

  for(const x of (tx||[]))if(hit(x.transactionDate,x.transactionType,x.description,x.notes,x.amount,x.currency))
   rows.push({kind:'Transaction',title:x.description||x.transactionType||'Transaction',meta:[x.transactionDate,x.transactionType,x.amount].filter(Boolean).join(' · '),tab:'transactions',id:x.id});

  for(const x of (assets||[]))if(hit(x.name,x.assetType,x.costValue,x.currentValue,x.currency,x.notes))
   rows.push({kind:'Asset',title:x.name||'Asset',meta:[x.assetType,x.currency].filter(Boolean).join(' · '),tab:'assets',id:x.id});

  for(const x of (debts||[]))if(hit(x.name,x.lender,x.originalAmount,x.outstandingAmount,x.status,x.notes))
   rows.push({kind:'Debt / Loan',title:x.name||'Debt',meta:[x.lender,x.status].filter(Boolean).join(' · '),tab:'debts',id:x.id});

  for(const x of (clubs||[]))if(hit(x.code,x.name,x.clubType,x.status,x.notes))
   rows.push({kind:'Club',title:x.name||x.code||'Club',meta:[x.code,x.clubType,x.status].filter(Boolean).join(' · '),tab:'clubs',clubId:x.id,id:x.id});

  for(const x of (pfcSearchExtra.members||[]))if(hit(
    x.memberCode,x.memberName,x.fullName,x.email,x.cellPhone,x.phone,x.sex,x.skillRank,
    x.memberRole,x.membershipType,x.status,x.clubName,x.clubCode
  ))
   rows.push({kind:'Club Member',title:`${x.memberName||x.fullName||'Member'}${x.memberCode?` — ${x.memberCode}`:''}`,meta:[x.clubName,x.email||x.cellPhone||x.phone,x.skillRank].filter(Boolean).join(' · '),tab:'clubs',clubId:x.clubId,subTab:'members',id:x.id});

  for(const x of (pfcSearchExtra.tournaments||[]))if(hit(
    x.code,x.name,x.tournamentDate,x.season,x.venue,x.status,x.format,x.notes,x.clubName,x.clubCode
  ))
   rows.push({kind:'Tournament',title:x.name||x.code||'Tournament',meta:[x.code,x.clubName,x.tournamentDate||x.season,x.status].filter(Boolean).join(' · '),tab:'tournaments',clubId:x.clubId,tourId:x.id,id:x.id});

  for(const x of (categories||[]))if(hit(x.code,x.categoryName,x.type,x.parentCategory))
   rows.push({kind:'Category',title:x.categoryName||x.code||'Category',meta:[x.type,x.parentCategory,x.code].filter(Boolean).join(' · '),tab:'settings',id:x.id});

  for(const x of (club?.funds||[]))if(hit(x.name,x.fundCode,x.fundType,x.currency,x.status))
   rows.push({kind:'Club Fund',title:x.name||x.fundCode||'Fund',meta:[club?.club?.name,x.fundType,x.currency].filter(Boolean).join(' · '),tab:'clubs',clubId:clubId,subTab:'funds',id:x.id});

  for(const x of (club?.sponsors||[]))if(hit(x.sponsorName,x.notes,x.currency))
   rows.push({kind:'Sponsor',title:x.sponsorName||'Sponsor',meta:[club?.club?.name,x.currency].filter(Boolean).join(' · '),tab:'clubs',clubId:clubId,subTab:'overview',id:x.id});

  for(const x of (club?.events||[]))if(hit(x.name,x.eventDate,x.status,x.notes))
   rows.push({kind:'Club Event',title:x.name||'Event',meta:[club?.club?.name,x.eventDate,x.status].filter(Boolean).join(' · '),tab:'clubs',clubId:clubId,subTab:'overview',id:x.id});

  setPfcSearchRows(rows.slice(0,30));
  setPfcSearchBusy(false);
  setPfcSearchOpen(true);
 },[pfcSearch,accounts,tx,assets,debts,clubs,categories,club,pfcSearchExtra]);

 const openPfcSearchResult=(r:any)=>{
  if(r.clubId)setClubId(Number(r.clubId));
  if(r.subTab)setClubSubTab(r.subTab);
  if(r.tourId){
   try{sessionStorage.setItem('pfcOpenTournament',JSON.stringify({clubId:Number(r.clubId||0),tourId:Number(r.tourId)}))}catch{}
  }
  setTab(r.tab||'overview');
  setPfcSearchOpen(false);
 };
 const defaults=(k:K)=>{
  if(k==='account')return{name:'',accountType:'BANK',institution:'',currency:'VND',openingBalance:0,includeInNetWorth:true,status:'ACTIVE',notes:''};
  if(k==='transaction')return{transactionDate:today(),transactionType:'EXPENSE',accountId:'',toAccountId:'',categoryId:'',amount:0,currency:'VND',description:'',notes:''};
  if(k==='asset')return{name:'',assetType:'INVESTMENT',costValue:0,currentValue:0,currency:'VND',includeInNetWorth:true,notes:''};
  if(k==='debt')return{name:'',lender:'',originalAmount:0,outstandingAmount:0,interestRate:0,currency:'VND',status:'ACTIVE',notes:''};
  if(k==='club')return{code:'',name:'',clubType:'SPORT',status:'ACTIVE',notes:''};
  if(k==='member')return{memberName:'',email:'',userId:'',memberRole:'MEMBER',membershipType:'MEMBER',membershipFee:0,joinDate:today(),status:'ACTIVE'};
  if(k==='fund')return{name:'',fundType:'GENERAL',currency:'VND',openingBalance:0,status:'ACTIVE'};
  if(k==='sponsor')return{sponsorName:'',committedAmount:0,receivedAmount:0,currency:'VND',notes:''};
  if(k==='event')return{name:'',eventDate:today(),budgetAmount:0,registrationIncome:0,sponsorIncome:0,expenseAmount:0,currency:'VND',status:'PLANNED',notes:''};
  if(k==='category')return{code:'',type:'EXPENSE',parentCategory:'Personal',categoryName:'',icon:'•',active:true,sortOrder:999};
  return{}
 };
 const open=(k:K,row?:any)=>{
  setModal(k);setEditId(row?.id??null);setForm(row?{...row}:defaults(k));
  if(k==='club'){
   setClubImageFile(null);setClubImagePreview('');
   if(row?.id){
    Promise.all([
     fetch(`/api/personal-fc/clubs/${row.id}/documents`,{credentials:'same-origin'}).then(r=>r.ok?r.json():null),
     fetch(`/api/personal-fc/clubs/${row.id}/profile`,{credentials:'same-origin'}).then(r=>r.ok?r.json():null)
    ]).then(([d,pr])=>{
     const logo=d?.items?.find((x:any)=>x.category==='CLUB_LOGO');
     if(logo)setClubImagePreview(logo.viewUrl);
     const profile=pr?.profile||{};
     setForm((f:any)=>({...f,...profile,id:row.id,code:row.code,name:row.name,clubType:row.clubType,status:row.status,notes:row.notes}));
    }).catch(()=>{});
   }
  }
 };
 const close=()=>{setModal(null);setEditId(null);setForm({});setClubImageFile(null);setClubImagePreview('')};
 const v=(k:string,val:any)=>setForm((f:any)=>({...f,[k]:val}));
 const save=async()=>{if(!modal)return;try{
  let url='';const method=editId?'PUT':'POST';const body={...form};
  if(modal==='transaction'){body.accountId=body.accountId?Number(body.accountId):null;body.toAccountId=body.toAccountId?Number(body.toAccountId):null;body.categoryId=body.categoryId?Number(body.categoryId):null}
  if(modal==='account')url=editId?`/api/personal-fc/accounts/${editId}`:'/api/personal-fc/accounts';
  if(modal==='transaction')url=editId?`/api/personal-fc/transactions/${editId}`:'/api/personal-fc/transactions';
  if(modal==='asset')url=editId?`/api/personal-fc/assets/${editId}`:'/api/personal-fc/assets';
  if(modal==='debt')url=editId?`/api/personal-fc/debts/${editId}`:'/api/personal-fc/debts';
  if(modal==='club')url=editId?`/api/personal-fc/clubs/${editId}`:'/api/personal-fc/clubs';
  if(modal==='member')url=editId?`/api/personal-fc/clubs/${clubId}/members/${editId}`:`/api/personal-fc/clubs/${clubId}/members`;
  if(modal==='fund')url=editId?`/api/personal-fc/clubs/${clubId}/funds/${editId}`:`/api/personal-fc/clubs/${clubId}/funds`;
  if(modal==='sponsor')url=editId?`/api/personal-fc/clubs/${clubId}/sponsors/${editId}`:`/api/personal-fc/clubs/${clubId}/sponsors`;
  if(modal==='event')url=editId?`/api/personal-fc/clubs/${clubId}/events/${editId}`:`/api/personal-fc/clubs/${clubId}/events`;
  if(modal==='category')url=editId?`/api/personal-fc/categories/${editId}`:'/api/personal-fc/categories';
  const saved=await api(url,{method,body:JSON.stringify(body)});
  const clubSavedId=Number(saved?.id||saved?.Id||editId||0);
  if(modal==='club'&&clubSavedId){
   const profileBody={
    about:body.about||'',mission:body.mission||'',vision:body.vision||'',coreValues:body.coreValues||'',
    contactEmail:body.contactEmail||'',contactPhone:body.contactPhone||'',website:body.website||'',
    socialLink:body.socialLink||'',mainVenue:body.mainVenue||'',
    regulationsSummary:body.regulationsSummary||'',activitiesSummary:body.activitiesSummary||''
   };
   await api(`/api/personal-fc/clubs/${clubSavedId}/profile`,{method:'PUT',body:JSON.stringify(profileBody)});
  }
  if(modal==='club'&&clubImageFile){
   if(clubSavedId){
    const fd=new FormData();fd.append('file',clubImageFile);fd.append('category','CLUB_LOGO');fd.append('name','Club Logo');
    const ur=await fetch(`/api/personal-fc/clubs/${clubSavedId}/documents/upload`,{method:'POST',credentials:'same-origin',body:fd});
    if(!ur.ok)throw new Error(await ur.text())
   }
  }close();await load();await refreshClub();if(modal==='club')setClubProfileRefreshKey(x=>x+1)
 }catch(e:any){setErr(e.message)}};

 // PERSONAL_FC_LIST_ACTIONS_V1
 const reloadRecords=async()=>{await loadBase();await loadTab(tab,true)};
 const deleteRecord=async(kind:'account'|'transaction'|'asset'|'debt'|'category',row:any)=>{
  const label=row.name||row.categoryName||row.description||row.id;
  const warning=kind==='category'?`Delete Category "${label}"? Linked transactions will be reassigned to "Other"; the transactions themselves will not be deleted.`:`Delete "${label}"? This action cannot be undone.`;
  if(!confirm(warning))return;
  try{setErr('');const path=kind==='category'?'categories':`${kind}s`;await api(`/api/personal-fc/${path}/${row.id}`,{method:'DELETE'});await reloadRecords()}catch(e:any){setErr(e.message)}
 };
 const duplicateTransaction=async(row:any)=>{try{setErr('');await api(`/api/personal-fc/transactions/${row.id}/duplicate`,{method:'POST'});await loadTab('transactions',true)}catch(e:any){setErr(e.message)}};
 const mergeRecord=async(kind:'account'|'asset'|'debt'|'category',row:any,items:any[])=>{
  const candidates=items.filter((x:any)=>Number(x.id)!==Number(row.id));if(!candidates.length){setErr('No target record is available for merge.');return}
  const label=(x:any)=>x.name||x.categoryName||x.code||`#${x.id}`;
  const targetText=prompt(`Merge "${label(row)}" into which target?\n\n${candidates.map((x:any)=>`${x.id}: ${label(x)}`).join('\n')}\n\nEnter target ID:`);
  if(!targetText)return;const targetId=Number(targetText);const target=candidates.find((x:any)=>Number(x.id)===targetId);if(!target){setErr('Invalid merge target ID.');return}
  if(!confirm(`Merge "${label(row)}" into "${label(target)}"? Linked data will move to the target and the source will be deleted.`))return;
  try{setErr('');const path=kind==='category'?'categories':`${kind}s`;await api(`/api/personal-fc/${path}/${row.id}/merge/${targetId}`,{method:'POST'});await reloadRecords()}catch(e:any){setErr(e.message)}
 };
 const deactivateRecord=async(kind:'account'|'category',row:any)=>{try{setErr('');if(kind==='category')await api(`/api/personal-fc/categories/${row.id}/deactivate`,{method:'POST'});else await api(`/api/personal-fc/accounts/${row.id}`,{method:'PUT',body:JSON.stringify({...row,status:'INACTIVE'})});await reloadRecords()}catch(e:any){setErr(e.message)}};
 const closeDebt=async(row:any)=>{try{setErr('');await api(`/api/personal-fc/debts/${row.id}/close`,{method:'POST'});await loadTab('debts',true)}catch(e:any){setErr(e.message)}};

 const tabs=[['overview','Overview'],['transactions','Transactions'],['accounts','Accounts'],['assets','Assets & Investments'],['debts','Debts & Loans'],['clubs','Clubs & Funds'],['tournaments','Tournaments'],['settings','Categories']];
 return <div className="pfc">
  <div className="pfc-head"><div><h1>Personal FC</h1><p>Personal Finance & Club Funds Management</p></div><button onClick={async()=>{await loadBase();await loadTab(tab,true);if(tab==='clubs')await refreshClub()}}><RefreshCw/> Refresh</button></div>
  

  {err&&<div className="budget-error">{err}</div>}
  <div className="pfc-tabs">{tabs.map(([k,l])=><button key={k} className={tab===k?'active':''} onClick={()=>setTab(k)}>{l}</button>)}</div>


  {tab==='overview'&&<div className="pfc-dashboard-v3">
<div className="pfc-overview-banner"><div><h2>Personal Finance Overview</h2><p>Here is your monthly financial overview report.</p></div><div className="pfc-overview-filters"><select value={ovAccount} onChange={e=>setOvAccount(e.target.value)}><option value="">All Accounts</option>{accounts.filter((x:any)=>sectionHit(x.name,x.accountType,x.institution,x.currency,x.status,x.notes)).map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select><select value={ovMonth} onChange={e=>setOvMonth(Number(e.target.value))}>{monthNames.map((x,i)=><option key={x} value={i+1}>{x}</option>)}</select><select value={ovYear} onChange={e=>setOvYear(Number(e.target.value))}>{[ovYear-2,ovYear-1,ovYear,ovYear+1].map(y=><option key={y}>{y}</option>)}</select></div></div>
<div className="pfc-top-summary">
<div className="pfc-summary-card"><div><span>Total Balance</span><b>{money(overviewDetail?.totalBalance)}</b><small className={(overviewDetail?.balanceChangePct||0)>=0?'up':'down'}>{overviewDetail?.balanceChangePct||0}% vs Previous Month</small></div><div><TrendLine values={(overviewDetail?.monthly||[]).map((x:any)=>Number(x.balance||0))}/><MiniBars values={(overviewDetail?.monthly||[]).map((x:any)=>Number(x.balance||0))}/></div></div>
<div className="pfc-summary-card"><div><span>Total Expense</span><b>{money(overviewDetail?.totalExpense)}</b><small className={(overviewDetail?.expenseChangePct||0)<=0?'up':'down'}>{overviewDetail?.expenseChangePct||0}% vs Previous Month</small></div><div><TrendLine values={(overviewDetail?.monthly||[]).map((x:any)=>Number(x.expense||0))}/><MiniBars values={(overviewDetail?.monthly||[]).map((x:any)=>Number(x.expense||0))}/></div></div>
<div className="pfc-summary-card"><div><span>Total Income</span><b>{money(overviewDetail?.totalIncome)}</b><small className={(overviewDetail?.incomeChangePct||0)>=0?'up':'down'}>{overviewDetail?.incomeChangePct||0}% vs Previous Month</small></div><div><TrendLine values={(overviewDetail?.monthly||[]).map((x:any)=>Number(x.income||0))}/><MiniBars values={(overviewDetail?.monthly||[]).map((x:any)=>Number(x.income||0))}/></div></div>
</div>
<div className="pfc-dashboard-grid">
<section className="pfc-db-card"><h3>Income vs. Expense</h3><DualLineChart rows={overviewDetail?.monthly||[]}/></section>
<section className="pfc-db-card"><h3>Account Balance Trend</h3><BalanceBars rows={overviewDetail?.monthly||[]}/></section>
<section className="pfc-db-card expense-category"><h3>Expense by Category</h3><ExpenseCategory rows={overviewDetail?.expenseByCategory||[]}/></section>
<section className="pfc-db-card"><h3>Pending Income vs. Expense by Account</h3><PendingAccountLine rows={pendingByAccount}/></section>
{/* PFC_ACCOUNT_SUMMARY_COMPACT_COLUMNS_V1 */}
<section className="pfc-db-card account-summary"><h3>Account Summary</h3><table style={{tableLayout:'fixed',width:'100%'}}><colgroup><col style={{width:'28%'}}/><col style={{width:'18%'}}/><col style={{width:'18%'}}/><col style={{width:'18%'}}/><col style={{width:'18%'}}/></colgroup><thead><tr><th style={{whiteSpace:'normal'}}>Account</th><th>Opening</th><th>Income</th><th>Expense</th><th>Current</th></tr></thead><tbody>{(overviewDetail?.accountSummary||[]).map((x:any)=><tr key={x.id}><td style={{whiteSpace:'normal',overflowWrap:'anywhere',wordBreak:'break-word'}}><b style={{display:'block'}}>{x.name}</b><small style={{display:'block'}}>{x.institution||x.accountType}</small></td><td style={{whiteSpace:'nowrap',fontSize:13}}>{money(x.openingBalance)}</td><td style={{whiteSpace:'nowrap',fontSize:13}}>{money(x.income)}</td><td style={{whiteSpace:'nowrap',fontSize:13}}>{money(x.expense)}</td><td style={{whiteSpace:'nowrap',fontSize:13}}><b>{money(x.currentBalance)}</b></td></tr>)}</tbody></table></section>
</div></div>}
  {/* PFC_INLINE_CARD_ACTIONS_V1 */}
  {tab==='accounts'&&<section><header><div><h3>Accounts</h3><p>Bank, cash, e-wallet and brokerage accounts</p></div><button onClick={()=>open('account')}><Plus/> Add Account</button></header><div className="pfc-cards">{accounts.filter((x:any)=>sectionHit(x.name,x.accountType,x.institution,x.currency,x.status,x.notes)).map(x=><article key={x.id}><WalletCards/><div style={{flex:1,minWidth:0}}><div style={{display:'flex',alignItems:'center',justifyContent:'space-between',gap:10}}><b>{x.name}</b><div className="ct-row-actions" style={{display:'flex',flexDirection:'row',alignItems:'center',gap:6,flexWrap:'nowrap',marginLeft:'auto'}}><button title="Edit" onClick={()=>open('account',x)}><Edit3/></button><button title="Merge" onClick={()=>mergeRecord('account',x,accounts)}><GitMerge/></button><button title="Deactivate / Close" onClick={()=>deactivateRecord('account',x)}><Archive/></button><button title="Delete" className="danger" onClick={()=>deleteRecord('account',x)}><Trash2/></button></div></div><small>{x.accountType} · {x.institution||'—'}</small><strong>{money(x.openingBalance)}</strong></div></article>)}</div></section>}
  {/* PFC_TRANSACTION_ACCOUNT_AMOUNT_COLORS_V1 */}
  {tab==='transactions'&&<section><header><div><h3>Transactions</h3><p>Income, expense, transfer, investment, debt and asset purchase</p></div><button onClick={()=>open('transaction')}><Plus/> Add Transaction</button></header><table><thead><tr><th>Date</th><th>Type</th><th>Description</th><th>From Account</th><th>Amount</th><th>Actions</th></tr></thead><tbody>{tx.filter((x:any)=>sectionHit(x.transactionDate,x.transactionType,x.description,x.notes,x.amount,x.currency,accounts.find((a:any)=>Number(a.id)===Number(x.accountId))?.name)).map(x=><tr key={x.id}><td>{x.transactionDate}</td><td>{x.transactionType}</td><td>{x.description||'—'}</td><td>{accounts.find((a:any)=>Number(a.id)===Number(x.accountId))?.name||x.accountName||'—'}</td><td><b className={`cf-amount ${x.transactionType==='INCOME'?'in':x.transactionType==='EXPENSE'?'out':''}`}>{money(x.amount)}</b></td><td><div className="ct-row-actions"><button title="Edit" onClick={()=>open('transaction',x)}><Edit3/></button><button title="Duplicate" onClick={()=>duplicateTransaction(x)}><Copy/></button><button title="Delete" className="danger" onClick={()=>deleteRecord('transaction',x)}><Trash2/></button></div></td></tr>)}</tbody></table></section>}
  {tab==='assets'&&<section><header><div><h3>Assets & Investments</h3></div><button onClick={()=>open('asset')}><Plus/> Add Asset</button></header><table><thead><tr><th>Name</th><th>Type</th><th>Cost</th><th>Current</th><th>Actions</th></tr></thead><tbody>{assets.filter((x:any)=>sectionHit(x.name,x.assetType,x.costValue,x.currentValue,x.currency,x.notes)).map(x=><tr key={x.id}><td>{x.name}</td><td>{x.assetType}</td><td>{money(x.costValue)}</td><td>{money(x.currentValue)}</td><td><div className="ct-row-actions"><button title="Edit" onClick={()=>open('asset',x)}><Edit3/></button><button title="Merge" onClick={()=>mergeRecord('asset',x,assets)}><GitMerge/></button><button title="Delete" className="danger" onClick={()=>deleteRecord('asset',x)}><Trash2/></button></div></td></tr>)}</tbody></table></section>}
  {tab==='debts'&&<section><header><div><h3>Debts & Loans</h3></div><button onClick={()=>open('debt')}><Plus/> Add Debt</button></header><table><thead><tr><th>Name</th><th>Lender</th><th>Original</th><th>Outstanding</th><th>Rate</th><th>Actions</th></tr></thead><tbody>{debts.filter((x:any)=>sectionHit(x.name,x.lender,x.originalAmount,x.outstandingAmount,x.status,x.notes)).map(x=><tr key={x.id}><td>{x.name}</td><td>{x.lender}</td><td>{money(x.originalAmount)}</td><td>{money(x.outstandingAmount)}</td><td>{x.interestRate}%</td><td><div className="ct-row-actions"><button title="Edit" onClick={()=>open('debt',x)}><Edit3/></button><button title="Merge" onClick={()=>mergeRecord('debt',x,debts)}><GitMerge/></button><button title="Close" onClick={()=>closeDebt(x)}><Archive/></button><button title="Delete" className="danger" onClick={()=>deleteRecord('debt',x)}><Trash2/></button></div></td></tr>)}</tbody></table></section>}
  {tab==='clubs'&&<div className="pfc-clubs">
   <section>
    <header><h3>Clubs</h3><button onClick={()=>open('club')}><Plus/> New Club</button></header>
    {clubs.filter((x:any)=>sectionHit(x.code,x.name,x.clubType,x.status,x.notes)).map(x=><button key={x.id} className={clubId===x.id?'sel':''} onClick={()=>{setClubId(x.id);setClubSubTab('overview')}}>
      <UsersRound/>
      <span><b>{x.name}</b><small>{x.code}</small></span>
      <span onClick={e=>{e.stopPropagation();open('club',x)}}><Edit3/></span>
    </button>)}
   </section>

   <section>
    {!club
      ? <p>Select a club.</p>
      : <>
        <header>
         <div><div className="club-title-wrap">
          <div className="club-title-line">
           <span className="club-title-logo">{clubLogoMap[clubId]?<img src={clubLogoMap[clubId]} alt=""/>:<UsersRound/>}</span>
           <h3>{club.club.name}</h3><button className="club-title-edit" title="Edit Club" onClick={()=>open('club',club.club)}><Edit3/></button>
          </div>
          <div className="club-title-summary">
           <span>Total Members <b>{club.totalMembers??club.members?.length??0}</b></span>
           <span>Active Members <b>{club.activeMembers??club.members?.filter((x:any)=>x.status==='ACTIVE').length??0}</b></span>
           <span>Fund Balance <b>{money(club.fundBalance)}</b></span>
           <span>Upcoming Activities <b>{club.upcomingActivities??0}</b></span>
          </div>
         </div><small>Fund balance: {money(club.fundBalance)}</small></div>
        </header>

        <div className="pfc-club-subtabs">
         <button className={clubSubTab==='overview'?'active':''} onClick={()=>setClubSubTab('overview')}>Overview & Funds</button>
         <button className={clubSubTab==='members'?'active':''} onClick={()=>setClubSubTab('members')}>Members</button>
         <button className={clubSubTab==='funds'?'active':''} onClick={()=>setClubSubTab('funds')}>Funds</button>
         <button className={clubSubTab==='transactions'?'active':''} onClick={()=>setClubSubTab('transactions')}>Transactions</button>
        </div>

        {clubSubTab==='overview'&&<ClubProfilePanel key={`${clubId}-${clubProfileRefreshKey}`} clubId={clubId} onClubChanged={()=>{load();refreshClub();setClubProfileRefreshKey(x=>x+1)}}/>}
        {clubSubTab==='members'&&<ClubTournamentPanel mode="members" clubIdOverride={clubId} embedded onChanged={async()=>{await loadBase();await refreshClub()}}/>}
        {clubSubTab==='funds'&&<ClubFinancePanel clubId={clubId} mode="funds" onChanged={async()=>{await loadBase();await refreshClub()}}/>}
        {clubSubTab==='transactions'&&<ClubFinancePanel clubId={clubId} mode="transactions" onChanged={async()=>{await loadBase();await refreshClub()}}/>}
       </>
    }
   </section>
  </div>}

  {tab==='tournaments'&&<ClubTournamentPanel mode="tournaments"/>}
  {tab==='settings'&&<section><header><div><h3>Category Master</h3><p>System categories and personal categories</p></div><button onClick={()=>open('category')}><Plus/> Add Category</button></header><div className="pfc-cat">{categories.filter((x:any)=>sectionHit(x.code,x.categoryName,x.type,x.parentCategory)).map(x=><div key={x.id}><span>{x.icon}</span><div style={{flex:1,minWidth:0}}><div style={{display:'flex',alignItems:'center',justifyContent:'space-between',gap:10}}><b>{x.categoryName}</b><div className="ct-row-actions" style={{display:'flex',flexDirection:'row',alignItems:'center',gap:6,flexWrap:'nowrap',marginLeft:'auto'}}><button title="Edit" onClick={()=>open('category',x)}><Edit3/></button><button title="Merge" onClick={()=>mergeRecord('category',x,categories.filter((c:any)=>c.type===x.type))}><GitMerge/></button><button title="Deactivate / Close" onClick={()=>deactivateRecord('category',x)}><Archive/></button><button title="Delete" className="danger" onClick={()=>deleteRecord('category',x)}><Trash2/></button></div></div><small>{x.type} · {x.parentCategory} · {x.code}</small></div></div>)}</div></section>}

  {modal==='account'&&<Modal title={editId?'Edit Account':'Add Account'} onClose={close} onSave={save}>
   <Field label="Account Name"><input value={form.name||''} onChange={e=>v('name',e.target.value)}/></Field>
   <Field label="Account Type"><select value={form.accountType||'BANK'} onChange={e=>v('accountType',e.target.value)}><option>BANK</option><option>CASH</option><option>EWALLET</option><option>BROKERAGE</option><option>CREDIT_CARD</option></select></Field>
   <Field label="Institution / Bank"><input value={form.institution||''} onChange={e=>v('institution',e.target.value)}/></Field>
   <Field label="Currency"><input value={form.currency||'VND'} onChange={e=>v('currency',e.target.value)}/></Field>
   <Field label="Opening Balance"><MoneyInput value={form.openingBalance} onChange={x=>v('openingBalance',x)}/></Field>
   <Field label="Status"><select value={form.status||'ACTIVE'} onChange={e=>v('status',e.target.value)}><option>ACTIVE</option><option>INACTIVE</option></select></Field>
   <Field label="Include in Net Worth"><input type="checkbox" checked={!!form.includeInNetWorth} onChange={e=>v('includeInNetWorth',e.target.checked)}/></Field>
   <Field label="Notes"><textarea value={form.notes||''} onChange={e=>v('notes',e.target.value)}/></Field>
  </Modal>}

  {modal==='transaction'&&<Modal title={editId?'Edit Transaction':'Add Transaction'} onClose={close} onSave={save}>
   <Field label="Date"><input type="date" value={form.transactionDate||today()} onChange={e=>v('transactionDate',e.target.value)}/></Field>
   <Field label="Type"><select value={form.transactionType||'EXPENSE'} onChange={e=>v('transactionType',e.target.value)}><option>INCOME</option><option>EXPENSE</option><option>TRANSFER</option><option>INVESTMENT</option><option>DEBT_PAYMENT</option><option>ASSET_PURCHASE</option></select></Field>
   <Field label="From Account"><select value={form.accountId||''} onChange={e=>v('accountId',e.target.value)}><option value="">—</option>{accounts.map(x=><option value={x.id} key={x.id}>{x.name}</option>)}</select></Field>
   <Field label="To Account"><select value={form.toAccountId||''} onChange={e=>v('toAccountId',e.target.value)}><option value="">—</option>{accounts.map(x=><option value={x.id} key={x.id}>{x.name}</option>)}</select></Field>
   <Field label="Category"><select value={form.categoryId||''} onChange={e=>v('categoryId',e.target.value)}><option value="">—</option>{categories.map(x=><option value={x.id} key={x.id}>{x.icon} {x.categoryName}</option>)}</select></Field>
   <Field label="Amount"><MoneyInput value={form.amount} onChange={x=>v('amount',x)}/></Field>
   <Field label="Currency"><input value={form.currency||'VND'} onChange={e=>v('currency',e.target.value)}/></Field>
   <Field label="Description"><input value={form.description||''} onChange={e=>v('description',e.target.value)}/></Field>
   <Field label="Notes"><textarea value={form.notes||''} onChange={e=>v('notes',e.target.value)}/></Field>
  </Modal>}

  {modal==='asset'&&<Modal title={editId?'Edit Asset / Investment':'Add Asset / Investment'} onClose={close} onSave={save}>
   <Field label="Name"><input value={form.name||''} onChange={e=>v('name',e.target.value)}/></Field><Field label="Asset Type"><select value={form.assetType||'INVESTMENT'} onChange={e=>v('assetType',e.target.value)}><option>INVESTMENT</option><option>REAL_ESTATE</option><option>VEHICLE</option><option>GOLD</option><option>OTHER</option></select></Field><Field label="Cost Value"><MoneyInput value={form.costValue} onChange={x=>v('costValue',x)}/></Field><Field label="Current Value"><MoneyInput value={form.currentValue} onChange={x=>v('currentValue',x)}/></Field><Field label="Currency"><input value={form.currency||'VND'} onChange={e=>v('currency',e.target.value)}/></Field><Field label="Include in Net Worth"><input type="checkbox" checked={!!form.includeInNetWorth} onChange={e=>v('includeInNetWorth',e.target.checked)}/></Field><Field label="Notes"><textarea value={form.notes||''} onChange={e=>v('notes',e.target.value)}/></Field>
  </Modal>}
  {modal==='debt'&&<Modal title={editId?'Edit Debt / Loan':'Add Debt / Loan'} onClose={close} onSave={save}>
   <Field label="Name"><input value={form.name||''} onChange={e=>v('name',e.target.value)}/></Field><Field label="Lender"><input value={form.lender||''} onChange={e=>v('lender',e.target.value)}/></Field><Field label="Original Amount"><MoneyInput value={form.originalAmount} onChange={x=>v('originalAmount',x)}/></Field><Field label="Outstanding Amount"><MoneyInput value={form.outstandingAmount} onChange={x=>v('outstandingAmount',x)}/></Field><Field label="Interest Rate (%)"><RateInput value={form.interestRate} onChange={x=>v('interestRate',x)}/></Field><Field label="Currency"><input value={form.currency||'VND'} onChange={e=>v('currency',e.target.value)}/></Field><Field label="Status"><select value={form.status||'ACTIVE'} onChange={e=>v('status',e.target.value)}><option>ACTIVE</option><option>PAID</option><option>CLOSED</option></select></Field><Field label="Notes"><textarea value={form.notes||''} onChange={e=>v('notes',e.target.value)}/></Field>
  </Modal>}
  {modal==='club'&&<Modal title={editId?'Edit Club':'Add Club'} onClose={close} onSave={save}>
   <div className="pfc-club-image-field"><div className="pfc-club-image-preview">{clubImagePreview?<img src={clubImagePreview}/>:<span>Club Logo</span>}</div><div className="club-image-controls"><b>Club Image / Logo</b><label>Choose Image<input hidden type="file" accept=".png,.jpg,.jpeg,.webp" onChange={e=>{const f=e.target.files?.[0];if(!f)return;setClubImageFile(f);setClubImagePreview(URL.createObjectURL(f))}}/></label><small>The image becomes the Club avatar/logo and is stored in SharePoint after Save.</small></div></div>
   <Field label="Club Code"><input value={form.code||''} onChange={e=>v('code',e.target.value)}/></Field>
   <Field label="Club Name"><input value={form.name||''} onChange={e=>v('name',e.target.value)}/></Field>
   <Field label="Club Type"><select value={form.clubType||'SPORT'} onChange={e=>v('clubType',e.target.value)}><option>SPORT</option><option>COMMUNITY</option><option>SOCIAL</option><option>OTHER</option></select></Field>
   <Field label="Status"><select value={form.status||'ACTIVE'} onChange={e=>v('status',e.target.value)}><option>ACTIVE</option><option>INACTIVE</option></select></Field>
   <Field label="About"><textarea value={form.about||''} onChange={e=>v('about',e.target.value)}/></Field>
   <Field label="Mission"><textarea value={form.mission||''} onChange={e=>v('mission',e.target.value)}/></Field>
   <Field label="Vision"><textarea value={form.vision||''} onChange={e=>v('vision',e.target.value)}/></Field>
   <Field label="Core Values"><textarea value={form.coreValues||''} onChange={e=>v('coreValues',e.target.value)}/></Field>
   <Field label="Contact Email"><input type="email" value={form.contactEmail||''} onChange={e=>v('contactEmail',e.target.value)}/></Field>
   <Field label="Contact Phone"><input value={form.contactPhone||''} onChange={e=>v('contactPhone',e.target.value)}/></Field>
   <Field label="Website"><input value={form.website||''} onChange={e=>v('website',e.target.value)}/></Field>
   <Field label="Social Link"><input value={form.socialLink||''} onChange={e=>v('socialLink',e.target.value)}/></Field>
   <Field label="Main Venue"><input value={form.mainVenue||''} onChange={e=>v('mainVenue',e.target.value)}/></Field>
   <Field label="Regulations Summary"><textarea value={form.regulationsSummary||''} onChange={e=>v('regulationsSummary',e.target.value)}/></Field>
   <Field label="Activities"><textarea value={form.activitiesSummary||''} onChange={e=>v('activitiesSummary',e.target.value)}/></Field>
   <Field label="Notes"><textarea value={form.notes||''} onChange={e=>v('notes',e.target.value)}/></Field>
  </Modal>}
  {modal==='member'&&<Modal title={editId?'Edit Club Member':'Add Club Member'} onClose={close} onSave={save}>
   <Field label="Member Name"><input value={form.memberName||''} onChange={e=>v('memberName',e.target.value)}/></Field><Field label="Email"><input value={form.email||''} onChange={e=>v('email',e.target.value)}/></Field><Field label="MPMS User ID"><IntegerInput value={form.userId} onChange={x=>v('userId',x)} min={1}/></Field><Field label="Role"><select value={form.memberRole||'MEMBER'} onChange={e=>v('memberRole',e.target.value)}><option>MEMBER</option><option>ADMIN</option><option>TREASURER</option><option>GUEST</option></select></Field><Field label="Membership Type"><select value={form.membershipType||'MEMBER'} onChange={e=>v('membershipType',e.target.value)}><option>MEMBER</option><option>MONTHLY</option><option>ANNUAL</option><option>GUEST</option></select></Field><Field label="Membership Fee"><MoneyInput value={form.membershipFee} onChange={x=>v('membershipFee',x)}/></Field><Field label="Join Date"><input type="date" value={form.joinDate||''} onChange={e=>v('joinDate',e.target.value)}/></Field><Field label="Status"><select value={form.status||'ACTIVE'} onChange={e=>v('status',e.target.value)}><option>ACTIVE</option><option>INACTIVE</option></select></Field>
  </Modal>}
  {modal==='fund'&&<Modal title={editId?'Edit Club Fund':'Add Club Fund'} onClose={close} onSave={save}>
   <Field label="Fund Name"><input value={form.name||''} onChange={e=>v('name',e.target.value)}/></Field><Field label="Fund Type"><select value={form.fundType||'GENERAL'} onChange={e=>v('fundType',e.target.value)}><option>GENERAL</option><option>COURT</option><option>TOURNAMENT</option><option>EVENT</option><option>SPONSOR</option></select></Field><Field label="Opening Balance"><MoneyInput value={form.openingBalance} onChange={x=>v('openingBalance',x)}/></Field><Field label="Currency"><input value={form.currency||'VND'} onChange={e=>v('currency',e.target.value)}/></Field><Field label="Status"><select value={form.status||'ACTIVE'} onChange={e=>v('status',e.target.value)}><option>ACTIVE</option><option>INACTIVE</option></select></Field>
  </Modal>}
  {modal==='sponsor'&&<Modal title={editId?'Edit Sponsor':'Add Sponsor'} onClose={close} onSave={save}>
   <Field label="Sponsor Name"><input value={form.sponsorName||''} onChange={e=>v('sponsorName',e.target.value)}/></Field><Field label="Committed Amount"><MoneyInput value={form.committedAmount} onChange={x=>v('committedAmount',x)}/></Field><Field label="Received Amount"><MoneyInput value={form.receivedAmount} onChange={x=>v('receivedAmount',x)}/></Field><Field label="Currency"><input value={form.currency||'VND'} onChange={e=>v('currency',e.target.value)}/></Field><Field label="Notes"><textarea value={form.notes||''} onChange={e=>v('notes',e.target.value)}/></Field>
  </Modal>}
  {modal==='event'&&<Modal title={editId?'Edit Club Event':'Add Club Event'} onClose={close} onSave={save}>
   <Field label="Event Name"><input value={form.name||''} onChange={e=>v('name',e.target.value)}/></Field><Field label="Event Date"><input type="date" value={form.eventDate||''} onChange={e=>v('eventDate',e.target.value)}/></Field><Field label="Budget"><MoneyInput value={form.budgetAmount} onChange={x=>v('budgetAmount',x)}/></Field><Field label="Registration Income"><MoneyInput value={form.registrationIncome} onChange={x=>v('registrationIncome',x)}/></Field><Field label="Sponsor Income"><MoneyInput value={form.sponsorIncome} onChange={x=>v('sponsorIncome',x)}/></Field><Field label="Expense"><MoneyInput value={form.expenseAmount} onChange={x=>v('expenseAmount',x)}/></Field><Field label="Currency"><input value={form.currency||'VND'} onChange={e=>v('currency',e.target.value)}/></Field><Field label="Status"><select value={form.status||'PLANNED'} onChange={e=>v('status',e.target.value)}><option>PLANNED</option><option>OPEN</option><option>COMPLETED</option><option>CANCELLED</option></select></Field><Field label="Notes"><textarea value={form.notes||''} onChange={e=>v('notes',e.target.value)}/></Field>
  </Modal>}
  {modal==='category'&&<Modal title={editId?'Edit Category':'Add Category'} onClose={close} onSave={save}>
   <Field label="Category Code"><input value={form.code||''} onChange={e=>v('code',e.target.value)}/></Field><Field label="Type"><select value={form.type||'EXPENSE'} onChange={e=>v('type',e.target.value)}><option>INCOME</option><option>EXPENSE</option></select></Field><Field label="Parent Category"><input value={form.parentCategory||''} onChange={e=>v('parentCategory',e.target.value)}/></Field><Field label="Category Name"><input value={form.categoryName||''} onChange={e=>v('categoryName',e.target.value)}/></Field><Field label="Icon"><input value={form.icon||''} onChange={e=>v('icon',e.target.value)}/></Field><Field label="Sort Order"><IntegerInput value={form.sortOrder} onChange={x=>v('sortOrder',x??0)} min={0}/></Field><Field label="Active"><input type="checkbox" checked={!!form.active} onChange={e=>v('active',e.target.checked)}/></Field>
  </Modal>}
 </div>
}
