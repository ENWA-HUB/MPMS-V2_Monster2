import {useEffect,useMemo,useState} from 'react';
import {LayoutDashboard, FolderKanban, ListTodo, ShieldAlert, WalletCards, Handshake, Gauge, Stamp, FileText, Bell, Settings, Search, Menu, X, BarChart3, Presentation, CalendarRange, ClipboardCheck, UsersRound, LogOut, KeyRound, HelpCircle, FileSignature, Landmark, TrendingUp} from 'lucide-react';
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
import AIHelpAssistant from './components/AIHelpAssistant'
import './components/AIHelpPositionFix.css'
import HelpExplorer from './components/HelpExplorer'

import { AccessControlPage } from './pages/AccessControlPage';
type NavItem={key:string;label:string;icon:any;path?:string;columns?:{key:string;label:string;format?:'money'|'pct'|'date'|'status'}[]};
import { TeamManagementPage } from './pages/TeamManagementPage';
import { ITAssetsPage } from './pages/ITAssetsPage';

import { ContractsPage } from './pages/ContractsPage';
import { GlobalImportProcessing } from './components/GlobalImportProcessing';
import {UserHrProfileDialog} from './components/UserHrProfileDialog';
import {PersonalFcPage} from './pages/PersonalFcPage';
import {CapitalInvestmentPage} from './pages/CapitalInvestmentPage';
import GlobalSearch from './components/GlobalSearch';
const nav:NavItem[]=[
 {key:'dashboard',label:'DASHBOARD',icon:LayoutDashboard},
 {key:'projects',label:'Projects',icon:FolderKanban},
 {key:'tasks',label:'Tasks & Milestones',icon:ListTodo},
 {key:'risks',label:'Risks & Issues',icon:ShieldAlert},
 {key:'budget',label:'Budget Control',icon:WalletCards},
 {key:'budgetPlan',label:'Annual Work & Budget Plan',icon:CalendarRange},
 {key:'contracts',label:'Contracts',icon:FileSignature},
  { key: 'PERSONAL_FC', label: 'PERSONAL FC', icon: FileSignature },
 {key:'suppliers',label:'Suppliers',icon:Handshake},
 {key:'personalFc',label:'Personal FC',icon:WalletCards},
 {key:'teamPerformance',label:'Team Performance',icon:ClipboardCheck},
 {key:'approvals',label:'Approvals',icon:Stamp},
 {key:'reports',label:'Reports & Analytics',icon:BarChart3},
 {key:'presentation',label:'Executive View',icon:Presentation},
 {key:'documents',label:'Documents',icon:FileText},
 {key:'capitalMgmt',label:'CAPITAL MGMT',icon:Landmark},
 {key:'investMgmt',label:'INVEST MGMT',icon:TrendingUp},

 {key:'peopleResources',label:'People & Resources',icon:UsersRound},
 {key:'itAssets',label:'IT Assets & Services',icon:Gauge},
];

export function PermissionDenied({module}:{module:string}){
 return <div className="panel" style={{padding:28,maxWidth:760}}><h2>Access denied</h2><p>You do not have permission to access <b>{module.replaceAll("_"," ")}</b>.</p></div>
}

