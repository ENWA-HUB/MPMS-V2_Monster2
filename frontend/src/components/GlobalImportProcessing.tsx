import {useEffect,useRef,useState} from 'react';
import {LoaderCircle} from 'lucide-react';

type State={
  active:boolean;
  label:string;
  pending:number;
  requestCount:number;
};

const IMPORT_RE=/\b(import|nhập dữ liệu|nhập file)\b/i;
const IMPORT_FILE_RE=/(\.csv|\.xlsx|\.xls|text\/csv|spreadsheet|excel)/i;

export function GlobalImportProcessing(){
  const[state,setState]=useState<State>({
    active:false,
    label:'Processing import…',
    pending:0,
    requestCount:0
  });

  const activeRef=useRef(false);
  const pendingRef=useRef(0);
  const requestCountRef=useRef(0);
  const importIntentUntil=useRef(0);
  const hideTimer=useRef<number|undefined>(undefined);
  const safetyTimer=useRef<number|undefined>(undefined);
  const originalFetch=useRef<typeof window.fetch|null>(null);

  const clearHide=()=>{
    if(hideTimer.current){
      window.clearTimeout(hideTimer.current);
      hideTimer.current=undefined;
    }
  };

  const stop=()=>{
    clearHide();
    if(safetyTimer.current){
      window.clearTimeout(safetyTimer.current);
      safetyTimer.current=undefined;
    }
    activeRef.current=false;
    pendingRef.current=0;
    requestCountRef.current=0;
    setState({active:false,label:'Processing import…',pending:0,requestCount:0});
  };

  const scheduleStop=(delay=1400)=>{
    clearHide();
    hideTimer.current=window.setTimeout(()=>{
      if(pendingRef.current===0)stop();
    },delay);
  };

  const start=(label='Processing import…')=>{
    clearHide();
    activeRef.current=true;
    requestCountRef.current=0;
    setState({active:true,label,pending:pendingRef.current,requestCount:0});

    if(safetyTimer.current)window.clearTimeout(safetyTimer.current);
    safetyTimer.current=window.setTimeout(stop,10*60*1000);
  };

  useEffect(()=>{
    // 1) Detect clicks on any current/future Import button.
    // We only remember the intent here; overlay starts after a file is selected.
    const clickCapture=(ev:MouseEvent)=>{
      const target=ev.target as HTMLElement|null;
      const btn=target?.closest?.('button,[role="button"],a') as HTMLElement|null;
      if(!btn)return;
      const text=(btn.innerText||btn.textContent||btn.getAttribute('title')||'').trim();
      if(IMPORT_RE.test(text)){
        importIntentUntil.current=Date.now()+30_000;
      }
    };

    // 2) Start indicator when an import file is actually selected.
    const changeCapture=(ev:Event)=>{
      const input=ev.target as HTMLInputElement|null;
      if(!input || input.tagName!=='INPUT' || input.type!=='file' || !input.files?.length)return;

      const accept=input.accept||'';
      const fileName=input.files[0]?.name||'';
      const recentImportIntent=Date.now()<=importIntentUntil.current;
      const importLikeFile=IMPORT_FILE_RE.test(accept)||IMPORT_FILE_RE.test(fileName);

      if(recentImportIntent || importLikeFile){
        start('Processing import…');

        // Hold the processing state long enough for client-side parsing to start.
        // Network-idle logic below will close it after the import completes.
        clearHide();
        hideTimer.current=window.setTimeout(()=>{
          if(pendingRef.current===0 && requestCountRef.current===0)stop();
        },15_000);
      }
    };

    document.addEventListener('click',clickCapture,true);
    document.addEventListener('change',changeCapture,true);

    // 3) Track all fetch calls made while an import is active.
    // This covers sequential POST/PUT loops used by CSV imports.
    originalFetch.current=window.fetch.bind(window);
    const original=originalFetch.current;

    window.fetch=async (...args:Parameters<typeof fetch>):Promise<Response>=>{
      if(!activeRef.current)return original(...args);

      clearHide();
      pendingRef.current++;
      requestCountRef.current++;
      setState(s=>({
        ...s,
        pending:pendingRef.current,
        requestCount:requestCountRef.current
      }));

      try{
        return await original(...args);
      }finally{
        pendingRef.current=Math.max(0,pendingRef.current-1);
        setState(s=>({...s,pending:pendingRef.current}));
        if(pendingRef.current===0)scheduleStop(1600);
      }
    };

    return ()=>{
      document.removeEventListener('click',clickCapture,true);
      document.removeEventListener('change',changeCapture,true);
      if(originalFetch.current)window.fetch=originalFetch.current;
      clearHide();
      if(safetyTimer.current)window.clearTimeout(safetyTimer.current);
    };
  },[]);

  if(!state.active)return null;

  return <div className="global-import-processing" role="status" aria-live="polite">
    <div className="global-import-processing-card">
      <LoaderCircle className="global-import-spinner" size={34}/>
      <div>
        <b>{state.label}</b>
        <span>Please keep this page open while MPMS processes the file.</span>
        {state.requestCount>0&&
          <small>
            Processing data… {state.pending>0?`${state.pending} request${state.pending>1?'s':''} active`:'finishing'}
          </small>}
      </div>
    </div>
  </div>;
}
