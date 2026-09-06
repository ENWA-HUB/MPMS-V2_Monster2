import {useMemo,useState} from 'react'
import {
 Search,BookOpen,Rocket,Building2,FolderKanban,ListTodo,
 ShieldAlert,WalletCards,Handshake,Gauge,BarChart3,UsersRound,
 Settings,FileText,Laptop,KeyRound,ChevronRight,X,ExternalLink,
 CircleHelp,ClipboardCheck,LayoutDashboard
} from 'lucide-react'
import './HelpExplorer.css'

type Guide={
 id:string
 group:string
 title:string
 summary:string
 keywords:string[]
 steps:string[]
 fields?:string[]
 permissions?:string[]
 tips?:string[]
 related?:string[]
 action?:{label:string,view:string}
}

const guides:Guide[]=[
 {id:'getting-started',group:'GETTING STARTED',title:'Getting Started',summary:'Recommended setup and data-entry sequence for a new MPMS workspace.',keywords:['start','setup','new','flow','data entry'],
 steps:['Review the Dashboard and Organization master data.','Create Portfolios, Projects and the Annual Work & Budget Plan.','Maintain Budget Control, Contracts and Suppliers.','Manage Performance & KPI, Tasks, Milestones, Risks, Approvals and Documents.','Manage funding, treasury and financial risk in Capital Management.','Appraise, allocate and monitor investments in Investment Management.'],
 tips:['Current menu flow: DASHBOARD → PLAN & BUDGET → FINANCIAL CONTROL → PERFORMANCE & KPI → CAPITAL & INVESTMENT → ORGANIZATION → ADMINISTRATION.'],
 related:['Business Units','Projects','Budget Plan','Access Control']},

 {id:'business-units',group:'ORGANIZATION',title:'Business Units',summary:'Maintain organization units used for ownership, reporting and Data Scope.',keywords:['business unit','orgunit','organization','department','bu'],
 steps:['Open Business Units.','Create or edit the unit profile and code.','Complete organization information and Save.','Use the Business Unit in Projects, People, IT Assets, Licenses and Data Scope.'],
 fields:['Code','Name','Parent Business Unit','Company / legal information','Status'],
 permissions:['Visibility and edit rights depend on Functional Permissions.','Business Unit selection is also used by Data Scope.'],
 action:{label:'Open Business Units',view:'businessUnits'},related:['People & Teams','Projects','Data Scope']},

 {id:'people',group:'ORGANIZATION',title:'People & Teams',summary:'Maintain employee/resource profiles and project participation.',keywords:['people','employee','team','user','member','profile'],
 steps:['Open People & Teams.','Add or edit an employee profile.','Select Business Unit and manager where applicable.','Maintain contact and employment information.','Assign the person to Projects from Access Control when required.'],
 fields:['Employee Code','Name','Email','Business Unit','Job Title','Manager'],
 permissions:['Employee profile information and user login/access are separate concepts.'],
 action:{label:'Open People & Teams',view:'team'},related:['Access Control','Project Assignments','IT Asset Assignment']},

 {id:'suppliers',group:'ORGANIZATION',title:'Suppliers',summary:'Maintain supplier master data used by Contracts, Budget and IT resources.',keywords:['supplier','vendor','nhà cung cấp','nha cung cap'],
 steps:['Open Suppliers.','Create the supplier master record.','Enter legal, tax, contact, payment and bank information.','Save once and reuse the supplier across related modules.'],
 action:{label:'Open Suppliers',view:'suppliers'},related:['Contracts','Budget','IT Assets & Services']},

 {id:'it-assets',group:'ORGANIZATION',title:'IT Assets & Services',summary:'Manage IT equipment, services, stock, assignment, handover, return and lifecycle history.',keywords:['asset','it asset','laptop','desktop','server','network','handover','return','stock','service'],
 steps:['Create the asset/service and select its Business Unit.','Record supplier, contract, purchase and warranty information where applicable.','Keep available equipment in IT Stock.','Use Assignment & Handover when issuing equipment to a user.','Record Return so the asset can return to stock or be reassigned.','Use Maintenance for repair/service history.'],
 fields:['Asset Code','Category','Asset Name','Business Unit','Manager / Using User','Supplier / Contract','Serial Number','Status'],
 permissions:['Users only see IT resources allowed by their Business Unit/Data Scope.'],
 tips:['Lifecycle: Purchase → In Stock → Assign → Handover → In Use → Return → Reassign / Retire.'],
 action:{label:'Open IT Assets & Services',view:'itAssets'},related:['Licenses','Suppliers','Documents']},

 {id:'licenses',group:'ORGANIZATION',title:'Licenses',summary:'Manage software/subscription licenses, assignment and validity.',keywords:['license','licenses','software','subscription','key'],
 steps:['Open Licenses under IT Assets & Services.','Create the license record.','Enter product, license type/key and quantity.','Select Business Unit, assigned user/device, supplier or contract where applicable.','Maintain activation and expiry information.'],
 fields:['Product','License Type','License Key','Quantity','Assigned User','Assigned Asset','Business Unit','Expiry Date'],
 action:{label:'Open Licenses',view:'itAssets'},related:['IT Assets & Services','Suppliers','Contracts']},

 {id:'portfolios',group:'PLAN & BUDGET',title:'Portfolio',summary:'Group Projects for planning, ownership and management reporting.',keywords:['portfolio','planning'],
 steps:['Open Portfolio.','Create or edit the Portfolio.','Select Business Unit and owner where applicable.','Save, then assign Projects to the Portfolio.'],
 action:{label:'Open Portfolio',view:'portfolio'},related:['Projects','Dashboard']},

 {id:'projects',group:'PLAN & BUDGET',title:'Projects',summary:'Central project record connecting planning, finance, documents and performance.',keywords:['project','projects','dự án','du an'],
 steps:['Open Projects.','Create a new Project.','Select Portfolio and Business Unit information.','Enter owner, dates, status and project details.','Save before creating dependent Tasks, Budget, Contracts or Documents.'],
 fields:['Project Code','Project Name','Portfolio','Business Unit','Owner','Start/End Date','Status'],
 permissions:['A Project may be mapped to multiple Business Units where configured.','Project visibility follows Data Scope / Project Assignment rules.'],
 action:{label:'Open Projects',view:'projects'},related:['Project Assignments','Budget Plan','Tasks & Milestones','Documents']},

 {id:'annual-work-plan',group:'PLAN & BUDGET',title:'Annual Work & Budget Plan',summary:'Plan yearly activities, expected budget and approval by Business Unit.',keywords:['annual work','work plan','annual plan','budget plan','recall','submit'],
 steps:['Select planning year and Business Unit.','Select Project where applicable.','Enter plan item/category/vendor information.','Enter quantity, unit price, planned amount and Planned Month.','Save or Import using the supported template.','Submit one Business Unit and year through sequential approval levels.','Recall while pending when adjustment is required; rejected plans become editable and can be submitted again.'],
 tips:['Planned Month must belong to the selected Budget Year.'],
 action:{label:'Open Annual Work Plan',view:'budgetPlan'},related:['Budget Control','Projects','Approvals','Import / Export']},

 {id:'budget-control',group:'FINANCIAL CONTROL',title:'Budget Control',summary:'Monitor planned, revised, committed, actual and remaining budget.',keywords:['budget','financial','cost','actual','committed','forecast'],
 steps:['Select Business Unit / Project and reporting period.','Review approved/planned budget.','Enter or import financial/budget data as permitted.','Review Actual, Committed, Remaining and variance.','Export data or an empty template where permitted.'],
 permissions:['Functional Permission controls actions.','Data Scope controls which Business Units/Projects are visible.'],
 action:{label:'Open Budget Control',view:'budget'},related:['Annual Work & Budget Plan','Contracts','Import / Export']},

 {id:'contracts',group:'FINANCIAL CONTROL',title:'Contracts',summary:'Maintain project/vendor contracts, values, dates and supporting documents.',keywords:['contract','contracts','hợp đồng','hop dong'],
 steps:['Create the Supplier first where applicable.','Open Contracts and create the contract.','Select Project and Supplier.','Enter value, dates, status and relevant commercial information.','Attach supporting documents where available.'],
 action:{label:'Open Contracts',view:'contracts'},related:['Suppliers','Budget Control','Documents']},

 {id:'personal-fc',group:'FINANCIAL CONTROL',title:'Personal FC',summary:'Manage personal finance functions available to the signed-in user.',keywords:['personal fc','personal finance','club finance'],
 steps:['Open Personal FC.','Select the relevant account/area.','Create or review transactions, assets, debts or enabled club finance records.','Use approval/status workflow where provided.'],
 permissions:['Personal/owner information is separated from broader organizational scope.'],
 action:{label:'Open Personal FC',view:'personalFc'},related:['Club Funds','Transactions']},

 {id:'tasks',group:'PERFORMANCE & KPI',title:'Tasks & Milestones',summary:'Plan and track work items and key project checkpoints.',keywords:['task','tasks','milestone','milestones','due date'],
 steps:['Select Project.','Create Task or Milestone.','Assign owner/responsible person.','Enter target/due dates, priority, status and progress.','Update regularly until completion.'],
 action:{label:'Open Tasks',view:'tasks'},related:['Projects','Risks & Issues','Dashboard']},

 {id:'risks',group:'PERFORMANCE & KPI',title:'Risks & Issues',summary:'Track potential risks and issues that have already occurred.',keywords:['risk','issue','risks','issues','mitigation'],
 steps:['Identify the Risk or Issue.','Assess impact/severity and probability where applicable.','Assign an owner.','Enter mitigation/corrective action and target date.','Monitor until Closed/Resolved.'],
 tips:['Risk = potential future event. Issue = problem that has already occurred.'],
 action:{label:'Open Risks & Issues',view:'risks'},related:['Projects','Tasks & Milestones','Dashboard']},

 {id:'documents',group:'PERFORMANCE & KPI',title:'Documents',summary:'Maintain project and business documents with secure view/download controls.',keywords:['document','documents','attachment','sharepoint','file','preview'],
 steps:['Open Documents or the attachment area of a record.','Upload the file and select category/context.','Use View/Preview for supported formats.','Use Download only when DOWNLOAD permission is granted.','Maintain file/version information when applicable.'],
 permissions:['VIEW and DOWNLOAD are separate permissions.','Documents follow the access rules of their linked data/context.'],
 action:{label:'Open Documents',view:'documents'},related:['Projects','Suppliers','IT Assets & Services']},

 {id:'kpi',group:'PERFORMANCE & KPI',title:'Performance & KPI',summary:'Define member, department or project KPI periods, enter results and complete sequential approval.',keywords:['kpi','performance','target','score','member','level','unit'],
 steps:['Select year, month, member, KPI Level and Business Unit.','Create or select the unique KPI period identified by Member + Level + Month + Year + Unit.','Maintain KPI items, weights, targets and actual results.','Submit through the configured approval levels.','Use Approvals → Review & Comment before Approve or Reject.'],
 permissions:['OWN KPI may be visible to its owner.','Team/manager visibility depends on Functional Permissions and Data Scope.'],
 action:{label:'Open Performance & KPI',view:'teamPerformance'},related:['How to score and KPI valuation.','Dashboard','Reports','Approvals']},

 {id:'kpi-scoring',group:'PERFORMANCE & KPI',title:'How to score and KPI valuation.',summary:'Score quantitative and qualitative KPIs consistently using the approved 1–5 scale.',keywords:['kpi score','scoring','valuation','evaluation','completion rate','quantitative','qualitative','competency','weight'],
 steps:['Confirm that total KPI weight is 100%.','Enter the actual result. If it meets the target, record “Meet requirement”; otherwise describe the actual result and variance.','For a quantitative KPI, calculate Completion rate (F) = Actual / Plan × 100%, then use the correct direction table below.','For a qualitative KPI, select the score whose description best matches the demonstrated result.','Add an explanation and supporting evidence for every score of 4.0 or above.','The manager reviews scores from every participating Company/Project before completing the evaluation.'],
 fields:['Plan / Target','Actual Result','Weight','Self Score','Manager Score','HOD / Final Score','Evidence / Note'],
 tips:['Each KPI allocated across Companies/Projects must total 100% workload.','Competency weights total 100%; where five competencies are used, each competency carries 20%.','Use the “Higher is better” table for growth/output KPIs and the “Lower is better” table for cost, delay, defect or incident KPIs.'],
 action:{label:'Open Performance & KPI',view:'teamPerformance'},related:['Performance & KPI','Approvals']},

 {id:'dashboard',group:'DASHBOARD',title:'Dashboard & Reports',summary:'Monitor project/portfolio health and management indicators within permitted scope.',keywords:['dashboard','report','reports','analysis','presentation'],
 steps:['Open Dashboard or Reports.','Review indicators, trends and exception items.','Filter/drill down to underlying modules.','Use presentation/reporting views where available.'],
 permissions:['Totals must follow the same Data Scope as the underlying records.'],
 action:{label:'Open Dashboard',view:'dashboard'},related:['Projects','Budget Control','Performance & KPI']},

 {id:'access-control',group:'ADMINISTRATION',title:'Access Control',summary:'Control what a user can do and which data the user can access.',keywords:['permission','permissions','data scope','access','user','project assignment'],
 steps:['Open Access Control.','Use Permissions to select the user.','Set Functional Permissions: VIEW / CREATE / EDIT / DELETE / SUBMIT / APPROVE / REPORT / UPLOAD / DOWNLOAD.','Use Data Scope to select permitted Business Units/Projects by domain.','Use Project Assignments to assign users to Projects.'],
 fields:['User','Functional Permissions','Data Scope','Project Assignments'],
 permissions:['Functional Permissions answer: What can the user do?','Data Scope answers: Which data can the user do it on?'],
 tips:['Use searchable user lookup by Name, Email or Employee Code when the user list is long.'],
 action:{label:'Open Access Control',view:'accessControl'},related:['Users','Projects','Business Units']},

 {id:'users',group:'ADMINISTRATION',title:'Users',summary:'Maintain user accounts and account-related information.',keywords:['users','user account','login','email','employee code'],
 steps:['Open Access Control → Users.','Search by name, email, employee code, Business Unit or job title.','Select the user.','Edit permitted information and Save.'],
 action:{label:'Open Users',view:'accessControl'},related:['Access Control','People & Teams']},

 {id:'import-export',group:'ADMINISTRATION',title:'Import / Export',summary:'Use standard module templates for bulk data entry and controlled export.',keywords:['import','export','excel','template','download'],
 steps:['Open the required module.','Use Export to download current data or the valid empty template where supported.','Complete the template without changing required structure.','Use Import and wait for the processing indicator.','Review validation messages and correct rejected rows.'],
 permissions:['Import/Export actions require the corresponding permission.','Export with no records in permitted scope should still return a valid empty template where the module supports import/export.'],
 related:['Budget Control','Performance & KPI','IT Assets & Services','Projects']},

 {id:'approvals',group:'PERFORMANCE & KPI',title:'Approvals',summary:'Review KPI and Plan/Budget submissions assigned to the signed-in user.',keywords:['approval','approve','submit','workflow','comment','reject','plan budget'],
 steps:['Open Approvals.','Filter by year, month, member, Business Unit, KPI Level and status.','Use Review & Comment to inspect KPI or Plan/Budget content.','Approve or Reject from the request row.','Sequential workflows notify the next selected level only after the current level approves.'],
 permissions:['Users should only see/act on requests allowed by their role and scope.'],
 action:{label:'Open Approvals',view:'approvals'},related:['Performance & KPI','Budget Control']},

 {id:'reports-presentations',group:'PERFORMANCE & KPI',title:'Reports & Executive Presentation',summary:'Analyse permitted project, financial and KPI information for management reporting.',keywords:['reports','presentation','executive','analysis','management'],
 steps:['Open Reports or Executive Presentation.','Select the reporting scope and period.','Review trends, exceptions and management indicators.','Use the presentation view for management communication.'],
 permissions:['Reported totals must follow the same Functional Permissions and Data Scope as source records.'],
 action:{label:'Open Reports',view:'reports'},related:['Dashboard & Reports','Performance & KPI','Budget Control']},

 {id:'capital-management',group:'CAPITAL & INVESTMENT',title:'Capital Management',summary:'Manage capital structure, funding, working capital, treasury, financial risk and dividend policy.',keywords:['capital','funding','debt equity','wacc','treasury','working capital','hedging','dividend'],
 steps:['Open CAPITAL & INVESTMENT → CAPITAL MGMT.','Select Overview or a function tab directly below the title.','Filter by year, Business Unit and status.','Create a record with proposal, value, metric, risk and counterparty.','Use Capital Structure for Debt/Equity and WACC; Funding & Raising for debt/equity sources; Treasury for liquidity and facilities.'],
 fields:['Function','Business Unit','Fiscal Year','Amount / Value','Metric','Risk Level','Status'],
 permissions:['Visibility and actions follow BUDGET Functional Permissions and Data Scope.'],
 action:{label:'Open Capital Management',view:'capitalMgmt'},related:['Investment Management','Budget Control','Business Units']},

 {id:'investment-management',group:'CAPITAL & INVESTMENT',title:'Investment Management',summary:'Appraise opportunities, allocate CAPEX, monitor portfolio performance and manage divestment.',keywords:['investment','npv','irr','roi','payback','portfolio','capex','divestment','ma'],
 steps:['Open CAPITAL & INVESTMENT → INVEST MGMT.','Select Overview or a function tab directly below the title.','Use Investment Planning/Appraisal to record opportunity and feasibility analysis.','Use Investment Decision and Capital Allocation/CAPEX for approval and prioritisation.','Use Portfolio and Performance Monitoring for actual-versus-plan results.','Use Divestment for exit or withdrawal decisions.'],
 fields:['Function','Proposal','Business Unit','Fiscal Year','Investment Value','NPV / IRR / ROI / Payback','Risk','Status'],
 permissions:['Visibility and actions follow BUDGET Functional Permissions and Data Scope.'],
 action:{label:'Open Investment Management',view:'investMgmt'},related:['Capital Management','Projects','Annual Work & Budget Plan']},

 {id:'settings',group:'ADMINISTRATION',title:'System Settings',summary:'Maintain system master/configuration data available to administrators.',keywords:['settings','admin','system settings','configuration'],
 steps:['Open System Settings.','Select the required configuration/master data area.','Create or update permitted values.','Save changes and verify dependent screens.'],
 permissions:['Administrative permissions are required.'],
 action:{label:'Open Settings',view:'settings'},related:['Access Control','Business Units']}
]

