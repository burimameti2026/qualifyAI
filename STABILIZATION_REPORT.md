# QualifyAI Enterprise — Stable v1 Stabilization Report

This branch is based on the current Complete Functional Master. It keeps the current architecture and restores/regresses fixes from the previous working sessions without reintroducing the old Gateway or the old many-business-API layout.

## Active architecture

- Separate Identity service:
  - QualifyAI.Identity.Api
  - QualifyAI.Identity.Application
  - QualifyAI.Identity.Domain
  - QualifyAI.Identity.Infrastructure
- Consolidated Business API:
  - QualifyAI.Domain.Core
  - QualifyAI.Application
  - QualifyAI.Application.Commands
  - QualifyAI.Application.Queries
  - QualifyAI.Infrastructure
  - QualifyAI.Api
- Specialized services remain separate: Automation, Notifications, Knowledge, AI Orchestration, Integrations.
- One active Angular source tree: `admin/qualifyai-admin`.

## Restored/fixed regressions

1. Removed duplicate obsolete `ui/qualifyai-admin` source tree. Docker and developers now use one Angular source of truth.
2. `Array.findLast()` regression is absent from the active source.
3. `CreateLeadTool` duplicate anonymous `Id` regression is absent.
4. Authenticated tenant context can no longer be overridden with `X-Tenant` or query string. It comes from the signed `tenant_slug` claim.
5. Identity password failures now increment ASP.NET Identity lockout counters and successful login resets them.
6. Knowledge Gap resolve no longer mutates the UI to success after an API failure.
7. Core dead UI actions were fixed or removed:
   - Leads `+ Create lead` is a real API-backed form with contact selection.
   - Audit Export produces CSV.
   - Ticket Open is explicitly wired.
   - Pipeline dead ellipsis action removed.
   - White-label conversation button is explicitly preview-only.
   - form submit buttons use explicit `type="submit"`.
8. Core CQRS is now real, not only empty projects:
   - DashboardOverviewQuery + handler
   - CreateContactCommand + handler
   - CreateLeadCommand + handler
   - QualifyLeadCommand + handler
   - CreateTicketCommand + handler
   - MediatR registered in Business API
9. Docker/NuGet restore hardened:
   - root `NuGet.Config`
   - `maxHttpRequestsPerSource=4`
   - `dotnet restore --disable-parallel`
   - BuildKit NuGet package cache
   - bounded restore retry (3 attempts)
   - application images build sequentially in `services/install-api.ps1`
   - stack starts with `--no-build` after all images succeed

## Static validation result

- Broken `.csproj` references: 0
- Broken `.sln` project paths: 0
- Invalid JSON: 0
- Invalid Docker YAML: 0
- Known `findLast()` regression: 0
- Known duplicate anonymous lead/contact `Id`: 0
- Old Gateway source references: 0
- Old TenantManagement API service references: 0
- Dead non-submit/non-preview buttons in active feature templates: 0
- Angular business API calls with no static business route match: 0
- Active Angular API calls checked: 69
- Business route declarations checked: 67

## Mandatory runtime gates on the developer machine

Static validation does not replace compilation/runtime tests. Run in this order:

```powershell
cd infra
Copy-Item .env.example .env
.\install-infra.ps1

cd ..\services
Copy-Item .env.example .env
.\install-api.ps1
```

`install-api.ps1` intentionally builds each image sequentially to reduce Docker Desktop/WSL NuGet TLS pressure.

Then verify:

```text
UI:       http://localhost:8088
Business: http://localhost:8080/swagger
Identity: http://localhost:8081/swagger
Seq:      http://localhost:5341
Gateway:  http://localhost:10000
```

Demo login:

```text
workspace: demo
email: admin@demo.local
password: Admin123!
```

If a .NET compiler error occurs, fix it in this Stable-v1 branch; do not regenerate/restructure the project.
