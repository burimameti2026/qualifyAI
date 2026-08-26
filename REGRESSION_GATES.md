# Regression gates

These failures are no longer acceptable as runtime discoveries:

- UI auth route exists but Identity endpoint is missing
- tenant is accepted from an arbitrary request body when it should come from the token
- user password is stored outside ASP.NET Core Identity hashing
- roles/permissions are static UI-only values
- Identity reads TenantManagement DbContext
- microservice directly reads another service's DbContext
- AI tool returns `accepted=true` without invoking the owning service
- duplicated anonymous `Id` properties in generated response objects
- Angular config missing outputPath/development configuration/styles
- TypeScript uses unsupported lib APIs without compiler target support

Before a feature is marked DONE:
1. compile;
2. route exists;
3. auth contract exists;
4. tenant boundary is enforced;
5. DB round-trip works;
6. event contract/consumer exists where required;
7. UI uses the Gateway route;
8. integration test is present.
