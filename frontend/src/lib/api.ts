export const API = import.meta.env.VITE_API_URL || '/api';

export async function getJson<T>(path:string):Promise<T>{
  const r=await fetch(`${API}${path}`);
  if(!r.ok) throw new Error(`${r.status} ${r.statusText}`);
  return r.json();
}

export async function postJson<T>(path:string, body?:unknown):Promise<T>{
  const r=await fetch(`${API}${path}`,{
    method:'POST',
    headers:{'Content-Type':'application/json'},
    body:body===undefined?undefined:JSON.stringify(body)
  });
  if(!r.ok) throw new Error(await r.text());
  return r.json();
}


export async function uploadForm<T>(path:string, form:FormData):Promise<T>{
  const response=await fetch(`${API}${path}`,{method:'POST',body:form});
  if(!response.ok) throw new Error(await response.text());
  return response.json();
}

export async function putJson<T>(path:string, body?:unknown):Promise<T>{
  const r=await fetch(`${API}${path}`,{method:'PUT',headers:{'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
  if(!r.ok) throw new Error(await r.text());
  return r.status===204 ? (undefined as T) : r.json();
}

export async function deleteJson(path:string):Promise<void>{
  const r=await fetch(`${API}${path}`,{method:'DELETE'});
  if(!r.ok) throw new Error(await r.text());
}
