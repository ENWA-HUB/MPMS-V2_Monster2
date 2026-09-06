import {useEffect,useMemo,useState} from 'react';
import {Search} from 'lucide-react';

type ResultRow={
  kind:string;
  module:string;
  title:string;
  meta?:string;
};

const arr=(x:any):any[]=>{
  if(Array.isArray(x))return x;
  if(!x||typeof x!=='object')return [];
  for(const k of ['items','data','rows','clubs','members','directory','tournaments','registrations','results','value']){
    if(Array.isArray(x[k]))return x[k];
  }
  for(const v of Object.values(x)){
    if(Array.isArray(v))return v as any[];
  }
  return [];
};

async function api(path:string){
  const r=await fetch(`/api${path}`,{credentials:'same-origin'});
  if(!r.ok)throw new Error(`HTTP ${r.status}`);
  return await r.json();
}

export default function GlobalSearch({onOpen}:{onOpen:(module:string)=>void}){
  const[q,setQ]=useState('');
  const[rows,setRows]=useState<ResultRow[]>([]);
  const[busy,setBusy]=useState(false);
  const[open,setOpen]=useState(false);

  const needle=useMemo(()=>q.trim().toLowerCase(),[q]);

  useEffect(()=>{
    if(needle.length<2){setRows([]);setBusy(false);return}
    let cancelled=false;
    const timer=window.setTimeout(async()=>{
      setBusy(true);

      const [projectsRaw,tasksRaw,milestonesRaw,kpiCriteriaRaw,kpiEvalRaw,clubsRaw]=await Promise.all([
        api('/projects').catch(()=>[]),
        api('/tasks').catch(()=>[]),
        api('/milestones').catch(()=>[]),
        api('/kpi/criteria').catch(()=>[]),
        api('/kpi/evaluations').catch(()=>[]),
        api('/personal-fc/clubs').catch(()=>api('/personal-fc/dashboard').catch(()=>[]))
      ]);

      if(cancelled)return;

      const projects=arr(projectsRaw);
      const tasks=arr(tasksRaw);
      const milestones=arr(milestonesRaw);
      const criteria=arr(kpiCriteriaRaw);
      const evaluations=arr(kpiEvalRaw);
      const clubs=arr(clubsRaw);
      if(import.meta.env.DEV)console.debug('[GlobalSearch]',{q:needle,projects:projects.length,tasks:tasks.length,milestones:milestones.length,clubs:clubs.length});

      const hit=(...v:any[])=>v.filter(x=>x!==null&&x!==undefined).join(' ').toLowerCase().includes(needle);
      const result:ResultRow[]=[];

      for(const x of projects){
        if(hit(x.code,x.name,x.title,x.description,x.portfolio,x.owner,x.status))
          result.push({kind:'Project',module:'projects',title:`${x.code||''}${x.code?' — ':''}${x.name||x.title||''}`,meta:[x.portfolio,x.status].filter(Boolean).join(' · ')});
      }
      for(const x of tasks){
        if(hit(x.code,x.name,x.title,x.project,x.projectName,x.assignee,x.status,x.priority))
          result.push({kind:'Task',module:'tasks',title:`${x.code||''}${x.code?' — ':''}${x.name||x.title||''}`,meta:[x.projectName||x.project,x.assignee,x.status].filter(Boolean).join(' · ')});
      }
      for(const x of milestones){
        if(hit(x.name,x.title,x.project,x.projectName,x.status))
          result.push({kind:'Milestone',module:'tasks',title:x.name||x.title||'Milestone',meta:[x.projectName||x.project,x.status].filter(Boolean).join(' · ')});
      }
      for(const x of criteria){
        if(hit(x.code,x.name,x.title))
          result.push({kind:'KPI Criteria',module:'kpi',title:`${x.code||''}${x.code?' — ':''}${x.name||x.title||''}`,meta:x.weightPct!=null?`${x.weightPct}%`:''});
      }
      for(const x of evaluations){
        if(hit(x.supplier,x.supplierName,x.project,x.projectName,x.periodType,x.status,x.rating))
          result.push({kind:'KPI Evaluation',module:'kpi',title:x.supplierName||x.supplier||'KPI Evaluation',meta:[x.projectName||x.project,x.periodType,x.status].filter(Boolean).join(' · ')});
      }

      // Personal FC: Club Members + Tournament Members
      const clubBundles=await Promise.all(clubs.map(async(club:any)=>{
        const [membersRaw,toursRaw]=await Promise.all([
          api(`/personal-fc/clubs/${club.id}/directory`).catch(()=>api(`/personal-fc/clubs/${club.id}/members`).catch(()=>[])),
          api(`/personal-fc/clubs/${club.id}/tournaments`).catch(()=>[])
        ]);
        return {club,members:arr(membersRaw),tours:arr(toursRaw)};
      }));

      if(cancelled)return;

      for(const {club,members} of clubBundles){
        for(const m of members){
          if(hit(
            m.memberCode,m.memberName,m.name,m.fullName,m.email,m.phone,m.sex,
            m.skillRank,m.rank,m.company,m.department,club.code,club.name
          )){
            result.push({
              kind:'Club Member',
              module:'personalFc',
              title:`${m.memberName||m.fullName||m.name||'Member'}${m.memberCode?` — ${m.memberCode}`:''}`,
              meta:[club.name||club.code,m.email||m.phone].filter(Boolean).join(' · ')
            });
          }
        }
      }

      const tourJobs:any[]=[];
      for(const {club,tours} of clubBundles){
        for(const t of tours){
          tourJobs.push(
            api(`/personal-fc/tournaments/${t.id}/registrations`)
              .catch(()=>api(`/personal-fc/tournaments/${t.id}/members`).catch(()=>[]))
              .then(raw=>({club,tour:t,regs:arr(raw)}))
          );
        }
      }

      const tourBundles=await Promise.all(tourJobs);
      if(cancelled)return;

      for(const {club,tour,regs} of tourBundles){
        for(const m of regs){
          if(hit(
            m.fullName,m.name,m.phone,m.company,m.department,m.sex,m.skillRank,m.rank,
            m.division,m.group,m.shirtSize,club.code,club.name,tour.code,tour.name
          )){
            result.push({
              kind:'Tournament Member',
              module:'personalFc',
              title:m.fullName||m.name||'Tournament Member',
              meta:[tour.name||tour.code,club.name||club.code,m.phone].filter(Boolean).join(' · ')
            });
          }
        }
      }

      if(!cancelled){
        setRows(result.slice(0,24));
        setBusy(false);
        setOpen(true);
      }
    },250);

    return()=>{cancelled=true;window.clearTimeout(timer)};
  },[needle]);

  return <div className="search global-search-v3">
    <Search size={18}/>
    <input
      value={q}
      onChange={e=>{setQ(e.target.value);setOpen(true)}}
      onFocus={()=>{if(needle.length>=2)setOpen(true)}}
      onKeyDown={e=>{
        if(e.key==='Escape')setOpen(false);
        if(e.key==='Enter'&&rows[0]){onOpen(rows[0].module);setOpen(false)}
      }}
      placeholder="Search projects, tasks, KPI, club & tournament members..."
     onInput={e=>window.dispatchEvent(new CustomEvent('mpms-global-search',{detail:{query:(e.currentTarget as HTMLInputElement).value}}))} />
    {q&&<button type="button" className="gsv3-clear" onClick={()=>{setQ('');setRows([]);setOpen(false)}}>×</button>}
    {open&&needle.length>=2&&<div className="gsv3-results">
      {busy?<div className="gsv3-state">Searching…</div>:
       rows.length?rows.map((r,i)=><button type="button" key={`${r.kind}-${i}`} onMouseDown={e=>e.preventDefault()} onClick={()=>{onOpen(r.module);setOpen(false)}}>
        <span className="gsv3-kind">{r.kind}</span>
        <span className="gsv3-text"><b>{r.title}</b>{r.meta&&<small>{r.meta}</small>}</span>
       </button>):<div className="gsv3-state">No matching results.</div>}
    </div>}
  </div>
}

