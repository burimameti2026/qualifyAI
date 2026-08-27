using MediatR;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Application.Commands;
using QualifyAI.Application.Queries;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure;

public sealed class CreateContactCommandHandler(ICrmRepository crm, IBusinessUnitOfWork unitOfWork) : IRequestHandler<CreateContactCommand, Contact>
{
    public async Task<Contact> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        var entity = new Contact { Id=Guid.NewGuid(), TenantId=request.TenantId };
        entity.UpdateProfile(request.CompanyId, request.FirstName, request.LastName, request.Email, request.Phone, request.LifecycleStage);
        crm.AddContact(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }
}

public sealed class CreateLeadCommandHandler(ICrmRepository crm, IBusinessUnitOfWork unitOfWork) : IRequestHandler<CreateLeadCommand, Lead>
{
    public async Task<Lead> Handle(CreateLeadCommand request, CancellationToken cancellationToken)
    {
        if (!await crm.ContactExistsAsync(request.TenantId, request.ContactId, cancellationToken))
            throw new InvalidOperationException("Lead contact does not exist in this tenant.");
        var entity = new Lead
        {
            Id=Guid.NewGuid(), TenantId=request.TenantId, ContactId=request.ContactId, CompanyId=request.CompanyId,
            Source=string.IsNullOrWhiteSpace(request.Source)?"web":request.Source.Trim()
        };
        entity.SetScore(request.Score, request.IntentSummary);
        entity.SetEstimatedValue(request.EstimatedValue);
        crm.AddLead(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }
}

public sealed class QualifyLeadCommandHandler(ICrmRepository crm, IBusinessUnitOfWork unitOfWork) : IRequestHandler<QualifyLeadCommand, Lead?>
{
    public async Task<Lead?> Handle(QualifyLeadCommand request, CancellationToken cancellationToken)
    {
        var lead=await crm.GetLeadAsync(request.TenantId,request.LeadId,cancellationToken);
        if(lead is null)return null;
        lead.Qualify();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return lead;
    }
}

public sealed class CreateTicketCommandHandler(ISupportRepository support, IBusinessUnitOfWork unitOfWork) : IRequestHandler<CreateTicketCommand, Ticket>
{
    public async Task<Ticket> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        var entity=new Ticket { Id=Guid.NewGuid(),TenantId=request.TenantId,ConversationId=request.ConversationId,ContactId=request.ContactId,Number=$"T-{DateTime.UtcNow:yyyyMMddHHmmssfff}",Subject=request.Subject?.Trim()??"",Description=request.Description?.Trim()??"",Priority=request.Priority,SlaPolicyId=request.SlaPolicyId };
        support.AddTicket(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }
}

public sealed class DashboardOverviewQueryHandler(ICrmRepository crm, ISupportRepository support) : IRequestHandler<DashboardOverviewQuery, DashboardOverviewDto>
{
    public async Task<DashboardOverviewDto> Handle(DashboardOverviewQuery request,CancellationToken cancellationToken)
    {
        var tenantId=request.TenantId;
        return new(await crm.CountContactsAsync(tenantId,cancellationToken),await crm.CountLeadsAsync(tenantId,cancellationToken),await crm.CountHotLeadsAsync(tenantId,cancellationToken),await support.CountOpenConversationsAsync(tenantId,cancellationToken),await support.CountOpenTicketsAsync(tenantId,cancellationToken),await crm.SumOpenPipelineAsync(tenantId,cancellationToken));
    }
}
