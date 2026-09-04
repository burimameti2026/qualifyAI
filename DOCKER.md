# QualifyAI local backend

The backend is one Docker Compose project: Identity API, Platform API, API Gateway,
MongoDB, RabbitMQ, Redis, and Seq. The Angular UI remains in the separate
`qualifyai-admin` repository.

SQL Server is intentionally external. Both APIs run in Linux containers and connect
to the Windows `SQLEXPRESS` instance through `host.docker.internal` and a fixed TCP port.

## One-time SQL Express setup

1. In SQL Server Configuration Manager, enable TCP/IP for `SQLEXPRESS`.
2. Under `IPAll`, clear `TCP Dynamic Ports` and set `TCP Port` to `1433`.
3. Restart `SQL Server (SQLEXPRESS)`.
4. Enable mixed authentication and the SQL login used in `.env`.
5. Allow inbound TCP port 1433 in Windows Firewall.

Do not put `.\SQLEXPRESS` in the container connection string. That name only works
from Windows processes on the host.

## Start

From the solution root:

```powershell
Copy-Item .env.example .env
# Edit DB_USER, DB_PASSWORD and the other local secrets.
.\start-all.ps1
```

`start-all.ps1` validates `.env`, verifies that SQL Server is listening, validates
Compose, and starts the complete backend with one command.

## Endpoints

| Component | URL |
|---|---|
| API Gateway | http://localhost:10000 |
| Gateway health | http://localhost:10000/health |
| Platform API | http://localhost:8080/swagger |
| Identity API | http://localhost:8081/swagger |
| RabbitMQ management | http://localhost:15672 |
| Seq | http://localhost:5341 |

Portainer is optional and starts only with:

```powershell
docker compose --profile tools up -d portainer
```

## Diagnose and stop

```powershell
.\status-all.ps1
docker compose logs platform-api identity-api api-gateway --tail 200
.\stop-all.ps1
```

To rebuild cleanly without deleting data:

```powershell
docker compose down --remove-orphans
docker compose up -d --build
```

Use `docker compose down -v` only when you intentionally want to delete MongoDB,
RabbitMQ, Redis, Seq, and Portainer local data.
