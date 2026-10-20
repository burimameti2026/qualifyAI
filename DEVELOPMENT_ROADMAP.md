# LeadsAI Development Roadmap

This document tracks the major development work remaining after completion of the core tenant lifecycle, licensing, provisioning, and enterprise billing hardening foundations.

## Completed Foundations

- Tenant lifecycle and provisioning orchestration
- Licensing and entitlement lifecycle
- Persistent audit and lifecycle events
- Enterprise billing lifecycle
- Trials, grace periods, retries and dunning foundations
- Billing provider adapter architecture
- Persistent billing state and alerts
- Persistent usage metering and quota enforcement foundations
- Billing lifecycle and usage UI
- Automated billing hardening regression tests and CI gate

---

# Next Phase — Acquisition Backend Closure and Verification

**Objective:** verify and harden the existing acquisition implementation before adding more architecture. Keep `refactor/tenant-job-runtime` as the working branch and preserve the canonical `AgentJob + CampaignContainerRuntime + AgentJobTaskFactory` execution path. Do not introduce a second runtime path.

## A. Mandatory end-to-end verification

- [ ] Run the new `CampaignContainerActivities` database migration against a disposable/test SQL Server database; verify schema, indexes, and safe legacy activity backfill.
- [ ] Add/run integration coverage for Brief → ICP/Industry Pack → Campaign/Agent → Container → AgentJob → Discovery → Qualification → Enrichment → Target List.
- [ ] Verify persisted database results at every stage; a successful HTTP response or status transition alone is not sufficient.
- [ ] Verify approval blocks outreach until explicit approval, and that approve/reject outcomes are persisted and visible in history.
- [ ] Verify manual start, pause, resume, stop, completion, failure, retry, and lease-expiry recovery against real database state.
- [ ] Verify Container Activity and campaign execution history filters and status transitions, including recovery events.
- [ ] Run the existing FusionFleet E2E/smoke scripts where their prerequisites are available; record which steps use demo providers versus live integrations.
- [ ] Add CI gates for acquisition integration tests and migration validation so runtime regressions are caught before merge.

## B. Close functional gaps found by verification

- [ ] Confirm the discovery provider used in production is a real configured integration; the deterministic demo provider is only for development/testing.
- [ ] Verify qualification scoring and enrichment have concrete providers, persisted outputs, error handling, and retry-safe behavior—not just orchestration steps.
- [ ] Verify target-list membership is idempotent and qualified prospects are not duplicated on job retry.
- [ ] Verify outreach provider configuration, delivery result persistence, retry rules, and duplicate-send protection. Keep approval as a mandatory gate.
- [ ] Verify the AI Campaign Operator produces a plan appropriate to the brief. Distinguish current template/heuristic parsing from genuine model-generated planning, and return actionable configuration errors when AI is unavailable.
- [ ] Verify provisioning prerequisites end-to-end: active tenant, active license, required acquisition/CRM entitlements, tenant-scoped data, and authorization policies. Do not bypass tenant middleware to resolve 403s.
- [ ] Check campaign, container, job, task, and agent status consistency for every terminal and recovery path; fix only observed defects.

## C. Runtime reliability and tenant isolation

- [ ] Add automated tests proving atomic job claiming prevents two workers from executing the same job concurrently.
- [ ] Test lease heartbeat loss, worker/process shutdown, expired-lease recovery, max-attempt failure, and task/container reconciliation.
- [ ] Test that a tenant's worker pool establishes tenant context before resolving tenant-scoped services and cannot read or execute another tenant's jobs.
- [ ] Verify retry and recovery are idempotent and do not duplicate prospects, target-list entries, approvals, or outreach.
- [ ] Run a controlled multi-tenant/multi-campaign concurrency test and document observed throughput and failure behavior.
- [ ] Verify cancellation tokens and timeouts are respected by discovery, qualification, enrichment, and outreach providers.

## Exit criteria

