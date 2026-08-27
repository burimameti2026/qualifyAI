# QualifyAI local Docker stack

The solution root is the single entry point for all backend APIs and their infrastructure.
The Angular admin UI lives in the separate `qualifyai-admin` repository and is not built by this compose project.

## Start

1. Install Docker Desktop and enable Linux containers.
2. From the solution root, create the local environment file:

   ```powershell
   Copy-Item .env.example .env
   ```

3. Set `OPENAI_API_KEY` and change the development passwords in `.env` when required.
4. Build and start the complete backend:

   ```powershell
   docker compose up -d --build
   docker compose ps
   ```

You can also run `./start-all.ps1` from PowerShell.

## Local endpoints

| Component | URL |
|---|---|
| Business API | http://localhost:8080/swagger |
| Identity API | http://localhost:8081/swagger |
| Automation API | http://localhost:8082/swagger |
| Notifications API | http://localhost:8083/swagger |
| Knowledge API | http://localhost:8084/swagger |
| AI Orchestration API | http://localhost:8085/swagger |
| Integrations API | http://localhost:8086/swagger |
| Consul | http://localhost:8500 |
| RabbitMQ management | http://localhost:15672 |
| Seq | http://localhost:5341 |

Run the Angular UI from `qualifyai-admin` on `http://localhost:4200`.

## Diagnose and stop

```powershell
docker compose logs -f --tail=200
docker compose ps
docker compose down
```

Use `docker compose down -v` only when you intentionally want to delete all local database and broker data.
