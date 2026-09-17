using Microsoft.EntityFrameworkCore;
using ShopCommon;

namespace ShopData;

/// <summary>
/// One context over the whole shop domain. Inventory uses timestamp-based optimistic
/// concurrency so concurrent checkouts cannot oversell - the core high-traffic guarantee.
/// </summary>
public class ShopDbContext : DbContext
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<ReturnRequest> Returns => Set<ReturnRequest>();
    public DbSet<CarrierSetting> CarrierSettings => Set<CarrierSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.Property(p => p.Sku).HasMaxLength(40).IsRequired();
            e.HasIndex(p => p.Sku).IsUnique();
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Material).HasMaxLength(200);
            e.Property(p => p.ImageUrl).HasMaxLength(500);
            e.Property(p => p.Price).HasPrecision(12, 2);
            e.Property(p => p.CompareAtPrice).HasPrecision(12, 2);
            e.HasMany(p => p.Variants).WithOne(v => v.Product!).HasForeignKey(v => v.ProductId);
        });

        b.Entity<ProductVariant>(e =>
        {
            e.Property(v => v.Sku).HasMaxLength(40).IsRequired();
            e.HasIndex(v => v.Sku).IsUnique();
            e.Property(v => v.Color).HasMaxLength(50);
            e.Property(v => v.Size).HasMaxLength(50);
            e.Property(v => v.PriceOverride).HasPrecision(12, 2);
            e.HasOne(v => v.Inventory).WithOne().HasForeignKey<InventoryItem>(i => i.ProductVariantId);
        });

        // Optimistic concurrency: the last-adjusted timestamp doubles as the version token,
        // so two simultaneous checkouts reserving the same stock cannot both commit.
        b.Entity<InventoryItem>(e =>
        {
            e.Property(i => i.LastAdjustedUtc).IsConcurrencyToken();
            e.Property(i => i.OnHand).IsRequired();
            e.Property(i => i.Reserved).IsRequired();
        });

        b.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Email).IsUnique();
            e.Property(c => c.Email).HasMaxLength(320).IsRequired();
            e.Property(c => c.Name).HasMaxLength(200);
        });

        b.Entity<Order>(e =>
        {
            e.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.CustomerEmail);
            e.Property(o => o.CustomerEmail).HasMaxLength(320).IsRequired();
            e.Property(o => o.CouponCode).HasMaxLength(16);
            e.Property(o => o.Subtotal).HasPrecision(12, 2);
            e.Property(o => o.DiscountTotal).HasPrecision(12, 2);
            e.Property(o => o.ShippingTotal).HasPrecision(12, 2);
            e.Property(o => o.TaxTotal).HasPrecision(12, 2);
            e.Property(o => o.GrandTotal).HasPrecision(12, 2);

            e.OwnsOne(o => o.ShipTo, nav =>
            {
                nav.Property(a => a.Name).HasMaxLength(200);
                nav.Property(a => a.Line1).HasMaxLength(200).IsRequired();
                nav.Property(a => a.Line2).HasMaxLength(200);
                nav.Property(a => a.City).HasMaxLength(100).IsRequired();
                nav.Property(a => a.State).HasMaxLength(50).IsRequired();
                nav.Property(a => a.PostalCode).HasMaxLength(20).IsRequired();
                nav.Property(a => a.Country).HasMaxLength(2).IsRequired();
                nav.Property(a => a.Phone).HasMaxLength(40);
            });

            e.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId);
            e.HasOne(o => o.Payment).WithOne().HasForeignKey<PaymentRecord>(p => p.OrderId);
            e.HasMany(o => o.Shipments).WithOne(s => s.Order).HasForeignKey(s => s.OrderId);
            e.HasMany(o => o.Events).WithOne().HasForeignKey(ev => ev.OrderId);
            e.HasOne(o => o.Return).WithOne(r => r.Order).HasForeignKey<ReturnRequest>(r => r.OrderId);
        });

        b.Entity<OrderLine>(e =>
        {
            e.Property(l => l.Sku).HasMaxLength(40).IsRequired();
            e.Property(l => l.Name).HasMaxLength(200).IsRequired();
            e.Property(l => l.UnitPrice).HasPrecision(12, 2);
            e.Property(l => l.LineTotal).HasPrecision(12, 2);
        });

        b.Entity<PaymentRecord>(e =>
        {
            e.Property(p => p.ProviderReference).HasMaxLength(200);
            e.Property(p => p.Amount).HasPrecision(12, 2);
            e.Property(p => p.RefundedAmount).HasPrecision(12, 2);
            e.Property(p => p.FailureReason).HasMaxLength(500);
        });

        b.Entity<Shipment>(e =>
        {
            e.Property(s => s.TrackingNumber).HasMaxLength(64).IsRequired();
            e.HasIndex(s => s.TrackingNumber);
            e.Property(s => s.CarrierName).HasMaxLength(100);
            e.Property(s => s.LabelCost).HasPrecision(12, 2);
        });

        b.Entity<OrderEvent>(e =>
        {
            e.Property(ev => ev.Actor).HasMaxLength(120);
            e.Property(ev => ev.Message).HasMaxLength(500);
            e.HasIndex(ev => new { ev.OrderId, ev.TimestampUtc });
        });

        b.Entity<Coupon>(e =>
        {
            e.Property(c => c.Code).HasMaxLength(16).IsRequired();
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.PercentOff).HasPrecision(5, 2);
            e.Property(c => c.MinSubtotal).HasPrecision(12, 2);
        });

        b.Entity<ReturnRequest>(e =>
        {
            e.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
            e.Property(r => r.AdminNotes).HasMaxLength(2000);
            e.Property(r => r.ClaimReference).HasMaxLength(100);
            e.Property(r => r.RefundAmount).HasPrecision(12, 2);
        });

        b.Entity<CarrierSetting>(e =>
        {
            e.Property(cs => cs.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(cs => cs.ApiBaseUrl).HasMaxLength(500);
            e.Property(cs => cs.AccountNumber).HasMaxLength(100);
            e.Property(cs => cs.TrackingUrlTemplate).HasMaxLength(500);
            e.Property(cs => cs.FallbackBaseRate).HasPrecision(12, 2);
            e.Property(cs => cs.FallbackPerKgRate).HasPrecision(12, 2);
        });
    }
}
