using MediatR;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure;

public sealed class CrmLifecycleCommandHandlers(
    ICrmRepository crm,
    IBusinessUnitOfWork unitOfWork) :
    IRequestHandler<CreateCompanyCommand, Company>,
    IRequestHandler<UpdateContactCommand, Contact?>,
    IRequestHandler<UpdateOpportunityCommand, Opportunity?>,
    IRequestHandler<MoveOpportunityStageCommand, Opportunity?>,
    IRequestHandler<CloseOpportunityCommand, Opportunity?>,
    IRequestHandler<ReopenOpportunityCommand, Opportunity?>
{
    public async Task<Company> Handle(CreateCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = Company.Create(
            command.TenantId,
            command.Company.Name,
            command.Company.Domain,
            command.Company.Industry,
            command.Company.Employees,
            command.Company.Country,
            command.Company.AnnualRevenue);

        crm.AddCompany(company);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return company;
    }

    public async Task<Contact?> Handle(UpdateContactCommand command, CancellationToken cancellationToken)
    {
        var contact = await crm.GetContactAsync(command.TenantId, command.Id, cancellationToken);
        if (contact is null) return null;

        contact.UpdateProfile(
            command.Contact.CompanyId,
            command.Contact.FirstName,
            command.Contact.LastName,
            command.Contact.Email,
            command.Contact.Phone,
            command.Contact.LifecycleStage);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return contact;
    }

    public async Task<Opportunity?> Handle(UpdateOpportunityCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await crm.GetOpportunityAsync(command.TenantId, command.Id, cancellationToken);
        if (opportunity is null) return null;

        opportunity.UpdateDetails(command.Opportunity.Name, command.Opportunity.Amount, command.Opportunity.ExpectedCloseUtc);

        if (command.Opportunity.PipelineStageId.HasValue && command.Opportunity.PipelineStageId != opportunity.PipelineStageId)
            await MoveStageAsync(opportunity, command.TenantId, command.Opportunity.PipelineStageId.Value, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return opportunity;
    }

    public async Task<Opportunity?> Handle(MoveOpportunityStageCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await crm.GetOpportunityAsync(command.TenantId, command.Id, cancellationToken);
        if (opportunity is null) return null;

        await MoveStageAsync(opportunity, command.TenantId, command.StageId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return opportunity;
    }

    public async Task<Opportunity?> Handle(CloseOpportunityCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await crm.GetOpportunityAsync(command.TenantId, command.Id, cancellationToken);
        if (opportunity is null) return null;

        if (command.Won)
            opportunity.MarkWon();
        else
            opportunity.MarkLost(command.LossReason ?? string.Empty);

        crm.AddActivity(CrmActivity.ForOpportunity(
            opportunity,
            command.Won ? "Opportunity won" : "Opportunity lost",
            command.Won ? "won" : opportunity.LossReason));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return opportunity;
    }

    public async Task<Opportunity?> Handle(ReopenOpportunityCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await crm.GetOpportunityAsync(command.TenantId, command.Id, cancellationToken);
        if (opportunity is null) return null;

        opportunity.Reopen();
        crm.AddActivity(CrmActivity.ForOpportunity(opportunity, "Opportunity reopened", "open"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return opportunity;
    }

    private async Task MoveStageAsync(Opportunity opportunity, Guid tenantId, Guid stageId, CancellationToken cancellationToken)
    {
        var stage = await crm.GetPipelineStageAsync(tenantId, stageId, cancellationToken)
            ?? throw new InvalidOperationException("Invalid pipeline stage.");

        opportunity.MoveToStage(stage.Id);
        crm.AddActivity(CrmActivity.ForOpportunity(opportunity, "Opportunity moved", stage.Name));
    }
}
