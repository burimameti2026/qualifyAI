using QualifyAI.Domain;

namespace QualifyAI.Application.Abstractions.Persistence;

public interface ICrmRepository
{
    Task<bool> ContactExistsAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default);
    Task<Contact?> GetContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> ListContactsAsync(Guid tenantId, int take = 500, CancellationToken cancellationToken = default);
    void AddContact(Contact contact);

    Task<Lead?> GetLeadAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Lead>> ListLeadsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    void AddLead(Lead lead);

    Task<IReadOnlyList<Company>> ListCompaniesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Opportunity>> ListOpportunitiesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<int> CountContactsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<int> CountLeadsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<int> CountHotLeadsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<decimal> SumOpenPipelineAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
