using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Entities.Notifications;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BuildingManagement.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<ServiceRequestAttachment> ServiceRequestAttachments => Set<ServiceRequestAttachment>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderNote> WorkOrderNotes => Set<WorkOrderNote>();
    public DbSet<WorkOrderAttachment> WorkOrderAttachments => Set<WorkOrderAttachment>();
    public DbSet<PreventivePlan> PreventivePlans => Set<PreventivePlan>();
    public DbSet<CleaningPlan> CleaningPlans => Set<CleaningPlan>();
    public DbSet<JobRunLog> JobRunLogs => Set<JobRunLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BuildingManager> BuildingManagers => Set<BuildingManager>();
    public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();

    // Finance
    public DbSet<HOAFeePlan> HOAFeePlans => Set<HOAFeePlan>();
    public DbSet<UnitCharge> UnitCharges => Set<UnitCharge>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<PaymentProviderConfig> PaymentProviderConfigs => Set<PaymentProviderConfig>();
    public DbSet<WebhookEventLog> WebhookEventLogs => Set<WebhookEventLog>();
    public DbSet<VendorInvoice> VendorInvoices => Set<VendorInvoice>();
    public DbSet<VendorPayment> VendorPayments => Set<VendorPayment>();
    public DbSet<StandingOrder> StandingOrders => Set<StandingOrder>();

    // Notifications
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<SmsCampaign> SmsCampaigns => Set<SmsCampaign>();
    public DbSet<SmsCampaignRecipient> SmsCampaignRecipients => Set<SmsCampaignRecipient>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Soft-delete query filter for BaseEntity derived types
        builder.Entity<Building>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Unit>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Vendor>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Asset>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ServiceRequest>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<WorkOrder>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PreventivePlan>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CleaningPlan>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<TenantProfile>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<VendorInvoice>().HasQueryFilter(e => !e.IsDeleted);

        // Building -> Units
        builder.Entity<Unit>()
            .HasOne(u => u.Building)
            .WithMany(b => b.Units)
            .HasForeignKey(u => u.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Unit>()
            .HasOne(u => u.TenantUser)
            .WithMany(au => au.TenantUnits)
            .HasForeignKey(u => u.TenantUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // BuildingManager join table
        builder.Entity<BuildingManager>()
            .HasOne(bm => bm.User)
            .WithMany(u => u.ManagedBuildings)
            .HasForeignKey(bm => bm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BuildingManager>()
            .HasOne(bm => bm.Building)
            .WithMany()
            .HasForeignKey(bm => bm.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BuildingManager>()
            .HasIndex(bm => new { bm.UserId, bm.BuildingId }).IsUnique();

        // Asset -> Building, Vendor
        builder.Entity<Asset>()
            .HasOne(a => a.Building)
            .WithMany(b => b.Assets)
            .HasForeignKey(a => a.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Asset>()
            .HasOne(a => a.Vendor)
            .WithMany(v => v.Assets)
            .HasForeignKey(a => a.VendorId)
            .OnDelete(DeleteBehavior.SetNull);

        // ServiceRequest relationships
        builder.Entity<ServiceRequest>()
            .HasOne(sr => sr.Building)
            .WithMany(b => b.ServiceRequests)
            .HasForeignKey(sr => sr.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ServiceRequest>()
            .HasOne(sr => sr.Unit)
            .WithMany()
            .HasForeignKey(sr => sr.UnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ServiceRequest>()
            .HasOne(sr => sr.SubmittedByUser)
            .WithMany()
            .HasForeignKey(sr => sr.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ServiceRequestAttachment>()
            .HasOne(a => a.ServiceRequest)
            .WithMany(sr => sr.Attachments)
            .HasForeignKey(a => a.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // WorkOrder relationships
        builder.Entity<WorkOrder>()
            .HasOne(wo => wo.Building)
            .WithMany(b => b.WorkOrders)
            .HasForeignKey(wo => wo.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<WorkOrder>()
            .HasOne(wo => wo.ServiceRequest)
            .WithMany(sr => sr.WorkOrders)
            .HasForeignKey(wo => wo.ServiceRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        // One work order per service request
        builder.Entity<WorkOrder>()
            .HasIndex(wo => wo.ServiceRequestId)
            .IsUnique()
            .HasFilter("[ServiceRequestId] IS NOT NULL AND [IsDeleted] = 0");

        builder.Entity<WorkOrder>()
            .HasOne(wo => wo.Vendor)
            .WithMany(v => v.WorkOrders)
            .HasForeignKey(wo => wo.VendorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<WorkOrderNote>()
            .HasOne(n => n.WorkOrder)
            .WithMany(wo => wo.Notes)
            .HasForeignKey(n => n.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<WorkOrderNote>()
            .HasOne(n => n.CreatedByUser)
            .WithMany()
            .HasForeignKey(n => n.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<WorkOrderAttachment>()
            .HasOne(a => a.WorkOrder)
            .WithMany(wo => wo.Attachments)
            .HasForeignKey(a => a.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // PreventivePlan -> Asset
        builder.Entity<PreventivePlan>()
            .HasOne(pp => pp.Asset)
            .WithMany(a => a.PreventivePlans)
            .HasForeignKey(pp => pp.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        // CleaningPlan -> Building, Vendor
        builder.Entity<CleaningPlan>()
            .HasOne(cp => cp.Building)
            .WithMany(b => b.CleaningPlans)
            .HasForeignKey(cp => cp.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CleaningPlan>()
            .HasOne(cp => cp.CleaningVendor)
            .WithMany()
            .HasForeignKey(cp => cp.CleaningVendorId)
            .OnDelete(DeleteBehavior.Restrict);

        // JobRunLog unique constraint
        builder.Entity<JobRunLog>()
            .HasIndex(j => new { j.JobName, j.PeriodKey }).IsUnique();

        // ApplicationUser -> Vendor
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Vendor)
            .WithMany()
            .HasForeignKey(u => u.VendorId)
            .OnDelete(DeleteBehavior.SetNull);

        // ─── TenantProfile ───────────────────────────────────

        builder.Entity<TenantProfile>()
            .HasOne(tp => tp.Unit)
            .WithMany()
            .HasForeignKey(tp => tp.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TenantProfile>()
            .HasOne(tp => tp.User)
            .WithMany()
            .HasForeignKey(tp => tp.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<TenantProfile>()
            .HasIndex(tp => new { tp.UnitId, tp.IsActive });

        // ─── Finance Entities ────────────────────────────────

        builder.Entity<HOAFeePlan>()
            .HasOne(h => h.Building)
            .WithMany()
            .HasForeignKey(h => h.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<HOAFeePlan>()
            .Property(h => h.AmountPerSqm).HasColumnType("decimal(18,2)");
        builder.Entity<HOAFeePlan>()
            .Property(h => h.FixedAmountPerUnit).HasColumnType("decimal(18,2)");

        builder.Entity<UnitCharge>()
            .HasOne(uc => uc.Unit)
            .WithMany()
            .HasForeignKey(uc => uc.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UnitCharge>()
            .HasOne(uc => uc.HOAFeePlan)
            .WithMany(h => h.UnitCharges)
            .HasForeignKey(uc => uc.HOAFeePlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UnitCharge>()
            .HasIndex(uc => new { uc.UnitId, uc.HOAFeePlanId, uc.Period }).IsUnique();

        builder.Entity<PaymentMethod>()
            .HasOne(pm => pm.User)
            .WithMany()
            .HasForeignKey(pm => pm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Payment>()
            .HasOne(p => p.Unit)
            .WithMany()
            .HasForeignKey(p => p.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Payment>()
            .HasOne(p => p.PaymentMethod)
            .WithMany()
            .HasForeignKey(p => p.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<PaymentAllocation>()
            .HasOne(pa => pa.Payment)
            .WithMany(p => p.Allocations)
            .HasForeignKey(pa => pa.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentAllocation>()
            .HasOne(pa => pa.UnitCharge)
            .WithMany(uc => uc.Allocations)
            .HasForeignKey(pa => pa.UnitChargeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LedgerEntry>()
            .HasOne(le => le.Building)
            .WithMany()
            .HasForeignKey(le => le.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LedgerEntry>()
            .HasOne(le => le.Unit)
            .WithMany()
            .HasForeignKey(le => le.UnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // PaymentProviderConfig
        builder.Entity<PaymentProviderConfig>()
            .HasQueryFilter(e => !e.IsDeleted);

        builder.Entity<PaymentProviderConfig>()
            .HasOne(c => c.Building)
            .WithMany()
            .HasForeignKey(c => c.BuildingId)
            .OnDelete(DeleteBehavior.SetNull);

        // WebhookEventLog unique on provider+eventId
        builder.Entity<WebhookEventLog>()
            .HasIndex(w => new { w.ProviderType, w.EventId }).IsUnique();

        // ─── Vendor Invoices & Payments ───────────────────────

        builder.Entity<VendorInvoice>()
            .HasOne(vi => vi.Building)
            .WithMany()
            .HasForeignKey(vi => vi.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VendorInvoice>()
            .HasOne(vi => vi.Vendor)
            .WithMany()
            .HasForeignKey(vi => vi.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VendorInvoice>()
            .HasOne(vi => vi.WorkOrder)
            .WithMany()
            .HasForeignKey(vi => vi.WorkOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<VendorInvoice>()
            .Property(vi => vi.Amount).HasColumnType("decimal(18,2)");

        builder.Entity<VendorPayment>()
            .HasOne(vp => vp.VendorInvoice)
            .WithMany(vi => vi.Payments)
            .HasForeignKey(vp => vp.VendorInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<VendorPayment>()
            .Property(vp => vp.PaidAmount).HasColumnType("decimal(18,2)");

        // ─── SMS Notifications ───────────────────────────────

        builder.Entity<SmsCampaign>()
            .HasOne(c => c.Building)
            .WithMany()
            .HasForeignKey(c => c.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SmsCampaign>()
            .HasOne(c => c.Template)
            .WithMany()
            .HasForeignKey(c => c.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SmsCampaignRecipient>()
            .HasOne(r => r.Campaign)
            .WithMany(c => c.Recipients)
            .HasForeignKey(r => r.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── Standing Orders ──────────────────────────────────

        builder.Entity<StandingOrder>()
            .HasOne(so => so.User)
            .WithMany()
            .HasForeignKey(so => so.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StandingOrder>()
            .HasOne(so => so.Unit)
            .WithMany()
            .HasForeignKey(so => so.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StandingOrder>()
            .HasOne(so => so.Building)
            .WithMany()
            .HasForeignKey(so => so.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StandingOrder>()
            .Property(so => so.Amount).HasColumnType("decimal(18,2)");

        builder.Entity<StandingOrder>()
            .HasIndex(so => new { so.UserId, so.UnitId, so.Status });
    }

    public override int SaveChanges()
    {
        SetAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditFields()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
    }
}