The acquisition backend phase is complete only when:
1. Database migration and schema checks pass.
2. The end-to-end flow has passing integration evidence through persisted target-list results.
3. Approval and outreach have a verified success path and a verified rejection/blocking path.
4. Pause/resume/stop/retry/recovery tests pass.
5. Tenant isolation and concurrent job claiming tests pass.
6. CI runs the relevant tests and the result is recorded.
7. Demo-only capabilities and production integrations are explicitly distinguished.

Do not mark the entire acquisition flow as production-ready solely because the solution builds or CI's unrelated tests pass.

---

# Priority 1 — Production Readiness

## 1. Observability and Monitoring

- Structured logging
- Distributed tracing
- Application and infrastructure metrics
- Metrics dashboards
- Dependency health monitoring
- Error tracking
- Performance monitoring and alert thresholds

## 2. Security Hardening

- Rate limiting
- Per-tenant API throttling
- Extended security audit trails
- Secret rotation strategy
- Stronger API key lifecycle management
- Advanced RBAC policies
- Security and penetration testing

## 3. Database and Infrastructure Resilience

- Automated backups
- Automated restore testing
- Disaster recovery plan
- Database retention policies
- Environment separation
- Dedicated staging environment

---

# Priority 2 — Tenant Enterprise Operations

## 4. Tenant Backup and Restore

Lifecycle target:

```text
Active -> Backup -> Archive -> Restore
```

- Tenant data backup
- Backup metadata and verification
- Restore workflow
- Restore validation

## 5. Tenant Deletion Lifecycle

- Soft deletion
- Configurable retention period
- Scheduled permanent deletion
- GDPR deletion support
- Data export before deletion
- Deletion audit trail

## 6. Tenant Archival Lifecycle

- Inactive tenant detection
- Archive workflow
- Archived tenant access restrictions
- Restore archived tenant
- Lower-cost storage strategy

---

# Priority 3 — Enterprise Scale

## 7. Durable Async Job Infrastructure

- Persistent background jobs
- Retry policies
- Dead-letter handling
- Job monitoring
- Job priority
- Worker health monitoring

## 8. Event Bus and Outbox Pattern

- Domain events
- Integration events
- Transactional outbox
- Reliable event delivery
- Consumer retries
- Dead-letter queues

## 9. Distributed Cache

- Distributed cache integration
- Tenant-aware cache keys
- Cache invalidation strategy
- Cache observability
- Distributed rate limiting support

---

# Priority 4 — Product Growth

## 10. Advanced Analytics

- Tenant usage dashboards
- Revenue analytics
- Funnel analytics
- Cohort analysis
- AI performance analytics

## 11. AI Platform Hardening

- Multiple AI provider abstraction
- Provider fallback strategy
- AI cost tracking
- Token budgets
- Prompt evaluation automation
- Model performance monitoring

## 12. Integration Marketplace

- Integration catalog
- OAuth connection flows
- Webhook management
- Integration monitoring
- Retry dashboard

---

# Priority 5 — Developer and Operations Experience

## 13. Admin Operations Center

Central operations UI for:

- Tenants
- Licenses and entitlements
- Billing lifecycle
- Provisioning
- Background jobs
- Failed events
- Retries
- System health
- Audit and operational activity

## 14. Production CI/CD Pipeline

- Automated test gates
- Security scanning
- Staging deployment
- Production deployment
- Rollback strategy
- Release verification

---

# Recommended Execution Order

1. Acquisition backend closure and end-to-end verification
2. Observability and Monitoring
3. Security Hardening
4. Tenant Backup / Archive / Deletion
5. Durable Async Job Infrastructure
6. Event Bus and Transactional Outbox
7. Distributed Cache
8. Admin Operations Center
9. Production CI/CD Pipeline
10. Advanced Analytics
11. AI Platform Hardening
12. Integration Marketplace

## Goal

The objective of these priorities is to move LeadsAI from an enterprise feature-complete platform toward a fully production-grade SaaS platform with strong operational reliability, security, scalability, and observability.
