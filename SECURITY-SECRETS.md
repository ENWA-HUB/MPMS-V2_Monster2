# MPMS server secrets

MPMS does not read the Microsoft 365 client secret or SQL Server connection
string from tracked JSON configuration. Configure them on the server process:

- `MPMS_SQLSERVER_CONNECTION_STRING`
- `MPMS_M365_CLIENT_SECRET`

The following non-secret M365 settings may also be supplied by environment
variables:

- `MPMS_M365_ENABLED`
- `MPMS_M365_TENANT_ID`
- `MPMS_M365_CLIENT_ID`
- `MPMS_M365_DRIVE_ID`

## IIS deployment

Copy `backend/MAIPT.PM.Api/web.config.example` to `web.config` on the server and
replace placeholders there, or configure equivalent application settings in
the hosting control panel. The real `web.config` is ignored by Git.

Restrict filesystem permissions so only the application-pool identity and
server administrators can read the deployed configuration. Do not include the
real `web.config`, `.env`, database backups, or publish directories in source
control or deployment archives shared with third parties.

If a secret has ever been committed, copied into a publish package, sent in
chat/email, or exposed in logs, removing it from the current file is not enough:
rotate it at the provider and invalidate the old value.