const groups=[
 'GETTING STARTED','DASHBOARD','PLAN & BUDGET','FINANCIAL CONTROL',
 'PERFORMANCE & KPI','CAPITAL & INVESTMENT','ORGANIZATION','ADMINISTRATION'
]

function go(view:string){
 const u=new URL(window.location.href)
 u.searchParams.set('view',view)
 window.location.href=u.toString()
}

export default function HelpExplorer({onClose}:{onClose:()=>void}){
 const[query,setQuery]=useState('')
 const[selected,setSelected]=useState('getting-started')
 const[mobileNav,setMobileNav]=useState(false)

 const filtered=useMemo(()=>{
   const q=query.trim().toLowerCase()
   if(!q)return guides
   return guides.filter(g=>
     [g.title,g.summary,g.group,...g.keywords,...g.steps,...(g.fields||[]),...(g.related||[])]
       .join(' ').toLowerCase().includes(q)
   )
 },[query])

 const current=guides.find(x=>x.id===selected) || filtered[0] || guides[0]

 const choose=(id:string)=>{setSelected(id);setMobileNav(false)}

 return <div className="help-explorer-overlay" onMouseDown={onClose}>
  <div className="help-explorer" onMouseDown={e=>e.stopPropagation()}>
   <header className="help-explorer-head">
    <div>
     <span>MPMS USER GUIDE</span>
     <h2>Help & User Guide</h2>
    </div>
    <div className="help-explorer-head-actions">
     <button className="help-mobile-nav-btn" onClick={()=>setMobileNav(v=>!v)}><BookOpen size={17}/> Guides</button>
     <button className="help-close" onClick={onClose}><X size={24}/></button>
    </div>
   </header>

   <div className="help-explorer-body">
    <aside className={`help-explorer-sidebar ${mobileNav?'open':''}`}>
     <div className="help-search">
      <Search size={17}/>
      <input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Search Help & User Guide"/>
      {!!query&&<button onClick={()=>setQuery('')}>×</button>}
     </div>

     {query&&<div className="help-result-count">{filtered.length} matching guides</div>}

     <nav>
      {groups.map(group=>{
       const rows=filtered.filter(x=>x.group===group)
       if(!rows.length)return null
       return <section key={group} className="help-nav-group">
        <h4>{group}<span>{rows.length}</span></h4>
        {rows.map(g=><button key={g.id} className={current.id===g.id?'active':''} onClick={()=>choose(g.id)}>
         <ChevronRight size={14}/><span>{g.title}</span>
        </button>)}
       </section>
      })}
     </nav>
    </aside>

    <main className="help-explorer-main">
     <div className="help-article-top">
      <div>
       <span className="help-article-group">{current.group}</span>
       <h3>{current.title}</h3>
       <p>{current.summary}</p>
      </div>
      {current.action&&<button className="help-open-action" onClick={()=>go(current.action!.view)}>
       {current.action.label}<ExternalLink size={15}/>
      </button>}
     </div>

     {current.id==='getting-started'&&<div className="help-start-flow">
      {['DASHBOARD','PLAN & BUDGET','FINANCIAL CONTROL','PERFORMANCE & KPI','CAPITAL & INVESTMENT','ORGANIZATION','ADMINISTRATION'].map((x,i)=><div key={x} className="help-flow-step">
       <span>{String(i+1).padStart(2,'0')}</span><b>{x}</b>{i<6&&<i>→</i>}
      </div>)}
     </div>}

     <section className="help-card">
      <div className="help-card-title"><Rocket size={18}/><h4>How to use</h4></div>
      <ol>{current.steps.map((x,i)=><li key={i}>{x}</li>)}</ol>
     </section>

     {current.id==='kpi-scoring'&&<>
      <section className="help-card">
       <div className="help-card-title"><Gauge size={18}/><h4>Quantitative KPI score</h4></div>
       <p><b>Completion rate (F) = Actual ÷ Plan × 100%</b></p>
       <div style={{overflowX:'auto'}}><table style={{width:'100%',borderCollapse:'collapse',minWidth:760}}>
        <thead><tr><th style={{textAlign:'left',padding:10}}>KPI direction</th>{['5','4.5','4','3.5','3','2','1'].map(x=><th key={x} style={{padding:10}}>Score {x}</th>)}</tr></thead>
        <tbody>
         <tr><th style={{textAlign:'left',padding:10}}>Higher is better</th>{['F ≥ 100%','95% < F < 100%','F = 95%','90% ≤ F < 95%','85% ≤ F < 90%','50% ≤ F < 85%','F < 50%'].map(x=><td key={x} style={{padding:10,textAlign:'center'}}>{x}</td>)}</tr>
         <tr><th style={{textAlign:'left',padding:10}}>Lower is better</th>{['F < 85%','85% ≤ F < 90%','90% ≤ F < 95%','95% ≤ F < 100%','F = 100%','100% < F ≤ 110%','F > 110%'].map(x=><td key={x} style={{padding:10,textAlign:'center'}}>{x}</td>)}</tr>
        </tbody>
       </table></div>
      </section>
      <section className="help-card">
       <div className="help-card-title"><ClipboardCheck size={18}/><h4>Qualitative KPI valuation</h4></div>
       <div style={{overflowX:'auto'}}><table style={{width:'100%',borderCollapse:'collapse',minWidth:760}}>
        <thead><tr>{['Score','5','4.5','4','3.5','3','2','1'].map(x=><th key={x} style={{padding:10}}>{x}</th>)}</tr></thead>
        <tbody><tr>{['Meaning','Far exceeds all requirements','Exceeds most requirements','Exceeds some requirements','Meets requirements by overcoming resource, time or other constraints','Meets requirements','Meets some requirements','Fails to meet most requirements'].map((x,i)=>i===0?<th key={x} style={{textAlign:'left',padding:10}}>{x}</th>:<td key={x} style={{padding:10,textAlign:'center'}}>{x}</td>)}</tr></tbody>
       </table></div>
      </section>
      <section className="help-card">
       <div className="help-card-title"><UsersRound size={18}/><h4>Individual competency valuation</h4></div>
       <div style={{overflowX:'auto'}}><table style={{width:'100%',borderCollapse:'collapse',minWidth:680}}>
        <thead><tr>{['Score','5','4','3','2','1'].map(x=><th key={x} style={{padding:10}}>{x}</th>)}</tr></thead>
        <tbody><tr>{['Demonstration level','Always demonstrates the described behaviours and acts as a role model','Usually demonstrates the described behaviours','Often demonstrates the described behaviours','Sometimes demonstrates the described behaviours','Rarely or never demonstrates the described behaviours'].map((x,i)=>i===0?<th key={x} style={{textAlign:'left',padding:10}}>{x}</th>:<td key={x} style={{padding:10,textAlign:'center'}}>{x}</td>)}</tr></tbody>
       </table></div>
      </section>
     </>}

     {!!current.fields?.length&&<section className="help-card">
      <div className="help-card-title"><ClipboardCheck size={18}/><h4>Key fields</h4></div>
      <div className="help-tags">{current.fields.map(x=><span key={x}>{x}</span>)}</div>
     </section>}

     {!!current.permissions?.length&&<section className="help-card">
      <div className="help-card-title"><KeyRound size={18}/><h4>Permissions & Data Scope</h4></div>
      <ul>{current.permissions.map((x,i)=><li key={i}>{x}</li>)}</ul>
     </section>}

     {!!current.tips?.length&&<section className="help-card">
      <div className="help-card-title"><CircleHelp size={18}/><h4>Tips & Notes</h4></div>
      <ul>{current.tips.map((x,i)=><li key={i}>{x}</li>)}</ul>
     </section>}

     {!!current.related?.length&&<section className="help-card">
      <div className="help-card-title"><BookOpen size={18}/><h4>Related guides</h4></div>
      <div className="help-related">
       {current.related.map(name=>{
        const g=guides.find(x=>x.title===name)
        return <button key={name} onClick={()=>g&&choose(g.id)} disabled={!g}>{name}</button>
       })}
      </div>
     </section>}
    </main>
   </div>
  </div>
 </div>
}