export function App(){
  const [openMenu,setOpenMenu]=useState<string>('');

  const [helpOpen,setHelpOpen]=useState(false);

 const [accountOpen,setAccountOpen]=useState(false);
 const [auth,setAuth]=useState<AuthUser|null>(null);
 const [authReady,setAuthReady]=useState(false);
 const [active,setActive]=useState(()=>{
  const p=new URLSearchParams(window.location.search);
  return p.get('view')||'dashboard';
});
 const [mobile,setMobile]=useState(false);
 const [notifications,setNotifications]=useState<any[]>([]);
 const [notificationOpen,setNotificationOpen]=useState(false);
 const [access,setAccess]=useState<any>(null);
 const isRoot=(auth?.role||'').trim().toUpperCase()==='ROOT';
 const navModule=(key:string)=>({dashboard:'DASHBOARD',projects:'PROJECTS',tasks:'TASKS',risks:'RISKS',budget:'BUDGET',budgetPlan:'BUDGET',contracts:'CONTRACTS',suppliers:'SUPPLIERS',kpi:'KPI',peopleResources:'USERS',itAssets:'IT_ASSETS',projectTeam:'PROJECT_TEAM',teamPerformance:'KPI',approvals:'APPROVALS',reports:'REPORTS',presentation:'EXECUTIVE',documents:'DOCUMENTS',capitalMgmt:'CAPITAL',investMgmt:'INVESTMENT'} as Record<string,string>)[key]||key.toUpperCase();
 const canView=(module:string)=>isRoot||!!access?.modules?.includes(module);
 const denied=(module:string)=><PermissionDenied module={module}/>;
 const visibleNav=useMemo(()=>nav,[access]);
 const orderedNav=(keys:string[])=>keys.map(key=>visibleNav.find(item=>item.key===key)).filter((item):item is NavItem=>!!item);
 const inactiveSidebarItemStyle={background:'transparent',borderColor:'transparent',boxShadow:'none',color:'#aebacd'};
 const navGroups=useMemo(()=>[
   {key:'overview',label:'OVERVIEW',items:visibleNav.filter(x=>['dashboard'].includes(x.key))},
   {key:'planning',label:'PLAN & BUDGET',items:orderedNav(['budgetPlan','projects','portfolio'])},
   {key:'financial',label:'FINANCIAL CONTROL',items:visibleNav.filter(x=>['budget','contracts','suppliers','personalFc'].includes(x.key))},
   {key:'performance',label:'PERFORMANCE & KPI',items:orderedNav(['teamPerformance','tasks','risks','approvals','documents','reports','presentation','kpi'])},
   {key:'capitalInvestment',label:'CAPITAL & INVESTMENT',items:orderedNav(['capitalMgmt','investMgmt'])},
   {key:'organization',label:'ORGANIZATION',items:visibleNav.filter(x=>['peopleResources','itAssets','projectTeam'].includes(x.key))}
 ].filter(g=>g.items.length>0),[visibleNav]);

 useEffect(()=>{
   if(active==='dashboard'){setOpenMenu('');return}
   const group=navGroups.find(g=>g.items.some(item=>item.key===active));
   if(group)setOpenMenu(group.key);
   else if(active==='settings'||active==='accessControl')setOpenMenu('administration');
 },[active,navGroups]);

 const canSettings=canView('SETTINGS');
 const canAccessControl=canView('ACCESS_CONTROL');
 const canUsers=canView('USERS');
 const canProjectTeam=canView('PROJECT_TEAM');

 // MPMS_VIEW_QUERY_SYNC_V1_1
 useEffect(()=>{
   const url=new URL(window.location.href);
   if(url.searchParams.get('view')!==active){
     url.searchParams.set('view',active);
     window.history.replaceState(null,'',url);
   }
 },[active]);

 useEffect(()=>{
   const restore=()=>{
     const v=new URLSearchParams(window.location.search).get('view');
     if(v&&v!==active)setActive(v);
   };
   window.addEventListener('popstate',restore);
   return ()=>window.removeEventListener('popstate',restore);
 },[active]);

 useEffect(()=>{getJson<AuthUser>('/auth/me').then(u=>{setAuth(u);setAuthReady(true)}).catch(()=>{setAuth(null);setAuthReady(true)})},[]);
 const loadNotifications=()=>getJson<any[]>('/notifications').then(setNotifications).catch(()=>{});
 useEffect(()=>{
   if(!auth)return;
   loadNotifications();
   const timer=window.setInterval(loadNotifications,30000);
   const onFocus=()=>loadNotifications();
   window.addEventListener('focus',onFocus);
   return()=>{window.clearInterval(timer);window.removeEventListener('focus',onFocus)};
 },[auth]);
 useEffect(()=>{if(auth)getJson<any>('/access/me').then(setAccess).catch(()=>setAccess({modules:[],permissions:{},scopes:{}}));else setAccess(null)},[auth]);

 // Redirect to a valid view if the current one is not in the menu (e.g. bookmarked
 // link the user no longer has access to). Runs as an effect, never during render.
 useEffect(()=>{
   if(!access)return;
   const allowed=[...nav.map(x=>x.key),'settings','accessControl'];
   if(!allowed.includes(active))setActive('dashboard');
 },[access,active]);

 const logout=async()=>{try{await postJson('/auth/logout')}catch{}setAuth(null);setAccess(null);setNotifications([]);setActive('dashboard')};

 const unreadNotifications=notifications.filter((x:any)=>!x.isRead).length;
 const notificationTarget=(n:any)=>{
   const t=String(n.entityType||'').toUpperCase();
   if(t==='PERFORMANCE_PERIOD'||t==='APPROVAL_REQUEST')return 'approvals';
   if(t==='PROJECT')return 'projects';
   if(t==='CONTRACT')return 'contracts';
   if(t==='DOCUMENT')return 'documents';
   if(t.includes('IT'))return 'itAssets';
   return '';
 };
 const openNotification=async(n:any)=>{
   if(!n.isRead){
     try{await postJson(`/notifications/${n.id}/read`,{})}catch{}
     setNotifications((rows:any[])=>rows.map((x:any)=>x.id===n.id?{...x,isRead:true,readAt:new Date().toISOString()}:x));
   }
   const target=notificationTarget(n);
   if(target)setActive(target);
   setNotificationOpen(false);
 };
 const markAllNotificationsRead=async()=>{
   try{await postJson('/notifications/read-all',{})}catch{}
   setNotifications((rows:any[])=>rows.map((x:any)=>({...x,isRead:true,readAt:x.readAt||new Date().toISOString()})));
 };
 const notificationTime=(v:any)=>{
   if(!v)return '';
   const d=new Date(v);
   return Number.isNaN(d.getTime())?'':d.toLocaleString();
 };

 const resetMyPassword=async()=>{if(!confirm(`Reset your MPMS password and email a temporary password to ${auth?.email||'your email'}?`))return;try{const r=await postJson<{message:string}>('/auth/reset-password',{});alert(r.message);setAccountOpen(false)}catch(e){alert(String(e))}};
 const changedPassword=async()=>{try{const u=await getJson<AuthUser>('/auth/me');setAuth(u)}catch{setAuth(null)}};

 if(!authReady)return <div className="loading">Loading workspace…</div>;
 if(!auth)return <LoginPage onLogin={setAuth}/>;
 if(auth.mustChangePassword)return <ChangePasswordPage user={auth} onChanged={changedPassword} onLogout={logout}/>;
 if(!access)return <div className="loading">Loading access…</div>;

 const current = visibleNav.find(x=>x.key===active);

 return <div className="shell">
   <aside className={`sidebar ${mobile?'open':''}`}>
    <div className="brand" role="link" tabIndex={0} title="Go to DASHBOARD" style={{cursor:'pointer'}} onClick={()=>{setActive('dashboard');setMobile(false);setOpenMenu('')}} onKeyDown={e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();setActive('dashboard');setMobile(false);setOpenMenu('')}}}><img className="brand-logo" src="/maipt-logo.png" alt="MAIPT"/><div><b>MAIPT</b><span>PROJECT MANAGEMENT</span></div><button className="close-mobile" onClick={e=>{e.stopPropagation();setMobile(false)}}><X size={20}/></button></div>
    <div className="mpms-primary-dashboard">
      <button title="DASHBOARD" className={active==='dashboard'?'active':''} style={active==='dashboard'?undefined:inactiveSidebarItemStyle} onClick={()=>{setActive('dashboard');setMobile(false);setOpenMenu('')}}>
        <LayoutDashboard size={18}/><span>DASHBOARD</span>
      </button>
    </div>

    <div className="mpms-accordion">
      {navGroups.filter(g=>g.key!=='overview').map(group=>{
        const isOpen=openMenu===group.key;
        const isActive=group.items.some(x=>x.key===active);
        const HeadIcon=group.key==='planning'?CalendarRange:
          group.key==='financial'?WalletCards:
          group.key==='performance'?BarChart3:
          group.key==='capitalInvestment'?Landmark:
          group.key==='organization'?UsersRound:Settings;
        return <div className={`mpms-accordion-group ${isOpen?'open':''}`} key={group.key}>
          <button className={`mpms-accordion-head ${isActive?'active':''}`} onClick={()=>{const first=group.items[0];setOpenMenu(group.key);if(first)setActive(first.key);setMobile(false)}}>
            <HeadIcon size={17}/>
            <span className="mpms-accordion-title">{group.label}</span>
            <span className="mpms-accordion-arrow">›</span>
          </button>
          <div className="mpms-accordion-items">
            {group.items.map(item=>{const Icon=item.icon;return <button key={item.key} title={item.label} className={active===item.key?'active':''} style={active===item.key?undefined:inactiveSidebarItemStyle} onClick={()=>{setActive(item.key);setMobile(false)}}><Icon size={16}/><span>{item.label}</span>{item.key==='approvals'&&<em>1</em>}</button>})}
          </div>
        </div>
      })}

      <div className={`mpms-accordion-group ${openMenu==='administration'?'open':''}`}>
        <button className={`mpms-accordion-head ${(active==='settings'||active==='accessControl')?'active':''}`} onClick={()=>{setOpenMenu('administration');setActive('settings');setMobile(false)}}>
          <Settings size={17}/>
          <span className="mpms-accordion-title">ADMINISTRATION</span>
          <span className="mpms-accordion-arrow">›</span>
        </button>
        <div className="mpms-accordion-items">
          <button title="System Settings" onClick={()=>{setActive('settings');setMobile(false)}} className={active==='settings'?'active':''} style={active==='settings'?undefined:inactiveSidebarItemStyle}><Settings size={16}/><span>System Settings</span></button>
          <button title="Users & Access" onClick={()=>{setActive('accessControl');setMobile(false)}} className={active==='accessControl'?'active':''} style={active==='accessControl'?undefined:inactiveSidebarItemStyle}><UsersRound size={16}/><span>Users & Access</span></button>
        </div>
      </div>
    </div>
    <div className="profile"><div className="avatar">{initials(auth.name)}</div><div><b>{auth.name}</b><span>{auth.jobTitle||auth.role}</span></div><button className="sidebar-logout" onClick={logout} title="Sign out"><LogOut/></button></div>
   </aside>
   <main>
    <header><button className="menu-mobile" onClick={()=>setMobile(true)}><Menu/></button><GlobalSearch onOpen={m=>setActive(m)}/><div className="header-actions"><div className="notification-wrap">
