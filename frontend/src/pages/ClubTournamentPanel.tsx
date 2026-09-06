import {useEffect,useMemo,useState} from 'react';
import {Check,Copy,Download,Edit3,GitMerge,Plus,RefreshCw,Trash2,Trophy,Upload,UserCheck,UserX,X,Search} from 'lucide-react';

const api=async(u:string,o?:RequestInit)=>{
  const r=await fetch(u,{
    credentials:'same-origin',
    ...o,
    headers:{...(o?.body?{'Content-Type':'application/json'}:{}),...(o?.headers||{})}
  });
  const d=await r.json().catch(()=>null);
  if(!r.ok)throw new Error(d?.message||`HTTP ${r.status}`);
  return d;
};
const today=()=>new Date().toISOString().slice(0,10);
const money=(v:any)=>Number(v||0).toLocaleString('vi-VN');
const moneyText=(v:any)=>{const n=Number(v||0);return n?Math.trunc(n).toLocaleString('vi-VN'):''};
const moneyNumber=(v:string)=>{const d=(v||'').replace(/[^\d]/g,'');return d?Number(d):0};

function Field({label,children}:{label:string;children:any}){
  return <label className="ct-field"><span>{label}</span>{children}</label>;
}
function Modal({title,onClose,onSave,children}:{title:string;onClose:()=>void;onSave:()=>void;children:any}){
  return <div className="ct-back"><div className="ct-modal">
    <header><h3>{title}</h3><button onClick={onClose}><X/></button></header>
    <div className="ct-form">{children}</div>
    <footer><button onClick={onClose}>Cancel</button><button className="primary" onClick={onSave}>Save</button></footer>
  </div></div>;
}

