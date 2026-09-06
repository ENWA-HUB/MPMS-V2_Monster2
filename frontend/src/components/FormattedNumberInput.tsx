import React from "react";
type P=Omit<React.InputHTMLAttributes<HTMLInputElement>,"value"|"onChange"|"type"> & {value:any;onValueChange:(raw:string)=>void;decimals?:number};
export default function FormattedNumberInput({value,onValueChange,decimals,...rest}:P){
 const raw=String(value??"").replace(/,/g,"");
 const parts=raw.split(".");
 const a=parts[0]||"", b=parts.length>1?parts.slice(1).join(""):undefined;
 const sign=a.startsWith("-")?"-":"";
 const ints=(sign?a.slice(1):a).replace(/\D/g,"");
 const display=raw===""?"":sign+ints.replace(/\B(?=(\d{3})+(?!\d))/g,",")+(b!==undefined?"."+b.slice(0,decimals??20):"");
 return <input {...rest} inputMode="decimal" value={display} onChange={e=>{
  let v=e.target.value.replace(/,/g,"").replace(/[^\d.-]/g,"");
  const neg=v.startsWith("-");v=v.replace(/-/g,"");if(neg)v="-"+v;
  const p=v.split(".");if(p.length>2)v=p.shift()+"."+p.join("");
  if(decimals!==undefined&&v.includes(".")){const q=v.split(".");v=q[0]+"."+q[1].slice(0,decimals)}
  onValueChange(v);
 }}/>;
}
