using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Persistence.Repositories;

public sealed class CrmRepository(AppDbContext dbContext) : ICrmRepository
{
    public Task<bool> ContactExistsAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default)
        => dbContext.Contacts.AnyAsync(x => x.TenantId == tenantId && x.Id == contactId, cancellationToken);

    public Task<Contact?> GetContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default)
        => dbContext.Contacts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == contactId, cancellationToken);

    public async Task<IReadOnlyList<Contact>> ListContactsAsync(Guid tenantId, int take = 500, CancellationToken cancellationToken = default)
        => await dbContext.Contacts.AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public void AddContact(Contact contact) => dbContext.Contacts.Add(contact);

    public Task<Lead?> GetLeadAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default)
        => dbContext.Leads.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == leadId, cancellationToken);

    public async Task<IReadOnlyList<Lead>> ListLeadsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await dbContext.Leads.AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.Score).ToListAsync(cancellationToken);

    public void AddLead(Lead lead) => dbContext.Leads.Add(lead);

    public async Task<IReadOnlyList<Company>> ListCompaniesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await dbContext.Companys.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public void AddCompany(Company company) => dbContext.Companys.Add(company);

    public Task<Opportunity?> GetOpportunityAsync(Guid tenantId, Guid opportunityId, CancellationToken cancellationToken = default)
        => dbContext.Opportunitys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == opportunityId, cancellationToken);

    public async Task<IReadOnlyList<Opportunity>> ListOpportunitiesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await dbContext.Opportunitys.AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task<PipelineStage?> GetPipelineStageAsync(Guid tenantId, Guid stageId, CancellationToken cancellationToken = default)
        => dbContext.PipelineStages.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == stageId, cancellationToken);

    public void AddActivity(CrmActivity activity) => dbContext.CrmActivitys.Add(activity);

    public Task<int> CountContactsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.Contacts.CountAsync(x => x.TenantId == tenantId, cancellationToken);

    public Task<int> CountLeadsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.Leads.CountAsync(x => x.TenantId == tenantId, cancellationToken);

    public Task<int> CountHotLeadsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.Leads.CountAsync(x => x.TenantId == tenantId && x.Score >= 80, cancellationToken);

    public async Task<decimal> SumOpenPipelineAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await dbContext.Opportunitys.Where(x => x.TenantId == tenantId && x.Status == OpportunityStatus.Open).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
}
