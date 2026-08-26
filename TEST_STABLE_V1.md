# Stable v1 Test Sequence

## 1. Infrastructure

```powershell
cd infra
Copy-Item .env.example .env
Unblock-File .\install-infra.ps1
.\install-infra.ps1
```

Check Docker Desktop plus:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml ps
```

## 2. Application stack — sequential build

```powershell
cd ..\services
Copy-Item .env.example .env
Unblock-File .\install-api.ps1
.\install-api.ps1
```

Do not replace this with `docker compose up -d --build` while diagnosing the NuGet EOF issue. The script builds one service at a time and each .NET image restores with `--disable-parallel`.

## 3. Login

Open `http://localhost:8088`.

```text
workspace: demo
email: admin@demo.local
password: Admin123!
```

## 4. Functional order

1. Platform → Users & Access: list/create/enable-disable user.
2. CRM → Contacts: create/edit/delete contact.
3. CRM → Leads: create lead using a real contact, qualify, convert.
4. Pipeline: drag converted opportunity to another stage and refresh.
5. Meetings: create/edit/cancel meeting.
6. Inbox: send reply, internal note, takeover, close.
7. Tickets: create/open/edit status and priority.
8. Knowledge: create/edit/reindex/retrieve; test a knowledge gap failure does not fake success.
9. AI Agents: create/update/test agent.
10. Workflows: add/connect/save nodes without `findLast` dependency.
11. Automations: create/run and inspect run history.
12. Integrations: create/update/test generic connection.
13. Analytics/Billing/Security/White Label/Audit.

## 5. Logs

For any failure inspect Seq first: `http://localhost:5341`.

For container logs:

```powershell
docker compose logs --tail 200 business-api
docker compose logs --tail 200 identity-api
```
