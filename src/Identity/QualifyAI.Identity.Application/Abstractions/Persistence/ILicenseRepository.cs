using QualifyAI.Identity.Domain.Licensing;

namespace QualifyAI.Identity.Application.Abstractions.Persistence;

public interface ILicenseRepository
{
    Task<License?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(License license, CancellationToken cancellationToken = default);
}
