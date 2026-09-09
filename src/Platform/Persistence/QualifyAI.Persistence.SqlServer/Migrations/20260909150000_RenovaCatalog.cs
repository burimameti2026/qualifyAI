using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Persistence.SqlServer.Migrations;

[Migration("20260909150000_RenovaCatalog")]
public partial class RenovaCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.ProductCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductCategories (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        ParentCategoryId uniqueidentifier NULL,
        Name nvarchar(256) NOT NULL,
        Code nvarchar(128) NULL,
        Description nvarchar(max) NULL,
        SortOrder int NOT NULL,
        IsActive bit NOT NULL,
        CONSTRAINT PK_ProductCategories PRIMARY KEY (Id)
    );
    CREATE INDEX IX_ProductCategories_TenantId_Code ON dbo.ProductCategories(TenantId, Code);
END

IF OBJECT_ID(N'dbo.CatalogProducts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CatalogProducts (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        ProductCategoryId uniqueidentifier NOT NULL,
        Name nvarchar(256) NOT NULL,
        Code nvarchar(128) NOT NULL,
        Brand nvarchar(128) NULL,
        ShortDescription nvarchar(max) NULL,
        Description nvarchar(max) NULL,
        KeyBenefits nvarchar(max) NULL,
        Applications nvarchar(max) NULL,
        TechnicalSpecifications nvarchar(max) NULL,
        IsActive bit NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        UpdatedAt datetimeoffset NULL,
        CONSTRAINT PK_CatalogProducts PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_CatalogProducts_TenantId_Code ON dbo.CatalogProducts(TenantId, Code);
    CREATE INDEX IX_CatalogProducts_TenantId_ProductCategoryId ON dbo.CatalogProducts(TenantId, ProductCategoryId);
END

IF OBJECT_ID(N'dbo.ProductVariants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductVariants (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL,
        Name nvarchar(256) NOT NULL,
        Sku nvarchar(128) NULL,
        Packaging nvarchar(max) NULL,
        NetWeight decimal(18,3) NULL,
        WeightUnit nvarchar(max) NULL,
        Color nvarchar(max) NULL,
        Specifications nvarchar(max) NULL,
        IsActive bit NOT NULL,
        CONSTRAINT PK_ProductVariants PRIMARY KEY (Id)
    );
    CREATE INDEX IX_ProductVariants_TenantId_CatalogProductId ON dbo.ProductVariants(TenantId, CatalogProductId);
END

IF OBJECT_ID(N'dbo.ProductAssets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductAssets (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL,
        AssetType nvarchar(64) NOT NULL,
        FileName nvarchar(512) NOT NULL,
        Uri nvarchar(2048) NOT NULL,
        Language nvarchar(max) NULL,
        Title nvarchar(max) NULL,
        IsPublic bit NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        CONSTRAINT PK_ProductAssets PRIMARY KEY (Id)
    );
    CREATE INDEX IX_ProductAssets_TenantId_CatalogProductId ON dbo.ProductAssets(TenantId, CatalogProductId);
END

IF OBJECT_ID(N'dbo.ProductLocalizations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductLocalizations (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL,
        Language nvarchar(16) NOT NULL,
        Name nvarchar(256) NOT NULL,
        ShortDescription nvarchar(max) NULL,
        Description nvarchar(max) NULL,
        KeyBenefits nvarchar(max) NULL,
        Applications nvarchar(max) NULL,
        CONSTRAINT PK_ProductLocalizations PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_ProductLocalizations_TenantId_CatalogProductId_Language ON dbo.ProductLocalizations(TenantId, CatalogProductId, Language);
END

IF OBJECT_ID(N'dbo.TargetMarkets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TargetMarkets (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL,
        CountryCode nvarchar(8) NOT NULL,
        CountryName nvarchar(128) NOT NULL,
        DefaultLanguage nvarchar(16) NOT NULL,
        TargetIndustries nvarchar(max) NULL,
        TargetCustomerTypes nvarchar(max) NULL,
        IsActive bit NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        CONSTRAINT PK_TargetMarkets PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_TargetMarkets_TenantId_CatalogProductId_CountryCode ON dbo.TargetMarkets(TenantId, CatalogProductId, CountryCode);
END

IF OBJECT_ID(N'dbo.ProductPromotionPlans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductPromotionPlans (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL,
        TargetMarketId uniqueidentifier NOT NULL,
        Name nvarchar(256) NOT NULL,
        CampaignLanguage nvarchar(16) NOT NULL,
        Status nvarchar(32) NOT NULL,
        TargetCustomerProfile nvarchar(max) NULL,
        QualificationRules nvarchar(max) NULL,
        MessagingStrategy nvarchar(max) NULL,
        EnableAutonomousProspecting bit NOT NULL,
        EnableAutomaticCampaignEnrollment bit NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        ActivatedAt datetimeoffset NULL,
        CONSTRAINT PK_ProductPromotionPlans PRIMARY KEY (Id)
    );
    CREATE INDEX IX_ProductPromotionPlans_TenantId_CatalogProductId_TargetMarketId ON dbo.ProductPromotionPlans(TenantId, CatalogProductId, TargetMarketId);
END

IF OBJECT_ID(N'dbo.PortalPublications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalPublications (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL,
        Slug nvarchar(256) NOT NULL,
        Status nvarchar(32) NOT NULL,
        IsVisible bit NOT NULL,
        Version int NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        PublishedAt datetimeoffset NULL,
        UpdatedAt datetimeoffset NULL,
        CONSTRAINT PK_PortalPublications PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_PortalPublications_TenantId_CatalogProductId ON dbo.PortalPublications(TenantId, CatalogProductId);
    CREATE UNIQUE INDEX IX_PortalPublications_TenantId_Slug ON dbo.PortalPublications(TenantId, Slug);
END

IF OBJECT_ID(N'dbo.PortalInquiries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalInquiries (
        Id uniqueidentifier NOT NULL,
        TenantId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NULL,
        Name nvarchar(256) NOT NULL,
        Company nvarchar(max) NULL,
        Email nvarchar(320) NOT NULL,
        Phone nvarchar(max) NULL,
        CountryCode nvarchar(max) NULL,
        Language nvarchar(16) NOT NULL,
        Message nvarchar(max) NULL,
        Status nvarchar(32) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        CONSTRAINT PK_PortalInquiries PRIMARY KEY (Id)
    );
    CREATE INDEX IX_PortalInquiries_TenantId_CreatedAt ON dbo.PortalInquiries(TenantId, CreatedAt);
END
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.PortalInquiries', N'U') IS NOT NULL DROP TABLE dbo.PortalInquiries;
IF OBJECT_ID(N'dbo.PortalPublications', N'U') IS NOT NULL DROP TABLE dbo.PortalPublications;
IF OBJECT_ID(N'dbo.ProductPromotionPlans', N'U') IS NOT NULL DROP TABLE dbo.ProductPromotionPlans;
IF OBJECT_ID(N'dbo.TargetMarkets', N'U') IS NOT NULL DROP TABLE dbo.TargetMarkets;
IF OBJECT_ID(N'dbo.ProductLocalizations', N'U') IS NOT NULL DROP TABLE dbo.ProductLocalizations;
IF OBJECT_ID(N'dbo.ProductAssets', N'U') IS NOT NULL DROP TABLE dbo.ProductAssets;
IF OBJECT_ID(N'dbo.ProductVariants', N'U') IS NOT NULL DROP TABLE dbo.ProductVariants;
IF OBJECT_ID(N'dbo.CatalogProducts', N'U') IS NOT NULL DROP TABLE dbo.CatalogProducts;
IF OBJECT_ID(N'dbo.ProductCategories', N'U') IS NOT NULL DROP TABLE dbo.ProductCategories;
");
    }
}
