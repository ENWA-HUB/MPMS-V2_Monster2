BEGIN TRANSACTION;

UPDATE Users
SET Role='ADMIN'
WHERE LOWER(Email)='maipt@maipt.org';

DECLARE @uid BIGINT = (
    SELECT TOP 1 Id
    FROM Users
    WHERE LOWER(Email)='maipt@maipt.org'
);

IF @uid IS NOT NULL
BEGIN
    DECLARE @Modules TABLE(Module NVARCHAR(64));
    INSERT INTO @Modules(Module) VALUES
    ('DASHBOARD'),('PROJECTS'),('TASKS'),('RISKS'),('BUDGET'),
    ('SUPPLIERS'),('KPI'),('APPROVALS'),('REPORTS'),('EXECUTIVE'),
    ('DOCUMENTS'),('SETTINGS'),('ACCESS_CONTROL'),('USERS'),('PROJECT_TEAM');

    DECLARE @Actions TABLE(Permission NVARCHAR(32));
    INSERT INTO @Actions(Permission) VALUES
    ('VIEW'),('CREATE'),('EDIT'),('DELETE'),
    ('SUBMIT'),('APPROVE'),('REPORT'),('FULL');

    MERGE UserModulePermissions AS T
    USING (
        SELECT @uid AS UserId,m.Module,a.Permission,CAST(1 AS bit) AS IsAllowed
        FROM @Modules m
        CROSS JOIN @Actions a
    ) AS S
    ON T.UserId=S.UserId
       AND T.Module=S.Module
       AND T.Permission=S.Permission
    WHEN MATCHED THEN
      UPDATE SET T.IsAllowed=1
    WHEN NOT MATCHED THEN
      INSERT(UserId,Module,Permission,IsAllowed)
      VALUES(S.UserId,S.Module,S.Permission,S.IsAllowed);
END;

COMMIT;
