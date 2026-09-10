using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Persistence.SqlServer.Configurations;

internal static class EnterpriseModelConfiguration
{
    internal static void ConfigureEnterpriseModel(this ModelBuilder builder)
    {
        builder.Entity<Facility>(e =>
        {
            e.ToTable("Facilities");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.CountryCode).HasMaxLength(8);
            e.Property(x => x.City).HasMaxLength(128);
            e.Property(x => x.Address).HasMaxLength(512);
            e.Property(x => x.ContactEmail).HasMaxLength(320);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        builder.Entity<FacilityCapability>(e =>
        {
            e.ToTable("FacilityCapabilities");
            e.HasKey(x => x.Id);
            e.Property(x => x.CapabilityType).HasMaxLength(128).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(32);
            e.HasIndex(x => new { x.TenantId, x.FacilityId, x.CatalogProductId });
        });

        builder.Entity<CustomerAccount>(e =>
        {
            e.ToTable("CustomerAccounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.CustomerType).HasMaxLength(64).IsRequired();
            e.Property(x => x.PaymentTerms).HasMaxLength(128).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.CompanyId }).IsUnique();
        });

        builder.Entity<PriceList>(e =>
        {
            e.ToTable("PriceLists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            e.Property(x => x.CustomerType).HasMaxLength(64).IsRequired();
            e.Property(x => x.CountryCode).HasMaxLength(8);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        builder.Entity<PriceListItem>(e =>
        {
            e.ToTable("PriceListItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(1024);
            e.Property(x => x.Unit).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.PriceListId, x.CatalogProductId, x.ProductVariantId });
        });

        builder.Entity<SalesOrder>(e =>
        {
            e.ToTable("SalesOrders");
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasMaxLength(64).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAtUtc });
        });

        builder.Entity<SalesOrderItem>(e =>
        {
            e.ToTable("SalesOrderItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(1024).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.SalesOrderId });
        });

        builder.Entity<Fulfillment>(e =>
        {
            e.ToTable("Fulfillments");
            e.HasKey(x => x.Id);
            e.Property(x => x.DestinationName).HasMaxLength(256);
            e.Property(x => x.DestinationAddress).HasMaxLength(512);
            e.Property(x => x.DestinationCountryCode).HasMaxLength(8);
            e.Property(x => x.DestinationCity).HasMaxLength(128);
            e.Property(x => x.Status).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.SalesOrderId });
        });

        builder.Entity<FulfillmentItem>(e =>
        {
            e.ToTable("FulfillmentItems");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.FulfillmentId, x.SalesOrderItemId }).IsUnique();
        });

        builder.Entity<StockBalance>(e =>
        {
            e.ToTable("StockBalances");
            e.HasKey(x => x.Id);
            e.Property(x => x.Unit).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.FacilityId, x.CatalogProductId, x.ProductVariantId }).IsUnique();
        });

        builder.Entity<StockReservation>(e =>
        {
            e.ToTable("StockReservations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.StockBalanceId, x.Status });
        });

        builder.Entity<StockMovement>(e =>
        {
            e.ToTable("StockMovements");
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasMaxLength(64).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAtUtc });
        });

        builder.Entity<Shipment>(e =>
        {
            e.ToTable("Shipments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasMaxLength(64).IsRequired();
            e.Property(x => x.CarrierName).HasMaxLength(256);
            e.Property(x => x.TrackingNumber).HasMaxLength(256);
            e.Property(x => x.ProofOfDeliveryUri).HasMaxLength(2048);
            e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status, x.EstimatedDeliveryAtUtc });
        });

        builder.Entity<RoutePlan>(e =>
        {
            e.ToTable("RoutePlans");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(128);
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Status, x.PlannedStartAtUtc });
        });

        builder.Entity<RouteStop>(e =>
        {
            e.ToTable("RouteStops");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Address).HasMaxLength(512);
            e.HasIndex(x => new { x.TenantId, x.RoutePlanId, x.Sequence }).IsUnique();
        });

        builder.Entity<CommercialDocument>(e =>
        {
            e.ToTable("CommercialDocuments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasMaxLength(64).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.Uri).HasMaxLength(2048);
            e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Type, x.Status });
        });

        builder.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(128);
            e.Property(x => x.ExternalTransactionId).HasMaxLength(256);
            e.Property(x => x.Reference).HasMaxLength(256);
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAtUtc });
            e.HasIndex(x => new { x.TenantId, x.ExternalTransactionId });
        });

        builder.Entity<PaymentAllocation>(e =>
        {
            e.ToTable("PaymentAllocations");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.PaymentId, x.CommercialDocumentId }).IsUnique();
        });
    }
}
