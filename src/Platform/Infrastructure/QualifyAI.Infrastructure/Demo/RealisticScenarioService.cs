using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Demo;

public sealed record ScenarioInstallResult(string Scenario, int Prospects, int Campaigns, int Opportunities, int Meetings, int Tickets, int Automations);
public sealed record ScenarioResetResult(int DeletedProspects, int DeletedLists, int DeletedAgents, int DeletedContacts, int DeletedLeads, int DeletedOpportunities, int DeletedPipelines, int DeletedMeetings, int DeletedAutomations);
public sealed record ResetAndInstallResult(ScenarioResetResult Reset, ScenarioInstallResult Scenario);

public sealed class RealisticScenarioService(AppDbContext db)
{
    public async Task<ResetAndInstallResult> ResetAndInstallAsync(Guid tenantId, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var reset = await ResetBusinessDataAsync(tenantId, ct);
            var scenario = await InstallAsync(tenantId, ct);
            await transaction.CommitAsync(ct);
            return new ResetAndInstallResult(reset, scenario);
        });
    }

    public async Task<ScenarioResetResult> ResetBusinessDataAsync(Guid tenantId, CancellationToken ct = default)
    {
        await DeleteAsync(db.ProspectReplies, tenantId, ct);
        await DeleteAsync(db.OutreachMessages, tenantId, ct);
        await DeleteAsync(db.CampaignRecipients, tenantId, ct);
        await DeleteAsync(db.CampaignSteps, tenantId, ct);
        await DeleteAsync(db.Campaigns, tenantId, ct);
        await DeleteAsync(db.TargetListMembers, tenantId, ct);
        var deletedLists = await DeleteAsync(db.TargetLists, tenantId, ct);
        await DeleteAsync(db.ProspectSignals, tenantId, ct);
        var deletedProspects = await DeleteAsync(db.Prospects, tenantId, ct);
        await DeleteAsync(db.IcpProfiles, tenantId, ct);

        await DeleteAsync(db.RevenueAttributions, tenantId, ct);
        await DeleteAsync(db.LeadScoreExplanations, tenantId, ct);
        await DeleteAsync(db.QualificationAnswers, tenantId, ct);
        await DeleteAsync(db.CrmTasks, tenantId, ct);
        await DeleteAsync(db.CrmActivitys, tenantId, ct);
        var deletedOpportunities = await DeleteAsync(db.Opportunitys, tenantId, ct);
        var deletedLeads = await DeleteAsync(db.Leads, tenantId, ct);
        var deletedContacts = await DeleteAsync(db.Contacts, tenantId, ct);
        await DeleteAsync(db.Companys, tenantId, ct);
        var deletedMeetings = await DeleteAsync(db.MeetingBookings, tenantId, ct);
        await DeleteAsync(db.MeetingTypes, tenantId, ct);
        var deletedPipelines = await DeleteAsync(db.PipelineStages, tenantId, ct);
        deletedPipelines += await DeleteAsync(db.Pipelines, tenantId, ct);

        await DeleteAsync(db.AiToolExecutions, tenantId, ct);
        await DeleteAsync(db.AiAgentVersions, tenantId, ct);
        var deletedAgents = await DeleteAsync(db.AiAgents, tenantId, ct);
        await DeleteAsync(db.AiToolDefinitions, tenantId, ct);
        await DeleteAsync(db.PromptVersions, tenantId, ct);
        await DeleteAsync(db.WorkflowEdges, tenantId, ct);
        await DeleteAsync(db.WorkflowNodes, tenantId, ct);
        await DeleteAsync(db.QualificationFlows, tenantId, ct);
        var deletedAutomations = await DeleteAsync(db.AutomationRuns, tenantId, ct);
        deletedAutomations += await DeleteAsync(db.AutomationRules, tenantId, ct);

        await DeleteAsync(db.TicketEvents, tenantId, ct);
        await DeleteAsync(db.CsatResponses, tenantId, ct);
        await DeleteAsync(db.ConversationNotes, tenantId, ct);
        await DeleteAsync(db.Messages, tenantId, ct);
        await DeleteAsync(db.Conversations, tenantId, ct);
        await DeleteAsync(db.Channels, tenantId, ct);
        await DeleteAsync(db.Tickets, tenantId, ct);
        await DeleteAsync(db.KnowledgeGaps, tenantId, ct);
        await DeleteAsync(db.MetricSnapshots, tenantId, ct);
        await DeleteAsync(db.Notifications, tenantId, ct);

        return new ScenarioResetResult(deletedProspects, deletedLists, deletedAgents, deletedContacts, deletedLeads, deletedOpportunities, deletedPipelines, deletedMeetings, deletedAutomations);
    }

    private static Task<int> DeleteAsync<TEntity>(DbSet<TEntity> set, Guid tenantId, CancellationToken ct) where TEntity : TenantEntity
        => set.Where(x => x.TenantId == tenantId).ExecuteDeleteAsync(ct);

    public async Task<ScenarioInstallResult> InstallAsync(Guid tenantId, CancellationToken ct = default)
    {
        // A presentation scenario must never attach campaigns or CRM records to real
        // imported prospects. Use ResetAndInstallAsync when replacing a workspace.
        var hasPresentationProspects = await db.Prospects.AnyAsync(x => x.TenantId == tenantId && x.Source == "presentation-scenario", ct);
        var hasRealProspects = await db.Prospects.AnyAsync(x => x.TenantId == tenantId && x.Source != "presentation-scenario", ct);
        var hasOtherBusinessData = !hasPresentationProspects &&
            (await db.Companys.AnyAsync(x => x.TenantId == tenantId, ct) ||
             await db.Contacts.AnyAsync(x => x.TenantId == tenantId, ct) ||
             await db.Leads.AnyAsync(x => x.TenantId == tenantId, ct) ||
             await db.Opportunitys.AnyAsync(x => x.TenantId == tenantId, ct) ||
             await db.Campaigns.AnyAsync(x => x.TenantId == tenantId, ct) ||
             await db.MeetingBookings.AnyAsync(x => x.TenantId == tenantId, ct) ||
             await db.Tickets.AnyAsync(x => x.TenantId == tenantId, ct));
        if (hasRealProspects || hasOtherBusinessData)
            throw new InvalidOperationException("This workspace contains business data. Use Prepare real workspace for imports, or explicitly reset business data before loading a presentation demo.");

        var now = DateTime.UtcNow;
        const string demoIcpName = "[PRESENTATION] FusionFleet — EU logistics growth";
        var icp = await db.IcpProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId &&
            (x.Name == demoIcpName || x.Name == "DACH & Southern Europe logistics growth"), ct);
        if (icp is null)
        {
            icp = new IcpProfile
            {
                TenantId = tenantId, Name = demoIcpName,
                Industry = "Logistics, Distribution, Manufacturing, E-commerce",
                CountriesCsv = "Germany, France, Italy", MinimumEmployees = 20, MaximumEmployees = 1000,
                IntentKeywordsCsv = "freight tender, warehouse expansion, fleet growth, delivery delays",
                CriteriaJson = "{\"requiredSignals\":1,\"minimumFitScore\":70}", Active = true, LastDiscoveryAtUtc = now
            };
            db.IcpProfiles.Add(icp);
        }
        else
        {
            icp.Name = demoIcpName;
        }

        var prospectSeeds = new[]
        {
            new ProspectSeed("NordCargo Solutions", "nordcargo.example", "Lukas Meyer", "Head of Operations", "lukas.meyer@nordcargo.example", "Logistics", "Germany", 92, 81, "Published a freight technology tender after expanding its cross-border fleet."),
            new ProspectSeed("RheinFulfil GmbH", "rheinfulfil.example", "Anna Fischer", "VP Supply Chain", "anna.fischer@rheinfulfil.example", "E-commerce", "Germany", 89, 74, "Hiring warehouse systems specialists for a new fulfilment location."),
            new ProspectSeed("Atlas Components", "atlascomponents.example", "Sofia Romano", "Logistics Director", "sofia.romano@atlascomponents.example", "Manufacturing", "Italy", 91, 69, "Announced distribution expansion into two additional regions."),
            new ProspectSeed("BlueLine 3PL", "blueline3pl.example", "Marc Dubois", "Managing Director", "marc.dubois@blueline3pl.example", "Logistics", "France", 88, 84, "Customer reviews indicate recurring shipment visibility and delivery-delay issues."),
            new ProspectSeed("Milano Distribution", "milanodistribution.example", "Giulia Conti", "Warehouse Director", "giulia.conti@milanodistribution.example", "Distribution", "Italy", 86, 77, "Opened a second warehouse and increased transport coordinator hiring.")
        };

        var prospects = await db.Prospects.Where(x => x.TenantId == tenantId && x.Source == "presentation-scenario")
            .OrderByDescending(x => x.FitScore).ThenByDescending(x => x.IntentScore).Take(5).ToListAsync(ct);

        if (prospects.Count == 0)
        {
            foreach (var seed in prospectSeeds)
            {
                var prospect = new Prospect
                {
                    TenantId = tenantId, CompanyName = seed.Company, Domain = seed.Domain,
                    ContactName = seed.Contact, JobTitle = seed.JobTitle, Email = seed.Email,
                    Industry = seed.Industry, Country = seed.Country, Source = "presentation-scenario", Priority = "A", ContactReadiness = "Demo ready", SuggestedBuyer = seed.JobTitle, PainHypothesis = seed.Evidence, Offer = "FusionFleet operational platform pilot", SourceUrl = $"https://signals.example/{seed.Domain}", VerificationStatus = "Presentation scenario", OutreachStatus = "Ready for approval", DatasetOrigin = "FusionFleet presentation scenario"
                };
                prospect.Evaluate(seed.Fit, seed.Intent);
                db.Prospects.Add(prospect);
                prospects.Add(prospect);
                db.ProspectSignals.Add(new ProspectSignal { TenantId = tenantId, ProspectId = prospect.Id, Type = "presentation-buying-intent", Source = "presentation-scenario", Evidence = seed.Evidence, Score = seed.Intent, SourceUrl = prospect.SourceUrl });
            }
        }

        const string demoTargetListName = "[PRESENTATION] FusionFleet — EU logistics accounts";
        var targetList = await db.TargetLists.FirstOrDefaultAsync(x => x.TenantId == tenantId &&
            (x.Name == demoTargetListName || x.Name == "European logistics accounts – high intent"), ct);
        if (targetList is null)
        {
            targetList = new TargetList { TenantId = tenantId, IcpProfileId = icp.Id, Name = demoTargetListName, Description = "FusionFleet presentation accounts. Replace with verified imported companies before live outreach." };
            db.TargetLists.Add(targetList);
        }
        else
        {
            targetList.Name = demoTargetListName;
            targetList.Description = "FusionFleet presentation accounts. Replace with verified imported companies before live outreach.";
        }
        foreach (var prospect in prospects)
            if (!await db.TargetListMembers.AnyAsync(x => x.TenantId == tenantId && x.TargetListId == targetList.Id && x.ProspectId == prospect.Id, ct))
                db.TargetListMembers.Add(new TargetListMember { TenantId = tenantId, TargetListId = targetList.Id, ProspectId = prospect.Id });

        const string demoCampaignName = "[PRESENTATION] FusionFleet — book operational demo";
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId &&
            (x.Name == demoCampaignName || x.Name == "Logistics growth – book operational demo"), ct);
        if (campaign is null)
        {
            campaign = new Campaign { TenantId = tenantId, TargetListId = targetList.Id, Name = demoCampaignName, Goal = "book-demo", Status = CampaignStatus.Running, SenderName = "FusionFleet Growth", SenderEmail = "growth@fusionfleet.example", StartsAtUtc = now.AddDays(-5) };
            db.Campaigns.Add(campaign);
            db.CampaignSteps.AddRange(
                new CampaignStep { TenantId = tenantId, CampaignId = campaign.Id, StepNumber = 1, DelayHours = 0, Channel = "email", SubjectTemplate = "{{company}}: reduce dispatch and delivery exceptions", BodyTemplate = "Hi {{contact}}, I noticed current growth signals at {{company}}. We help {{industry}} teams automate dispatch, warehouse and customer operations. Would a 25-minute operational demo be useful?" },
                new CampaignStep { TenantId = tenantId, CampaignId = campaign.Id, StepNumber = 2, DelayHours = 72, Channel = "email", SubjectTemplate = "Operational benchmark for {{company}}", BodyTemplate = "Hi {{contact}}, I prepared a short benchmark for teams operating across {{country}}. I can tailor the demo to your fleet, warehouse and delivery workflow." });
        }
        else
        {
            campaign.Name = demoCampaignName;
            campaign.SenderName = "FusionFleet Growth";
        }
        foreach (var prospect in prospects)
            if (!await db.CampaignRecipients.AnyAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id, ct))
                db.CampaignRecipients.Add(new CampaignRecipient { TenantId = tenantId, CampaignId = campaign.Id, ProspectId = prospect.Id, Status = "active", NextRunAtUtc = now });

        var firstStep = db.CampaignSteps.Local.FirstOrDefault(x => x.CampaignId == campaign.Id && x.StepNumber == 1)
            ?? await db.CampaignSteps.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.StepNumber == 1, ct);
        if (firstStep is not null)
            foreach (var prospect in prospects)
                if (!await db.OutreachMessages.AnyAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id && x.CampaignStepId == firstStep.Id, ct))
                    db.OutreachMessages.Add(new OutreachMessage
                    {
                        TenantId = tenantId, CampaignId = campaign.Id, ProspectId = prospect.Id, CampaignStepId = firstStep.Id,
                        Channel = "email", Subject = $"{prospect.CompanyName}: reduce dispatch and delivery exceptions",
                        Body = $"Hi {prospect.ContactName}, I noticed current growth signals at {prospect.CompanyName}. Would a focused operational demo be useful?",
                        Status = prospect == prospects[0] ? OutreachStatus.Delivered : OutreachStatus.Queued,
                        ProviderMessageId = prospect == prospects[0] ? "demo-msg-nordcargo" : "",
                        SentAtUtc = prospect == prospects[0] ? now.AddDays(-2) : null
                    });

        var fusionFleetPipeline = await EnsurePipelineAsync(tenantId, "FusionFleet revenue pipeline", true, ct);
        var qualifyAiPipeline = await EnsurePipelineAsync(tenantId, "QualifyAI revenue pipeline", false, ct);
        var contactReadyProspect = prospects.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Email)) ?? prospects[0];
        await EnsureConvertedJourneyAsync(tenantId, contactReadyProspect, campaign, fusionFleetPipeline, now, ct);
        await EnsureQualifyAiScenarioAsync(tenantId, qualifyAiPipeline, now, ct);
        await EnsureSupportScenarioAsync(tenantId, now, ct);
        await EnsureAutomationsAsync(tenantId, icp.Id, now, ct);
        await EnsureAgentsAsync(tenantId, ct);
        await db.SaveChangesAsync(ct);

        return new ScenarioInstallResult(
            "FusionFleet and QualifyAI customer acquisition + controlled support operations",
            await db.Prospects.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Campaigns.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Opportunitys.CountAsync(x => x.TenantId == tenantId, ct),
            await db.MeetingBookings.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Tickets.CountAsync(x => x.TenantId == tenantId, ct),
            await db.AutomationRules.CountAsync(x => x.TenantId == tenantId, ct));
    }

    private async Task<PipelineStage> EnsurePipelineAsync(Guid tenantId, string name, bool isDefault, CancellationToken ct)
    {
        var pipeline = await db.Pipelines.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == name, ct);
        if (pipeline is null)
        {
            pipeline = new Pipeline { TenantId = tenantId, Name = name, IsDefault = isDefault };
            db.Pipelines.Add(pipeline);
            db.PipelineStages.AddRange(
                new PipelineStage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Qualified", SortOrder = 1, Probability = 30 },
                new PipelineStage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Discovery demo", SortOrder = 2, Probability = 55 },
                new PipelineStage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Proposal", SortOrder = 3, Probability = 75 },
                new PipelineStage { TenantId = tenantId, PipelineId = pipeline.Id, Name = "Won", SortOrder = 4, Probability = 100 });
        }
        var stage = db.PipelineStages.Local.FirstOrDefault(x => x.TenantId == tenantId && x.PipelineId == pipeline.Id && x.Name == "Discovery demo")
            ?? await db.PipelineStages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PipelineId == pipeline.Id && x.Name == "Discovery demo", ct);
        return stage ?? throw new InvalidOperationException("The presentation pipeline stage could not be created.");
    }

    private async Task EnsureConvertedJourneyAsync(Guid tenantId, Prospect prospect, Campaign campaign, PipelineStage stage, DateTime now, CancellationToken ct)
    {
        var company = await db.Companys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Domain == prospect.Domain, ct);
        if (company is null) { company = Company.Create(tenantId, prospect.CompanyName, prospect.Domain, prospect.Industry, 86, prospect.Country, 18_400_000m); db.Companys.Add(company); }
        var contact = await db.Contacts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email == prospect.Email, ct);
        if (contact is null) { var names = prospect.ContactName.Split(' ', 2); contact = Contact.Create(tenantId, company.Id, names[0], names.Length > 1 ? names[1] : "", prospect.Email, "+49 30 555 0142", "sql"); db.Contacts.Add(contact); }
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ContactId == contact.Id, ct);
        if (lead is null) { lead = Lead.Create(tenantId, contact.Id, company.Id, "outbound-campaign", 94, 48_000m, "Fleet expansion and freight technology tender; interested reply received."); lead.Qualify(); db.Leads.Add(lead); }
        var opportunity = await db.Opportunitys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id, ct);
        if (opportunity is null) { opportunity = new Opportunity { TenantId = tenantId, LeadId = lead.Id, CompanyId = company.Id, ContactId = contact.Id, PipelineStageId = stage.Id, Name = "NordCargo operational platform rollout", Amount = 48_000m, Status = OpportunityStatus.Open, ExpectedCloseUtc = now.AddDays(28) }; db.Opportunitys.Add(opportunity); }
        if (!await db.RevenueAttributions.AnyAsync(x => x.TenantId == tenantId && x.OpportunityId == opportunity.Id, ct))
            db.RevenueAttributions.Add(new RevenueAttribution { TenantId = tenantId, LeadId = lead.Id, OpportunityId = opportunity.Id, InfluencedRevenue = opportunity.Amount, Model = "automation-assisted" });
        var meetingType = await db.MeetingTypes.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "Operational discovery demo", ct);
        if (meetingType is null) { meetingType = new MeetingType { TenantId = tenantId, Name = "Operational discovery demo", DurationMinutes = 30, LocationType = "video" }; db.MeetingTypes.Add(meetingType); }
        if (!await db.MeetingBookings.AnyAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id, ct))
            db.MeetingBookings.Add(new MeetingBooking { TenantId = tenantId, MeetingTypeId = meetingType.Id, ContactId = contact.Id, LeadId = lead.Id, StartsAtUtc = now.AddDays(3).Date.AddHours(10), EndsAtUtc = now.AddDays(3).Date.AddHours(10.5), Status = "booked", ExternalEventId = "demo-calendar-event" });
        if (!await db.ProspectReplies.AnyAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id, ct))
        {
            var recipient = db.CampaignRecipients.Local.FirstOrDefault(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id)
                ?? await db.CampaignRecipients.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id, ct);
            if (recipient is not null) { recipient.Status = "replied"; recipient.RepliedAtUtc = now.AddDays(-1); recipient.NextRunAtUtc = null; }
            db.ProspectReplies.Add(new ProspectReply { TenantId = tenantId, CampaignId = campaign.Id, ProspectId = prospect.Id, Body = "Yes, visibility across dispatch and delivery exceptions is a priority. Tuesday morning works for a demo.", Classification = "interested", SentimentScore = 91, RequiresHuman = true, ReceivedAtUtc = now.AddDays(-1) });
            prospect.Status = ProspectStatus.DemoReady;
        }
    }

    private async Task EnsureQualifyAiScenarioAsync(Guid tenantId, PipelineStage stage, DateTime now, CancellationToken ct)
    {
        const string icpName = "[PRESENTATION] QualifyAI B2B revenue operations";
        var icp = await db.IcpProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == icpName, ct);
        if (icp is null)
        {
            icp = new IcpProfile { TenantId = tenantId, Name = icpName, Industry = "B2B SaaS, professional services, operations", CountriesCsv = "Germany, France, United Kingdom", MinimumEmployees = 20, MaximumEmployees = 2000, IntentKeywordsCsv = "manual qualification, slow follow-up, fragmented customer requests", CriteriaJson = "{\"requiredSignals\":1,\"minimumFitScore\":70}", Active = true, LastDiscoveryAtUtc = now };
            db.IcpProfiles.Add(icp);
        }

        var seed = new ProspectSeed("Cobalt Operations Cloud", "cobalt-operations.example", "Elena Brooks", "VP Revenue Operations", "elena.brooks@cobalt-operations.example", "B2B SaaS", "United Kingdom", 93, 82, "Announced a revenue operations hiring plan and a need to standardize lead qualification.");
        var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Domain == seed.Domain, ct);
        if (prospect is null)
        {
            prospect = new Prospect { TenantId = tenantId, CompanyName = seed.Company, Domain = seed.Domain, ContactName = seed.Contact, JobTitle = seed.JobTitle, Email = seed.Email, Industry = seed.Industry, Country = seed.Country, Source = "presentation-scenario", Priority = "A", ContactReadiness = "Demo ready", SuggestedBuyer = seed.JobTitle, PainHypothesis = "Qualification and follow-up are fragmented across spreadsheets and inboxes.", Offer = "QualifyAI revenue operations pilot", SourceUrl = "https://example.com/cobalt-operations", VerificationStatus = "Presentation scenario", OutreachStatus = "Ready for approval", DatasetOrigin = "QualifyAI presentation scenario" };
            prospect.Evaluate(seed.Fit, seed.Intent);
            db.Prospects.Add(prospect);
            db.ProspectSignals.Add(new ProspectSignal { TenantId = tenantId, ProspectId = prospect.Id, Type = "revenue-operations", Source = "presentation-scenario", Evidence = seed.Evidence, Score = seed.Intent, SourceUrl = prospect.SourceUrl });
        }

        const string listName = "[PRESENTATION] QualifyAI revenue operations accounts";
        var list = await db.TargetLists.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == listName, ct);
        if (list is null) { list = new TargetList { TenantId = tenantId, IcpProfileId = icp.Id, Name = listName, Description = "Presentation accounts for the QualifyAI go-to-market pipeline." }; db.TargetLists.Add(list); }
        if (!await db.TargetListMembers.AnyAsync(x => x.TenantId == tenantId && x.TargetListId == list.Id && x.ProspectId == prospect.Id, ct))
            db.TargetListMembers.Add(new TargetListMember { TenantId = tenantId, TargetListId = list.Id, ProspectId = prospect.Id });

        const string campaignName = "[PRESENTATION] QualifyAI — revenue operations pilot";
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == campaignName, ct);
        if (campaign is null)
        {
            campaign = new Campaign { TenantId = tenantId, TargetListId = list.Id, Name = campaignName, Goal = "book-demo", Status = CampaignStatus.Running, SenderName = "QualifyAI Growth", SenderEmail = "growth@qualifyai.example", StartsAtUtc = now.AddDays(-4) };
            db.Campaigns.Add(campaign);
            db.CampaignSteps.Add(new CampaignStep { TenantId = tenantId, CampaignId = campaign.Id, StepNumber = 1, DelayHours = 0, Channel = "email", SubjectTemplate = "{{company}}: turn qualified demand into booked demos", BodyTemplate = "Hi {{contact}}, QualifyAI connects discovery, qualification, approval-controlled outreach and revenue operations in one flow. Would a 25-minute walkthrough be useful?" });
        }
        if (!await db.CampaignRecipients.AnyAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.ProspectId == prospect.Id, ct))
            db.CampaignRecipients.Add(new CampaignRecipient { TenantId = tenantId, CampaignId = campaign.Id, ProspectId = prospect.Id, Status = "replied", RepliedAtUtc = now.AddHours(-8) });

        var company = await db.Companys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Domain == prospect.Domain, ct);
        if (company is null) { company = Company.Create(tenantId, prospect.CompanyName, prospect.Domain, prospect.Industry, 120, prospect.Country, 12_000_000m); db.Companys.Add(company); }
        var contact = await db.Contacts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email == prospect.Email, ct);
        if (contact is null) { contact = Contact.Create(tenantId, company.Id, "Elena", "Brooks", prospect.Email, "+44 20 7946 0184", "sql"); db.Contacts.Add(contact); }
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ContactId == contact.Id, ct);
        if (lead is null) { lead = Lead.Create(tenantId, contact.Id, company.Id, "outbound-campaign", 93, 36_000m, "Revenue operations team requested a product walkthrough."); lead.Qualify(); db.Leads.Add(lead); }
        if (!await db.Opportunitys.AnyAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id, ct))
            db.Opportunitys.Add(new Opportunity { TenantId = tenantId, LeadId = lead.Id, CompanyId = company.Id, ContactId = contact.Id, PipelineStageId = stage.Id, Name = "Cobalt Operations — QualifyAI revenue operations pilot", Amount = 36_000m, ExpectedCloseUtc = now.AddDays(21) });
        var meetingType = await db.MeetingTypes.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "Revenue operations walkthrough", ct);
        if (meetingType is null) { meetingType = new MeetingType { TenantId = tenantId, Name = "Revenue operations walkthrough", DurationMinutes = 30, LocationType = "video" }; db.MeetingTypes.Add(meetingType); }
        if (!await db.MeetingBookings.AnyAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id, ct))
            db.MeetingBookings.Add(new MeetingBooking { TenantId = tenantId, MeetingTypeId = meetingType.Id, ContactId = contact.Id, LeadId = lead.Id, StartsAtUtc = now.AddDays(2).Date.AddHours(14), EndsAtUtc = now.AddDays(2).Date.AddHours(14.5), Status = "booked", ExternalEventId = "qualifyai-revenue-ops-demo" });
    }

    private async Task EnsureSupportScenarioAsync(Guid tenantId, DateTime now, CancellationToken ct)
    {
        if (!await db.Tickets.AnyAsync(x => x.TenantId == tenantId && x.Number == "T-DEMO-DELIVERY", ct))
            db.Tickets.Add(new Ticket { TenantId = tenantId, Number = "T-DEMO-DELIVERY", Subject = "Cross-border delivery delayed", Description = "Customer contract includes priority delivery support. Shipment has not updated for 18 hours.", Status = TicketStatus.Open, Priority = TicketPriority.High, FirstResponseDueUtc = now.AddMinutes(25), ResolutionDueUtc = now.AddHours(4) });
        if (!await db.Tickets.AnyAsync(x => x.TenantId == tenantId && x.Number == "T-DEMO-PAYMENT", ct))
            db.Tickets.Add(new Ticket { TenantId = tenantId, Number = "T-DEMO-PAYMENT", Subject = "Duplicate payment transaction", Description = "Customer reports two captures for the same invoice. Refund requires finance approval.", Status = TicketStatus.Pending, Priority = TicketPriority.Urgent, FirstResponseDueUtc = now.AddMinutes(10), ResolutionDueUtc = now.AddHours(2) });
        if (!await db.KnowledgeGaps.AnyAsync(x => x.TenantId == tenantId && x.Topic == "Cross-border delivery exception policy", ct))
            db.KnowledgeGaps.Add(new KnowledgeGap { TenantId = tenantId, Topic = "Cross-border delivery exception policy", Occurrences = 18, ExampleQuestion = "When should a delayed international shipment be escalated?", ImpactScore = 86, Status = "open" });
    }

    private async Task EnsureAgentsAsync(Guid tenantId, CancellationToken ct)
    {
        var definitions = new[]
        {
            new { Name = "FusionFleet prospecting agent", Role = "Logistics market researcher", Instructions = "Review logistics prospect evidence, preserve verification notes and route only qualified, approval-ready accounts into the FusionFleet campaign.", Languages = "en,de,it,fr" },
            new { Name = "QualifyAI revenue operations agent", Role = "B2B revenue qualification", Instructions = "Turn verified demand signals into qualified leads, a controlled outreach draft, a demo booking and an opportunity with a clear next step.", Languages = "en,de,fr" }
        };
        foreach (var definition in definitions)
        {
            var agent = await db.AiAgents.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == definition.Name, ct);
            if (agent is not null) continue;
            agent = AiAgent.Create(tenantId, definition.Name, definition.Role, definition.Instructions, "professional", "gpt-5", definition.Languages, true, null);
            db.AiAgents.Add(agent);
            db.AiAgentVersions.Add(new AiAgentVersion { TenantId = tenantId, AgentId = agent.Id, Version = 1, ConfigurationJson = JsonSerializer.Serialize(definition), Published = true });
        }
    }

    private async Task EnsureAutomationsAsync(Guid tenantId, Guid discoveryIcpId, DateTime now, CancellationToken ct)
    {
        var flow = await db.QualificationFlows.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "Find FusionFleet Customers & Book Demos", ct);
        if (flow is null)
        {
            flow = new QualificationFlow { TenantId = tenantId, Name = "Find FusionFleet Customers & Book Demos", Active = true };
            db.QualificationFlows.Add(flow);
            db.WorkflowNodes.AddRange(
                WorkflowNode.Create(tenantId, flow.Id, "schedule", "start", "{\"trigger\":\"weekday 08:00\"}", 60, 160),
                WorkflowNode.Create(tenantId, flow.Id, "discover", "discoverProspects", $"{{\"source\":\"serpapi\",\"icpId\":\"{discoveryIcpId}\"}}", 300, 160),
                WorkflowNode.Create(tenantId, flow.Id, "score", "score", "{\"points\":75}", 540, 160),
                WorkflowNode.Create(tenantId, flow.Id, "campaign", "email", "{\"template\":\"logistics-growth\"}", 780, 160),
                WorkflowNode.Create(tenantId, flow.Id, "reply", "bookMeeting", "{\"classification\":\"interested\"}", 1020, 160));
            db.WorkflowEdges.AddRange(
                WorkflowEdge.Create(tenantId, flow.Id, "schedule", "discover", "{}"),
                WorkflowEdge.Create(tenantId, flow.Id, "discover", "score", "{}"),
                WorkflowEdge.Create(tenantId, flow.Id, "score", "campaign", "{\"minimumScore\":75}"),
                WorkflowEdge.Create(tenantId, flow.Id, "campaign", "reply", "{\"reply\":\"interested\"}"));
        }
        var qualifyFlow = await db.QualificationFlows.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "QualifyAI Revenue Operations", ct);
        if (qualifyFlow is null)
        {
            qualifyFlow = new QualificationFlow { TenantId = tenantId, Name = "QualifyAI Revenue Operations", Active = true };
            db.QualificationFlows.Add(qualifyFlow);
            db.WorkflowNodes.AddRange(
                WorkflowNode.Create(tenantId, qualifyFlow.Id, "signal", "start", "{\"trigger\":\"buyer-signal\"}", 60, 160),
                WorkflowNode.Create(tenantId, qualifyFlow.Id, "qualify", "score", "{\"minimumScore\":75}", 300, 160),
                WorkflowNode.Create(tenantId, qualifyFlow.Id, "approve", "approval", "{\"required\":true}", 540, 160),
                WorkflowNode.Create(tenantId, qualifyFlow.Id, "demo", "bookMeeting", "{\"meetingType\":\"Revenue operations walkthrough\"}", 780, 160),
                WorkflowNode.Create(tenantId, qualifyFlow.Id, "opportunity", "createOpportunity", "{\"pipeline\":\"QualifyAI revenue pipeline\"}", 1020, 160));
            db.WorkflowEdges.AddRange(
                WorkflowEdge.Create(tenantId, qualifyFlow.Id, "signal", "qualify", "{}"),
                WorkflowEdge.Create(tenantId, qualifyFlow.Id, "qualify", "approve", "{}"),
                WorkflowEdge.Create(tenantId, qualifyFlow.Id, "approve", "demo", "{\"decision\":\"approved\"}"),
                WorkflowEdge.Create(tenantId, qualifyFlow.Id, "demo", "opportunity", "{}"));
        }
        var discoveryActions = JsonSerializer.Serialize(new[]
        {
            new { type = "discoverProspects", icpId = discoveryIcpId, source = "serpapi", maximumResults = 50, minimumScore = 70, createTargetList = true },
            new { type = "enrichProspects" },
            new { type = "scoreProspects" },
            new { type = "createTargetList" }
        });
        var definitions = new[]
        {
            new { Name = "FusionFleet ICP discovery → qualified target list", Trigger = "schedule.weekday", Conditions = "[{\"field\":\"icp.active\",\"operator\":\"equals\",\"value\":true}]", Actions = discoveryActions },
            new { Name = "Interested reply → demo and opportunity", Trigger = "campaign.reply.interested", Conditions = "[{\"field\":\"sentimentScore\",\"operator\":\">=\",\"value\":70}]", Actions = "[{\"type\":\"createOpportunity\"},{\"type\":\"bookMeeting\"},{\"type\":\"notifySales\"}]" },
            new { Name = "Qualified buyer signal → controlled QualifyAI demo", Trigger = "buyer.signal.qualified", Conditions = "[{\"field\":\"priority\",\"operator\":\"equals\",\"value\":\"A\"}]", Actions = "[{\"type\":\"requestApproval\"},{\"type\":\"createCampaignDraft\"},{\"type\":\"bookMeeting\"},{\"type\":\"createOpportunity\"}]" },
            new { Name = "Payment dispute → finance approval", Trigger = "ticket.payment.dispute", Conditions = "[{\"field\":\"priority\",\"operator\":\"equals\",\"value\":\"Urgent\"}]", Actions = "[{\"type\":\"requestApproval\",\"title\":\"Approve duplicate-payment refund\",\"dueInHours\":2},{\"type\":\"notify\"}]" }
        };
        foreach (var definition in definitions)
        {
            var rule = await db.AutomationRules.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == definition.Name, ct);
            if (rule is null)
            {
                rule = AutomationRule.Create(tenantId, definition.Name, definition.Trigger, definition.Conditions, definition.Actions, true);
                db.AutomationRules.Add(rule);
                var run = AutomationRun.Create(tenantId, rule.Id, "{\"source\":\"scenario-installer\"}"); run.Start();
                run.Complete(JsonSerializer.Serialize(new[] { new { status = "completed", message = "Scenario workflow persisted and validated.", atUtc = now } }));
                db.AutomationRuns.Add(run);
            }
            else if (definition.Name == "FusionFleet ICP discovery → qualified target list")
            {
                // Upgrade the presentation workflow from synthetic discovery to the
                // same configured live-source action used by real workspaces.
                rule.UpdateConfiguration(definition.Name, definition.Trigger, definition.Conditions, definition.Actions, rule.Active);
            }
        }
        if (!await db.UsageRecords.AnyAsync(x => x.TenantId == tenantId && x.Meter == "automation_actions" && x.ReferenceId == "realistic-scenario", ct))
            db.UsageRecords.Add(new UsageRecord { TenantId = tenantId, Meter = "automation_actions", Quantity = 12, ReferenceId = "realistic-scenario", RecordedAtUtc = now });
    }

    private sealed record ProspectSeed(string Company, string Domain, string Contact, string JobTitle, string Email, string Industry, string Country, int Fit, int Intent, string Evidence);
}
