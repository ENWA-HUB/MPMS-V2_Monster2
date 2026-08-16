# MAIPT Project Management V2

SQLite-first project management platform based on the MAIPT-PM V2 ERD.

## Stack

- Frontend: React + Vite + TypeScript
- Backend: ASP.NET Core 8 Minimal API + EF Core
- Database V1: SQLite
- Upgrade path: SQL Server via `DB_PROVIDER=sqlserver`

## Included modules

- Executive Dashboard
- Portfolio / Projects
- Tasks & Milestones
- Risks & Issues
- Budget Control
- Suppliers / Contracts / Deliverables
- Supplier KPI with versioned criteria snapshots
- Approval Workflow
- Notifications
- Document metadata foundation
- Activity Log foundation
- Organization / User / RBAC data model

## Run with Docker (recommended)

```bash
docker compose up --build
```

Then open:

- Frontend: http://localhost:8088
- API: http://localhost:5088
- Swagger: http://localhost:5088/swagger

SQLite data is stored in a persistent Docker volume.

## Run without Docker

Backend requires .NET 8 SDK:

```bash
cd backend/MAIPT.PM.Api
dotnet restore
dotnet run --urls http://localhost:5088
```

Frontend:

```bash
cd frontend
npm install
npm run dev
```

Set `VITE_API_URL=http://localhost:5088/api` if required.

## Switch to SQL Server

Provide a SQL Server connection string and run the API with:

```bash
DB_PROVIDER=sqlserver dotnet run
```

The project already includes both EF Core SQLite and SQL Server providers. For production migration, replace `EnsureCreated()` with normal EF Core migrations.

## Deployment target

Recommended split:

- `project.maipt.org`: React frontend on Cloudflare Pages
- `api-project.maipt.org`: ASP.NET Core API on a persistent container/VPS
- SQLite stays with the API on persistent storage
- Later: move DB to SQL Server without rewriting the frontend/API contracts

## V2 seed data

The first run creates demo data for projects, budgets, risks, suppliers, contracts, deliverables, KPI and approvals so the dashboard is immediately usable.
