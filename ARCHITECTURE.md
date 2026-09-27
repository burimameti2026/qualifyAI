# Architecture

## Platform identity

The platform name is **RaiseLead**. `QualifyAI` is the legacy solution/project naming and is being retired as part of the architecture migration.

## Target deployment model

RaiseLead is moving to independently deployable service boundaries. Identity is a completely independent service and other services access it through HTTP APIs only. No other microservice references Identity's internal projects or database.

```text
Browser / Frontend
       |
       v
   API Gateway
       |
       +--------------------+-------------------+------------------+
       v                    v                   v                  v
   Identity API         CRM API            Support API       Billing API ...
       |                    |                   |                  |
   Identity DB          CRM DB             Support DB          Billing DB
```

Each business microservice owns its API, application layer, domain model, infrastructure and SQL persistence. Services communicate through explicit HTTP contracts for synchronous operations and through the messaging/event system for asynchronous integration.

## Root organization

The target repository organization is:

```text
RaiseLead/
├── core/
├── infrastructure/
├── sql/
├── commands/
├── queries/
├── services/
│   ├── identity/
│   ├── crm/
│   ├── support/
│   ├── billing/
│   └── ...
├── event-processors/
├── engines/
├── agents/
├── orchestration/
└── tests/
```

`services/<name>` is the master folder for each microservice. The exact sub-project names may evolve during the Identity-first migration, but the ownership boundary must remain explicit.

## Service structure

A service should follow this dependency direction:

```text
API
  -> Application / Commands / Queries
  -> Domain
  -> Infrastructure
  -> SQL / external stores
```

A service may expose repositories, domain services, handlers and contracts internally, but those implementation details are not shared directly with another microservice.

## Identity boundary

Identity owns:

- users
- authentication and token issuance
- tenants
- roles and permissions
- licenses / entitlements
- client applications
- identity-related persistence

Other services consume Identity through HTTP contracts. Identity events can also be published for eventual-consistency workflows, but consumers must never read Identity's database directly.

## Commands and queries

Commands and queries are separated from HTTP presentation. They should be organized by owning service rather than forming one cross-service business layer.

```text
commands/
├── identity/
├── crm/
├── support/
└── ...

queries/
├── identity/
├── crm/
├── support/
└── ...
```

Handlers remain owned by the service that owns the business capability.

## Event processors

Event processors are separate workers/processes responsible for consuming integration events and driving asynchronous workflows or projections.

```text
event-processors/
├── identity/
├── crm/
├── billing/
└── ...
```

API projects must not become the general-purpose event processing layer.

## Engines

Engines contain substantial business processing that may be reused by an API, event processor or agent without coupling those runtime hosts together.

```text
engines/
├── acquisition/
├── qualification/
├── scoring/
└── ...
```

## Agents

Agents are separate application/runtime components with explicit tools and state. They are not embedded inside API controllers or infrastructure implementations.

```text
agents/
├── acquisition/
├── qualification/
├── support/
└── ...
```

## Existing architecture during migration

The current `dev` branch still contains the legacy `QualifyAI` solution naming and a mixed `src/Platform`, `src/Identity` and `src/Shared` layout. The migration will be performed incrementally, starting with a complete cleanup and isolation of Identity, then moving service-by-service.

Until a service is migrated, existing runtime behavior may temporarily differ from the target structure. New work should follow the target rules above and must not introduce new cross-service project/database coupling.
