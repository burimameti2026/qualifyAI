# Architecture

## Public request path

```text
Browser / Angular
       |
       v
Nginx :8088
  |            |
  | /connect   | /api + /hubs
  v            v
Identity API   Business API
OpenIddict     CRM / Sales / Inbox / Ticketing / Billing / Analytics / Admin
```

Identity is a separate bounded service with its own SQL database. The Business API validates access tokens issued by Identity and resolves the tenant from `tenant_slug` / `tenant_id` claims.

## Service architecture rule

All transactional services follow the same dependency direction:

```text
API / Controller / Endpoint
        |
        v
Application Command / Query
        |
        v
Handler / Application Service
        |
        v
Repository Contract + Unit of Work
        |
        v
Infrastructure Repository
        |
        v
DbContext / External Store
        |
        v
Domain Aggregate
```

API code must not contain business rules. Direct `DbContext` access from API endpoints is legacy code and is migrated feature-by-feature behind application contracts.

## Bounded contexts

A bounded context exists only when there is a real domain ownership boundary: different invariants, lifecycle, security model, transaction boundary, scaling characteristic or persistence technology. A folder, screen, entity, use case, command or feature is not automatically a bounded context.

Inside one microservice, related aggregates may share the same relational persistence boundary. We intentionally avoid creating one DbContext per feature.

## DbContext policy

- Default: **one relational DbContext per microservice**.
- A service may use **up to 4–5 contexts only when there are genuinely separate persistence or transaction boundaries**.
- Do not create `CrmDbContext`, `TicketDbContext`, `BillingDbContext`, etc. only to mirror feature folders when those areas are part of one transactional service/database.
- Organize a large context with domain-oriented `Persistence/Configurations` and repositories rather than splitting it artificially.
- Separate stores such as MongoDB/vector storage are adapters, not a reason to multiply relational DbContexts.
- Cross-service synchronization uses integration events/outbox/inbox patterns; services do not share DbContexts.

Current intended persistence boundaries:

| Service | Relational DbContext policy | Notes |
| --- | --- | --- |
| Identity | 1 Identity context | Users, roles, permissions, tenants, licensing, clients, outbox |
| Business | 1 Business context | CRM, sales, inbox/support, workflows, billing, analytics, white-label |
| Automation | 1 Automation context | Definitions and executions owned by Automation |
| Knowledge | 1 Knowledge SQL context | Mongo/vector store is a separate adapter for unstructured chunks |
| Integrations | 1 Integrations context | Connections and integration metadata |
| Notifications | 1 Notifications context | Notification persistence/delivery state |
| AI Orchestration | 1 AI orchestration context | Agent metadata and runtime configuration |

## Specialized services

Automation, Notifications, Knowledge, AI Orchestration and Integrations remain separate workloads because their execution/storage/integration characteristics differ from conventional transactional business modules. They share RabbitMQ/Consul/Seq infrastructure.

They should follow the same four-layer structure used by Identity:

```text
<Service>.Domain
<Service>.Application
<Service>.Infrastructure
<Service>.Api
```

Within those projects, organize code by domain capability, for example:

```text
Application/
  Agents/
    Commands/
    Queries/
  Runtime/
  Tools/

Infrastructure/
  Persistence/
    Configurations/
    Repositories/
  Messaging/
  Integrations/
```

This organization does not imply a separate DbContext for every folder.

## Data

- Business DB: `QualifyAI_Business`
- Identity DB: `QualifyAI_IdentityDb`
- specialized services: independent databases
- MongoDB: knowledge/vector/unstructured workloads where enabled
- Redis: shared cache/idempotency infrastructure

Each service owns its schema. Other services consume published contracts/events rather than querying another service's tables.

## Authentication

Identity uses ASP.NET Core Identity + OpenIddict:

- password grant for the first-party admin client
- refresh tokens
- client credentials for service-to-service clients
- tenant claims
- license/entitlement claims
- roles + permissions
- lockout
- authenticator MFA
- password reset/change
- user enable/disable

The Angular admin uses `/connect/token` through Nginx. Business and specialized APIs use Identity as their JWT authority.

## Messaging and consistency

Identity is authoritative for tenant, licensing and access state. Changes are written transactionally to its outbox and published over RabbitMQ. Other services maintain only the projections they need and must process integration events idempotently through inbox/consumer patterns.
