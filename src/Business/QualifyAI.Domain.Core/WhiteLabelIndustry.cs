namespace QualifyAI.Domain;
public class Agency : Entity { public string Name { get; set; }=""; public string Slug { get; set; }=""; }
public class AgencyClient : Entity { public Guid AgencyId { get; set; } public Guid TenantId { get; set; } public string Label { get; set; }=""; }
public class BrandingProfile : TenantEntity { public string ProductName { get; set; }="QualifyAI"; public string LogoUrl { get; set; }=""; public string PrimaryColor { get; set; }="#2563EB"; public string AccentColor { get; set; }="#0F172A"; public string SupportEmail { get; set; }=""; }
public class CustomDomain : TenantEntity { public string Host { get; set; }=""; public string Status { get; set; }="pending"; public string VerificationToken { get; set; }=""; }
public class IndustryPack : Entity { public string Code { get; set; }=""; public string Name { get; set; }=""; public string Description { get; set; }=""; public string TemplateJson { get; set; }="{}"; }
public class TenantIndustryPack : TenantEntity { public Guid IndustryPackId { get; set; } public bool Enabled { get; set; }=true; public string OverridesJson { get; set; }="{}"; }
