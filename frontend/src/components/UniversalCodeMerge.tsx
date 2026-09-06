import {useEffect,useState} from 'react';

type MergeEntity={entity:string;table:string};

export function UniversalCodeMerge({
  defaultEntity,
  label='Merge'
}:{
  defaultEntity:string;
  label?:string;
}){
  const [open,setOpen]=useState(false);
  const [entities,setEntities]=useState<MergeEntity[]>([]);
  const [entity,setEntity]=useState(defaultEntity);
  const [sourceCode,setSourceCode]=useState('');
  const [targetCode,setTargetCode]=useState('');
  const [preview,setPreview]=useState<any>(null);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');

  useEffect(()=>{
    if(!open)return;
    fetch('/api/code-merge/entities',{credentials:'same-origin'})
      .then(async r=>{
        if(!r.ok)throw new Error(await r.text());
        return r.json();
      })
      .then((x:MergeEntity[])=>{
        setEntities(x);
        if(x.some(i=>i.entity===defaultEntity)) setEntity(defaultEntity);
        else if(x.length) setEntity(x[0].entity);
      })
      .catch(e=>setError(String(e)));
  },[open,defaultEntity]);

  const reset=()=>{
    setSourceCode('');
    setTargetCode('');
    setPreview(null);
    setError('');
  };

  const close=()=>{
    setOpen(false);
    reset();
  };

  const previewMerge=async()=>{
    setBusy(true);setError('');
    try{
      const r=await fetch('/api/code-merge/preview',{
        method:'POST',
        credentials:'same-origin',
        headers:{'Content-Type':'application/json'},
        body:JSON.stringify({entity,sourceCode,targetCode})
      });
      if(!r.ok)throw new Error(await r.text());
      setPreview(await r.json());
    }catch(e){setError(String(e))}
    finally{setBusy(false)}
  };

  const executeMerge=async()=>{
    if(!preview)return;
    const ok=confirm(
      `${preview.entity}: ${preview.source.code} → ${preview.target.code}\n\n`+
      `Toàn bộ dữ liệu liên quan sẽ chuyển về mã đích.\n`+
      `Mã nguồn sẽ bị xoá sau khi merge thành công.\n\nTiếp tục?`
    );
    if(!ok)return;

    setBusy(true);setError('');
    try{
      const r=await fetch('/api/code-merge/execute',{
        method:'POST',
        credentials:'same-origin',
        headers:{'Content-Type':'application/json'},
        body:JSON.stringify({entity,sourceCode,targetCode})
      });
      if(!r.ok)throw new Error(await r.text());
      const result=await r.json();
      alert(result.message);
      close();
      window.location.reload();
    }catch(e){setError(String(e))}
    finally{setBusy(false)}
  };

  return <>
    <button type="button" className="secondary" onClick={()=>setOpen(true)}>
      {label}
    </button>

    {open&&<div className="budget-modal-backdrop">
      <div className="budget-modal" style={{maxWidth:760}}>
        <div className="budget-modal-head">
          <div>
            <h2>Merge Code</h2>
            <p>Gộp một mã vào mã khác và chuyển toàn bộ dữ liệu liên quan về mã đích.</p>
          </div>
          <button type="button" onClick={close}>×</button>
        </div>

        <div className="budget-modal-body">
          <div className="budget-form-grid">
            <label>
              Module
              <select
                value={entity}
                onChange={e=>{setEntity(e.target.value);setPreview(null);setError('')}}>
                {entities.map(x=>
                  <option key={x.entity} value={x.entity}>{x.entity}</option>
                )}
              </select>
            </label>

            <div/>

            <label>
              Source Code — mã sẽ bỏ
              <input
                value={sourceCode}
                onChange={e=>{setSourceCode(e.target.value);setPreview(null)}}
                placeholder="VD: PRJ-0001"/>
            </label>

            <label>
              Target Code — mã giữ lại
              <input
                value={targetCode}
                onChange={e=>{setTargetCode(e.target.value);setPreview(null)}}
                placeholder="VD: PRJ-0002"/>
            </label>
          </div>

          {error&&<div className="budget-error" style={{marginTop:16}}>{error}</div>}

          {preview&&<div style={{marginTop:18}}>
            <h3>Merge Preview</h3>
            <p><b>{preview.source.code}</b> → <b>{preview.target.code}</b></p>

            {(preview.references||[])
              .filter((x:any)=>x.rows>0)
              .map((x:any,i:number)=>
                <div key={i}>{x.table}.{x.column}: <b>{x.rows}</b> row(s)</div>
              )
            }

            {preview.documents>0&&
              <div>Documents: <b>{preview.documents}</b> row(s)</div>
            }
          </div>}
        </div>

        <div className="budget-modal-actions">
          <button type="button" className="secondary" onClick={close}>
            Cancel
          </button>

          {!preview
            ? <button
                type="button"
                disabled={busy||!entity||!sourceCode.trim()||!targetCode.trim()}
                onClick={previewMerge}>
                {busy?'Checking…':'Preview Merge'}
              </button>
            : <button
                type="button"
                disabled={busy}
                onClick={executeMerge}>
                {busy?'Merging…':'Merge & Remove Source'}
              </button>
          }
        </div>
      </div>
    </div>}
  </>;
}