export function ClubTournamentPanel({
  mode,clubIdOverride,embedded=false,onChanged
}:{
  mode:'members'|'tournaments';
  clubIdOverride?:number;
  embedded?:boolean;
  onChanged?:()=>void|Promise<void>
}){
  const[localClubSearch,setLocalClubSearch]=useState('');
  const[localMemberSearch,setLocalMemberSearch]=useState('');
  const[localTournamentSearch,setLocalTournamentSearch]=useState('');
  const[localTournamentMemberSearch,setLocalTournamentMemberSearch]=useState('');
  const[localTeamSearch,setLocalTeamSearch]=useState('');
  const[localMatchSearch,setLocalMatchSearch]=useState('');

  const[clubs,setClubs]=useState<any[]>([]);
  const[clubId,setClubId]=useState(0);
  const activeClubId=clubIdOverride||clubId;

  const[members,setMembers]=useState<any[]>([]);
  const[tours,setTours]=useState<any[]>([]);
  const[tourId,setTourId]=useState(0);
  const[detail,setDetail]=useState<any>(null);
  const[tourTab,setTourTab]=useState<'overview'|'members'|'teams'|'matches'|'rankings'>('overview');

  const[err,setErr]=useState('');
  const[busy,setBusy]=useState(false);

  const[memberModal,setMemberModal]=useState(false);
  const[memberEdit,setMemberEdit]=useState<any>(null);
  const[memberForm,setMemberForm]=useState<any>({});
  const[memberAvatarFile,setMemberAvatarFile]=useState<File|null>(null);
  const[memberAvatarPreview,setMemberAvatarPreview]=useState('');
  const[memberAvatarMap,setMemberAvatarMap]=useState<Record<number,string>>({});

  const[tourModal,setTourModal]=useState(false);
  const[tourEdit,setTourEdit]=useState<any>(null);
  const[scoreDrafts,setScoreDrafts]=useState<Record<number,{score1:string;score2:string}>>({});
  const[advanceMode,setAdvanceMode]=useState('TOP2_CROSS');
  const[advanceTopN,setAdvanceTopN]=useState(2);
  const[tourForm,setTourForm]=useState<any>({});

  const[regModal,setRegModal]=useState(false);
  const[regEdit,setRegEdit]=useState<any>(null);
  const[regForm,setRegForm]=useState<any>({});
  const[teamModal,setTeamModal]=useState(false);
  const[teamEdit,setTeamEdit]=useState<any>(null);
  const[teamForm,setTeamForm]=useState<any>({});

  const[regAvatarFile,setRegAvatarFile]=useState<File|null>(null);
  const[regAvatarPreview,setRegAvatarPreview]=useState('');
  const[regAvatarMap,setRegAvatarMap]=useState<Record<number,string>>({});
  const[participantMembers,setParticipantMembers]=useState<any[]>([]);
  const[participantMembersLoading,setParticipantMembersLoading]=useState(false);
  const uploadAvatar=async(file:File,url:string)=>{
    const fd=new FormData();fd.append('file',file);
    const r=await fetch(url,{method:'POST',credentials:'same-origin',body:fd});
    if(!r.ok)throw new Error(await r.text());
    return await r.json().catch(()=>null);
  };
  const loadClubs=async()=>{
    try{
      const c=await api('/api/personal-fc/clubs');
      setClubs(c);
      if(!clubId&&c[0])setClubId(c[0].id);
    }catch(e:any){setErr(e.message)}
  };
  const loadClub=async()=>{
    if(!activeClubId)return;
    try{
      const [ms,t]=await Promise.all([
        api(`/api/personal-fc/clubs/${activeClubId}/directory`),
        api(`/api/personal-fc/clubs/${activeClubId}/tournaments`)
      ]);
      setMembers(ms);setTours(t);
      // PERSONAL_FC_MEMBER_AVATAR_URLS
      const stamp=Date.now();
      setMemberAvatarMap(Object.fromEntries((ms||[]).map((m:any)=>[Number(m.id),`/api/personal-fc/clubs/${activeClubId}/members/${m.id}/avatar?v=${stamp}`])));
      if(!tourId&&t[0])setTourId(t[0].id);
      if(tourId&&!t.find((x:any)=>x.id===tourId)&&t[0])setTourId(t[0].id);
      return {members:ms||[],tours:t||[]};
    }catch(e:any){setErr(e.message);return null}
  };
  // PFC_MEMBER_REFRESH_DELETE_V1
  const refreshClubData=async()=>{
    try{
      setBusy(true);setErr('');
      await loadClub();
      await onChanged?.();
    }finally{setBusy(false)}
  };
  const loadTour=async()=>{
    if(!tourId)return;
    try{setDetail(await api(`/api/personal-fc/tournaments/${tourId}/detail`))}
    catch(e:any){setErr(e.message)}
  };

  const loadParticipantMembers=async()=>{
    if(!tourId){setParticipantMembers([]);return}
    try{
      setParticipantMembersLoading(true);setErr('');
      const ms=await api(`/api/personal-fc/tournaments/${tourId}/participant-candidates`);
      setParticipantMembers(Array.isArray(ms)?ms:[]);
    }catch(e:any){
      setParticipantMembers([]);
      setErr(e.message);
    }finally{
      setParticipantMembersLoading(false);
    }
  };

  useEffect(()=>{loadClubs()},[]);

  useEffect(()=>{
    if(mode!=='tournaments')return;
    try{
      const raw=sessionStorage.getItem('pfcOpenTournament');
      if(!raw)return;
      const x=JSON.parse(raw);
      if(x?.clubId)setClubId(Number(x.clubId));
      if(x?.tourId)setTourId(Number(x.tourId));
      sessionStorage.removeItem('pfcOpenTournament');
    }catch{}
  },[mode]);

  useEffect(()=>{loadClub()},[activeClubId]);
  useEffect(()=>{loadTour()},[tourId]);
  useEffect(()=>{if(tourId)loadParticipantMembers()},[tourId]);

  const sortedMembers=useMemo(()=>[...members].sort((a,b)=>
    String(a.memberCode||'').localeCompare(String(b.memberCode||''),undefined,{numeric:true})||
    String(a.memberName||'').localeCompare(String(b.memberName||''))
  ),[members]);
  const availableMembers=useMemo(()=>[...participantMembers].sort((a:any,b:any)=>
    String(a.memberCode||'').localeCompare(String(b.memberCode||''),undefined,{numeric:true})||
    String(a.memberName||'').localeCompare(String(b.memberName||''))
  ),[participantMembers]);


  // CLUB MEMBERS
  const memberDefaults={
    memberCode:'',memberName:'',cellPhone:'',email:'',sex:'',skillRank:'',
    birthDate:null,joinDate:today(),memberRole:'MEMBER',membershipType:'MONTHLY',
    monthlyFeeAmount:0,recurringFeeEnabled:false,recurringFeeDay:1,
    registrationStatus:'PENDING',status:'ACTIVE',active:true,notes:''
  };
  const openMember=(row?:any)=>{
    setMemberEdit(row||null);
    setMemberAvatarFile(null);setMemberAvatarPreview(row?.id?memberAvatarMap[row.id]||'':'');
    setMemberForm(row?{...row}:memberDefaults);
    setMemberModal(true);
  };
  const saveMember=async()=>{
    try{
      setBusy(true);setErr('');
      const payload={...memberForm,birthDate:memberForm.birthDate||null,joinDate:memberForm.joinDate||null,membershipFee:Number(memberForm.monthlyFeeAmount||0)};
      const url=memberEdit
        ?`/api/personal-fc/clubs/${activeClubId}/directory/${memberEdit.id}`
        :`/api/personal-fc/clubs/${activeClubId}/directory`;
      const saved=await api(url,{method:memberEdit?'PUT':'POST',body:JSON.stringify(payload)});
      const savedId=Number(saved?.id||saved?.Id||memberEdit?.id||0);
      if(memberAvatarFile&&savedId)await uploadAvatar(memberAvatarFile,`/api/personal-fc/clubs/${activeClubId}/members/${savedId}/avatar`);
      setMemberModal(false);setMemberAvatarFile(null);setMemberAvatarPreview('');setMemberEdit(null);await loadClub();await onChanged?.();
    }catch(e:any){setErr(e.message)}finally{setBusy(false)}
  };
  const deleteMember=async(row:any)=>{
    if(!confirm(`Delete member "${row.memberName}" (${row.memberCode||row.id})?`))return;
    try{
      setBusy(true);setErr('');
      // Existing deployments may return either 204 No Content or JSON after a successful delete.
      await api(`/api/personal-fc/clubs/${activeClubId}/directory/${row.id}`,{method:'DELETE'});
      const fresh=await loadClub();
      if(fresh?.members.some((m:any)=>Number(m.id)===Number(row.id)))
        throw new Error('Member still exists after delete. Database delete was not persisted.');
      await onChanged?.();
    }
    catch(e:any){setErr(e.message)}finally{setBusy(false)}
  };
  const memberAction=async(row:any,action:'approve'|'reject'|'activate'|'deactivate')=>{
    try{setBusy(true);await api(`/api/personal-fc/clubs/${activeClubId}/directory/${row.id}/${action}`,{method:'POST'});await loadClub();await onChanged?.()}
    catch(e:any){setErr(e.message)}finally{setBusy(false)}
  };

  // TOURNAMENTS
  const tourDefaults={
    code:'',name:'',tournamentDate:today(),season:'',venue:'',
    status:'DRAFT',format:'GROUP_ROUND_ROBIN',notes:''
  };
  const openTournament=(row?:any)=>{
    setTourEdit(row||null);
    setTourForm(row?{...row}:tourDefaults);
    setTourModal(true);
  };
  const saveTournament=async()=>{
    try{
      setBusy(true);setErr('');
      const editingId=Number(tourEdit?.id||0);
      const isEdit=editingId>0;
      const payload={...tourForm,
        code:String(tourForm.code||'').trim().toUpperCase(),
        name:String(tourForm.name||'').trim()
      };
      const url=isEdit
        ?`/api/personal-fc/clubs/${activeClubId}/tournaments/${editingId}`
        :`/api/personal-fc/clubs/${activeClubId}/tournaments`;

      const saved=await api(url,{method:isEdit?'PUT':'POST',body:JSON.stringify(payload)});

      const fresh=await api(`/api/personal-fc/clubs/${activeClubId}/tournaments`);
      setTours(fresh);

      const selectedId=Number(saved?.id||editingId||0);
      const selected=fresh.find((x:any)=>Number(x.id)===selectedId);
      const nextId=Number(selected?.id||fresh[0]?.id||0);

      setTourModal(false);
      setTourEdit(null);
      setTourForm({});
      setDetail(null);
      setTourId(nextId);
      setTourTab('overview');
    }catch(e:any){setErr(e.message)}finally{setBusy(false)}
  };

  const deleteTournament=async(row:any)=>{
    const deletingId=Number(row?.id||0);
    if(!deletingId)return;
    if(!confirm(`Delete tournament "${row.name}" and all its participants, teams and matches?`))return;

    try{
      setBusy(true);setErr('');

      const result=await api(`/api/personal-fc/clubs/${activeClubId}/tournaments/${deletingId}`,{
        method:'DELETE'
      });

      if(result?.deleted!==true)
        throw new Error('Delete was not confirmed by server.');

      const fresh=await api(`/api/personal-fc/clubs/${activeClubId}/tournaments`);
      if((fresh||[]).some((x:any)=>Number(x.id)===deletingId))
        throw new Error('Tournament still exists after delete. Database delete was not persisted.');

      setTours(fresh||[]);
      setDetail(null);
      setTourId(Number(fresh?.[0]?.id||0));
      setTourTab('overview');
    }catch(e:any){
      setErr(e?.message||String(e));
    }finally{
      setBusy(false);
    }
  };
  const duplicateTournament=async(row:any)=>{try{setBusy(true);setErr('');const saved=await api(`/api/personal-fc/clubs/${activeClubId}/tournaments/${row.id}/duplicate`,{method:'POST'});await loadClub();setTourId(Number(saved?.id||0));setTourTab('overview')}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
  const mergeTournament=async(row:any)=>{
    const candidates=tours.filter((x:any)=>Number(x.id)!==Number(row.id));if(!candidates.length){setErr('No target tournament is available for merge.');return}
    const targetText=prompt(`Merge "${row.name}" into which tournament?\n\n${candidates.map((x:any)=>`${x.id}: ${x.name} (${x.code})`).join('\n')}\n\nEnter target ID:`);if(!targetText)return;
    const targetId=Number(targetText);const target=candidates.find((x:any)=>Number(x.id)===targetId);if(!target){setErr('Invalid target tournament ID.');return}if(!confirm(`Merge "${row.name}" into "${target.name}"? The source tournament will be deleted.`))return;
    try{setBusy(true);setErr('');await api(`/api/personal-fc/clubs/${activeClubId}/tournaments/${row.id}/merge/${targetId}`,{method:'POST'});await loadClub();setDetail(null);setTourId(targetId);setTourTab('overview')}catch(e:any){setErr(e.message)}finally{setBusy(false)}
  };
  const tournamentAction=async(action:'activate'|'deactivate'|'complete')=>{
    if(!tourId)return;
    try{
      setBusy(true);
      await api(`/api/personal-fc/tournaments/${tourId}/${action}`,{method:'POST'});
      await loadClub();await loadTour();
    }catch(e:any){setErr(e.message)}finally{setBusy(false)}
  };

  // TOURNAMENT MEMBERS / REGISTRATIONS
  const regDefaults={
    memberId:null,fullName:'',company:'',department:'',phone:'',
    birthYear:null,birthDate:null,sex:'',skillRank:'',
    divisionName:'',groupName:'',shirtSize:'',notes:'',orderNo:null
  };
  const openRegistration=async(row?:any)=>{
    setRegEdit(row||null);
    setRegAvatarFile(null);setRegAvatarPreview(row?.memberId?memberAvatarMap[Number(row.memberId)]||'':row?.id?regAvatarMap[row.id]||'':'');
    setRegForm(row?{...row}:regDefaults);
    await loadParticipantMembers();
    setRegModal(true);
  };
  const saveRegistration=async()=>{
    if(!tourId)return;
    try{
      setBusy(true);setErr('');
      const url=regEdit
        ?`/api/personal-fc/tournaments/${tourId}/registrations/${regEdit.id}`
        :`/api/personal-fc/tournaments/${tourId}/registrations`;
      const saved=await api(url,{method:regEdit?'PUT':'POST',body:JSON.stringify(regForm)});
      const savedId=Number(saved?.id||saved?.Id||regEdit?.id||0);
      if(regAvatarFile&&savedId)await uploadAvatar(regAvatarFile,`/api/personal-fc/tournaments/${tourId}/registrations/${savedId}/avatar`);
      setRegModal(false);setRegAvatarFile(null);setRegAvatarPreview('');setRegEdit(null);await loadTour();
    }catch(e:any){setErr(e.message)}finally{setBusy(false)}
  };
  const deleteRegistration=async(row:any)=>{
    const registrationId=Number(row?.id||0);
    if(!registrationId||!tourId)return;

    if(!confirm(
      `Delete tournament member "${row.fullName||row.name||''}"?\n\n`+
      `If this member belongs to a team/pair, that team/pair and related matches will also be removed.`
    ))return;

    try{
      setBusy(true);
      setErr('');

      const result=await api(
        `/api/personal-fc/tournaments/${tourId}/registrations/${registrationId}`,
        {method:'DELETE'}
      );

      if(result?.deleted!==true)
        throw new Error('Delete was not confirmed by server.');

      await loadTour();
    }catch(e:any){
      setErr(e?.message||String(e));
    }finally{
      setBusy(false);
    }
  };


  const teamDefaults={teamCode:'',teamName:'',divisionName:'',groupName:'',registration1Id:null,registration2Id:null};
  const openTeam=(row?:any)=>{setTeamEdit(row||null);setTeamForm(row?{...row}:teamDefaults);setTeamModal(true)};
  const saveTeam=async()=>{if(!tourId)return;try{setBusy(true);setErr('');const payload={...teamForm,registration1Id:teamForm.registration1Id?Number(teamForm.registration1Id):null,registration2Id:teamForm.registration2Id?Number(teamForm.registration2Id):null};const url=teamEdit?`/api/personal-fc/tournaments/${tourId}/teams/${teamEdit.id}`:`/api/personal-fc/tournaments/${tourId}/teams`;await api(url,{method:teamEdit?'PUT':'POST',body:JSON.stringify(payload)});setTeamModal(false);setTeamEdit(null);await loadTour()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
  const deleteTeam=async(row:any)=>{if(!confirm(`Delete team/pair "${row.teamCode}"?`))return;try{setBusy(true);await api(`/api/personal-fc/tournaments/${tourId}/teams/${row.id}`,{method:'DELETE'});await loadTour()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
  const csvCell=(v:any)=>`"${String(v??'').replaceAll('"','""')}"`;
  const exportTeams=()=>{const regs=detail?.registrations||[];const regName=(id:any)=>regs.find((r:any)=>Number(r.id)===Number(id))?.fullName||'';const rows=[['Content','Group','Team Code','Team Name','Player 1','Player 2'],...(detail?.teams||[]).map((x:any)=>[x.divisionName||'',x.groupName||'',x.teamCode||'',x.teamName||'',regName(x.registration1Id),regName(x.registration2Id)])];const csv='\ufeff'+rows.map((r:any[])=>r.map(csvCell).join(',')).join('\n');const blob=new Blob([csv],{type:'text/csv;charset=utf-8'});const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download=`${detail?.tournament?.code||'TOURNAMENT'}-Teams.csv`;a.click();URL.revokeObjectURL(a.href)};
  const parseCsvLine=(line:string)=>{const out:string[]=[];let cur='';let quote=false;for(let i=0;i<line.length;i++){const ch=line[i];if(ch==='"'&&quote&&line[i+1]==='"'){cur+='"';i++;continue}if(ch==='"'){quote=!quote;continue}if(ch===','&&!quote){out.push(cur.trim());cur='';continue}cur+=ch}out.push(cur.trim());return out};
  const importTeams=async(file:File)=>{if(!tourId)return;try{setBusy(true);setErr('');const text=await file.text();const lines=text.replace(/^\ufeff/,'').split(/\r?\n/).filter(x=>x.trim());if(lines.length<2)throw new Error('CSV has no team rows.');const regs=detail?.registrations||[];const byName=(name:string)=>regs.find((r:any)=>String(r.fullName||'').trim().toLowerCase()===name.trim().toLowerCase());let created=0;for(const line of lines.slice(1)){const [divisionName='',groupName='',teamCode='',teamName='',p1='',p2='']=parseCsvLine(line);if(!teamCode)continue;const r1=byName(p1),r2=byName(p2);await api(`/api/personal-fc/tournaments/${tourId}/teams`,{method:'POST',body:JSON.stringify({divisionName,groupName,teamCode,teamName,registration1Id:r1?.id||null,registration2Id:r2?.id||null})});created++}await loadTour();alert(`Imported ${created} team(s)/pair(s).`)}catch(e:any){setErr(e.message)}finally{setBusy(false)}};

  const saveMatchScore=async(match:any)=>{if(!tourId)return;const d=scoreDrafts[match.id]||{score1:String(match.score1??''),score2:String(match.score2??'')};const score1=d.score1===''?null:Number(d.score1),score2=d.score2===''?null:Number(d.score2);try{setBusy(true);setErr('');await api(`/api/personal-fc/tournaments/${tourId}/matches/${match.id}/score`,{method:'PUT',body:JSON.stringify({score1,score2})});setScoreDrafts(prev=>{const n={...prev};delete n[match.id];return n});await loadTour()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
  const rankingRows=()=>{if(!detail)return[];const teams=detail.teams||[],matches=(detail.matches||[]).filter((m:any)=>m.roundName==='GROUP'&&m.score1!=null&&m.score2!=null);const rows:any[]=[];for(const t of teams){const ms=matches.filter((m:any)=>m.team1Id===t.id||m.team2Id===t.id);let pf=0,pa=0,w=0,l=0;for(const m of ms){const is1=m.team1Id===t.id;const a=Number(is1?m.score1:m.score2),b=Number(is1?m.score2:m.score1);pf+=a;pa+=b;if(a>b)w++;else l++}rows.push({team:t,division:t.divisionName||'',group:t.groupName||'',played:ms.length,won:w,lost:l,pf,pa,diff:pf-pa})}const mp=new Map<string,any[]>();for(const r of rows){const k=`${r.division}|||${r.group}`;if(!mp.has(k))mp.set(k,[]);mp.get(k)!.push(r)}const out:any[]=[];for(const [k,rs] of mp){rs.sort((a,b)=>b.won-a.won||b.diff-a.diff||b.pf-a.pf);rs.forEach((r,i)=>out.push({...r,rank:i+1,key:k}))}return out};
  const generateNextRound=async()=>{if(!tourId)return;try{setBusy(true);setErr('');const r=await api(`/api/personal-fc/tournaments/${tourId}/generate-next-round`,{method:'POST',body:JSON.stringify({mode:advanceMode,topN:advanceTopN})});await loadTour();setTourTab('matches');alert(`Created ${r?.created||0} match(es) for ${r?.round||'next round'}.`)}catch(e:any){setErr(e.message)}finally{setBusy(false)}};

  // TOURNAMENT MEMBER IMPORT / EXPORT V11
  const memberCsvCell=(v:any)=>`"${String(v??'').replaceAll('"','""')}"`;

  const parseMemberCsvLine=(line:string)=>{
    const out:string[]=[];let cur='';let quote=false;
    for(let i=0;i<line.length;i++){
      const ch=line[i];
      if(ch==='"'&&quote&&line[i+1]==='"'){cur+='"';i++;continue}
      if(ch==='"'){quote=!quote;continue}
      if(ch===','&&!quote){out.push(cur.trim());cur='';continue}
      cur+=ch;
    }
    out.push(cur.trim());
    return out;
  };

  const exportTournamentMembers=()=>{
    const rows:any[][]=[[
      'Member ID','Full Name','Phone','Company','Department','Sex',
      'Skill / Rank','Division','Group','Shirt Size','Order No.','Notes'
    ]];

    for(const x of (detail?.registrations||[])){
      rows.push([
        x.memberId??'',x.fullName??'',x.phone??'',x.company??'',x.department??'',
        x.sex??'',x.skillRank??x.rank??'',x.division??'',x.group??'',x.shirtSize??'',
        x.orderNo??'',x.notes??''
      ]);
    }

    const csv='\ufeff'+rows.map(r=>r.map(memberCsvCell).join(',')).join('\n');
    const blob=new Blob([csv],{type:'text/csv;charset=utf-8'});
    const a=document.createElement('a');
    a.href=URL.createObjectURL(blob);
    a.download=`${detail?.tournament?.code||'TOURNAMENT'}-Members.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  const importTournamentMembers=async(file:File)=>{
    if(!tourId)return;

    try{
      setBusy(true);setErr('');

      const text=await file.text();
      const lines=text.replace(/^\ufeff/,'').split(/\r?\n/).filter(x=>x.trim());
      if(lines.length<1)throw new Error('CSV is empty.');

      const header=parseMemberCsvLine(lines[0]).map(x=>x.trim().toLowerCase());
      const col=(...names:string[])=>{
        for(const n of names){
          const i=header.indexOf(n.toLowerCase());
          if(i>=0)return i;
        }
        return -1;
      };

      const ix={
        memberId:col('Member ID','MemberId'),
        fullName:col('Full Name','Name'),
        phone:col('Phone','Mobile'),
        company:col('Company'),
        department:col('Department'),
        sex:col('Sex','Gender'),
        skillRank:col('Skill / Rank','Skill Rank','Rank'),
        division:col('Division'),
        group:col('Group'),
        shirtSize:col('Shirt Size','ShirtSize'),
        orderNo:col('Order No.','Order No','OrderNo'),
        notes:col('Notes','Note')
      };

      if(ix.fullName<0)throw new Error('CSV must contain a Full Name column.');

      const clubId=Number(activeClubId||detail?.tournament?.clubId||0);
      const directoryRaw=clubId
        ?await api(`/api/personal-fc/clubs/${clubId}/directory`).catch(()=>[])
        :[];

      const directory=Array.isArray(directoryRaw)
        ?directoryRaw
        :(directoryRaw?.members||directoryRaw?.directory||directoryRaw?.items||directoryRaw?.data||[]);

      const norm=(v:any)=>String(v??'').trim().toLowerCase();
      const existing=(detail?.registrations||[]);
      let created=0,skipped=0;

      for(const line of lines.slice(1)){
        const a=parseMemberCsvLine(line);
        const fullName=ix.fullName>=0?a[ix.fullName]?.trim()||'':'';
        if(!fullName){skipped++;continue}

        const phone=ix.phone>=0?a[ix.phone]?.trim()||'':'';
        const explicitMemberId=ix.memberId>=0?Number(a[ix.memberId]||0):0;

        const linked=explicitMemberId
          ?directory.find((m:any)=>Number(m.id)===explicitMemberId)
          :directory.find((m:any)=>
              (phone&&norm(m.phone)===norm(phone)) ||
              norm(m.memberName||m.fullName||m.name)===norm(fullName)
            );

        const duplicate=existing.some((r:any)=>
          (phone&&norm(r.phone)===norm(phone)) ||
          norm(r.fullName)===norm(fullName)
        );

        if(duplicate){skipped++;continue}

        const payload:any={
          memberId:linked?.id||explicitMemberId||null,
          fullName,
          phone,
          company:ix.company>=0?a[ix.company]?.trim()||'':'',
          department:ix.department>=0?a[ix.department]?.trim()||'':'',
          sex:ix.sex>=0?a[ix.sex]?.trim()||'':'',
          skillRank:ix.skillRank>=0?a[ix.skillRank]?.trim()||'':'',
          division:ix.division>=0?a[ix.division]?.trim()||'':'',
          group:ix.group>=0?a[ix.group]?.trim()||'':'',
          shirtSize:ix.shirtSize>=0?a[ix.shirtSize]?.trim()||'':'',
          orderNo:ix.orderNo>=0&&a[ix.orderNo]?.trim()?Number(a[ix.orderNo]):null,
          notes:ix.notes>=0?a[ix.notes]?.trim()||'':''
        };

        await api(`/api/personal-fc/tournaments/${tourId}/registrations`,{
          method:'POST',
          body:JSON.stringify(payload)
        });

        created++;
      }

      await loadTour();
      alert(`Imported ${created} member(s). Skipped ${skipped} duplicate/invalid row(s).`);
    }catch(e:any){
      setErr(e?.message||String(e));
    }finally{
      setBusy(false);
    }
  };

  const autoPair=async()=>{try{setBusy(true);await api(`/api/personal-fc/tournaments/${tourId}/auto-pair`,{method:'POST'});await loadTour()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};
  const schedule=async()=>{try{setBusy(true);await api(`/api/personal-fc/tournaments/${tourId}/generate-round-robin`,{method:'POST'});await loadTour()}catch(e:any){setErr(e.message)}finally{setBusy(false)}};


  const localSearchHit=(q:string,...values:any[])=>{
    const n=q.trim().toLowerCase();
    return !n||values.filter(v=>v!==null&&v!==undefined).join(' ').toLowerCase().includes(n);
  };
  const filteredClubMembers=useMemo(()=>{
    const q=localMemberSearch.trim().toLowerCase();
    if(!q)return sortedMembers;
    return sortedMembers.filter((x:any)=>[
      x.memberCode,
      x.memberName,
      x.email,
      x.cellPhone,
      x.sex,
      x.skillRank,
      x.registrationStatus,
      x.status,
      x.memberRole,
      x.membershipType,
      x.notes
    ].filter(v=>v!==null&&v!==undefined).join(' ').toLowerCase().includes(q));
  },[sortedMembers,localMemberSearch]);



  const filteredClubs=(clubs||[]).filter((x:any)=>{
    const q=localClubSearch.trim().toLowerCase();
    return !q||[x.code,x.name,x.clubType,x.status,x.notes]
      .filter(v=>v!==null&&v!==undefined).join(' ').toLowerCase().includes(q);
  });


  const filteredTournaments=useMemo(
    ()=>(tours||[]).filter((x:any)=>localSearchHit(
      localTournamentSearch,
      x.code,x.name,x.tournamentDate,x.season,x.venue,x.status,x.format,x.notes
    )),
    [tours,localTournamentSearch]
  );


  const filteredTournamentMembers=useMemo(
    ()=>(detail?.registrations||[]).filter((x:any)=>localSearchHit(
      localTournamentMemberSearch,
      x.fullName,x.phone,x.company,x.department,
      x.divisionName,x.groupName,x.sex,x.skillRank,x.orderNo,x.notes
    )),
    [detail,localTournamentMemberSearch]
  );


  const filteredTournamentTeams=useMemo(
    ()=>(detail?.teams||[]).filter((x:any)=>localSearchHit(
      localTeamSearch,
      x.teamCode,x.teamName,x.divisionName,x.groupName
    )),
    [detail,localTeamSearch]
  );


  const filteredTournamentMatches=useMemo(
    ()=>(detail?.matches||[]).filter((x:any)=>{
      const a=(detail?.teams||[]).find((t:any)=>t.id===x.team1Id);
      const b=(detail?.teams||[]).find((t:any)=>t.id===x.team2Id);
      return localSearchHit(
        localMatchSearch,
        x.sequenceNo,x.divisionName,x.groupName,x.roundName,x.status,
        a?.teamCode,a?.teamName,b?.teamCode,b?.teamName,x.score1,x.score2
      );
    }),
    [detail,localMatchSearch]
  );


  // CT_TOP_SEARCH_BRIDGE_V4_1
  useEffect(()=>{
    const handler=(ev:any)=>{
      const q=String(ev?.detail?.query??'');
      if(mode==='members')setLocalMemberSearch(q);
      if(mode==='tournaments'){
        setLocalTournamentSearch(q);
        if(tourTab==='members')setLocalTournamentMemberSearch(q);
        if(tourTab==='teams')setLocalTeamSearch(q);
        if(tourTab==='matches')setLocalMatchSearch(q);
      }
    };
    window.addEventListener('mpms-global-search',handler as EventListener);
    return()=>window.removeEventListener('mpms-global-search',handler as EventListener);
  },[mode,tourTab]);

  return <section className={`club-tournament-panel${embedded?' embedded':''}`}>
    {err&&<div className="budget-error">{err}</div>}

    <div className="ct-toolbar">
      <div>
        <h3>{mode==='members'?'Club Members':'Tournaments'}</h3>
        <p>{mode==='members'?'Membership, approval, fee and player profile':'Tournament profile, participants, teams, groups and matches'}</p>
      </div>
<div className="pfc-local-search member-search"><Search/><input value={localMemberSearch} onChange={e=>setLocalMemberSearch(e.target.value)} placeholder="Search members by name, code, phone, email, rank..."/>{localMemberSearch&&<button type="button" onClick={()=>setLocalMemberSearch('')}>Clear</button>}</div>

      <div>
        {!embedded&&<select value={clubId} onChange={e=>{setClubId(Number(e.target.value));setTourId(0)}}>
          <div className="pfc-local-search compact"><Search/><input value={localClubSearch} onChange={e=>setLocalClubSearch(e.target.value)} placeholder="Search clubs..."/>{localClubSearch&&<button type="button" onClick={()=>setLocalClubSearch('')}>Clear</button>}</div>
{filteredClubs.map(c=><option value={c.id} key={c.id}>{c.name}</option>)}
        </select>}
        <button disabled={busy} onClick={refreshClubData}><RefreshCw/>Refresh</button>
        {mode==='members'&&<button className="primary" disabled={busy} onClick={()=>openMember()}><Plus/>Add Member</button>}
        {mode==='tournaments'&&<button className="primary" disabled={busy} onClick={()=>{setTourEdit(null);openTournament()}}><Plus/>New Tournament</button>}
      </div>
    </div>

    {mode==='members'&&<table>
      <thead><tr>
        <th>Code</th><th>Member</th><th>Phone</th><th>Sex</th><th>Rank</th>
        <th>Joined</th><th>Monthly Fee</th><th>Approval</th><th>Status</th><th>Actions</th>
      </tr></thead>
      <tbody>{filteredClubMembers.map(x=><tr key={x.id}>
        <td>{x.memberCode}</td>
        <td><div className="ct-member-name-with-avatar"><span className="ct-avatar-sm"><img src={`/api/personal-fc/clubs/${activeClubId}/members/${x.id}/avatar`} alt="" onError={e=>{const img=e.currentTarget as HTMLImageElement;img.style.display='none';const fb=img.nextElementSibling as HTMLElement|null;if(fb)fb.style.display='inline-flex'}}/><span className="ct-avatar-fallback" style={{display:'none'}}>{String(x.memberName||'?').slice(0,1).toUpperCase()}</span></span><span><b>{x.memberName}</b><small>{x.email}</small></span></div></td>
        <td>{x.cellPhone}</td><td>{x.sex}</td><td>{x.skillRank}</td><td>{x.joinDate||'—'}</td>
        <td>{money(x.monthlyFeeAmount||x.membershipFee)} ₫</td>
        <td><span className={`ct-badge ${(x.registrationStatus||'').toLowerCase()}`}>{x.registrationStatus||'PENDING'}</span></td>
        <td><span className={`ct-badge ${(x.status||'').toLowerCase()}`}>{x.status||'ACTIVE'}</span></td>
        <td><div className="ct-row-actions">
          <button title="Edit" onClick={()=>openMember(x)}><Edit3/></button>
          {x.registrationStatus!=='APPROVED'&&<button title="Approve" className="approve" onClick={()=>memberAction(x,'approve')}><Check/></button>}
          {x.registrationStatus!=='REJECTED'&&<button title="Reject" className="reject" onClick={()=>memberAction(x,'reject')}><UserX/></button>}
          {x.status==='INACTIVE'
            ?<button title="Activate" onClick={()=>memberAction(x,'activate')}><UserCheck/></button>
            :<button title="Deactivate" onClick={()=>memberAction(x,'deactivate')}><UserX/></button>}
          <button title="Delete" className="danger" onClick={()=>deleteMember(x)}><Trash2/></button>
        </div></td>
      </tr>)}</tbody>
    </table>}

    {mode==='tournaments'&&<div className="ct-tours">
      <aside><div className="pfc-local-search compact"><Search/><input value={localTournamentSearch} onChange={e=>setLocalTournamentSearch(e.target.value)} placeholder="Search tournaments..."/>{localTournamentSearch&&<button type="button" onClick={()=>setLocalTournamentSearch('')}>Clear</button>}</div>
        {filteredTournaments.map(t=><button key={t.id} className={tourId===t.id?'sel':''} onClick={()=>{setTourId(t.id);setTourTab('overview')}}>
          <Trophy/><span><b>{t.name}</b><small>{t.tournamentDate||t.season||'Date TBD'} · {t.status}</small></span>
        </button>)}
      </aside>

      <main>
        {!detail?<p>Select a tournament.</p>:<>
          <div className="ct-tour-head">
            <div>
              <h3>{detail.tournament.name}</h3>
              <p>{detail.tournament.code} · {detail.tournament.tournamentDate||'Date TBD'} · {detail.tournament.status}</p>
            </div>
            <div className="ct-row-actions">
              <button title="Edit Tournament" onClick={()=>openTournament(detail.tournament)}><Edit3/></button>
              <button title="Duplicate Tournament" onClick={()=>duplicateTournament(detail.tournament)}><Copy/></button>
              <button title="Merge Tournament" onClick={()=>mergeTournament(detail.tournament)}><GitMerge/></button>
              {detail.tournament.status==='INACTIVE'
                ?<button title="Activate" onClick={()=>tournamentAction('activate')}><UserCheck/></button>
                :<button title="Deactivate" onClick={()=>tournamentAction('deactivate')}><UserX/></button>}
              <button title="Complete" className="approve" onClick={()=>tournamentAction('complete')}><Check/></button>
              <button title="Delete Tournament" className="danger" onClick={()=>deleteTournament(detail.tournament)}><Trash2/></button>
            </div>
          </div>

          <div className="ct-tour-tabs">
            <button className={tourTab==='overview'?'active':''} onClick={()=>setTourTab('overview')}>Overview</button>
            <button className={tourTab==='members'?'active':''} onClick={()=>setTourTab('members')}>Members ({detail.registrations.length})</button>
            <button className={tourTab==='teams'?'active':''} onClick={()=>setTourTab('teams')}>Teams & Groups ({detail.teams.length})</button>
            <button className={tourTab==='matches'?'active':''} onClick={()=>setTourTab('matches')}>Matches ({detail.matches.length})</button><button className={tourTab==='rankings'?'active':''} onClick={()=>setTourTab('rankings')}>Rankings</button>
          </div>

          {tourTab==='overview'&&<>
            <div className="ct-kpis">
              <div><span>Participants</span><b>{detail.registrations.length}</b></div>
              <div><span>Teams</span><b>{detail.teams.length}</b></div>
              <div><span>Matches</span><b>{detail.matches.length}</b></div>
              <div><span>Groups</span><b>{new Set(detail.registrations.map((x:any)=>x.groupName).filter(Boolean)).size}</b></div>
            </div>

            <div className="ct-overview-grid">
              <div><span>Code</span><b>{detail.tournament.code}</b></div>
              <div><span>Date</span><b>{detail.tournament.tournamentDate||'—'}</b></div>
              <div><span>Season</span><b>{detail.tournament.season||'—'}</b></div>
              <div><span>Venue</span><b>{detail.tournament.venue||'—'}</b></div>
              <div><span>Format</span><b>{detail.tournament.format||'—'}</b></div>
              <div><span>Status</span><b>{detail.tournament.status||'—'}</b></div>
            </div>

            <div className="ct-about">
              <h4>About / Notes</h4>
              <p>{detail.tournament.notes||'No tournament description yet.'}</p>
            </div>
          </>}

          {tourTab==='members'&&<>
            <div className="ct-section-head">
              <h4>Tournament Members</h4>
              <button className="primary" onClick={()=>openRegistration()}><Plus/>Add Participant</button>
            
              <div className="ct-member-transfer-actions">
                <button type="button" onClick={exportTournamentMembers}><Download/> Download Members</button>
                <label className="ct-upload-btn"><Upload/> Upload Members
                  <input hidden type="file" accept=".csv,text/csv" onChange={e=>{const f=e.target.files?.[0];if(f)importTournamentMembers(f);e.currentTarget.value=''}}/>
                </label>
              </div>
</div>
            <div className="pfc-local-search"><Search/><input value={localTournamentMemberSearch} onChange={e=>setLocalTournamentMemberSearch(e.target.value)} placeholder="Search tournament members by name, phone, company, division..."/>{localTournamentMemberSearch&&<button type="button" onClick={()=>setLocalTournamentMemberSearch('')}>Clear</button>}</div>
<table>
              <thead><tr><th>#</th><th>Name</th><th>Phone</th><th>Company</th><th>Division</th><th>Group</th><th>Sex</th><th>Rank</th><th>Actions</th></tr></thead>
              <tbody>{filteredTournamentMembers.map((x:any)=><tr key={x.id}>
                <td>{x.orderNo||''}</td><td><div className="ct-member-name-with-avatar"><span className="ct-avatar-sm"><img src={`/api/personal-fc/tournaments/${tourId}/registrations/${x.id}/avatar`} alt="" onError={e=>{const img=e.currentTarget as HTMLImageElement;img.style.display='none';const fb=img.nextElementSibling as HTMLElement|null;if(fb)fb.style.display='inline-flex'}}/><span className="ct-avatar-fallback" style={{display:'none'}}>{String(x.fullName||'?').slice(0,1).toUpperCase()}</span></span><b>{x.fullName}</b></div></td><td>{x.phone}</td><td>{x.company}</td>
                <td>{x.divisionName||'—'}</td><td>{x.groupName||'—'}</td><td>{x.sex}</td><td>{x.skillRank}</td>
                <td><div className="ct-row-actions">
                  <button title="Edit" onClick={()=>openRegistration(x)}><Edit3/></button>
                  <button title="Delete" className="danger" onClick={()=>deleteRegistration(x)}><Trash2/></button>
                </div></td>
              </tr>)}</tbody>
            </table>
          </>}

          {tourTab==='teams'&&<>
            <div className="ct-section-head ct-team-head-v2">
              <div><h4>Teams & Groups</h4><small>Organized by Content / Event, then teams or pairs.</small></div>
              <div className="ct-team-actions-v2">
                <button onClick={exportTeams}><Download/> Download CSV</button>
                <label className="ct-upload-btn"><Upload/> Upload CSV<input hidden type="file" accept=".csv,text/csv" onChange={e=>{const f=e.target.files?.[0];if(f)importTeams(f);e.currentTarget.value=''}}/></label>
                <button onClick={()=>openTeam()}><Plus/> Add Team / Pair</button>
                <button onClick={autoPair}>Auto Pair Teams</button>
              </div>
            </div>
            <div className="ct-content-sections">
              {Array.from(new Set(filteredTournamentTeams.map((x:any)=>x.divisionName||'Uncategorized'))).map((content:any)=>{
                const teams=(detail.teams||[]).filter((x:any)=>(x.divisionName||'Uncategorized')===content);
                return <section className="ct-content-section" key={content}>
                  <header><div><h4>{content}</h4><span>{teams.length} team(s) / pair(s)</span></div></header>
                  <div className="pfc-local-search"><Search/><input value={localTeamSearch} onChange={e=>setLocalTeamSearch(e.target.value)} placeholder="Search teams / pairs by code, name, content or group..."/>{localTeamSearch&&<button type="button" onClick={()=>setLocalTeamSearch('')}>Clear</button>}</div>
<div className="ct-team-grid ct-team-grid-v2">{teams.map((x:any)=><article key={x.id} className="ct-team-card-v2">
                    <div className="ct-team-card-top"><div><b>{x.teamCode}</b>{x.groupName&&<small>Group {x.groupName}</small>}</div><div className="ct-row-actions"><button title="Edit team / pair" onClick={()=>openTeam(x)}><Edit3/></button><button title="Delete team / pair" className="danger" onClick={()=>deleteTeam(x)}><Trash2/></button></div></div>
                    <span>{x.teamName}</span>
                    <small>{(()=>{const r1=(detail.registrations||[]).find((r:any)=>r.id===x.registration1Id);const r2=(detail.registrations||[]).find((r:any)=>r.id===x.registration2Id);return [r1?.fullName,r2?.fullName].filter(Boolean).join(' / ')||'No players assigned'})()}</small>
                  </article>)}</div>
                </section>
              })}
              {!detail.teams?.length&&<div className="ct-empty-teams">No teams yet. Add manually, upload CSV, or use Auto Pair Teams.</div>}
            </div>
          </>}

          {tourTab==='matches'&&<><div className="ct-section-head"><h4>Matches</h4><button onClick={schedule}>Generate Round Robin</button></div><div className="pfc-local-search"><Search/><input value={localMatchSearch} onChange={e=>setLocalMatchSearch(e.target.value)} placeholder="Search matches by team, group, round or status..."/>{localMatchSearch&&<button type="button" onClick={()=>setLocalMatchSearch('')}>Clear</button>}</div>
<table className="ct-match-score-table"><thead><tr><th>#</th><th>Division</th><th>Group</th><th>Round</th><th>Team 1</th><th>Score</th><th>Team 2</th><th>Status</th><th></th></tr></thead><tbody>{filteredTournamentMatches.map((x:any)=>{const a=detail.teams.find((t:any)=>t.id===x.team1Id),b=detail.teams.find((t:any)=>t.id===x.team2Id);const d=scoreDrafts[x.id]||{score1:String(x.score1??''),score2:String(x.score2??'')};return <tr key={x.id}><td>{x.sequenceNo}</td><td>{x.divisionName}</td><td>{x.groupName||'—'}</td><td>{x.roundName}</td><td>{a?.teamName||'TBD'}</td><td><div className="ct-score-editor"><input inputMode="numeric" value={d.score1} onChange={e=>setScoreDrafts(p=>({...p,[x.id]:{...d,score1:e.target.value.replace(/[^\d]/g,'')}}))}/><span>:</span><input inputMode="numeric" value={d.score2} onChange={e=>setScoreDrafts(p=>({...p,[x.id]:{...d,score2:e.target.value.replace(/[^\d]/g,'')}}))}/></div></td><td>{b?.teamName||'TBD'}</td><td>{x.status}</td><td><button className="ct-score-save" onClick={()=>saveMatchScore(x)}>Save</button></td></tr>})}</tbody></table></>}{tourTab==='rankings'&&<><div className="ct-section-head ct-ranking-head"><div><h4>Standings & Advancement</h4><small>Rank by Wins → Point Difference → Points For.</small></div><div className="ct-advance-controls"><label>Qualify<select value={advanceTopN} onChange={e=>setAdvanceTopN(Number(e.target.value))}><option value={1}>Top 1 / group</option><option value={2}>Top 2 / group</option><option value={3}>Top 3 / group</option><option value={4}>Top 4 / group</option></select></label><label>Next round pairing<select value={advanceMode} onChange={e=>setAdvanceMode(e.target.value)}><option value="TOP2_CROSS">Cross groups: 1A–2B, 1B–2A</option><option value="SEEDED">Seeded: best vs lowest seed</option><option value="SEQUENTIAL">Sequential ranking order</option></select></label><button className="primary" onClick={generateNextRound}>Generate Next Round</button></div></div><div className="ct-ranking-sections">{Array.from(new Set(rankingRows().map((r:any)=>r.key))).map((key:any)=>{const rs=rankingRows().filter((r:any)=>r.key===key),first=rs[0];return <section className="ct-ranking-section" key={key}><header><h4>{first?.division||'Division'} · Group {first?.group||'—'}</h4></header><table><thead><tr><th>Rank</th><th>Team / Pair</th><th>P</th><th>W</th><th>L</th><th>PF</th><th>PA</th><th>Diff</th><th>Qualified</th></tr></thead><tbody>{rs.map((r:any)=><tr key={r.team.id} className={r.rank<=advanceTopN?'ct-qualified':''}><td><b>{r.rank}</b></td><td>{r.team.teamName||r.team.teamCode}</td><td>{r.played}</td><td>{r.won}</td><td>{r.lost}</td><td>{r.pf}</td><td>{r.pa}</td><td>{r.diff>0?'+':''}{r.diff}</td><td>{r.rank<=advanceTopN?'✓':''}</td></tr>)}</tbody></table></section>})}</div></>}
        </>}
      </main>
    </div>}

    {memberModal&&<Modal title={memberEdit?'Edit Club Member':'Add Club Member'} onClose={()=>{setMemberModal(false);setMemberAvatarFile(null);setMemberAvatarPreview('')}} onSave={saveMember}>
      <Field label="Member Image / Avatar"><div className="ct-avatar-edit"><div className="ct-avatar-preview">{memberAvatarPreview?<img src={memberAvatarPreview} alt=""/>:<span>{String(memberForm.memberName||'?').slice(0,1).toUpperCase()}</span>}</div><label className="ct-avatar-choose">Choose Image<input hidden type="file" accept=".png,.jpg,.jpeg,.webp" onChange={e=>{const f=e.target.files?.[0];if(!f)return;setMemberAvatarFile(f);setMemberAvatarPreview(URL.createObjectURL(f))}}/></label></div></Field>
      <Field label="Member Code"><input value={memberForm.memberCode||''} onChange={e=>setMemberForm((f:any)=>({...f,memberCode:e.target.value}))}/></Field>
      <Field label="Full Name"><input value={memberForm.memberName||''} onChange={e=>setMemberForm((f:any)=>({...f,memberName:e.target.value}))}/></Field>
      <Field label="Cell Phone"><input value={memberForm.cellPhone||''} onChange={e=>setMemberForm((f:any)=>({...f,cellPhone:e.target.value}))}/></Field>
      <Field label="Email"><input value={memberForm.email||''} onChange={e=>setMemberForm((f:any)=>({...f,email:e.target.value}))}/></Field>
      <Field label="Sex"><select value={memberForm.sex||''} onChange={e=>setMemberForm((f:any)=>({...f,sex:e.target.value}))}><option value="">—</option><option>MALE</option><option>FEMALE</option></select></Field>
      <Field label="Skill / Rank"><input value={memberForm.skillRank||''} onChange={e=>setMemberForm((f:any)=>({...f,skillRank:e.target.value}))}/></Field>
      <Field label="Birth Date"><input type="date" value={memberForm.birthDate||''} onChange={e=>setMemberForm((f:any)=>({...f,birthDate:e.target.value||null}))}/></Field>
      <Field label="Joined Date"><input type="date" value={memberForm.joinDate||''} onChange={e=>setMemberForm((f:any)=>({...f,joinDate:e.target.value||null}))}/></Field>
      <Field label="Member Role"><select value={memberForm.memberRole||'MEMBER'} onChange={e=>setMemberForm((f:any)=>({...f,memberRole:e.target.value}))}><option>MEMBER</option><option>ADMIN</option><option>TREASURER</option><option>GUEST</option></select></Field>
      <Field label="Membership Type"><select value={memberForm.membershipType||'MONTHLY'} onChange={e=>setMemberForm((f:any)=>({...f,membershipType:e.target.value}))}><option>MONTHLY</option><option>ANNUAL</option><option>GUEST</option></select></Field>
      <Field label="Monthly Fee"><div className="ct-money-input"><input inputMode="numeric" value={moneyText(memberForm.monthlyFeeAmount)} placeholder="0" onChange={e=>setMemberForm((f:any)=>({...f,monthlyFeeAmount:moneyNumber(e.target.value)}))}/><span>₫</span></div></Field>
      <Field label="Recurring Fee"><input type="checkbox" checked={!!memberForm.recurringFeeEnabled} onChange={e=>setMemberForm((f:any)=>({...f,recurringFeeEnabled:e.target.checked}))}/></Field>
      <Field label="Recurring Day"><input inputMode="numeric" value={memberForm.recurringFeeDay??1} onChange={e=>{const d=e.target.value.replace(/[^\d]/g,'');const n=d?Math.min(28,Math.max(1,Number(d))):1;setMemberForm((f:any)=>({...f,recurringFeeDay:n}))}}/></Field>
      <Field label="Approval"><select value={memberForm.registrationStatus||'PENDING'} onChange={e=>setMemberForm((f:any)=>({...f,registrationStatus:e.target.value}))}><option>PENDING</option><option>APPROVED</option><option>REJECTED</option></select></Field>
      <Field label="Status"><select value={memberForm.status||'ACTIVE'} onChange={e=>setMemberForm((f:any)=>({...f,status:e.target.value}))}><option>ACTIVE</option><option>INACTIVE</option></select></Field>
      <Field label="Notes"><textarea value={memberForm.notes||''} onChange={e=>setMemberForm((f:any)=>({...f,notes:e.target.value}))}/></Field>
    </Modal>}

    {teamModal&&<Modal title={teamEdit?'Edit Team / Pair':'Add Team / Pair'} onClose={()=>{setTeamModal(false);setTeamEdit(null)}} onSave={saveTeam}>
      <Field label="Content / Event"><input placeholder="e.g. Đồng đội, Nam Nam, Nam Nữ, NewBie" value={teamForm.divisionName||''} onChange={e=>setTeamForm((f:any)=>({...f,divisionName:e.target.value}))}/></Field>
      <Field label="Group"><input placeholder="e.g. A, B, C" value={teamForm.groupName||''} onChange={e=>setTeamForm((f:any)=>({...f,groupName:e.target.value}))}/></Field>
      <Field label="Team / Pair Code"><input value={teamForm.teamCode||''} onChange={e=>setTeamForm((f:any)=>({...f,teamCode:e.target.value}))}/></Field>
      <Field label="Team / Pair Name"><input value={teamForm.teamName||''} onChange={e=>setTeamForm((f:any)=>({...f,teamName:e.target.value}))}/></Field>
      <Field label="Player 1"><select value={teamForm.registration1Id||''} onChange={e=>setTeamForm((f:any)=>({...f,registration1Id:Number(e.target.value)||null}))}><option value="">— Select participant —</option>{(detail?.registrations||[]).map((r:any)=><option key={r.id} value={r.id}>{r.fullName} · {r.sex||'—'} · {r.skillRank||'—'}</option>)}</select></Field>
      <Field label="Player 2"><select value={teamForm.registration2Id||''} onChange={e=>setTeamForm((f:any)=>({...f,registration2Id:Number(e.target.value)||null}))}><option value="">— Single / No second player —</option>{(detail?.registrations||[]).map((r:any)=><option key={r.id} value={r.id}>{r.fullName} · {r.sex||'—'} · {r.skillRank||'—'}</option>)}</select></Field>
    </Modal>}

    {tourModal&&<Modal title={tourEdit?'Edit Tournament':'New Tournament'} onClose={()=>{setTourModal(false);setTourEdit(null);setTourForm({})}} onSave={saveTournament}>
      <Field label="Tournament Code"><input value={tourForm.code||''} onChange={e=>setTourForm((f:any)=>({...f,code:e.target.value.toUpperCase()}))}/></Field>
      <Field label="Tournament Name"><input value={tourForm.name||''} onChange={e=>setTourForm((f:any)=>({...f,name:e.target.value}))}/></Field>
      <Field label="Tournament Date"><input type="date" value={tourForm.tournamentDate||''} onChange={e=>setTourForm((f:any)=>({...f,tournamentDate:e.target.value||null}))}/></Field>
      <Field label="Season"><input value={tourForm.season||''} onChange={e=>setTourForm((f:any)=>({...f,season:e.target.value}))}/></Field>
      <Field label="Venue"><input value={tourForm.venue||''} onChange={e=>setTourForm((f:any)=>({...f,venue:e.target.value}))}/></Field>
      <Field label="Format"><select value={tourForm.format||'GROUP_ROUND_ROBIN'} onChange={e=>setTourForm((f:any)=>({...f,format:e.target.value}))}><option>GROUP_ROUND_ROBIN</option><option>ROUND_ROBIN</option><option>KNOCKOUT</option><option>GROUP_KNOCKOUT</option></select></Field>
      <Field label="Status"><select value={tourForm.status||'DRAFT'} onChange={e=>setTourForm((f:any)=>({...f,status:e.target.value}))}><option>DRAFT</option><option>REGISTRATION</option><option>ACTIVE</option><option>INACTIVE</option><option>COMPLETED</option></select></Field>
      <Field label="About / Notes"><textarea value={tourForm.notes||''} onChange={e=>setTourForm((f:any)=>({...f,notes:e.target.value}))}/></Field>
    </Modal>}

    {regModal&&<Modal title={regEdit?'Edit Tournament Member':'Add Tournament Member'} onClose={()=>{setRegModal(false);setRegAvatarFile(null);setRegAvatarPreview('')}} onSave={saveRegistration}>
      <Field label="Participant Image / Avatar"><div className="ct-avatar-edit"><div className="ct-avatar-preview">{regAvatarPreview?<img src={regAvatarPreview} alt=""/>:<span>{String(regForm.fullName||'?').slice(0,1).toUpperCase()}</span>}</div><label className="ct-avatar-choose">Choose Image<input hidden type="file" accept=".png,.jpg,.jpeg,.webp" onChange={e=>{const f=e.target.files?.[0];if(!f)return;setRegAvatarFile(f);setRegAvatarPreview(URL.createObjectURL(f))}}/></label></div></Field>
      <Field label="Club Member">
        <select value={regForm.memberId||''} onChange={e=>{
          const id=Number(e.target.value)||null;
          const m=participantMembers.find((x:any)=>x.id===id);
          setRegAvatarPreview(id?memberAvatarMap[id]||'':'');
          const by=String(m?.birthDate||'').slice(0,4);
          setRegForm((f:any)=>({...f,
            memberId:id,
            fullName:m?.memberName||f.fullName,
            phone:m?.cellPhone||f.phone,
            sex:m?.sex||f.sex,
            skillRank:m?.skillRank||f.skillRank,
            birthDate:m?.birthDate||f.birthDate,
            birthYear:by?Number(by):f.birthYear
          }));
        }}>
          {participantMembersLoading&&<option value="" disabled>Loading club members...</option>}
          {!participantMembersLoading&&availableMembers.map((m:any)=><option key={m.id} value={m.id}>{m.memberCode} · {m.memberName}</option>)}
          <option value="">External / Manual</option>
          {regEdit?.memberId&&!availableMembers.some((m:any)=>m.id===regEdit.memberId)&&
            <option value={regEdit.memberId}>{regEdit.fullName}</option>}
          </select>
        <small className="ct-member-load-hint">{participantMembersLoading?'Loading...':`${availableMembers.length} club member(s) available for this tournament`}</small>
      </Field>
      <Field label="Full Name"><input value={regForm.fullName||''} onChange={e=>setRegForm((f:any)=>({...f,fullName:e.target.value}))}/></Field>
      <Field label="Phone"><input value={regForm.phone||''} onChange={e=>setRegForm((f:any)=>({...f,phone:e.target.value}))}/></Field>
      <Field label="Company"><input value={regForm.company||''} onChange={e=>setRegForm((f:any)=>({...f,company:e.target.value}))}/></Field>
      <Field label="Department"><input value={regForm.department||''} onChange={e=>setRegForm((f:any)=>({...f,department:e.target.value}))}/></Field>
      <Field label="Sex"><select value={regForm.sex||''} onChange={e=>setRegForm((f:any)=>({...f,sex:e.target.value}))}><option value="">—</option><option>MALE</option><option>FEMALE</option></select></Field>
      <Field label="Skill / Rank"><input value={regForm.skillRank||''} onChange={e=>setRegForm((f:any)=>({...f,skillRank:e.target.value}))}/></Field>
      <Field label="Division"><input value={regForm.divisionName||''} onChange={e=>setRegForm((f:any)=>({...f,divisionName:e.target.value}))}/></Field>
      <Field label="Group"><input value={regForm.groupName||''} onChange={e=>setRegForm((f:any)=>({...f,groupName:e.target.value}))}/></Field>
      <Field label="Shirt Size"><input value={regForm.shirtSize||''} onChange={e=>setRegForm((f:any)=>({...f,shirtSize:e.target.value}))}/></Field>
      <Field label="Order No."><input inputMode="numeric" value={regForm.orderNo??''} onChange={e=>{const d=e.target.value.replace(/[^\d]/g,'');setRegForm((f:any)=>({...f,orderNo:d?Number(d):null}))}}/></Field>
      <Field label="Notes"><textarea value={regForm.notes||''} onChange={e=>setRegForm((f:any)=>({...f,notes:e.target.value}))}/></Field>
    </Modal>}
  </section>;
}
