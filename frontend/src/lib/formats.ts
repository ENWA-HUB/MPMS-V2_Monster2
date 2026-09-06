export const formatDateDisplay=(value:any)=>{
 if(!value)return ''; const s=String(value).trim();
 const m=s.match(/^(\d{4})-(\d{2})-(\d{2})/); if(m)return `${m[3]}/${m[2]}/${m[1]}`;
 return s;
};
export const formatMonthDisplay=(value:any)=>{
 if(!value)return ''; const s=String(value).trim();
 const m=s.match(/^(\d{4})-(\d{2})/); return m?`${m[2]}/${m[1]}`:s;
};
export const formatNumberDisplay=(value:any,decimals?:number)=>{
 if(value===null||value===undefined||value==='')return '';
 const n=Number(String(value).replace(/,/g,'')); if(!Number.isFinite(n))return String(value);
 const o:Intl.NumberFormatOptions={useGrouping:true,maximumFractionDigits:decimals??2};
 if(decimals!==undefined)o.minimumFractionDigits=decimals;
 return new Intl.NumberFormat('en-US',o).format(n);
};
export const formatMoneyDisplay=(value:any,currency?:string)=>
 formatNumberDisplay(value,(currency||'VND').toUpperCase()==='VND'?0:2);
export const parseFormattedNumber=(value:any)=>{
 const n=Number(String(value??'').replace(/,/g,'').trim()); return Number.isFinite(n)?n:0;
};
