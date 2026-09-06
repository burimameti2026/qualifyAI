# Identity Service

RaiseLead identity owns authentication, authorization, tenants, clients, licensing, and identity persistence.

## Project boundaries

- `Api` — HTTP endpoints and composition only.
- `Core` — identity domain and application behavior.
- `Infrastructure` — external integrations and infrastructure implementations.
- `Sql` — SQL Server persistence and Entity Framework Core.
- `Commands` and `Queries` — application use-case projects to be extracted from Core in the next Identity pass.
- `EventProcessors` — event handling separated from transport adapters.
- `Engines` — long-running/domain processing engines.
- `Agents` — autonomous identity-related agents when introduced.

The existing tenant-management integration remains part of the service boundary during reorganization.
