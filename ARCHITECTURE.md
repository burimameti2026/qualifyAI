# Architecture

## Deployment model

QualifyAI is a modular platform with three deployable .NET hosts:

1. `QualifyAI.ApiGateway` — YARP edge routing.
2. `QualifyAI.Api` — the authenticated platform HTTP host and SignalR endpoint.
3. `QualifyAI.Identity.Api` — OpenIddict, users, tenants, roles, permissions and licenses.

Automation, Notifications, Knowledge, AI Orchestration and Integrations are modules inside the
platform process. Each module retains separate Domain, Application and Infrastructure projects,
its own EF Core DbContext and its own database. They are not separate Web API processes.

## Public request path

```text
Browser / Angular
       |
       v
QualifyAI.ApiGateway :10000
  |                         |
  | /connect + /identity    | /api + /hubs + /services
  v                         v
Identity API                Platform API
OpenIddict                  CRM / Support / Sales / Modules / SignalR
```

The Angular UI uses relative URLs through the gateway and never addresses an internal container
directly.

## HTTP contract

- `/connect/*` and `/identity/*` route to Identity.
- `/api/*` and `/hubs/*` route to the platform API.
- `/services/{module}/*` is a compatibility route to `/api/modules/{module}/*`.

## Application dependency rule

All modules follow the same dependency direction:

```text
HTTP Endpoint
    -> Application Command / Query
    -> Handler / Application Service
    -> Repository Contract + Unit of Work
    -> Infrastructure Repository
    -> DbContext / External Store
    -> Domain Aggregate
```

API code must not contain business rules. Direct `DbContext` access from API endpoints is legacy
code and is migrated feature-by-feature behind application contracts.

## Module boundaries

A module exists when there is a real domain ownership, persistence, lifecycle or integration
boundary. A screen, entity or folder alone is not a module.

Each module keeps the following shape:

```text
<Module>.Domain
<Module>.Application
<Module>.Infrastructure
```

The shared platform API owns HTTP composition only. This keeps modules extractable if a future
scaling or operational requirement justifies moving one back into a separate process.

## Persistence boundaries

| Module | Relational context | Additional store |
| --- | --- | --- |
| Platform | `AppDbContext` | Redis cache |
| Identity | `IdentityDbContext` | — |
| Automation | `AutomationDbContext` | — |
| Notifications | `NotificationsDbContext` | — |
| Knowledge | `KnowledgeDbContext` | MongoDB chunks |
| AI Orchestration | `AIOrchestrationDbContext` | provider APIs |
| Integrations | `IntegrationsDbContext` | provider APIs |

The platform host migrates its module databases during startup. Identity migrates and owns its
database separately.

## Authentication

Identity uses ASP.NET Core Identity and OpenIddict with password, refresh-token and
client-credentials flows. It issues tenant, license, module, role and permission claims. The
platform API validates tokens using Identity as its JWT authority and the `qualifyai-api`
audience.

## Messaging and consistency

The platform host configures one MassTransit bus instance with all module consumers. Identity is
authoritative for tenant, licensing and access state. Identity changes are written to its outbox
and published through RabbitMQ; module consumers update their projections idempotently through
their inbox state.

## Infrastructure contract

- SQL Server: external Windows SQL Express in development, configured through the ignored `.env`.
- MongoDB: knowledge chunk documents.
- Redis: distributed cache registered through ServiceDefaults.
- RabbitMQ: integration events and entitlement propagation.
- Docker DNS: direct service-to-service addressing inside the single Compose network.
- Seq: centralized structured logs.
- Portainer: local container operations.

The Docker network is `qualifyai-network`.
