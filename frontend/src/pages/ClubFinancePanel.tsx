import {useEffect,useMemo,useState} from 'react';
import {Check,Edit3,Plus,RefreshCw,Trash2,X} from 'lucide-react';

const api=async(u:string,o?:RequestInit)=>{const r=await fetch(u,{credentials:'same-origin',cache:'no-store',...o,headers:{...(o?.body?{'Content-Type':'application/json'}:{}),...(o?.headers||{})}});const d=await r.json().catch(()=>null);if(!r.ok)throw new Error(d?.message||`HTTP ${r.status}`);return d};
const money=(v:any)=>Number(v||0).toLocaleString('vi-VN');
const today=()=>new Date().toISOString().slice(0,10);
function MoneyInput({value,onChange}:{value:any;onChange:(v:number)=>void}){const text=Number(value||0)?Math.trunc(Number(value)).toLocaleString('vi-VN'):'';return <div className="cf-money"><input inputMode="numeric" value={text} placeholder="0" onChange={e=>{const d=e.target.value.replace(/[^\d]/g,'');onChange(d?Number(d):0)}}/><span>₫</span></div>}
function Field({label,children}:{label:string;children:any}){return <label className="ct-field"><span>{label}</span>{children}</label>}
function Modal({title,onClose,onSave,children}:{title:string;onClose:()=>void;onSave:()=>void;children:any}){return <div className="ct-back"><div className="ct-modal"><header><h3>{title}</h3><button onClick={onClose}><X/></button></header><div className="ct-form">{children}</div><footer><button onClick={onClose}>Cancel</button><button className="primary" onClick={onSave}>Save</button></footer></div></div>}

