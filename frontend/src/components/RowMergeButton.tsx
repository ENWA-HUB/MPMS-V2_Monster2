import {useEffect,useState} from 'react';
import {GitMerge} from 'lucide-react';

type MergeItem={
  id:number;
  code?:string|null;
};

type MergeOption={
  id:number;
  code:string;
};

export function RowMergeButton({
  entity,
  source
}:{
  entity:string;
  source:MergeItem;
}){
  const [open,setOpen]=useState(false);
  const [options,setOptions]=useState<MergeOption[]>([]);
  const [targetCode,setTargetCode]=useState('');
  const [preview,setPreview]=useState<any>(null);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');

  useEffect(()=>{
    if(!open)return;
    setBusy(true);
    setError('');
    fetch(`/api/code-merge/options/${encodeURIComponent(entity)}`,{
      credentials:'same-origin'
    })
      .then(async r=>{
        if(!r.ok)throw new Error(await r.text());
        return r.json();
      })
      .then((rows:MergeOption[])=>setOptions(rows.filter(x=>x.id!==source.id)))
      .catch(e=>setError(String(e)))
      .finally(()=>setBusy(false));
  },[open,entity,source.id]);

  const close=()=>{
    setOpen(false);
    setOptions([]);
    setTargetCode('');
    setPreview(null);
    setError('');
  };

  const previewMerge=async()=>{
    if(!source.code||!targetCode)return;
    setBusy(true);setError('');
    try{
      const r=await fetch('/api/code-merge/preview',{
        method:'POST',
        credentials:'same-origin',
        headers:{'Content-Type':'application/json'},
        body:JSON.stringify({entity,sourceCode:source.code,targetCode})
      });
      if(!r.ok)throw new Error(await r.text());
      setPreview(await r.json());
    }catch(e){setError(String(e))}
    finally{setBusy(false)}
  };

  const executeMerge=async()=>{
    if(!source.code||!targetCode||!preview)return;
    if(!confirm(`${source.code} -> ${targetCode}

Move all related data to the target code and remove the source code?`))return;

    setBusy(true);setError('');
    try{
      const r=await fetch('/api/code-merge/execute',{
        method:'POST',
        credentials:'same-origin',
        headers:{'Content-Type':'application/json'},
        body:JSON.stringify({entity,sourceCode:source.code,targetCode})
      });
      if(!r.ok)throw new Error(await r.text());
      const result=await r.json();
      alert(result.message);
      close();
      window.location.reload();
    }catch(e){setError(String(e))}
    finally{setBusy(false)}
  };

  if(!source.code)return null;

  return <>
    <button type="button" title={`Merge ${source.code}`} onClick={()=>setOpen(true)}>
      <GitMerge/>
    </button>

    {open&&<div className="budget-modal-backdrop" onMouseDown={close}>
      <div className="budget-modal" style={{maxWidth:640}} onMouseDown={e=>e.stopPropagation()}>
        <div className="budget-modal-head">
          <div>
            <h3>Merge {source.code}</h3>
            <p>Source is fixed to the selected row. Choose the target code.</p>
          </div>
          <button type="button" onClick={close}>×</button>
        </div>

        <div className="budget-modal-body">
          <div className="budget-form-grid">
            <label>Source<input value={source.code||''} disabled/></label>
            <label>Target
              <select value={targetCode} disabled={busy}
                onChange={e=>{setTargetCode(e.target.value);setPreview(null);setError('')}}>
                <option value="">{busy?'Loading...':'Select target...'}</option>
                {options.map(x=><option key={x.id} value={x.code}>{x.code}</option>)}
              </select>
            </label>
          </div>

          {error&&<div className="budget-error" style={{marginTop:14}}>{error}</div>}

          {preview&&<div style={{marginTop:18}}>
            <h4>Preview</h4>
            <p><b>{preview.source.code}</b> → <b>{preview.target.code}</b></p>
            {(preview.references||[]).filter((x:any)=>x.rows>0).map((x:any,i:number)=>
              <div key={i}>{x.table}.{x.column}: <b>{x.rows}</b> row(s)</div>
            )}
            {preview.documents>0&&<div>Documents: <b>{preview.documents}</b> row(s)</div>}
          </div>}
        </div>

        <div className="budget-modal-actions">
          <button type="button" className="secondary" onClick={close}>Cancel</button>
          {!preview
            ? <button type="button" disabled={busy||!targetCode} onClick={previewMerge}>{busy?'Checking...':'Preview'}</button>
            : <button type="button" disabled={busy} onClick={executeMerge}>{busy?'Merging...':'Merge'}</button>}
        </div>
      </div>
    </div>}
  </>;
}
