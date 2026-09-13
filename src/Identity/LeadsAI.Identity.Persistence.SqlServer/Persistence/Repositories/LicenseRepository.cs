using Microsoft.EntityFrameworkCore;
using LeadsAI.Identity.Application.Abstractions.Persistence;
using LeadsAI.Identity.Domain.Licensing;

namespace LeadsAI.Identity.Persistence.SqlServer.Repositories;

public sealed class LicenseRepository(IdentityDbContext dbContext) : ILicenseRepository
{
    public Task<License?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.Licenses
            .Include(x => x.Modules)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

    public Task AddAsync(License license, CancellationToken cancellationToken = default)
        => dbContext.Licenses.AddAsync(license, cancellationToken).AsTask();
}
