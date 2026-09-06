import {useEffect,useMemo,useState} from 'react';
import {Search,X} from 'lucide-react';

type Approver={
 id:number;
 name:string;
 email:string;
 role?:string;
 jobTitle?:string;
 department?:string;
 isProjectManager?:boolean;
};

type Props={
 open:boolean;
 period:any;
 onClose:()=>void;
 onSubmitted:()=>void;
};

type Levels={
 level1:number|null;
 projectManager:number|null;
 level2:number|null;
 level3:number|null;
 level4:number[];
 level5:number|null;
 level6:number|null;
};

const emptyLevels:Levels={
 level1:null,
 projectManager:null,
 level2:null,
 level3:null,
 level4:[],
 level5:null,
 level6:null
};

function labelOf(a?:Approver){
 if(!a)return '';
 return `${a.name}${a.email?` · ${a.email}`:''}`;
}

function matches(a:Approver,q:string){
 const s=q.trim().toLowerCase();
 if(!s)return false;
 return `${a.name} ${a.email} ${a.jobTitle||''} ${a.department||''} ${a.role||''}`
  .toLowerCase()
  .includes(s);
}

function SingleApproverInput({
 label,
 value,
 approvers,
 onChange,
 exclude=[]
}:{
 label:string;
 value:number|null;
 approvers:Approver[];
 onChange:(id:number|null)=>void;
 exclude?:number[];
}){
 const current=approvers.find(a=>a.id===value);
 const [query,setQuery]=useState('');
 const [open,setOpen]=useState(false);

 useEffect(()=>{
   if(current)setQuery(labelOf(current));
   else setQuery('');
 },[value,current?.id]);

 const result=useMemo(()=>{
   if(!query.trim() || (current && query===labelOf(current)))return [];
   return approvers
    .filter(a=>!exclude.includes(a.id))
    .filter(a=>matches(a,query))
    .slice(0,8);
 },[approvers,exclude,query,current?.id]);

 const choose=(a:Approver)=>{
   onChange(a.id);
   setQuery(labelOf(a));
   setOpen(false);
 };

 const clear=()=>{
   onChange(null);
   setQuery('');
   setOpen(false);
 };

 return <div className="approval-ac-row">
   <label>{label}</label>
   <div className="approval-ac">
    <div className="approval-ac-input">
      <Search size={15}/>
      <input
       value={query}
       placeholder="Type name or email..."
       onFocus={()=>setOpen(true)}
       onChange={e=>{
         setQuery(e.target.value);
         setOpen(true);
         if(current && e.target.value!==labelOf(current))onChange(null);
       }}
      />
      {(value||query)&&<button type="button" onClick={clear}><X size={14}/></button>}
    </div>
    {open&&result.length>0&&<div className="approval-ac-menu">
      {result.map(a=><button type="button" key={a.id} onMouseDown={e=>e.preventDefault()} onClick={()=>choose(a)}>
        <b>{a.name}{a.isProjectManager?' · Project Manager':''}</b>
        <span>{a.email}</span>
        <small>{a.jobTitle||a.department||a.role||''}</small>
      </button>)}
    </div>}
   </div>
 </div>;
}

function MultiApproverInput({
 label,
 values,
 approvers,
 onChange,
 exclude=[]
}:{
 label:string;
 values:number[];
 approvers:Approver[];
 onChange:(ids:number[])=>void;
 exclude?:number[];
}){
 const [query,setQuery]=useState('');
 const [open,setOpen]=useState(false);

 const selected=values
  .map(id=>approvers.find(a=>a.id===id))
  .filter(Boolean) as Approver[];

 const result=useMemo(()=>{
   if(!query.trim())return [];
   return approvers
    .filter(a=>!values.includes(a.id))
    .filter(a=>!exclude.includes(a.id))
    .filter(a=>matches(a,query))
    .slice(0,8);
 },[approvers,values,exclude,query]);

 const add=(a:Approver)=>{
   onChange([...values,a.id]);
   setQuery('');
   setOpen(false);
 };

 const remove=(id:number)=>onChange(values.filter(x=>x!==id));

 return <div className="approval-ac-row approval-ac-row-multi">
   <label>{label}</label>
   <div className="approval-multi-box">
    <div className="approval-chip-wrap">
      {selected.map(a=><span className="approval-chip" key={a.id}>
        {a.name}
        <button type="button" onClick={()=>remove(a.id)}><X size={13}/></button>
      </span>)}
      <div className="approval-ac approval-ac-inline">
       <div className="approval-ac-input">
        <Search size={14}/>
        <input
         value={query}
         placeholder={selected.length?'Add another person...':'Type name or email...'}
         onFocus={()=>setOpen(true)}
         onChange={e=>{setQuery(e.target.value);setOpen(true)}}
        />
       </div>
       {open&&result.length>0&&<div className="approval-ac-menu">
        {result.map(a=><button type="button" key={a.id} onMouseDown={e=>e.preventDefault()} onClick={()=>add(a)}>
          <b>{a.name}</b>
          <span>{a.email}</span>
          <small>{a.jobTitle||a.department||a.role||''}</small>
        </button>)}
       </div>}
      </div>
    </div>
    <small>Multiple approvers at Level 4 · ALL must approve.</small>
   </div>
 </div>;
}