<button type="button" className={`bell ${notificationOpen?'active':''}`} title="Notifications" onClick={()=>setNotificationOpen(v=>!v)}>
  <Bell size={19}/>
  {unreadNotifications>0&&<i/>}
  {unreadNotifications>0&&<span className="notification-count">{unreadNotifications>99?'99+':unreadNotifications}</span>}
</button>
{notificationOpen&&<div className="notification-dropdown">
  <div className="notification-head">
    <div><b>Notifications</b><span>{unreadNotifications} unread</span></div>
    {unreadNotifications>0&&<button type="button" onClick={markAllNotificationsRead}>Mark all as read</button>}
  </div>
  <div className="notification-list">
    {notifications.length===0
      ? <div className="notification-empty">No notifications.</div>
      : notifications.map((n:any)=><button type="button" key={n.id} className={`notification-item ${n.isRead?'read':'unread'} severity-${String(n.severity||'INFO').toLowerCase()}`} onClick={()=>openNotification(n)}>
          <span className="notification-dot"/>
          <span className="notification-copy">
            <b>{n.title||'Notification'}</b>
            <span>{n.message||''}</span>
            <small>{notificationTime(n.createdAt)}{n.type?` - ${n.type}`:''}</small>
          </span>
        </button>)
    }
  </div>
</div>}
</div>
<button type="button" className="mpms-help-button" title="Help" onClick={()=>setHelpOpen(true)}>
  <HelpCircle size={20}/>
