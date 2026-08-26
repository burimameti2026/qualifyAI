using Microsoft.EntityFrameworkCore;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Domain.Licensing;

namespace QualifyAI.Identity.Infrastructure.Persistence.Repositories;

public sealed class LicenseRepository(IdentityDbContext dbContext) : ILicenseRepository
{
    public Task<License?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.Licenses
            .Include(x => x.Modules)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

    public Task AddAsync(License license, CancellationToken cancellationToken = default)
        => dbContext.Licenses.AddAsync(license, cancellationToken).AsTask();
}
