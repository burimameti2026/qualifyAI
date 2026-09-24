using LeadsAI.Identity.Domain.Licensing;

namespace LeadsAI.Identity.Application.Abstractions.Persistence;

public interface ILicenseRepository
{
    Task<License?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(License license, CancellationToken cancellationToken = default);
}
