using MediatR;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure;

public sealed class CrmLifecycleCommandHandlers(AppDbContext db) :
    IRequestHandler<CreateCompanyCommand, Company>,
    IRequestHandler<UpdateContactCommand, Contact?>,
    IRequestHandler<UpdateOpportunityCommand, Opportunity?>,
    IRequestHandler<MoveOpportunityStageCommand, Opportunity?>,
    IRequestHandler<CloseOpportunityCommand, Opportunity?>,
    IRequestHandler<ReopenOpportunityCommand, Opportunity?>
{
    public async Task<Company> Handle(CreateCompanyCommand c, CancellationToken ct)
    {
        var x=new Company{Id=Guid.NewGuid(),TenantId=c.TenantId};
        x.UpdateProfile(c.Company.Name,c.Company.Domain,c.Company.Industry,c.Company.Employees,c.Company.Country,c.Company.AnnualRevenue);
        db.Companys.Add(x); await db.SaveChangesAsync(ct); return x;
    }

    public async Task<Contact?> Handle(UpdateContactCommand c,CancellationToken ct)
    {
        var x=await db.Contacts.FirstOrDefaultAsync(v=>v.Id==c.Id&&v.TenantId==c.TenantId,ct); if(x is null)return null;
        x.UpdateProfile(c.Contact.CompanyId,c.Contact.FirstName,c.Contact.LastName,c.Contact.Email,c.Contact.Phone,c.Contact.LifecycleStage);
        await db.SaveChangesAsync(ct); return x;
    }

    public async Task<Opportunity?> Handle(UpdateOpportunityCommand c,CancellationToken ct)
    {
        var x=await db.Opportunitys.FirstOrDefaultAsync(v=>v.Id==c.Id&&v.TenantId==c.TenantId,ct); if(x is null)return null;
        x.UpdateDetails(c.Opportunity.Name,c.Opportunity.Amount,c.Opportunity.ExpectedCloseUtc);
        if(c.Opportunity.PipelineStageId.HasValue&&c.Opportunity.PipelineStageId!=x.PipelineStageId)
            await MoveStage(x,c.TenantId,c.Opportunity.PipelineStageId.Value,ct);
        await db.SaveChangesAsync(ct); return x;
    }

    public async Task<Opportunity?> Handle(MoveOpportunityStageCommand c,CancellationToken ct)
    {
        var x=await db.Opportunitys.FirstOrDefaultAsync(v=>v.Id==c.Id&&v.TenantId==c.TenantId,ct); if(x is null)return null;
        await MoveStage(x,c.TenantId,c.StageId,ct); await db.SaveChangesAsync(ct); return x;
    }

    public async Task<Opportunity?> Handle(CloseOpportunityCommand c,CancellationToken ct)
    {
        var x=await db.Opportunitys.FirstOrDefaultAsync(v=>v.Id==c.Id&&v.TenantId==c.TenantId,ct); if(x is null)return null;
        if(c.Won)x.MarkWon(); else x.MarkLost(c.LossReason??"");
        db.CrmActivitys.Add(Activity(x,c.TenantId,c.Won?"Opportunity won":"Opportunity lost",c.Won?"won":c.LossReason??"lost"));
        await db.SaveChangesAsync(ct); return x;
    }

    public async Task<Opportunity?> Handle(ReopenOpportunityCommand c,CancellationToken ct)
    {
        var x=await db.Opportunitys.FirstOrDefaultAsync(v=>v.Id==c.Id&&v.TenantId==c.TenantId,ct); if(x is null)return null;
        x.Reopen(); db.CrmActivitys.Add(Activity(x,c.TenantId,"Opportunity reopened","open")); await db.SaveChangesAsync(ct); return x;
    }

    private async Task MoveStage(Opportunity x,Guid tenantId,Guid stageId,CancellationToken ct)
    {
        var stage=await db.PipelineStages.AsNoTracking().FirstOrDefaultAsync(v=>v.Id==stageId&&v.TenantId==tenantId,ct)
            ?? throw new InvalidOperationException("Invalid pipeline stage.");
        x.MoveToStage(stage.Id);
        db.CrmActivitys.Add(Activity(x,tenantId,"Opportunity moved",stage.Name));
    }

    private static CrmActivity Activity(Opportunity x,Guid tenantId,string subject,string body)=>new()
    { TenantId=tenantId,LeadId=x.LeadId,CompanyId=x.CompanyId,ContactId=x.ContactId,Type="pipeline",Subject=subject,Body=body };
}
