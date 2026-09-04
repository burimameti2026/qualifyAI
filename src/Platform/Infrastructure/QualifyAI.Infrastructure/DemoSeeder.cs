using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Application;

namespace QualifyAI.Infrastructure;

public sealed class DemoSeeder(AppDbContext db, IPasswordService passwords)
{
    public async Task SeedAsync()
    {
        if (await db.Tenants.AnyAsync()) return;

        var tenant = new Tenant { Name = "QualifyAI Demo", Slug = "demo", PlanCode = "business" };
        db.Tenants.Add(tenant);

        var admin = new AppUser
        {
            TenantId = tenant.Id,
            Email = "admin@demo.local",
            DisplayName = "Demo Admin",
            PasswordHash = passwords.Hash("Admin123!ChangeMe")
        };
        db.AppUsers.Add(admin);

        var role = new Role { TenantId = tenant.Id, Name = "Admin", Description = "Full tenant administration" };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { TenantId = tenant.Id, UserId = admin.Id, RoleId = role.Id });

        var kb = new KnowledgeBase { TenantId = tenant.Id, Name = "Product & Sales Knowledge", Description = "Approved product, pricing, security and sales content." };
        db.KnowledgeBases.Add(kb);
        db.KnowledgeDocuments.AddRange(
            new KnowledgeDocument { TenantId = tenant.Id, KnowledgeBaseId = kb.Id, Title = "QualifyAI Product Overview", Body = "QualifyAI automates customer support, lead qualification, ticketing, workflows, CRM handoff and AI actions." },
            new KnowledgeDocument { TenantId = tenant.Id, KnowledgeBaseId = kb.Id, Title = "Enterprise Security & SSO", Body = "Business customers can configure SSO, retention, tenant isolation, audit controls and enterprise security policies." },
            new KnowledgeDocument { TenantId = tenant.Id, KnowledgeBaseId = kb.Id, Title = "CRM Integrations", Body = "QualifyAI integrates through provider adapters, OAuth connections, mappings, sync jobs and webhooks." }
        );
        db.AiAgents.Add(new AiAgent
        {
            TenantId = tenant.Id,
            Name = "Ava",
            Role = "Customer Support & Sales",
            Tone = "professional",
            KnowledgeBaseId = kb.Id,
            Instructions = "Answer from approved knowledge, qualify commercial intent, execute approved tools, and escalate when confidence is low."
        });

        var pipe = new Pipeline { TenantId = tenant.Id, Name = "B2B Sales", IsDefault = true };
        db.Pipelines.Add(pipe);
        var qualified = new PipelineStage { TenantId = tenant.Id, PipelineId = pipe.Id, Name = "Qualified", Probability = 35m, SortOrder = 2 };
        var meeting = new PipelineStage { TenantId = tenant.Id, PipelineId = pipe.Id, Name = "Meeting", Probability = 55m, SortOrder = 3 };
        var proposal = new PipelineStage { TenantId = tenant.Id, PipelineId = pipe.Id, Name = "Proposal", Probability = 70m, SortOrder = 4 };
        var negotiation = new PipelineStage { TenantId = tenant.Id, PipelineId = pipe.Id, Name = "Negotiation", Probability = 85m, SortOrder = 5 };
        db.PipelineStages.AddRange(
            new PipelineStage { TenantId = tenant.Id, PipelineId = pipe.Id, Name = "New", Probability = 10m, SortOrder = 1 },
            qualified, meeting, proposal, negotiation,
            new PipelineStage { TenantId = tenant.Id, PipelineId = pipe.Id, Name = "Won", Probability = 100m, SortOrder = 6 },
            new PipelineStage { TenantId = tenant.Id, PipelineId = pipe.Id, Name = "Lost", Probability = 0m, SortOrder = 7 }
        );

