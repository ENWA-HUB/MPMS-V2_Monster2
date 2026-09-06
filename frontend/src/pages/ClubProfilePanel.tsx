import {useEffect,useMemo,useRef,useState} from 'react';
import {BookOpen,Download,Edit3,Eye,File,FileSpreadsheet,FileText,Grid3X3,Image as ImageIcon,List,Upload,UsersRound,X} from 'lucide-react';

const api=async(u:string,o?:RequestInit)=>{
 const r=await fetch(u,{credentials:'same-origin',...o,headers:{...(o?.body&&!((o.body as any) instanceof FormData)?{'Content-Type':'application/json'}:{}),...(o?.headers||{})}});
 const d=await r.json().catch(()=>null);if(!r.ok)throw new Error(d?.message||`HTTP ${r.status}`);return d
};

function Preview({doc,close}:{doc:any;close:()=>void}){
 const mime=String(doc.mimeType||'').toLowerCase();
 const image=mime.startsWith('image/');
 const pdf=mime.includes('pdf');
 return <div className="ct-back"><div className="ct-modal club-preview-modal">
  <header><h3>{doc.originalFileName}</h3><button onClick={close}><X/></button></header>
  <div className="club-preview-body">
   {image?<img src={doc.viewUrl} alt={doc.originalFileName}/>:pdf?<iframe src={doc.viewUrl}/>:doc.storageWebUrl?<iframe src={doc.storageWebUrl}/>:<div className="club-preview-unsupported"><FileText/><p>Browser preview is not available for this file type.</p></div>}
  </div>
  <footer><button onClick={close}>Close</button>{doc.downloadUrl&&<a className="primary" href={doc.downloadUrl}><Download/> Download</a>}</footer>
 </div></div>
}

