import { useEffect, useMemo, useState } from 'react';
import { LayoutDashboard, FolderKanban, ListTodo, ShieldAlert, WalletCards, Handshake, Gauge, Stamp, FileText, Bell, Settings, Search, Menu, X, BarChart3, Presentation, CalendarRange, ClipboardCheck, UsersRound, LogOut } from 'lucide-react';
import { Dashboard } from './pages/Dashboard';
import { DataPage } from './pages/DataPage';
import { ApprovalsPage } from './pages/ApprovalsPage';
import { BudgetPage } from './pages/BudgetPage';
import { PresentationPage } from './pages/PresentationPage';
import { SupplierPage } from './pages/SupplierPage';
import { ReportsPage } from './pages/ReportsPage';
import { ProjectsPage } from './pages/ProjectsPage';
import { DocumentsPage } from './pages/DocumentsPage';
import { RisksIssuesPage } from './pages/RisksIssuesPage';
import { TasksMilestonesPage } from './pages/TasksMilestonesPage';
import { BudgetPlanningPage } from './pages/BudgetPlanningPage';
import { TeamPerformancePage } from './pages/TeamPerformancePage';
import { SettingsPage } from './pages/SettingsPage';
import { getJson, postJson } from './lib/api';
import { LoginPage, ChangePasswordPage, type AuthUser } from './pages/LoginPage';

type NavItem={key:string;label:string;icon:any;path?:string;columns?:{key:string;label:string;format?:'money'|'pct'|'date'|'status'}[]};
const nav:NavItem[]=[
 {key:'dashboard',label:'Dashboard',icon:LayoutDashboard},
 {key:'projects',label:'Projects',icon:FolderKanban},
 {key:'tasks',label:'Tasks & Milestones',icon:ListTodo},
 {key:'risks',label:'Risks & Issues',icon:ShieldAlert},
 {key:'budget',label:'Budget Control',icon:WalletCards},
 {key:'budgetPlan',label:'Annual Budget Plan',icon:CalendarRange},
 {key:'suppliers',label:'Supplier Management',icon:Handshake},
 {key:'teamPerformance',label:'Team Performance',icon:ClipboardCheck},
 {key:'approvals',label:'Approvals',icon:Stamp},
 {key:'reports',label:'Reports & Analytics',icon:BarChart3},
 {key:'presentation',label:'Executive View',icon:Presentation},
 {key:'documents',label:'Documents',icon:FileText},
];

export function App(){
 const [auth,setAuth]=useState<AuthUser|null>(null);
 const [authReady,setAuthReady]=useState(false);
 const [active,setActive]=useState('dashboard');
 const [mobile,setMobile]=useState(false);
 const [notifications,setNotifications]=useState<any[]>([]);

 const managerRoles=['ADMIN','CIO','HOD','PM','PROJECT_MANAGER'];
 const canManage=!!auth&&managerRoles.includes(String(auth.role||'').toUpperCase());
 const memberKeys=['projects','tasks','teamPerformance','documents'];
 const visibleNav=useMemo(()=>canManage?nav:nav.filter(x=>memberKeys.includes(x.key)),[canManage]);

 useEffect(()=>{getJson<AuthUser>('/auth/me').then(u=>{setAuth(u);setAuthReady(true)}).catch(()=>{setAuth(null);setAuthReady(true)})},[]);
 useEffect(()=>{if(auth)getJson<any[]>('/notifications').then(setNotifications).catch(()=>{})},[auth]);

 const logout=async()=>{try{await postJson('/auth/logout')}catch{}setAuth(null);setNotifications([]);setActive('dashboard')};
 const changedPassword=async()=>{try{const u=await getJson<AuthUser>('/auth/me');setAuth(u)}catch{setAuth(null)}};

 if(!authReady)return <div className="loading">Loading workspace…</div>;
 if(!auth)return <LoginPage onLogin={setAuth}/>;
 if(auth.mustChangePassword)return <ChangePasswordPage user={auth} onChanged={changedPassword} onLogout={logout}/>;

 const systemKeys = canManage ? ['settings'] : [];
 const allowedKeys = [...visibleNav.map(x=>x.key), ...systemKeys];
 const current = visibleNav.find(x=>x.key===active);

 if(!allowedKeys.includes(active))
   setTimeout(()=>setActive(visibleNav[0]?.key||'dashboard'),0);

 return <div className="shell">
   <aside className={`sidebar ${mobile?'open':''}`}>
    <div className="brand"><img className="brand-logo" src="/maipt-logo.png" alt="MAIPT"/><div><b>MAIPT</b><span>PROJECT MANAGEMENT</span></div><button className="close-mobile" onClick={()=>setMobile(false)}><X size={20}/></button></div>
    <div className="nav-section">WORKSPACE</div>
    <nav>{visibleNav.map(item=>{const Icon=item.icon;return <button key={item.key} className={active===item.key?'active':''} onClick={()=>{setActive(item.key);setMobile(false)}}><Icon size={18}/><span>{item.label}</span>{item.key==='approvals'&&<em>1</em>}</button>})}</nav>
    {canManage&&<><div className="nav-section">SYSTEM</div><nav><button onClick={()=>setActive('settings')} className={active==='settings'?'active':''}><Settings size={18}/><span>Settings</span></button></nav></>}
    <div className="profile"><div className="avatar">{initials(auth.name)}</div><div><b>{auth.name}</b><span>{auth.jobTitle||auth.role}</span></div><button className="sidebar-logout" onClick={logout} title="Sign out"><LogOut/></button></div>
   </aside>
   <main>
    <header><button className="menu-mobile" onClick={()=>setMobile(true)}><Menu/></button><div className="search"><Search size={18}/><input placeholder="Search projects, tasks and KPI..."/></div><div className="header-actions"><button className="bell"><Bell size={19}/>{notifications.some(x=>!x.isRead)&&<i/>}</button><span className="user-chip">{auth.name} · {auth.role}</span><button className="header-logout" onClick={logout}><LogOut size={16}/></button></div></header>
    <div className="page">
      {active==='dashboard'&&<Dashboard/>}
      {active==='projects'&&<ProjectsPage/>}
      {active==='tasks'&&<TasksMilestonesPage/>}
      {active==='risks'&&canManage&&<RisksIssuesPage/>}
      {current?.path&&<DataPage title={current.label} path={current.path} columns={current.columns||[]}/>}
      {active==='budget'&&canManage&&<BudgetPage/>}
      {active==='budgetPlan'&&canManage&&<BudgetPlanningPage/>}
      {active==='suppliers'&&canManage&&<SupplierPage/>}
      {active==='teamPerformance'&&<TeamPerformancePage/>}
      {active==='approvals'&&canManage&&<ApprovalsPage/>}
      {active==='presentation'&&canManage&&<PresentationPage/>}
      {active==='reports'&&canManage&&<ReportsPage/>}
      {active==='documents'&&<DocumentsPage/>}
      {active==='settings'&&canManage&&<SettingsPage/>}
    </div>
   </main>
 </div>
}
function initials(name:string){return name.split(/\s+/).filter(Boolean).slice(-2).map(x=>x[0]?.toUpperCase()).join('')||'U'}

function Placeholder({title,text}:{title:string;text:string}){return <><div className="page-title"><div><h1>{title}</h1><p>{text}</p></div></div><div className="panel empty"><div className="empty-icon"><Settings/></div><h3>Module foundation is ready</h3><p>This V2 package includes the database entities and API structure for this module.</p></div></>}