export function ClubFinancePanel({clubId,mode,onChanged}:{clubId:number;mode:'funds'|'transactions';onChanged?:()=>void|Promise<void>}){
 const[funds,setFunds]=useState<any[]>([]),[accounts,setAccounts]=useState<any[]>([]),[members,setMembers]=useState<any[]>([]),[categories,setCategories]=useState<any[]>([]),[tx,setTx]=useState<any[]>([]);
 const[err,setErr]=useState(''),[busy,setBusy]=useState(false),[year,setYear]=useState(new Date().getFullYear()),[month,setMonth]=useState(new Date().getMonth()+1);
 const[modal,setModal]=useState<'fund'|'tx'|null>(null),[edit,setEdit]=useState<any>(null),[form,setForm]=useState<any>({});
 // PFC_FINANCE_FORCE_REFRESH_V1
 const load=async()=>{if(!clubId)return;try{
  setErr('');
  const stamp=Date.now();
  const [fs,acs,ms,cats,transactions]=await Promise.all([
   api(`/api/personal-fc/clubs/${clubId}/finance/funds?_=${stamp}`),
   api(`/api/personal-fc/accounts?_=${stamp}`),
   api(`/api/personal-fc/clubs/${clubId}/directory?_=${stamp}`),
   api(`/api/personal-fc/categories?_=${stamp}`),
   mode==='transactions'?api(`/api/personal-fc/clubs/${clubId}/finance/transactions?year=${year}&month=${month}&_=${stamp}`):Promise.resolve([])
  ]);
  setFunds(Array.isArray(fs)?fs:[]);setAccounts(Array.isArray(acs)?acs:[]);setMembers(Array.isArray(ms)?ms:[]);setCategories(Array.isArray(cats)?cats:[]);
  if(mode==='transactions')setTx(Array.isArray(transactions)?transactions:[]);
 }catch(e:any){setErr(e.message)}};
 const refresh=async()=>{try{setBusy(true);await load();await onChanged?.()}finally{setBusy(false)}};
 useEffect(()=>{load()},[clubId,mode,year,month]);

 const fundDefaults={fundCode:'GENERAL',name:'General Fund',fundType:'GENERAL',purpose:'General club operations',sourceType:'MEMBER_FEE',defaultAccountId:null,openingBalance:0,currency:'VND',effectiveDate:today(),status:'ACTIVE',notes:''};
 const txDefaults={fundId:funds[0]?.id||null,accountId:funds[0]?.defaultAccountId||null,memberId:null,transactionDate:today(),dueDate:today(),transactionType:'INCOME',sourceType:'MANUAL',category:'OTHER',amount:0,currency:'VND',paymentMethod:'BANK_TRANSFER',referenceNo:'',description:''};
 const open=(kind:'fund'|'tx',row?:any)=>{setModal(kind);setEdit(row||null);setForm(row?{...row}:kind==='fund'?fundDefaults:txDefaults)};
 const save=async()=>{try{setBusy(true);const kind=modal!;const url=kind==='fund'?`/api/personal-fc/clubs/${clubId}/finance/funds${edit?`/${edit.id}`:''}`:`/api/personal-fc/clubs/${clubId}/finance/transactions${edit?`/${edit.id}`:''}`;await api(url,{method:edit?'PUT':'POST',body:JSON.stringify(form)});setModal(null);setEdit(null);await load();await onChanged?.()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
 const del=async(kind:'fund'|'tx',row:any)=>{if(!confirm(`Delete "${row.name||row.description||row.id}"?`))return;try{setBusy(true);await api(`/api/personal-fc/clubs/${clubId}/finance/${kind==='fund'?'funds':'transactions'}/${row.id}`,{method:'DELETE'});await load();await onChanged?.()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
 const action=async(row:any,a:'approve'|'reject')=>{try{setBusy(true);await api(`/api/personal-fc/clubs/${clubId}/finance/transactions/${row.id}/${a}`,{method:'POST'});await load();await onChanged?.()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
 // CLUB_FINANCE_CATEGORY_LOOKUP_V1
 const generate=async()=>{try{setBusy(true);const r=await api(`/api/personal-fc/clubs/${clubId}/finance/generate-member-fees?year=${year}&month=${month}`,{method:'POST'});await api(`/api/personal-fc/clubs/${clubId}/finance/assign-member-fee-category?year=${year}&month=${month}`,{method:'POST'});if(r.warning)alert(r.warning);await load();await onChanged?.()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
 const reconcile=async()=>{try{setBusy(true);const r=await api(`/api/personal-fc/clubs/${clubId}/finance/reconcile-approved`,{method:'POST'});alert(`Reconciled approved transactions. Linked: ${r.linked||0}; Fund fixed: ${r.fundFixed||0}; Account fixed: ${r.accountFixed||0}`);await load();await onChanged?.()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};


 const summary=useMemo(()=>({income:tx.filter(x=>x.status==='APPROVED'&&x.transactionType==='INCOME').reduce((a,x)=>a+Number(x.amount||0),0),expense:tx.filter(x=>x.status==='APPROVED'&&x.transactionType==='EXPENSE').reduce((a,x)=>a+Number(x.amount||0),0),pending:tx.filter(x=>x.status==='PENDING').reduce((a,x)=>a+Number(x.amount||0),0)}),[tx]);

 return <div className="club-finance-v11">
  {err&&<div className="budget-error">{err}</div>}
  <div className="cf-toolbar"><div><h3>{mode==='funds'?'Funds':'Transactions'}</h3><p>{mode==='funds'?'Fund source, default account and ledger balance':'Club income/expense ledger with approval workflow'}</p></div><div><button disabled={busy} onClick={refresh}><RefreshCw/>{busy?'Refreshing...':'Refresh'}</button>{mode==='funds'?<button className="primary" disabled={busy} onClick={()=>open('fund')}><Plus/>Add Fund</button>:<><select value={month} onChange={e=>setMonth(Number(e.target.value))}>{Array.from({length:12},(_,i)=><option key={i+1} value={i+1}>Month {i+1}</option>)}</select><input className="cf-year" inputMode="numeric" value={year} onChange={e=>setYear(Number(e.target.value.replace(/[^\d]/g,''))||new Date().getFullYear())}/><button disabled={busy} onClick={generate}>Generate Member Fees</button><button disabled={busy} onClick={reconcile}>Reconcile Approved</button><button className="primary" disabled={busy} onClick={()=>open('tx')}><Plus/>Add Transaction</button></>}</div></div>

  {mode==='funds'&&<table><thead><tr><th>Code</th><th>Fund</th><th>Type</th><th>Source</th><th>Default Account</th><th>Opening</th><th>Current</th><th>Status</th><th>Actions</th></tr></thead><tbody>{funds.map(f=><tr key={f.id}><td>{f.fundCode}</td><td><b>{f.name}</b><small>{f.purpose}</small></td><td>{f.fundType}</td><td>{f.sourceType}</td><td>{f.defaultAccount?.name||'—'}</td><td>{money(f.openingBalance)} ₫</td><td><b>{money(f.currentBalance)} ₫</b></td><td>{f.status}</td><td><div className="ct-row-actions"><button onClick={()=>open('fund',f)}><Edit3/></button><button className="danger" onClick={()=>del('fund',f)}><Trash2/></button></div></td></tr>)}</tbody></table>}

  {mode==='transactions'&&<>
   <div className="cf-kpis"><div><span>Approved Income</span><b>{money(summary.income)} ₫</b></div><div><span>Approved Expense</span><b>{money(summary.expense)} ₫</b></div><div><span>Net</span><b>{money(summary.income-summary.expense)} ₫</b></div><div><span>Pending</span><b>{money(summary.pending)} ₫</b></div></div>
   <table><thead><tr><th>Date</th><th>Type</th><th>Details</th><th>Source</th><th>Fund</th><th>Account</th><th>Status</th><th>Amount</th><th>Actions</th></tr></thead><tbody>{tx.map(x=><tr key={x.id}><td>{x.transactionDate}</td><td>{x.transactionType}</td><td><b>{x.memberName||x.description}</b><small>{x.category}{x.periodKey?` · ${x.periodKey}`:''}</small></td><td>{x.sourceType}</td><td>{x.fundName||'—'}</td><td>{x.accountName||'—'}</td><td><span className={`ct-badge ${(x.status||'').toLowerCase()}`}>{x.status}</span></td><td><b className={`cf-amount ${x.transactionType==='EXPENSE'?'out':'in'}`}>{x.transactionType==='EXPENSE'?'- ':'+ '}{money(x.amount)} ₫</b></td><td><div className="ct-row-actions">{x.status==='PENDING'&&<><button className="approve" onClick={()=>action(x,'approve')}><Check/></button><button className="reject" onClick={()=>action(x,'reject')}>×</button></>}<button onClick={()=>open('tx',x)}><Edit3/></button><button className="danger" onClick={()=>del('tx',x)}><Trash2/></button></div></td></tr>)}</tbody></table>
  </>}

  {modal==='fund'&&<Modal title={edit?'Edit Fund':'Add Fund'} onClose={()=>setModal(null)} onSave={save}>
   <Field label="Fund Code"><input value={form.fundCode||''} onChange={e=>setForm({...form,fundCode:e.target.value.toUpperCase()})}/></Field>
   <Field label="Fund Name"><input value={form.name||''} onChange={e=>setForm({...form,name:e.target.value})}/></Field>
   <Field label="Fund Type"><select value={form.fundType||'GENERAL'} onChange={e=>setForm({...form,fundType:e.target.value})}><option>GENERAL</option><option>TOURNAMENT</option><option>SPONSOR</option><option>RESERVE</option></select></Field>
   <Field label="Default Source"><select value={form.sourceType||'MEMBER_FEE'} onChange={e=>setForm({...form,sourceType:e.target.value})}><option>MEMBER_FEE</option><option>SPONSOR</option><option>TOURNAMENT_FEE</option><option>DONATION</option><option>OTHER_INCOME</option></select></Field>
   <Field label="Default Account"><select value={form.defaultAccountId||''} onChange={e=>setForm({...form,defaultAccountId:e.target.value?Number(e.target.value):null})}><option value="">— Select Account —</option>{accounts.map(a=><option key={a.id} value={a.id}>{a.name}</option>)}</select></Field>
   <Field label="Opening Balance"><MoneyInput value={form.openingBalance} onChange={v=>setForm({...form,openingBalance:v})}/></Field>
   <Field label="Effective Date"><input type="date" value={form.effectiveDate||''} onChange={e=>setForm({...form,effectiveDate:e.target.value||null})}/></Field>
   <Field label="Currency"><select value={form.currency||'VND'} onChange={e=>setForm({...form,currency:e.target.value})}><option>VND</option><option>USD</option></select></Field>
   <Field label="Status"><select value={form.status||'ACTIVE'} onChange={e=>setForm({...form,status:e.target.value})}><option>ACTIVE</option><option>INACTIVE</option></select></Field>
   <Field label="Purpose"><textarea value={form.purpose||''} onChange={e=>setForm({...form,purpose:e.target.value})}/></Field>
   <Field label="Notes"><textarea value={form.notes||''} onChange={e=>setForm({...form,notes:e.target.value})}/></Field>
  </Modal>}

  {modal==='tx'&&<Modal title={edit?'Edit Transaction':'Add Transaction'} onClose={()=>setModal(null)} onSave={save}>
   <Field label="Transaction Date"><input type="date" value={form.transactionDate||''} onChange={e=>setForm({...form,transactionDate:e.target.value})}/></Field>
   <Field label="Due Date"><input type="date" value={form.dueDate||''} onChange={e=>setForm({...form,dueDate:e.target.value||null})}/></Field>
   <Field label="Type"><select value={form.transactionType||'INCOME'} onChange={e=>setForm({...form,transactionType:e.target.value})}><option>INCOME</option><option>EXPENSE</option></select></Field>
   <Field label="Source"><select value={form.sourceType||'MANUAL'} onChange={e=>{const sourceType=e.target.value;setForm({...form,sourceType,category:sourceType==='MEMBER_FEE'?'Pickleball':form.category})}}><option>MEMBER_FEE</option><option>SPONSOR</option><option>TOURNAMENT_FEE</option><option>DONATION</option><option>OTHER_INCOME</option><option>MANUAL</option><option>TRANSFER</option></select></Field>
   <Field label="Category"><><input list="club-finance-category-options" value={form.category||''} placeholder="Type to search Category..." autoComplete="off" onChange={e=>setForm({...form,category:e.target.value})}/><datalist id="club-finance-category-options">{categories.filter(c=>c.active!==false&&(!c.type||String(c.type).toUpperCase()===String(form.transactionType||'').toUpperCase())).map(c=><option key={c.id} value={c.categoryName}>{c.code?`${c.code} · `:''}{c.parentCategory||c.type||''}</option>)}</datalist></></Field>
   <Field label="Fund"><select value={form.fundId||''} onChange={e=>{const id=e.target.value?Number(e.target.value):null;const f=funds.find(x=>x.id===id);setForm({...form,fundId:id,accountId:f?.defaultAccountId||form.accountId,currency:f?.currency||form.currency})}}><option value="">— Select Fund —</option>{funds.filter(f=>f.status==='ACTIVE').map(f=><option key={f.id} value={f.id}>{f.fundCode} · {f.name}</option>)}</select></Field>
   <Field label="Account"><select value={form.accountId||''} onChange={e=>setForm({...form,accountId:e.target.value?Number(e.target.value):null})}><option value="">— Select Account —</option>{accounts.map(a=><option key={a.id} value={a.id}>{a.name}</option>)}</select></Field>
   <Field label="Member"><select value={form.memberId||''} onChange={e=>setForm({...form,memberId:e.target.value?Number(e.target.value):null})}><option value="">— None —</option>{members.map(m=><option key={m.id} value={m.id}>{m.memberCode} · {m.memberName}</option>)}</select></Field>
   <Field label="Amount"><MoneyInput value={form.amount} onChange={v=>setForm({...form,amount:v})}/></Field>
   <Field label="Payment Method"><select value={form.paymentMethod||'BANK_TRANSFER'} onChange={e=>setForm({...form,paymentMethod:e.target.value})}><option>BANK_TRANSFER</option><option>CASH</option><option>E_WALLET</option><option>CARD</option><option>OTHER</option></select></Field>
   <Field label="Reference No."><input value={form.referenceNo||''} onChange={e=>setForm({...form,referenceNo:e.target.value})}/></Field>
   <Field label="Description"><textarea value={form.description||''} onChange={e=>setForm({...form,description:e.target.value})}/></Field>
  </Modal>}
 </div>
}
