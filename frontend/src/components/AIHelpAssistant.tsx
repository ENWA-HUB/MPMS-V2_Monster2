import {useMemo,useState} from 'react'
import {Bot,Send,X,Sparkles,HelpCircle} from 'lucide-react'
import './AIHelpAssistant.css'

type Msg={role:'user'|'assistant',text:string}

export default function AIHelpAssistant(){
 const [open,setOpen]=useState(false)
 const [question,setQuestion]=useState('')
 const [busy,setBusy]=useState(false)
 const [messages,setMessages]=useState<Msg[]>([
  {role:'assistant',text:'Hi, I am MPMS AI Help. I only guide end users on how to use MPMS features.'}
 ])
 const currentView=useMemo(()=>new URLSearchParams(window.location.search).get('view')||'dashboard',[open])

 const ask=async()=>{
  const q=question.trim()
  if(!q||busy)return
  setQuestion('')
  setMessages(v=>[...v,{role:'user',text:q}])
  setBusy(true)
  try{
   const r=await fetch('/api/ai-help/ask',{
    method:'POST',
    credentials:'same-origin',
    headers:{'Content-Type':'application/json'},
    body:JSON.stringify({question:q,view:currentView})
   })
   const text=await r.text()
   let data:any={}
   try{data=JSON.parse(text)}catch{}
   if(!r.ok) throw new Error(data?.message||text||`HTTP ${r.status}`)
   setMessages(v=>[...v,{role:'assistant',text:data?.answer||'AI Help could not find a suitable guide for that question.'}])
  }catch(e:any){
   const msg=String(e?.message||e||'')
   const text=msg.includes('401')||msg.toLowerCase().includes('authentication')
     ? 'Your MPMS session is no longer valid. Please sign in again and retry AI Help.'
     : 'AI Help could not answer this request. Please try again.'
   setMessages(v=>[...v,{role:'assistant',text}])
  }finally{setBusy(false)}
 }

 return <>
  <button className="mpms-ai-help-launcher" onClick={()=>setOpen(true)} title="AI Help"><Sparkles size={18}/><span>AI Help</span></button>
  {open&&<div className="mpms-ai-help-panel">
   <div className="mpms-ai-help-head">
    <div className="mpms-ai-help-title"><Bot size={20}/><div><b>MPMS AI Help</b><small>End User Guide</small></div></div>
    <button onClick={()=>setOpen(false)}><X size={19}/></button>
   </div>
   <div className="mpms-ai-help-context"><HelpCircle size={14}/> Current screen: <b>{currentView}</b></div>
   <div className="mpms-ai-help-messages">
    {messages.map((m,i)=><div key={i} className={`mpms-ai-msg ${m.role}`}>{m.text}</div>)}
    {busy&&<div className="mpms-ai-msg assistant">Thinking...</div>}
   </div>
   <div className="mpms-ai-help-quick">
    {['How do I create a Project?','How do I import Budget?','How do I assign an IT Asset?','How do Permissions & Data Scope work?'].map(x=><button key={x} onClick={()=>setQuestion(x)}>{x}</button>)}
   </div>
   <div className="mpms-ai-help-input">
    <input value={question} onChange={e=>setQuestion(e.target.value)} onKeyDown={e=>e.key==='Enter'&&ask()} placeholder="Ask how to use MPMS..."/>
    <button disabled={busy||!question.trim()} onClick={ask}><Send size={17}/></button>
   </div>
  </div>}
 </>
}
