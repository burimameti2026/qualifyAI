using MediatR;

namespace QualifyAI.Application.Queries;

public sealed record DashboardOverviewQuery(Guid TenantId) : IRequest<DashboardOverviewDto>;

public sealed record DashboardOverviewDto(
    int Contacts,
    int Leads,
    int HotLeads,
    int OpenConversations,
    int OpenTickets,
    decimal Pipeline);
