# QualifyAI Infrastructure

This stack is installed **before** application services.

Base `docker-compose.yml`:
- Consul
- RabbitMQ + Management
- Redis
- Seq
- Portainer

`docker-compose.override.yml`:
- SQL Server
- MongoDB

The override is intentionally used for persistence. Docker Compose automatically merges it with
the base file when `docker compose up` runs in this directory.

## Install

```powershell
cd infra
copy .env.example .env
# edit .env
.\install-infra.ps1
```

Verify:
- Consul http://localhost:8500
- RabbitMQ http://localhost:15672
- Seq http://localhost:5341
- Portainer https://localhost:9443

The shared external Docker network is `qualifyai-infra`.
