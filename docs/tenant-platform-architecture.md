# Tenant / Workspace / Module Architecture

## Purpose

The `renova` branch is the first fully configured tenant implementation and must remain reusable for future tenants. Renova-specific catalog/content is tenant data; tenant provisioning, licensing, module entitlements, portal entitlements, and workspace access are platform concerns.

## Canonical flow

```text
Master / Platform Admin
  -> Create tenant
  -> Provision dedicated tenant database
  -> Apply migrations
  -> Configure package/license
  -> Enable modules
  -> Enable portals
  -> Create tenant admin
  -> Grant workspace access
  -> Activate tenant

User login
  -> Authenticate credentials
  -> Resolve tenant/workspace memberships
  -> Select workspace when multiple are available
  -> Establish tenant context
  -> Resolve licensed/enabled modules and portals
  -> Render only permitted navigation/features
```

## Separation of concerns

### Control plane

The shared platform/control plane owns platform administration and provisioning metadata: tenant lifecycle, package/license state, module entitlements, portal entitlements, workspace membership, database mapping, and provisioning state.

### Tenant plane

Each tenant owns its business data in its dedicated database. For Renova this includes the product catalog, variants, localized content, promotion plans, acquisition data, prospects, campaigns, portal inquiries, and related business records.

### Identity

Identity remains centrally managed, but every user and access record is tenant-scoped. Authentication must never bypass tenant, license, or user status checks.

## Reusable tenant onboarding

Adding a future client must be configuration/provisioning work rather than a new application fork. A new tenant receives its own database, migrations, license/package, selected modules, selected portals, workspace, and administrator.

## Example module/portal configuration

A tenant can independently enable modules such as CRM, Marketing, Autonomous Acquisition, Campaigns, Automation, Golden Pipeline, and Analytics. It can independently enable portals such as a public product portal, distributor portal, customer portal, or partner portal.

The UI and API must enforce these entitlements consistently. A disabled module or portal must not merely be hidden in navigation; its protected API capabilities must also reject unauthorized access.

## Renova

Renova is the reference tenant used to validate this architecture. Its dedicated business database is `RenovaPromotions`. The public Renova portal is exposed at `/renova`, while the root application remains the main QualifyAI landing page.
