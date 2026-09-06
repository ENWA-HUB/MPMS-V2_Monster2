
import { useEffect, useMemo, useRef, useState } from 'react';
import { Download, Edit3, Eye, File, FileImage, FileSpreadsheet, FileText, Folder, Plus, RefreshCw, Trash2, Upload, X } from 'lucide-react';
import { deleteJson, getJson, postJson, putJson, uploadForm } from '../lib/api';
type FolderRow={name:string;count:number};type Doc={id:number;name:string;originalFileName:string;category:string;entityType:string;entityId:number;project?:string;author?:string;uploadedAt:string;fileSize:number;mimeType:string;downloadUrl:string};type Overview={folders:FolderRow[];documents:Doc[]};type Project={id:number;name:string};type SyncResult={scanned:number;added:number;updated:number};
const size=(n:number)=>n>=1048576?`${(n/1048576).toFixed(1)} MB`:n>=1024?`${Math.round(n/1024)} KB`:`${n} B`;
const docPath=(category:string)=>{const c=(category||'GENERAL').trim(),u=c.toUpperCase();if(u.startsWith('CLUB_'))return ['PERSONAL FC','CLUB',c];if(u==='IT POLICIES & PROCEDURES')return ['IT ASSETS & SERVICES',c];if(u.includes('CAPITAL'))return ['CAPITAL & INVESTMENT','CAPITAL MANAGEMENT',c];if(u.includes('INVEST'))return ['CAPITAL & INVESTMENT','INVESTMENT MANAGEMENT',c];if(u.includes('SUPPLIER'))return ['ORGANIZATION','SUPPLIERS',c];if(u.includes('CONTRACT'))return ['FINANCIAL CONTROL','CONTRACTS',c];if(u.includes('BUDGET'))return ['FINANCIAL CONTROL','BUDGET',c];if(u.includes('KPI')||u.includes('PERFORMANCE'))return ['PERFORMANCE & KPI',c];if(u.includes('ASSET')||u.includes('LICENSE')||u.includes('HANDOVER'))return ['IT ASSETS & SERVICES',c];if(u.includes('PROJECT'))return ['PLAN & BUDGET','PROJECTS',c];if(u==='AVATAR')return ['SYSTEM','AVATAR'];if(u==='GENERAL')return ['SYSTEM','GENERAL'];return ['GENERAL',c]};
const icon=(m:string)=>m.includes('sheet')||m.includes('excel')?<FileSpreadsheet/>:m.startsWith('image/')?<FileImage/>:m.includes('pdf')||m.includes('word')?<FileText/>:<File/>;
export function DocumentsPage(){
 const [canDownloadDocuments,setCanDownloadDocuments]=useState(false);
 useEffect(()=>{getJson<any>('/access/me').then(a=>{
   const p=a?.permissions?.DOCUMENTS||[];
   setCanDownloadDocuments(p.includes('DOWNLOAD'));
 }).catch(()=>setCanDownloadDocuments(false))},[]);

 const [folderPath,setFolderPath]=useState<string[]>([]);const [data,setData]=useState<Overview|null>(null),[projects,setProjects]=useState<Project[]>([]),[folder,setFolder]=useState('ALL'),[openFolder,setOpenFolder]=useState(false),[newFolder,setNewFolder]=useState(''),[error,setError]=useState(''),[notice,setNotice]=useState(''),[uploading,setUploading]=useState(false),[syncing,setSyncing]=useState(false);const fileRef=useRef<HTMLInputElement|null>(null);
 const load=async()=>{const [d,p]=await Promise.all([getJson<Overview>('/documents/overview'),getJson<Project[]>('/projects')]);setData(d);setProjects(p)};
 const syncSharePoint=async(silent=false)=>{if(syncing)return;setSyncing(true);if(!silent){setError('');setNotice('Scanning SharePoint...')}try{const r=await postJson<SyncResult>('/m365-documents/sync',{});await load();setNotice(`SharePoint sync completed: ${r.scanned} scanned, ${r.added} added, ${r.updated} updated.`)}catch(e){if(!silent)setError(`SharePoint sync failed: ${String(e)}`)}finally{setSyncing(false)}};
 useEffect(()=>{(async()=>{try{await load();await syncSharePoint(true)}catch(e){setError(String(e))}})()},[]);
 const docs=useMemo(()=>data?.documents.filter(d=>{if(folder!=='ALL')return d.category===folder;if(!folderPath.length)return true;return docPath(d.category).slice(0,folderPath.length).join('/')===folderPath.join('/')})||[],[data,folder,folderPath]);
 const folderCards=useMemo(()=>{const m=new Map<string,{name:string,count:number,path:string[],leaf:boolean,category?:string}>();(data?.folders||[]).forEach(f=>{const path=docPath(f.name);if(path.slice(0,folderPath.length).join('/')!==folderPath.join('/'))return;const n=path[folderPath.length];if(!n)return;const leaf=folderPath.length===path.length-1;const x=m.get(n)||{name:n,count:0,path:[...folderPath,n],leaf,category:leaf?f.name:undefined};x.count+=f.count;m.set(n,x)});return [...m.values()]},[data,folderPath]);
 const upload=async(e:React.ChangeEvent<HTMLInputElement>)=>{const files=Array.from(e.target.files||[]);if(!files.length)return;setUploading(true);setError('');let done=0;const failed:string[]=[];try{for(const f of files){setNotice(`Uploading ${done+1}/${files.length}: ${f.name}`);const fd=new FormData();fd.append('file',f);fd.append('category',folder==='ALL'?'GENERAL':folder);fd.append('entityType','DOCUMENT');fd.append('entityId','0');try{await uploadForm('/documents/upload',fd);done++}catch(err){failed.push(`${f.name}: ${String(err)}`)}}await load();setNotice(`${done}/${files.length} files uploaded successfully.`);if(failed.length)setError(`Failed ${failed.length} file(s): ${failed.join(' | ')}`)}finally{setUploading(false);e.target.value=''}};

 const editDoc=async(d:Doc)=>{const name=prompt('Document name',d.name);if(name===null)return;const category=prompt('Folder / category',d.category);if(category===null)return;try{await putJson(`/documents/${d.id}`,{...d,name,category,status:'ACTIVE'});await load()}catch(e){setError(String(e))}};
 const deleteDoc=async(d:Doc)=>{
  if(!confirm(`Delete document “${d.originalFileName||d.name}”?`))return;
  setError('');
  try{
    await deleteJson(`/documents/${d.id}`);

    // Update UI immediately after API returns 204.
    setData(prev=>{
      if(!prev)return prev;
      const documents=prev.documents.filter(x=>x.id!==d.id);
      const folders=prev.folders
        .map(f=>f.name===d.category?{...f,count:Math.max(0,f.count-1)}:f)
        .filter(f=>f.count>0);
      return {...prev,documents,folders};
    });

    // Reconcile against server with a cache-busting request.
    try{
      const fresh=await getJson<Overview>(`/documents/overview?_=${Date.now()}`);
      setData(fresh);
    }catch{
      // Optimistic state above is already correct; don't put the deleted row back.
    }
  }catch(e){
    setError(String(e));
    // If delete itself failed, reload authoritative server state.
    try{await load()}catch{}
  }
};

 const createFolder=async(e:React.FormEvent)=>{e.preventDefault();try{await postJson('/documents/folders',{name:newFolder});setNewFolder('');setOpenFolder(false);await load()}catch(e){setError(String(e))}};
 if(!data)return <div className="loading">{error||'Loading documents...'}</div>;
  const viewDocument=async(d:Doc)=>{
   try{
     const r=await fetch(`/api/documents/${d.id}/view`,{credentials:'same-origin'});
     if(!r.ok) throw new Error(`${r.status} ${await r.text()}`);
     const blob=await r.blob();
     const url=URL.createObjectURL(blob);
     window.open(url,'_blank','noopener,noreferrer');
     setTimeout(()=>URL.revokeObjectURL(url),60000);
   }catch(e){
     const msg=`Cannot view document: ${String(e)}`;
     setError(msg); alert(msg);
   }
 };
 const downloadDocument=async(d:Doc)=>{
   try{
     const r=await fetch(`/api/documents/${d.id}/download`,{credentials:'same-origin'});
     if(!r.ok) throw new Error(`${r.status} ${await r.text()}`);
     const blob=await r.blob();
     const url=URL.createObjectURL(blob);
     const a=document.createElement('a');
     a.href=url; a.download=d.originalFileName||d.name||`document-${d.id}`;
     document.body.appendChild(a); a.click(); a.remove();
     setTimeout(()=>URL.revokeObjectURL(url),60000);
   }catch(e){
     const msg=`Cannot download document: ${String(e)}`;
     setError(msg); alert(msg);
   }
 };
return <><div className="page-title"><div><h1>Document Management</h1><p>Manage project documents, contracts, deliverables and reports</p></div><div className="project-actions"><input ref={fileRef} type="file" multiple hidden onChange={upload}/><button className="secondary" disabled={syncing||uploading} onClick={()=>syncSharePoint(false)}><RefreshCw size={16} className={syncing?'spin':''}/> {syncing?'Syncing...':'Sync SharePoint'}</button><button className="secondary" disabled={uploading||syncing} onClick={()=>fileRef.current?.click()}><Upload size={16}/> {uploading?'Uploading...':'Upload Documents'}</button><button className="budget-primary" onClick={()=>setOpenFolder(true)}><Plus size={16}/> New Folder</button></div></div>{error&&<div className="budget-error">{error}</div>}{notice&&<div style={{margin:'0 0 16px',padding:'12px 16px',border:'1px solid #cfe0ef',borderRadius:10,background:'#f4f9fd',color:'#254767',fontSize:12}}>{notice}</div>}
 <section className="documents-panel"><div className="documents-head"><h3>Folders</h3>{folderPath.length>0&&<button className="secondary" onClick={()=>{setFolder('ALL');setFolderPath(x=>x.slice(0,-1))}}>← Back</button>}</div>{folderPath.length>0&&<div className="doc-breadcrumb">Documents / {folderPath.join(' / ')}</div>}<div className="folders-grid">{folderPath.length===0&&<button className={folder==='ALL'?'folder-card active':'folder-card'} onClick={()=>setFolder('ALL')}><Folder/><div><b>All Documents</b><span>{data.documents.length} files</span></div></button>}{folderCards.map(f=><button key={f.path.join('/')} className={folder===f.category?'folder-card active':'folder-card'} onClick={()=>{if(f.leaf){setFolder(f.category||'ALL')}else{setFolder('ALL');setFolderPath(f.path)}}}><Folder/><div><b>{f.name}</b><span>{f.count} files</span></div></button>)}</div></section>
 <section className="documents-panel"><div className="documents-head"><h3>Recent Documents</h3><span>{docs.length} files</span></div><div className="module-table-wrap"><table className="module-table documents-table"><thead><tr><th>Document</th><th>Folder / Entity</th><th>Author</th><th>Last Modified</th><th>Size</th><th>Actions</th></tr></thead><tbody>{docs.map(d=><tr key={d.id}><td><div className="doc-name">{icon(d.mimeType)}<b>{d.originalFileName||d.name}</b></div></td><td>{d.category}<small>{d.project||`${d.entityType} #${d.entityId}`}</small></td><td>{d.author||'System'}</td><td>{new Date(d.uploadedAt).toLocaleDateString()}</td><td>{size(d.fileSize)}</td><td><div className="doc-actions"><button type="button" onClick={()=>viewDocument(d)} title="View"><Eye/></button>{canDownloadDocuments&&<button type="button" onClick={()=>downloadDocument(d)} title="Download"><Download/></button>}<button onClick={()=>editDoc(d)}><Edit3/></button><button className="danger" onClick={()=>deleteDoc(d)}><Trash2/></button></div></td></tr>)}</tbody></table></div></section>
 {openFolder&&<div className="budget-modal-backdrop" onMouseDown={()=>setOpenFolder(false)}><div className="budget-modal small-modal" onMouseDown={e=>e.stopPropagation()}><div className="budget-modal-head"><div><h3>New Folder</h3><p>Create a document category/folder.</p></div><button onClick={()=>setOpenFolder(false)}><X/></button></div><form onSubmit={createFolder}><label>Folder name<input required value={newFolder} onChange={e=>setNewFolder(e.target.value)} placeholder="Project Charters"/></label><div className="budget-modal-actions"><button type="button" className="secondary" onClick={()=>setOpenFolder(false)}>Cancel</button><button className="budget-primary">Create Folder</button></div></form></div></div>}
 </>;
}
