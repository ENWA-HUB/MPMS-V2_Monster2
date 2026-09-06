
// GLOBAL_DELETED_SEMANTICS_V3_1
const isOperationallyDeleted=(x:any)=>{
  if(!x || typeof x!=='object') return false;
  const st=String(x.status ?? x.Status ?? '').trim().toUpperCase();
  return st==='DEACTIVATED' || st==='DELETED' || x.isDeleted===true || x.IsDeleted===true;
};

const stripDeletedDeep=(value:any):any=>{
  if(Array.isArray(value))
    return value.filter(x=>!isOperationallyDeleted(x)).map(stripDeletedDeep);
  if(value && typeof value==='object'){
    const out:any={};
    for(const [k,v] of Object.entries(value)) out[k]=stripDeletedDeep(v);
    return out;
  }
  return value;
};


async function safeJsonResponse<T>(r: Response): Promise<T> {
  const text = await r.text();
  let body: any = null;

  if (text) {
    try { body = JSON.parse(text); } catch { body = null; }
  }

  if (!r.ok) {
    const msg =
      body?.message ||
      body?.error ||
      (text && !text.trim().startsWith("<") ? text : "") ||
      `${r.status} ${r.statusText || "Request failed"}`;
    throw new Error(msg);
  }

  if (!text) return null as T;
  if (body !== null) return body as T;
  throw new Error(`Server returned an invalid response (${r.status}).`);
}

export const API = import.meta.env.VITE_API_URL || '/api';

async function apiError(response:Response):Promise<Error>{
  let raw='';
  try{ raw=await response.text(); }catch{}
  let message='';
  let detail='';

  if(raw){
    try{
      const j=JSON.parse(raw);
      message=String(j?.message||j?.title||'').trim();
      detail=String(j?.detail||'').trim();
    }catch{
      message=raw.trim();
    }
  }

  if(response.status===403){
    return new Error(message
      ? `Permission denied. ${message}`
      : 'Permission denied. You do not have permission to perform this action.');
  }

  if(response.status===401){
    return new Error(message
      ? `Authentication required. ${message}`
      : 'Authentication required. Please sign in again.');
  }

  if(response.status===404){
    return new Error(message
      ? `Not found. ${message}`
      : 'The requested item was not found or is no longer available.');
  }

  if(response.status===409){
    return new Error(detail
      ? `${message||'The action could not be completed.'} ${detail}`
      : (message||'The action could not be completed because related data still exists.'));
  }

  if(response.status>=500){
    return new Error(detail
      ? `${message||'System error.'} ${detail}`
      : (message||'System error. Please try again or contact the administrator.'));
  }

  return new Error(message||`Request failed (${response.status}).`);
}

export async function getJsonRawInternal<T>(path:string):Promise<T>{
  const url=`${API}${path}`;

  let r:Response;
  try{
    r=await fetch(url,{
      credentials:'same-origin',
      cache:'no-store'
    });
  }catch(e){
    throw new Error(
      `Unable to reach API ${url}: ${e instanceof Error ? e.message : String(e)}`
    );
  }

  if(!r.ok){
    throw await apiError(r);
  }

  const contentType=(r.headers.get('content-type')||'').toLowerCase();

  if(!contentType.includes('application/json') &&
     !contentType.includes('+json')){
    const text=await r.text();
    const preview=text.replace(/\\s+/g,' ').trim().slice(0,120);

    throw new Error(
      `API ${url} returned ${r.status} ${contentType||'unknown content-type'} instead of JSON` +
      (preview ? `: ${preview}` : '')
    );
  }

  const value=await safeJsonResponse<T>(r);
  return value;
}

export async function postJson<T>(path:string, body?:unknown):Promise<T>{
  const r=await fetch(`${API}${path}`,{
    method:'POST',
    credentials:'same-origin',
    headers:{'Content-Type':'application/json'},
    body:body===undefined?undefined:JSON.stringify(body)
  });
  if(!r.ok) throw await apiError(r);
  return r.json();
}


export async function uploadForm<T>(path:string, form:FormData):Promise<T>{
  const response=await fetch(`${API}${path}`,{method:'POST',credentials:'same-origin',body:form});
  if(!response.ok) throw await apiError(response);
  return response.json();
}

export async function putJson<T>(path:string, body?:unknown):Promise<T>{
  const r=await fetch(`${API}${path}`,{method:'PUT',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
  if(!r.ok) throw await apiError(r);
  return r.status===204 ? (undefined as T) : r.json();
}

export async function deleteJson(path:string):Promise<void>{
  const r=await fetch(`${API}${path}`,{method:'DELETE',credentials:'same-origin'});
  if(!r.ok) throw await apiError(r);
}


// Normal application reads: deleted/deactivated records never participate in UI/business logic.
export async function getJson<T=any>(path:string):Promise<T>{
  const value:any=await (getJsonRawInternal as any)(path);
  return stripDeletedDeep(value) as T;
}

// ADMIN/ROOT audit screens may explicitly use this to inspect deleted records.
export async function getJsonRaw<T=any>(path:string):Promise<T>{
  return await (getJsonRawInternal as any)(path) as T;
}