export function ClubProfilePanel({clubId,onClubChanged}:{clubId:number;onClubChanged?:()=>void}){
 const[data,setData]=useState<any>(null),[docs,setDocs]=useState<any[]>([]),[canDownload,setCanDownload]=useState(false),[err,setErr]=useState(''),[busy,setBusy]=useState(false);
 const[edit,setEdit]=useState(false),[form,setForm]=useState<any>({}),[preview,setPreview]=useState<any>(null),[libraryView,setLibraryView]=useState<'grid'|'list'>('grid');
 const fileRef=useRef<HTMLInputElement|null>(null),[cat,setCat]=useState('CLUB_LIBRARY');

 const load=async()=>{if(!clubId)return;try{setErr('');const[a,b]=await Promise.all([api(`/api/personal-fc/clubs/${clubId}/profile`),api(`/api/personal-fc/clubs/${clubId}/documents`)]);setData(a);setDocs(b.items||[]);setCanDownload(!!b.canDownload)}catch(e:any){setErr(e.message)}};
 useEffect(()=>{load()},[clubId]);

 const logo=useMemo(()=>docs.find(x=>x.category==='CLUB_LOGO'),[docs]);
 const team=useMemo(()=>docs.find(x=>x.category==='CLUB_TEAM_IMAGE'),[docs]);
 const regs=useMemo(()=>docs.filter(x=>x.category==='CLUB_REGULATION'),[docs]);
 const lib=useMemo(()=>docs.filter(x=>!['CLUB_LOGO','CLUB_TEAM_IMAGE','CLUB_REGULATION'].includes(x.category)),[docs]);

 const choose=(c:string)=>{setCat(c);setTimeout(()=>fileRef.current?.click(),0)};
 const upload=async(e:React.ChangeEvent<HTMLInputElement>)=>{const f=e.target.files?.[0];e.target.value='';if(!f)return;try{setBusy(true);const fd=new FormData();fd.append('file',f);fd.append('category',cat);fd.append('name',cat);await api(`/api/personal-fc/clubs/${clubId}/documents/upload`,{method:'POST',body:fd});await load()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
 const remove=async(d:any)=>{if(!confirm(`Delete ${d.originalFileName}?`))return;try{setBusy(true);await api(`/api/personal-fc/clubs/${clubId}/documents/${d.id}`,{method:'DELETE'});await load()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
 const save=async()=>{try{setBusy(true);await api(`/api/personal-fc/clubs/${clubId}/profile`,{method:'PUT',body:JSON.stringify(form)});setEdit(false);await load();onClubChanged?.()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};

 if(!data)return <div>{err||'Loading club profile...'}</div>;const p=data.profile||{};
 const fileKind=(d:any)=>{
  const mime=String(d?.mimeType||'').toLowerCase(),name=String(d?.originalFileName||'').toLowerCase();
  if(mime.startsWith('image/')||/\.(png|jpe?g|webp|gif)$/i.test(name))return 'image';
  if(mime.includes('pdf')||name.endsWith('.pdf'))return 'pdf';
  if(mime.includes('sheet')||mime.includes('excel')||/\.(xlsx?|csv)$/i.test(name))return 'excel';
  if(mime.includes('word')||/\.(docx?|rtf)$/i.test(name))return 'word';
  if(mime.includes('presentation')||/\.(pptx?)$/i.test(name))return 'ppt';
  return 'file';
 };
 const LibraryIcon=({d}:{d:any})=>fileKind(d)==='excel'?<FileSpreadsheet/>:fileKind(d)==='image'?<ImageIcon/>:fileKind(d)==='file'?<File/>:<FileText/>;
 const DocRow=({d}:{d:any})=><div className="cp-doc"><span><FileText/><b>{d.originalFileName}</b></span><span><button onClick={()=>setPreview(d)} title="View"><Eye/></button>{canDownload&&d.downloadUrl&&<a href={d.downloadUrl} title="Download"><Download/></a>}<button onClick={()=>remove(d)} title="Delete">×</button></span></div>;

 return <div className="club-profile-v13">
  {err&&<div className="budget-error">{err}</div>}
  <input ref={fileRef} hidden type="file" accept=".png,.jpg,.jpeg,.webp,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx" onChange={upload}/>

  <div className="cp-actions"><button onClick={()=>{setForm({...p});setEdit(true)}}><Edit3/> Edit Profile</button></div>

  <div className="cp-kpis"><div><span>Total Members</span><b>{data.totalMembers||0}</b></div><div><span>Active Members</span><b>{data.activeMembers||0}</b></div><div><span>Fund Balance</span><b>{Number(data.fundBalance||0).toLocaleString('vi-VN')} ₫</b></div><div><span>Upcoming Activities</span><b>{data.upcomingActivities?.length||0}</b></div></div>

  <div className="cp-grid">
   
   <article className="wide"><header><h4>Team Image</h4><button onClick={()=>choose('CLUB_TEAM_IMAGE')}><Upload/> Change</button></header>{team?<img className="cp-image team" src={team.viewUrl} onClick={()=>setPreview(team)}/>:<div className="cp-empty"><UsersRound/>No team image</div>}</article>
   <article className="wide"><h4>About</h4><p>{p.about||'—'}</p></article>
   <article><h4>Mission</h4><p>{p.mission||'—'}</p></article><article><h4>Vision</h4><p>{p.vision||'—'}</p></article>
   <article><h4>Core Values</h4><p>{p.coreValues||'—'}</p></article><article><h4>Contact</h4><p>{p.contactEmail||'—'}<br/>{p.contactPhone||'—'}<br/>{p.website||'—'}</p></article>
   <article><header><h4>Regulations</h4><button onClick={()=>choose('CLUB_REGULATION')}><Upload/> Add</button></header><p>{p.regulationsSummary||'—'}</p>{regs.map((d:any)=><DocRow d={d} key={d.id}/>)}</article>
   <article><h4>Activities</h4><p>{p.activitiesSummary||'—'}</p>{(data.upcomingActivities||[]).map((x:any)=><div className="cp-doc" key={x.id}><span>{x.name}</span><small>{x.eventDate}</small></div>)}</article>
   <article className="wide club-library-section">
    <header className="club-library-head"><h4>Libraries & Documents</h4><div className="club-library-actions">
     <button className={libraryView==='grid'?'active':''} title="Icon view" onClick={()=>setLibraryView('grid')}><Grid3X3/></button>
     <button className={libraryView==='list'?'active':''} title="List view" onClick={()=>setLibraryView('list')}><List/></button>
     <button onClick={()=>choose('CLUB_LIBRARY')}><Upload/> Add Document</button>
    </div></header>
    {!lib.length?<p>No documents.</p>:libraryView==='grid'?
     <div className="club-library-grid">{lib.map((d:any)=><div className="club-file-card" key={d.id}>
      <button className={`club-file-preview ${fileKind(d)}`} onClick={()=>setPreview(d)} title="View">
       {fileKind(d)==='image'?<img src={d.viewUrl} alt={d.originalFileName}/>:<LibraryIcon d={d}/>}
      </button>
      <div className="club-file-info"><b title={d.originalFileName}>{d.originalFileName}</b><small>{String(d.category||'DOCUMENT').replace('CLUB_','')}</small></div>
      <div className="club-file-tools"><button onClick={()=>setPreview(d)} title="View"><Eye/></button>{canDownload&&d.downloadUrl&&<a href={d.downloadUrl} title="Download"><Download/></a>}<button onClick={()=>remove(d)} title="Delete">×</button></div>
     </div>)}</div>:
     <div className="club-library-list">{lib.map((d:any)=><div className="club-file-row" key={d.id}>
      <button className={`club-file-row-icon ${fileKind(d)}`} onClick={()=>setPreview(d)}>{fileKind(d)==='image'?<img src={d.viewUrl} alt=""/>:<LibraryIcon d={d}/>}</button>
      <button className="club-file-row-name" onClick={()=>setPreview(d)}><b>{d.originalFileName}</b><small>{String(d.category||'DOCUMENT').replace('CLUB_','')}</small></button>
      <span className="club-file-row-tools"><button onClick={()=>setPreview(d)} title="View"><Eye/></button>{canDownload&&d.downloadUrl&&<a href={d.downloadUrl} title="Download"><Download/></a>}<button onClick={()=>remove(d)} title="Delete">×</button></span>
     </div>)}</div>}
   </article>
  </div>

  {edit&&<div className="ct-back"><div className="ct-modal"><header><h3>Edit Club Profile</h3><button onClick={()=>setEdit(false)}><X/></button></header><div className="ct-form">
   {['contactEmail','contactPhone','website','socialLink','mainVenue'].map(k=><label className="ct-field" key={k}><span>{k}</span><input value={form[k]||''} onChange={e=>setForm({...form,[k]:e.target.value})}/></label>)}
   {['about','mission','vision','coreValues','regulationsSummary','activitiesSummary'].map(k=><label className="ct-field" key={k}><span>{k}</span><textarea value={form[k]||''} onChange={e=>setForm({...form,[k]:e.target.value})}/></label>)}
  </div><footer><button onClick={()=>setEdit(false)}>Cancel</button><button className="primary" onClick={save} disabled={busy}>Save Profile</button></footer></div></div>}
  {preview&&<Preview doc={preview} close={()=>setPreview(null)}/>}
 </div>
}
