BEGIN TRANSACTION;
UPDATE p SET p.Module='USERS' FROM UserModulePermissions p WHERE p.Module='TEAM_MEMBERS' AND NOT EXISTS (SELECT 1 FROM UserModulePermissions x WHERE x.UserId=p.UserId AND x.Module='USERS' AND x.Permission=p.Permission);
DELETE FROM UserModulePermissions WHERE Module='TEAM_MEMBERS';
UPDATE s SET s.Module='USERS' FROM UserModuleScopes s WHERE s.Module='TEAM_MEMBERS' AND NOT EXISTS (SELECT 1 FROM UserModuleScopes x WHERE x.UserId=s.UserId AND x.Module='USERS' AND x.ScopeMode=s.ScopeMode AND x.ScopeValue=s.ScopeValue);
DELETE FROM UserModuleScopes WHERE Module='TEAM_MEMBERS';
COMMIT;