</button>
{helpOpen&&<HelpExplorer onClose={()=>setHelpOpen(false)}/>}

<div className="account-menu-wrap"><button type="button" className="account-chip-clean" onClick={()=>setAccountOpen(!accountOpen)}><span className="account-avatar-clean"><img src={`/api/auth/avatar?uid=${auth.id}`} alt="" onError={e=>{e.currentTarget.style.display='none'}}/><span className="account-avatar-fallback">{(auth.name||'U').split(' ').map((x:string)=>x[0]).join('').slice(0,2).toUpperCase()}</span></span><span className="account-name-clean">{auth.name}</span></button>{accountOpen&&<div className="account-dropdown"><div className="account-dropdown-user"><img src={`/api/auth/avatar?uid=${auth.id}`} onError={e=>{e.currentTarget.style.display='none'}}/><div><b>{auth.name}</b><span>{auth.email}</span><small>{auth.jobTitle||auth.role}</small></div></div><UserHrProfileDialog user={auth} menuMode onSaved={changedPassword}/><button onClick={()=>{setAccountOpen(false);setAuth({...auth,mustChangePassword:true})}}><KeyRound size={16}/> Change Password</button><button onClick={resetMyPassword}><KeyRound size={16}/> Reset Password by Email</button><button onClick={logout}><LogOut size={16}/> Sign out</button></div>}</div><button className="header-logout" onClick={logout}><LogOut size={16}/></button></div></header>
    <div className="page">
      {active==='dashboard'&&<Dashboard/>}
      {active==='projects'&&<ProjectsPage/>}
      {active==='peopleResources'&&((canUsers||canProjectTeam)?<TeamManagementPage/>:denied('USERS'))}
      {active==='itAssets'&&(canView('IT_ASSETS')?<ITAssetsPage canDownload={isRoot||!!access?.permissions?.IT_ASSETS?.includes('DOWNLOAD')}/>:denied('IT_ASSETS'))}
      {active==='tasks'&&<TasksMilestonesPage/>}
      {active==='risks'&&(canView('RISKS')?<RisksIssuesPage/>:denied('RISKS'))}
      {current?.path&&<DataPage title={current.label} path={current.path} columns={current.columns||[]}/>}
      {active==='budget'&&(canView('BUDGET')?<BudgetPage/>:denied('BUDGET'))}
      {active==='budgetPlan'&&(canView('BUDGET')?<BudgetPlanningPage/>:denied('BUDGET'))}
      {active==='contracts'&&(canView('CONTRACTS')?<ContractsPage canDownload={isRoot||!!access?.permissions?.CONTRACTS?.includes('DOWNLOAD')}/>:denied('CONTRACTS'))}
      {active==='personalFc'&&<PersonalFcPage/>}
      {active==='suppliers'&&(canView('SUPPLIERS')?<SupplierPage/>:denied('SUPPLIERS'))}
      {active==='teamPerformance'&&<TeamPerformancePage/>}
      {active==='approvals'&&(canView('APPROVALS')?<ApprovalsPage/>:denied('APPROVALS'))}
      {active==='presentation'&&(canView('EXECUTIVE')?<PresentationPage/>:denied('EXECUTIVE'))}
      {active==='reports'&&(canView('REPORTS')?<ReportsPage/>:denied('REPORTS'))}
      {active==='documents'&&<DocumentsPage/>}
      {active==='capitalMgmt'&&(canView('CAPITAL')?<CapitalInvestmentPage mode="CAPITAL"/>:denied('CAPITAL MANAGEMENT'))}
      {active==='investMgmt'&&(canView('INVESTMENT')?<CapitalInvestmentPage mode="INVESTMENT"/>:denied('INVESTMENT MANAGEMENT'))}
      {active==='settings'&&((canSettings||canUsers||canProjectTeam)?<SettingsPage/>:denied('SETTINGS'))}
      {active==='accessControl'&&(canAccessControl?<AccessControlPage/>:denied('ACCESS_CONTROL'))}
    </div>
   </main>
  <AIHelpAssistant/>
 </div>
}
function initials(name:string){return name.split(/\s+/).filter(Boolean).slice(-2).map(x=>x[0]?.toUpperCase()).join('')||'U'}

function Placeholder({title,text}:{title:string;text:string}){return <>
      <GlobalImportProcessing/><div className="page-title"><div><h1>{title}</h1><p>{text}</p></div></div><div className="panel empty"><div className="empty-icon"><Settings/></div><h3>Module foundation is ready</h3><p>This V2 package includes the database entities and API structure for this module.</p></div>  
</>}
