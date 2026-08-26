using MediatR;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Commands;
using QualifyAI.Application.Queries;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure;

public sealed class CreateContactCommandHandler(AppDbContext db)
    : IRequestHandler<CreateContactCommand, Contact>
{
    public async Task<Contact> Handle(CreateContactCommand r, CancellationToken ct)
    {
        var entity = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = r.TenantId,
            CompanyId = r.CompanyId,
            FirstName = r.FirstName?.Trim() ?? "",
            LastName = r.LastName?.Trim() ?? "",
            Email = r.Email?.Trim() ?? "",
            Phone = r.Phone?.Trim() ?? "",
            LifecycleStage = string.IsNullOrWhiteSpace(r.LifecycleStage) ? "visitor" : r.LifecycleStage.Trim()
        };
        db.Contacts.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}

public sealed class CreateLeadCommandHandler(AppDbContext db)
    : IRequestHandler<CreateLeadCommand, Lead>
{
    public async Task<Lead> Handle(CreateLeadCommand r, CancellationToken ct)
    {
        var contactExists = await db.Contacts.AnyAsync(x => x.TenantId == r.TenantId && x.Id == r.ContactId, ct);
        if (!contactExists) throw new InvalidOperationException("Lead contact does not exist in this tenant.");

        var score = Math.Clamp(r.Score, 0, 100);
        var entity = new Lead
        {
            Id = Guid.NewGuid(),
            TenantId = r.TenantId,
            ContactId = r.ContactId,
            CompanyId = r.CompanyId,
            Source = string.IsNullOrWhiteSpace(r.Source) ? "web" : r.Source.Trim(),
            Score = score,
            Temperature = score >= 80 ? LeadTemperature.Hot : score >= 50 ? LeadTemperature.Warm : LeadTemperature.Cold,
            Status = "new",
            EstimatedValue = r.EstimatedValue,
            IntentSummary = r.IntentSummary?.Trim() ?? ""
        };
        db.Leads.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}

public sealed class QualifyLeadCommandHandler(AppDbContext db)
    : IRequestHandler<QualifyLeadCommand, Lead?>
{
    public async Task<Lead?> Handle(QualifyLeadCommand r, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.TenantId == r.TenantId && x.Id == r.LeadId, ct);
        if (lead is null) return null;
        lead.Temperature = lead.Score >= 80 ? LeadTemperature.Hot : lead.Score >= 50 ? LeadTemperature.Warm : LeadTemperature.Cold;
        lead.Status = lead.Score >= 80 ? "qualified" : lead.Score >= 50 ? "nurture" : "new";
        await db.SaveChangesAsync(ct);
        return lead;
    }
}

public sealed class CreateTicketCommandHandler(AppDbContext db)
    : IRequestHandler<CreateTicketCommand, Ticket>
{
    public async Task<Ticket> Handle(CreateTicketCommand r, CancellationToken ct)
    {
        var entity = new Ticket
        {
            Id = Guid.NewGuid(),
            TenantId = r.TenantId,
            ConversationId = r.ConversationId,
            ContactId = r.ContactId,
            Number = $"T-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Subject = r.Subject?.Trim() ?? "",
            Description = r.Description?.Trim() ?? "",
            Priority = r.Priority,
            SlaPolicyId = r.SlaPolicyId
        };
        db.Tickets.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}

public sealed class DashboardOverviewQueryHandler(AppDbContext db)
    : IRequestHandler<DashboardOverviewQuery, DashboardOverviewDto>
{
    public async Task<DashboardOverviewDto> Handle(DashboardOverviewQuery r, CancellationToken ct)
    {
        var t = r.TenantId;
        return new DashboardOverviewDto(
            await db.Contacts.CountAsync(x => x.TenantId == t, ct),
            await db.Leads.CountAsync(x => x.TenantId == t, ct),
            await db.Leads.CountAsync(x => x.TenantId == t && x.Score >= 80, ct),
            await db.Conversations.CountAsync(x => x.TenantId == t && x.Status == ConversationStatus.Open, ct),
            await db.Tickets.CountAsync(x => x.TenantId == t && x.Status != TicketStatus.Closed && x.Status != TicketStatus.Resolved, ct),
            await db.Opportunitys.Where(x => x.TenantId == t && x.Status == OpportunityStatus.Open).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m);
    }
}
