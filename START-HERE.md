# MPMS-V2 CLEAN

This R2 package was rebuilt from the uploaded MPMS-V2 project and restores the backend Data source folder (AppDbContext + SeedData).

## What was repaired
- Preserved the existing SQLite database.
- Normalized DB path to `backend/MAIPT.PM.Api/data/maipt-pm.db`.
- Kept the Budget Management page and `/api/budget/overview`.
- Kept logo + favicon.
- Kept single-port hosting: frontend and API both run on port 5088.
- Removed copied `node_modules`, `bin`, `obj`, macOS metadata and stale TypeScript build cache.
- Aligned EF Core packages with .NET 10.
- Added safer nullable transaction-date handling.
- Added one-click-ish Mac scripts.

## First run
From Terminal:

```bash
cd /Users/maipt/Documents/MPMS-V2-CLEAN
chmod +x run-mpms.command rebuild-frontend.command
./run-mpms.command
```

Then open:
`http://localhost:5088`

The package already contains the current built frontend in `backend/MAIPT.PM.Api/wwwroot`, so you do NOT need npm for the first run.

## Only when frontend source is changed
```bash
cd /Users/maipt/Documents/MPMS-V2-CLEAN
./rebuild-frontend.command
./run-mpms.command
```

## Health checks
`http://localhost:5088/api/health`
`http://localhost:5088/api/budget/overview`

## Database
`backend/MAIPT.PM.Api/data/maipt-pm.db`


## Presentation Mode R3
- New Presentation menu
- Live portfolio KPIs from SQLite/API
- Portfolio distribution donut
- Performance trend
- Budget allocation vs spending
- Executive status callouts
- Full-screen presentation mode
- Export PDF via browser print


## R4 — Supplier Management + Reports & Executive View
- Supplier Management dashboard with performance radar, rankings, active supplier table, CSV export and Add Supplier form.
- Reports & Analytics dashboard with report cards, Gantt view, portfolio chart, Budget vs Actual, supplier radar and report export.
- Presentation menu renamed to Executive View.
- Repairs missing PresentationPage import in the R3 application shell.
- New live API endpoint: GET /api/suppliers/overview.


## R5 — Projects Management
- Dedicated Projects dashboard.
- Portfolio distribution.
- Upcoming milestones for next 60 days.
- Project KPI cards and health counts.
- Search + status + portfolio filters.
- All Projects table with schedule, progress, budget, health and status.
- New Project form.
- CSV export.
- New API: GET /api/projects/overview.


## R6 — Documents + Risks/Issues + Tasks/Milestones + Supplier KPI
- Dedicated Tasks & Milestones page with search/filter and create workflows.
- Dedicated Risks & Issues page with risk scoring and issue severity.
- Document Management with folders and physical file upload/download.
- Uploaded files: `backend/MAIPT.PM.Api/storage/documents`; SQLite stores metadata.
- Supplier KPI New Evaluation now writes weighted score snapshots into SQLite.
- Fixed PresentationPage TypeScript field mismatch from R5.


## Recommended R6 start
```bash
cd /Users/maipt/Documents/MPMS-V2-CLEAN-R6
chmod +x rebuild-frontend.command run-mpms.command
./rebuild-frontend.command
./run-mpms.command
```

Do not copy old R2/R3/R4/R5 patches into R6.
