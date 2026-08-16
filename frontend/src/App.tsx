import { useEffect, useMemo, useState } from 'react';
import { LayoutDashboard, FolderKanban, ListTodo, ShieldAlert, WalletCards, Handshake, Gauge, Stamp, FileText, Bell, Settings, Search, Menu, X, BarChart3, Presentation, CalendarRange, ClipboardCheck, UsersRound } from 'lucide-react';
import { Dashboard } from './pages/Dashboard';
import { DataPage } from './pages/DataPage';
import { KpiPage } from './pages/KpiPage';
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
import { TeamManagementPage } from './pages/TeamManagementPage';
import { getJson } from './lib/api';

type NavItem={key:string;label:string;icon:any;path?:string;columns?:{key:string;label:string;format?:'money'|'pct'|'date'|'status'}[]};
const nav:NavItem[]=[
 {key:'dashboard',label:'Dashboard',icon:LayoutDashboard},
 {key:'projects',label:'Projects',icon:FolderKanban},
 {key:'tasks',label:'Tasks & Milestones',icon:ListTodo},
 {key:'risks',label:'Risks & Issues',icon:ShieldAlert},
 {key:'budget',label:'Budget Control',icon:WalletCards},
 {key:'budgetPlan',label:'Annual Budget Plan',icon:CalendarRange},
 {key:'suppliers',label:'Supplier Management',icon:Handshake},
 {key:'kpi',label:'Supplier KPI',icon:Gauge},
 {key:'projectTeam',label:'Project Team',icon:UsersRound},
 {key:'teamPerformance',label:'Team Performance',icon:ClipboardCheck},
 {key:'approvals',label:'Approvals',icon:Stamp},
 {key:'reports',label:'Reports & Analytics',icon:BarChart3},
 {key:'presentation',label:'Executive View',icon:Presentation},
 {key:'documents',label:'Documents',icon:FileText},
];

export function App(){
 const [active,setActive]=useState('dashboard'); const [mobile,setMobile]=useState(false); const [notifications,setNotifications]=useState<any[]>([]);
 useEffect(()=>{getJson<any[]>('/notifications').then(setNotifications).catch(()=>{})},[]);
 const current=useMemo(()=>nav.find(x=>x.key===active)!,[active]);
 return <div className="shell">
   <aside className={`sidebar ${mobile?'open':''}`}>
    <div className="brand"><img className="brand-logo" src="/maipt-logo.png" alt="MAIPT"/><div><b>MAIPT</b><span>PROJECT MANAGEMENT</span></div><button className="close-mobile" onClick={()=>setMobile(false)}><X size={20}/></button></div>
    <div className="nav-section">WORKSPACE</div>
    <nav>{nav.map(item=>{const Icon=item.icon;return <button key={item.key} className={active===item.key?'active':''} onClick={()=>{setActive(item.key);setMobile(false)}}><Icon size={18}/><span>{item.label}</span>{item.key==='approvals'&&<em>1</em>}</button>})}</nav>
    <div className="nav-section">SYSTEM</div>
    <nav><button onClick={()=>setActive('settings')} className={active==='settings'?'active':''}><Settings size={18}/><span>Administration</span></button></nav>
    <div className="profile"><div className="avatar">MP</div><div><b>Mai Pham</b><span>CIO / Program Director</span></div></div>
   </aside>
   <main>
    <header><button className="menu-mobile" onClick={()=>setMobile(true)}><Menu/></button><div className="search"><Search size={18}/><input placeholder="Search projects, suppliers, risks..."/></div><div className="header-actions"><button className="bell"><Bell size={19}/>{notifications.some(x=>!x.isRead)&&<i/>}</button><span className="date">16 Aug 2026</span></div></header>
    <div className="page">
      {active==='dashboard'&&<Dashboard/>}
      {active==='projects'&&<ProjectsPage/>}
      {active==='tasks'&&<TasksMilestonesPage/>}
      {active==='risks'&&<RisksIssuesPage/>}
      {current?.path&&<DataPage title={current.label} path={current.path} columns={current.columns||[]}/>} 
      {active==='budget'&&<BudgetPage/>}
      {active==='budgetPlan'&&<BudgetPlanningPage/>}
      {active==='suppliers'&&<SupplierPage/>}
      {active==='kpi'&&<KpiPage/>}
      {active==='projectTeam'&&<TeamManagementPage/>}
      {active==='teamPerformance'&&<TeamPerformancePage/>}
      {active==='approvals'&&<ApprovalsPage/>}
      {active==='presentation'&&<PresentationPage/>}
      {active==='reports'&&<ReportsPage/>}
      {active==='documents'&&<DocumentsPage/>}
      {active==='settings'&&<Placeholder title="Administration" text="Organization, users, KPI criteria, master data, RBAC and audit-log administration."/>}
    </div>
   </main>
 </div>
}
function Placeholder({title,text}:{title:string;text:string}){return <><div className="page-title"><div><h1>{title}</h1><p>{text}</p></div></div><div className="panel empty"><div className="empty-icon"><Settings/></div><h3>Module foundation is ready</h3><p>This V2 package includes the database entities and API structure for this module.</p></div></>}
