# QualifyAI local Docker stack

The solution root is the single entry point for the modular platform API, Identity, API Gateway,
and their infrastructure.
The Angular admin UI lives in the separate `qualifyai-admin` repository and is not built by this compose project.

## Start

1. Install Docker Desktop and enable Linux containers.
2. From the solution root, create the local environment file:

   ```powershell
   Copy-Item .env.example .env
   ```

3. Configure the Windows SQL Server connection in `.env`. Docker containers reach a local
   SQL Express instance through `host.docker.internal` and its fixed TCP port:

   ```dotenv
   DB_SERVER=host.docker.internal,1433
   DB_USER=your-local-sql-user
   DB_PASSWORD=your-local-sql-password
   ```

   Also set `OPENAI_API_KEY` and change the remaining development passwords when required.
4. Build and start the complete backend:

   ```powershell
   docker compose --project-directory src/Infrastructure/QualifyAI.Infrastructure.Hosting up -d --build
   docker compose --project-directory src/Infrastructure/QualifyAI.Infrastructure.Hosting ps
   ```

You can also run `./start-all.ps1` from PowerShell.

## Local endpoints

| Component | URL |
|---|---|
| API Gateway | http://localhost:10000 |
| Platform API | http://localhost:8080/swagger |
| Identity API | http://localhost:8081/swagger |
| Consul | http://localhost:8500 |
| RabbitMQ management | http://localhost:15672 |
| Seq | http://localhost:5341 |
| Portainer | https://localhost:9443 |

Run the Angular UI from `qualifyai-admin` on `http://localhost:4200`.

## Diagnose and stop

```powershell
docker compose --project-directory src/Infrastructure/QualifyAI.Infrastructure.Hosting logs -f --tail=200
docker compose --project-directory src/Infrastructure/QualifyAI.Infrastructure.Hosting ps
docker compose --project-directory src/Infrastructure/QualifyAI.Infrastructure.Hosting down
```

Use `docker compose --project-directory src/Infrastructure/QualifyAI.Infrastructure.Hosting down -v` only when you intentionally want to delete all local database and broker data.
