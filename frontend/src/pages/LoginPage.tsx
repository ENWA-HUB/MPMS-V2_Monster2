import { useState } from 'react';
import { Eye, EyeOff, LockKeyhole, LogIn } from 'lucide-react';
import { postJson } from '../lib/api';

export type AuthUser={id:number;name:string;email:string;jobTitle:string;department:string;role:string;mustChangePassword:boolean};

export function LoginPage({onLogin}:{onLogin:(u:AuthUser)=>void}){
 const [email,setEmail]=useState('');const [password,setPassword]=useState('');const [show,setShow]=useState(false);const [error,setError]=useState('');const [busy,setBusy]=useState(false);
 const submit=async(e:React.FormEvent)=>{e.preventDefault();setBusy(true);setError('');try{const u=await postJson<AuthUser>('/auth/login',{email,password});onLogin(u)}catch{setError('Email or password is incorrect.')}finally{setBusy(false)}};
 return <div className="login-shell">
  <div className="login-visual"><div className="login-brand"><img src="/maipt-logo.png"/><div><b>MAIPT</b><span>PROJECT MANAGEMENT</span></div></div><div className="login-message"><span>PROJECT · BUDGET · PERFORMANCE</span><h1>One workspace for project delivery and accountability.</h1><p>Members update their own work and KPI. Project managers and management evaluate progress from the same controlled dataset.</p></div><div className="login-foot">MAIPT Project Management System</div></div>
  <div className="login-form-side"><form className="login-card" onSubmit={submit}><div className="login-icon"><LockKeyhole/></div><h2>Sign in</h2><p>Use your project account to continue.</p>
   {error&&<div className="login-error">{error}</div>}
   <label>Email<input autoFocus required type="email" value={email} onChange={e=>setEmail(e.target.value)} placeholder="name@company.com"/></label>
   <label>Password<div className="password-field"><input required type={show?'text':'password'} value={password} onChange={e=>setPassword(e.target.value)} placeholder="••••••••"/><button type="button" onClick={()=>setShow(!show)}>{show?<EyeOff/>:<Eye/>}</button></div></label>
   <button className="login-submit" disabled={busy}><LogIn/>{busy?'Signing in...':'Sign in'}</button>
   <small>Contact the project administrator if you do not have an account or need your password reset.</small>
  </form></div>
 </div>;
}

export function ChangePasswordPage({user,onChanged,onLogout}:{user:AuthUser;onChanged:()=>void;onLogout:()=>void}){
 const [currentPassword,setCurrent]=useState('');const [newPassword,setNew]=useState('');const [confirm,setConfirm]=useState('');const [error,setError]=useState('');
 const submit=async(e:React.FormEvent)=>{e.preventDefault();setError('');if(newPassword!==confirm){setError('New passwords do not match.');return}try{await postJson('/auth/change-password',{currentPassword,newPassword});onChanged()}catch(e){setError(String(e))}};
 return <div className="login-shell password-change"><div className="login-form-side"><form className="login-card" onSubmit={submit}><div className="login-icon"><LockKeyhole/></div><h2>Change temporary password</h2><p>{user.name}, your account requires a new password before continuing.</p>{error&&<div className="login-error">{error}</div>}
 <label>Temporary password<input required type="password" value={currentPassword} onChange={e=>setCurrent(e.target.value)}/></label>
 <label>New password<input required minLength={8} type="password" value={newPassword} onChange={e=>setNew(e.target.value)}/></label>
 <label>Confirm new password<input required minLength={8} type="password" value={confirm} onChange={e=>setConfirm(e.target.value)}/></label>
 <button className="login-submit">Update password</button><button className="login-cancel" type="button" onClick={onLogout}>Sign out</button></form></div></div>;
}
