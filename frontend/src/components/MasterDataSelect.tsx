import { useEffect, useMemo, useState } from 'react';
import { getJson } from '../lib/api';

type OrgUnit={id:number;code:string;name:string;status:string};
type Category={id:number;code:string;name:string;scope:string;status:string};
type Options={orgUnits:OrgUnit[];categories?:Category[]};

export function BusinessUnitSelect({value,onChange,required=true}:{value:string;onChange:(value:string)=>void;required?:boolean}){
 const [options,setOptions]=useState<Options>({orgUnits:[]});
 useEffect(()=>{getJson<Options>('/settings/options').then(setOptions).catch(()=>{})},[]);
 const rows=useMemo(()=>options.orgUnits.filter(x=>x.status==='ACTIVE'),[options]);
 return <select required={required} value={value||''} onChange={e=>onChange(e.target.value)}>
  <option value="">Select Business Unit</option>
  {rows.map(x=><option key={x.id} value={x.code}>{x.code} — {x.name}</option>)}
 </select>;
}

export function CategorySelect({value,onChange,scope='GENERAL',required=false}:{value:string;onChange:(value:string)=>void;scope?:string;required?:boolean}){
 const [options,setOptions]=useState<Options>({orgUnits:[],categories:[]});
 useEffect(()=>{getJson<Options>('/settings/options').then(setOptions).catch(()=>{})},[]);
 const rows=useMemo(()=>(options.categories||[]).filter(x=>x.status==='ACTIVE'&&(x.scope==='GENERAL'||x.scope===scope)),[options,scope]);
 return <select required={required} value={value||''} onChange={e=>onChange(e.target.value)}>
  <option value="">Select Category</option>
  {rows.map(x=><option key={x.id} value={x.name}>{x.code} — {x.name}</option>)}
 </select>;
}
