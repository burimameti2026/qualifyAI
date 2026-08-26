# QualifyAI Application Stack

Install `../infra` first.

This stack contains:
- Identity API
- Tenant Management API
- CRM API
- Conversations API
- Ticketing API
- Knowledge API
- AI Orchestration API
- Qualification API
- Automation API
- Sales API
- Integrations API
- Notifications API
- Analytics API
- Billing API
- YARP Gateway
- Angular Admin UI

All service containers join the existing `qualifyai-infra` Docker network.

## Network contract

Browser/UI -> Gateway only.

The UI never calls an internal API directly.

Gateway -> Consul -> healthy service instance -> API.

APIs use:
- RabbitMQ for integration events
- Redis for distributed cache/coordination
- SQL Server for relational service-owned persistence
- MongoDB where document/event/read-model workloads require it
- Seq for centralized structured logs

## Install

```powershell
cd services
copy .env.example .env
# make the credentials match infra\.env
.\install-api.ps1
```

UI: http://localhost:8088
Gateway: http://localhost:8080
