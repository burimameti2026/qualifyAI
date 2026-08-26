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

## Specialized services

Automation, Notifications, Knowledge, AI Orchestration and Integrations remain separate workloads because their execution/storage/integration characteristics differ from conventional transactional business modules. They share RabbitMQ/Consul/Seq infrastructure.

## Data

- Business DB: `QualifyAI_Business`
- Identity DB: `QualifyAI_IdentityDb`
- specialized services: independent databases
- MongoDB: knowledge/vector/unstructured workloads where enabled
- Redis: shared cache/idempotency infrastructure

## Authentication

Identity uses ASP.NET Core Identity + OpenIddict:

- password grant for the first-party admin client
- refresh tokens
- tenant claims
- roles + permissions
- lockout
- authenticator MFA
- password reset/change
- user enable/disable

The Angular admin uses `/connect/token` through Nginx. Business API uses Identity as its JWT authority.
