import {useEffect,useState} from 'react';
import {Download,FileText,Paperclip,Trash2,Upload} from 'lucide-react';

export type SupplierProfileFile={id:number;originalFileName:string;fileSize:number;mimeType:string;uploadedAt:string;downloadUrl:string};
const fmtSize=(n:number)=>n<1024?`${n} B`:n<1024*1024?`${(n/1024).toFixed(1)} KB`:`${(n/1024/1024).toFixed(1)} MB`;

export async function uploadSupplierProfiles(supplierId:number,files:File[]){
  for(const file of files){
    const fd=new FormData(); fd.append('file',file);
    const r=await fetch(`/api/suppliers/${supplierId}/profile-files`,{method:'POST',credentials:'same-origin',body:fd});
    if(!r.ok)throw new Error(await r.text());
  }
}

export function SupplierProfileFiles({supplierId,pendingFiles,onPendingFiles}:{supplierId?:number;pendingFiles?:File[];onPendingFiles?:(files:File[])=>void}){
  const [items,setItems]=useState<SupplierProfileFile[]>([]);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [canDownload,setCanDownload]=useState(false);

  const load=async()=>{if(!supplierId)return;const r=await fetch(`/api/suppliers/${supplierId}/profile-files`,{credentials:'same-origin'});if(!r.ok)throw new Error(await r.text());setItems(await r.json())};
  useEffect(()=>{fetch('/api/access/me',{credentials:'same-origin'}).then(r=>r.ok?r.json():null).then(a=>setCanDownload((a?.permissions?.SUPPLIERS||[]).includes('DOWNLOAD'))).catch(()=>setCanDownload(false))},[]);
  useEffect(()=>{if(supplierId)load().catch(e=>setError(String(e)))},[supplierId]);

  const choose=async(files:FileList|null)=>{if(!files?.length)return;const selected=Array.from(files);setError('');if(!supplierId){onPendingFiles?.([...(pendingFiles||[]),...selected]);return}setBusy(true);try{await uploadSupplierProfiles(supplierId,selected);await load()}catch(e){setError(String(e))}finally{setBusy(false)}};
  const removePending=(index:number)=>onPendingFiles?.((pendingFiles||[]).filter((_,i)=>i!==index));
  const removeExisting=async(id:number)=>{if(!supplierId||!confirm('Delete this Supplier Profile file?'))return;setBusy(true);try{const r=await fetch(`/api/suppliers/${supplierId}/profile-files/${id}`,{method:'DELETE',credentials:'same-origin'});if(!r.ok)throw new Error(await r.text());await load()}catch(e){setError(String(e))}finally{setBusy(false)}};

  return <div className="supplier-profile-files">
    <div className="supplier-profile-head"><div><b><Paperclip size={15}/> Supplier Profile / Attachment</b><small>PDF, Office, JPG/PNG or ZIP · maximum 20 MB/file</small></div><label className="secondary supplier-profile-upload"><Upload size={14}/> {busy?'Uploading…':'Attach Profile'}<input hidden type="file" multiple disabled={busy} accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.jpg,.jpeg,.png,.zip" onChange={e=>{choose(e.target.files);e.currentTarget.value=''}}/></label></div>
    {error&&<div className="budget-error">{error}</div>}
    {!supplierId&&(pendingFiles||[]).length>0&&<div className="supplier-profile-list">{(pendingFiles||[]).map((f,i)=><div key={`${f.name}-${i}`} className="supplier-profile-item"><FileText size={15}/><span><b>{f.name}</b><small>{fmtSize(f.size)} · uploads after Supplier is saved</small></span><button type="button" className="danger" onClick={()=>removePending(i)}><Trash2 size={14}/></button></div>)}</div>}
    {supplierId&&<div className="supplier-profile-list">{items.length===0&&<div className="supplier-profile-empty">No Supplier Profile attached.</div>}{items.map(x=><div key={x.id} className="supplier-profile-item"><FileText size={15}/><span><b>{x.originalFileName}</b><small>{fmtSize(x.fileSize)} · {new Date(x.uploadedAt).toLocaleString()}</small></span>{canDownload&&<a href={x.downloadUrl} download title="Download"><Download size={14}/></a>}<button type="button" className="danger" disabled={busy} onClick={()=>removeExisting(x.id)}><Trash2 size={14}/></button></div>)}</div>}
  </div>
}