        var nord = new Company { TenantId = tenant.Id, Name = "NordRoute GmbH", Domain = "nordroute.de", Industry = "Logistics", Employees = 86, Country = "Germany", AnnualRevenue = 18_400_000m };
        var atlas = new Company { TenantId = tenant.Id, Name = "Atlas Manufacturing", Domain = "atlas.it", Industry = "Manufacturing", Employees = 240, Country = "Italy", AnnualRevenue = 42_000_000m };
        var blue = new Company { TenantId = tenant.Id, Name = "BlueLine Logistics", Domain = "blueline.fr", Industry = "3PL", Employees = 72, Country = "France", AnnualRevenue = 13_800_000m };
        var vektor = new Company { TenantId = tenant.Id, Name = "Vektor Systems", Domain = "vektor.at", Industry = "Technology", Employees = 120, Country = "Austria", AnnualRevenue = 24_000_000m };
        db.Companys.AddRange(nord, atlas, blue, vektor);

        var lukas = new Contact { TenantId = tenant.Id, CompanyId = nord.Id, FirstName = "Lukas", LastName = "Meyer", Email = "lukas@nordroute.de", Phone = "+49 30 555 0142", LifecycleStage = "qualified" };
        var sofia = new Contact { TenantId = tenant.Id, CompanyId = atlas.Id, FirstName = "Sofia", LastName = "Romano", Email = "sofia@atlas.it", Phone = "+39 02 555 771", LifecycleStage = "lead" };
        var marc = new Contact { TenantId = tenant.Id, CompanyId = blue.Id, FirstName = "Marc", LastName = "Dubois", Email = "marc@blueline.fr", Phone = "+33 1 44 55 19", LifecycleStage = "lead" };
        var anna = new Contact { TenantId = tenant.Id, CompanyId = vektor.Id, FirstName = "Anna", LastName = "Keller", Email = "anna@vektor.at", LifecycleStage = "lead" };
        db.Contacts.AddRange(lukas, sofia, marc, anna);

        var lead1 = new Lead { TenantId = tenant.Id, ContactId = lukas.Id, CompanyId = nord.Id, Score = 94, Temperature = LeadTemperature.Hot, Status = "qualified", EstimatedValue = 48_000m, IntentSummary = "Fleet management platform", Source = "web" };
        var lead2 = new Lead { TenantId = tenant.Id, ContactId = sofia.Id, CompanyId = atlas.Id, Score = 91, Temperature = LeadTemperature.Hot, Status = "meeting", EstimatedValue = 32_500m, IntentSummary = "AI support automation", Source = "web" };
        var lead3 = new Lead { TenantId = tenant.Id, ContactId = marc.Id, CompanyId = blue.Id, Score = 87, Temperature = LeadTemperature.Hot, Status = "qualified", EstimatedValue = 27_000m, IntentSummary = "Warehouse AI", Source = "email" };
        var lead4 = new Lead { TenantId = tenant.Id, ContactId = anna.Id, CompanyId = vektor.Id, Score = 84, Temperature = LeadTemperature.Hot, Status = "qualified", EstimatedValue = 19_500m, IntentSummary = "AI service desk", Source = "web" };
        db.Leads.AddRange(lead1, lead2, lead3, lead4);

        db.Opportunitys.AddRange(
            new Opportunity { TenantId = tenant.Id, LeadId = lead1.Id, CompanyId = nord.Id, ContactId = lukas.Id, PipelineStageId = proposal.Id, Name = "NordRoute Fleet Transformation", Amount = 48_000m, Status = OpportunityStatus.Open, ExpectedCloseUtc = DateTime.UtcNow.AddDays(25) },
            new Opportunity { TenantId = tenant.Id, LeadId = lead2.Id, CompanyId = atlas.Id, ContactId = sofia.Id, PipelineStageId = meeting.Id, Name = "Atlas AI Support Rollout", Amount = 32_500m, Status = OpportunityStatus.Open, ExpectedCloseUtc = DateTime.UtcNow.AddDays(38) },
            new Opportunity { TenantId = tenant.Id, LeadId = lead3.Id, CompanyId = blue.Id, ContactId = marc.Id, PipelineStageId = qualified.Id, Name = "BlueLine Warehouse AI", Amount = 27_000m, Status = OpportunityStatus.Open, ExpectedCloseUtc = DateTime.UtcNow.AddDays(44) },
            new Opportunity { TenantId = tenant.Id, LeadId = lead4.Id, CompanyId = vektor.Id, ContactId = anna.Id, PipelineStageId = negotiation.Id, Name = "Vektor Service Desk", Amount = 77_000m, Status = OpportunityStatus.Open, ExpectedCloseUtc = DateTime.UtcNow.AddDays(18) }
        );