export function KpiApprovalModal({open,period,onClose,onSubmitted}:Props){
 const [approvers,setApprovers]=useState<Approver[]>([]);
 const [levels,setLevels]=useState<Levels>(emptyLevels);
 const [message,setMessage]=useState('Please review my KPI for this period.');
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');

 useEffect(()=>{
   if(!open||!period?.id)return;
   setError('');
   setMessage('Please review my KPI for this period.');

   fetch(`/api/performance/approval-level-options/${period.id}`,{credentials:'include'})
    .then(async r=>{
      const d=await r.json().catch(()=>({}));
      if(!r.ok)throw new Error(d?.message||`HTTP ${r.status}`);
      return d;
    })
    .then(d=>{
      setApprovers(d.approvers||[]);
      setLevels({...emptyLevels,projectManager:d.defaultProjectManagerId||null});
    })
    .catch(e=>setError(e?.message||String(e)));
 },[open,period?.id]);

 if(!open||!period)return null;

 const singles=[
  levels.level1,
  levels.projectManager,
  levels.level2,
  levels.level3,
  levels.level5,
  levels.level6
 ].filter((x):x is number=>!!x);

 const selectedCount=[
  ...singles,
  ...levels.level4
 ].length;

 const submit=async()=>{
   if(!selectedCount){
     setError('Please select at least one approver.');
     return;
   }

   setBusy(true);
   setError('');

   try{
     const r=await fetch(`/api/performance/periods/${period.id}/submit-approval-levels`,{
       method:'POST',
       credentials:'include',
       headers:{'Content-Type':'application/json'},
       body:JSON.stringify({levels,message})
     });

     const d=await r.json().catch(()=>({}));
     if(!r.ok)throw new Error(d?.message||`HTTP ${r.status}`);

     onSubmitted();
   }catch(e:any){
     setError(e?.message||String(e));
   }finally{
     setBusy(false);
   }
 };

 return <div className="budget-modal-backdrop" onMouseDown={onClose}>
  <div className="budget-modal approval-level-modal" onMouseDown={e=>e.stopPropagation()}>
   <div className="budget-modal-head">
    <div>
     <h3>Submit KPI for Approval</h3>
     <p>{period.employeeName||'Employee'} · {period.period||''} · Approval Levels</p>
    </div>
    <button onClick={onClose}><X/></button>
   </div>

   {error&&<div className="budget-error">{error}</div>}

   <div className="approval-level-list">
    <SingleApproverInput
      label="Level 1"
      value={levels.level1}
      approvers={approvers}
      exclude={[levels.projectManager||0,...levels.level4]}
      onChange={id=>setLevels(v=>({...v,level1:id}))}
    />

    <SingleApproverInput
      label="Project Manager"
      value={levels.projectManager}
      approvers={approvers}
      exclude={[levels.level1||0,levels.level2||0,levels.level3||0,levels.level5||0,levels.level6||0,...levels.level4]}
      onChange={id=>setLevels(v=>({...v,projectManager:id}))}
    />

    <SingleApproverInput
      label="Level 2"
      value={levels.level2}
      approvers={approvers}
      exclude={[levels.projectManager||0,...levels.level4]}
      onChange={id=>setLevels(v=>({...v,level2:id}))}
    />

    <SingleApproverInput
      label="Level 3"
      value={levels.level3}
      approvers={approvers}
      exclude={[levels.projectManager||0,...levels.level4]}
      onChange={id=>setLevels(v=>({...v,level3:id}))}
    />

    <MultiApproverInput
      label="Level 4"
      values={levels.level4}
      approvers={approvers}
      exclude={singles}
      onChange={ids=>setLevels(v=>({...v,level4:ids}))}
    />

    <SingleApproverInput
      label="Level 5"
      value={levels.level5}
      approvers={approvers}
      exclude={[levels.projectManager||0,...levels.level4]}
      onChange={id=>setLevels(v=>({...v,level5:id}))}
    />

    <SingleApproverInput
      label="Level 6 / Final"
      value={levels.level6}
      approvers={approvers}
      exclude={[levels.projectManager||0,...levels.level4]}
      onChange={id=>setLevels(v=>({...v,level6:id}))}
    />
   </div>

   <label>Message to approvers
    <textarea rows={3} value={message} onChange={e=>setMessage(e.target.value)}/>
   </label>

   <div className="approval-level-summary">
    <b>{selectedCount}</b> approver(s) selected · empty levels will be skipped.
   </div>

   <div className="budget-modal-actions">
    <button className="secondary" onClick={onClose}>Cancel</button>
    <button className="budget-primary" disabled={busy||!selectedCount} onClick={submit}>
     {busy?'Submitting…':'Submit KPI'}
    </button>
   </div>
  </div>
 </div>;
}