        var web = new Channel { TenantId = tenant.Id, Type = "web", Name = "Website", Enabled = true };
        db.Channels.Add(web);
        var conversation = new Conversation { TenantId = tenant.Id, ContactId = lukas.Id, LeadId = lead1.Id, ChannelId = web.Id, Status = ConversationStatus.Open, AiEnabled = true, LastMessageAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        db.Conversations.Add(conversation);
        db.Messages.AddRange(
            new Message { TenantId = tenant.Id, ConversationId = conversation.Id, SenderType = "visitor", Text = "We operate 60 trucks across Germany and Austria. We need better dispatching, driver tracking and maintenance planning." },
            new Message { TenantId = tenant.Id, ConversationId = conversation.Id, SenderType = "ai", Text = "That sounds like a strong fit. Are you evaluating software for an immediate rollout or a later project?" },
            new Message { TenantId = tenant.Id, ConversationId = conversation.Id, SenderType = "visitor", Text = "Immediate. We want to start within 2 months and our budget is around €40–50k." }
        );

        db.SlaPolicys.Add(new SlaPolicy { TenantId = tenant.Id, Name = "Business SLA", FirstResponseMinutes = 30, ResolutionMinutes = 240 });
        db.Tickets.AddRange(
            new Ticket { TenantId = tenant.Id, ConversationId = conversation.Id, ContactId = lukas.Id, Number = "T-1048", Subject = "ERP integration requirements", Description = "Confirm supported ERP integration path.", Status = TicketStatus.Open, Priority = TicketPriority.High, FirstResponseDueUtc = DateTime.UtcNow.AddMinutes(24), ResolutionDueUtc = DateTime.UtcNow.AddHours(4) },
            new Ticket { TenantId = tenant.Id, ContactId = marc.Id, Number = "T-1047", Subject = "Warehouse knowledge sync", Description = "Customer needs external documentation sync.", Status = TicketStatus.Pending, Priority = TicketPriority.Normal, ResolutionDueUtc = DateTime.UtcNow.AddHours(6) }
        );

        db.KnowledgeGaps.AddRange(
            new KnowledgeGap { TenantId = tenant.Id, Topic = "International freight pricing", Occurrences = 47, ExampleQuestion = "How is cross-border freight priced?", ImpactScore = 96, Status = "open" },
            new KnowledgeGap { TenantId = tenant.Id, Topic = "Enterprise SSO setup", Occurrences = 28, ExampleQuestion = "Can we use our SAML identity provider?", ImpactScore = 88, Status = "open" },
            new KnowledgeGap { TenantId = tenant.Id, Topic = "Data retention policy", Occurrences = 19, ExampleQuestion = "How long are messages retained?", ImpactScore = 72, Status = "open" }
        );

        var flow = new QualificationFlow { TenantId = tenant.Id, Name = "B2B Sales Qualification", Active = true };
        db.QualificationFlows.Add(flow);
        db.WorkflowNodes.AddRange(
            new WorkflowNode { TenantId = tenant.Id, FlowId = flow.Id, NodeKey = "start", Type = "start", X = 70, Y = 185 },
            new WorkflowNode { TenantId = tenant.Id, FlowId = flow.Id, NodeKey = "intent", Type = "ai-decision", ConfigJson = "{\"instruction\":\"Detect sales or support intent\"}", X = 340, Y = 160 },
            new WorkflowNode { TenantId = tenant.Id, FlowId = flow.Id, NodeKey = "company-size", Type = "question", ConfigJson = "{\"question\":\"How many employees does your company have?\"}", X = 600, Y = 80 },
            new WorkflowNode { TenantId = tenant.Id, FlowId = flow.Id, NodeKey = "budget", Type = "question", ConfigJson = "{\"question\":\"What is your estimated budget?\"}", X = 820, Y = 80 },
            new WorkflowNode { TenantId = tenant.Id, FlowId = flow.Id, NodeKey = "score", Type = "score", X = 1040, Y = 80 }
        );
        db.WorkflowEdges.AddRange(
            new WorkflowEdge { TenantId = tenant.Id, FlowId = flow.Id, FromNodeKey = "start", ToNodeKey = "intent" },
            new WorkflowEdge { TenantId = tenant.Id, FlowId = flow.Id, FromNodeKey = "intent", ToNodeKey = "company-size", ConditionJson = "{\"intent\":\"sales\"}" },
            new WorkflowEdge { TenantId = tenant.Id, FlowId = flow.Id, FromNodeKey = "company-size", ToNodeKey = "budget" },
            new WorkflowEdge { TenantId = tenant.Id, FlowId = flow.Id, FromNodeKey = "budget", ToNodeKey = "score" }
        );

        db.AutomationRules.AddRange(
            new AutomationRule { TenantId = tenant.Id, Name = "Route hot leads to sales", Trigger = "lead.scored", ConditionsJson = "[{\"field\":\"score\",\"gte\":80}]", ActionsJson = "[{\"type\":\"assign-team\",\"team\":\"sales\"},{\"type\":\"notify\"}]" },
            new AutomationRule { TenantId = tenant.Id, Name = "Escalate SLA risk", Trigger = "ticket.sla-risk", ActionsJson = "[{\"type\":\"notify\"},{\"type\":\"increase-priority\"}]" }
        );

        db.IntegrationConnections.AddRange(
            new IntegrationConnection { TenantId = tenant.Id, Provider = "HubSpot", Name = "Sales CRM", Status = IntegrationStatus.Connected, SettingsJson = "{\"sync\":\"contacts,leads,opportunities\"}" },
            new IntegrationConnection { TenantId = tenant.Id, Provider = "Slack", Name = "Revenue alerts", Status = IntegrationStatus.Connected },
            new IntegrationConnection { TenantId = tenant.Id, Provider = "Google Calendar", Name = "Sales calendars", Status = IntegrationStatus.Connected }
        );

        db.UsageRecords.AddRange(
            new UsageRecord { TenantId = tenant.Id, Meter = "ai_conversations", Quantity = 6842 },
            new UsageRecord { TenantId = tenant.Id, Meter = "messages", Quantity = 24819 },
            new UsageRecord { TenantId = tenant.Id, Meter = "qualified_leads", Quantity = 126 },
            new UsageRecord { TenantId = tenant.Id, Meter = "knowledge_chunks", Quantity = 4821 }
        );

        var plans = new[]
        {
            new Plan { Code = "starter", Name = "Starter", MonthlyPrice = 79, EntitlementsJson = "{\"ai\":true,\"seats\":3}" },
            new Plan { Code = "growth", Name = "Growth", MonthlyPrice = 199, EntitlementsJson = "{\"ai\":true,\"automations\":true,\"seats\":10}" },
            new Plan { Code = "business", Name = "Business", MonthlyPrice = 499, EntitlementsJson = "{\"ai\":true,\"automations\":true,\"sso\":true,\"seats\":50}" }
        };
        db.Plans.AddRange(plans);

        foreach (var p in new[] { ("saas", "SaaS Sales Agent"), ("agency", "Software Agency Agent"), ("logistics", "Logistics & 3PL Agent"), ("ecommerce", "Ecommerce Agent") })
            db.IndustryPacks.Add(new IndustryPack { Code = p.Item1, Name = p.Item2, TemplateJson = "{}" });

        db.BrandingProfiles.Add(new BrandingProfile { TenantId = tenant.Id, ProductName = "QualifyAI", PrimaryColor = "#2563EB", AccentColor = "#0B1220", SupportEmail = "support@demo.local" });
        db.CustomDomains.Add(new CustomDomain { TenantId = tenant.Id, Host = "support.demo.local", Status = "verified", VerificationToken = "demo" });
        db.SsoConfigurations.Add(new SsoConfiguration { TenantId = tenant.Id, ProviderType = "saml", EntityId = "urn:qualifyai:demo", Enabled = false });
        db.DataRetentionPolicys.Add(new DataRetentionPolicy { TenantId = tenant.Id, EntityType = "messages", RetentionDays = 365, Enabled = true });

        await db.SaveChangesAsync();
    }
}
