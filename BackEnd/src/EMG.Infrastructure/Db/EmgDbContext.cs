using EMG.Domain.Entities;
using EMG.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace EMG.Infrastructure.Db;

public sealed class EmgDbContext(DbContextOptions<EmgDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserProjectAssignment> UserProjectAssignments => Set<UserProjectAssignment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierSupply> SupplierSupplies => Set<SupplierSupply>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<SupplierInvoiceLine> SupplierInvoiceLines => Set<SupplierInvoiceLine>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<SiteMovement> SiteMovements => Set<SiteMovement>();
    public DbSet<ExcavationRecord> ExcavationRecords => Set<ExcavationRecord>();
    public DbSet<ConcreteRecord> ConcreteRecords => Set<ConcreteRecord>();
    public DbSet<SteelRecord> SteelRecords => Set<SteelRecord>();
    public DbSet<FinishingRecord> FinishingRecords => Set<FinishingRecord>();
    public DbSet<AdvancePayment> AdvancePayments => Set<AdvancePayment>();
    public DbSet<AdvanceAllocation> AdvanceAllocations => Set<AdvanceAllocation>();
    public DbSet<WorkflowAction> WorkflowActions => Set<WorkflowAction>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<HistoricalImportBatch> HistoricalImportBatches => Set<HistoricalImportBatch>();
    public DbSet<HistoricalImportRecord> HistoricalImportRecords => Set<HistoricalImportRecord>();
    public DbSet<HistoricalImportAuditEvent> HistoricalImportAuditEvents => Set<HistoricalImportAuditEvent>();
    public DbSet<HistoricalPostedEntry> HistoricalPostedEntries => Set<HistoricalPostedEntry>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<ContractorContract> ContractorContracts => Set<ContractorContract>();
    public DbSet<ContractorContractItem> ContractorContractItems => Set<ContractorContractItem>();
    public DbSet<ContractorStatement> ContractorStatements => Set<ContractorStatement>();
    public DbSet<ContractorStatementLine> ContractorStatementLines => Set<ContractorStatementLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Name).HasMaxLength(250).IsRequired();
            entity.Property(project => project.Code).HasMaxLength(80).IsRequired();
            entity.HasIndex(project => project.Code).IsUnique();
            entity.Property(project => project.ClientName).HasMaxLength(250);
            entity.Property(project => project.OwnerName).HasMaxLength(250);
            entity.Property(project => project.PayerName).HasMaxLength(250);
            entity.Property(project => project.Location).HasMaxLength(250);
            entity.Property(project => project.Status).HasMaxLength(80).IsRequired();
            entity.Property(project => project.EngineerName).HasMaxLength(250);
            entity.Property(project => project.OfficeSharePercentage).HasPrecision(5, 2);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.UserName).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.UserName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(250).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(250).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<AuthSession>(entity =>
        {
            entity.ToTable("auth_sessions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.UserId, item.RevokedAtUtc, item.ExpiresAtUtc });
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(role => role.Id);
            entity.HasIndex(role => role.Name).IsUnique();
            entity.Property(role => role.Name).HasMaxLength(100).IsRequired();
            entity.Property(role => role.DisplayName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(permission => permission.Id);
            entity.HasIndex(permission => permission.Code).IsUnique();
            entity.Property(permission => permission.Code).HasMaxLength(150).IsRequired();
            entity.Property(permission => permission.DisplayName).HasMaxLength(250).IsRequired();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(item => new { item.UserId, item.RoleId });
            entity.HasOne(item => item.User).WithMany(user => user.UserRoles).HasForeignKey(item => item.UserId);
            entity.HasOne(item => item.Role).WithMany(role => role.UserRoles).HasForeignKey(item => item.RoleId);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(item => new { item.RoleId, item.PermissionId });
            entity.Property(item => item.Scope).HasMaxLength(40).IsRequired().HasDefaultValue("all");
            entity.Property(item => item.AmountLimit).HasPrecision(18, 2);
            entity.HasOne(item => item.Role).WithMany(role => role.RolePermissions).HasForeignKey(item => item.RoleId);
            entity.HasOne(item => item.Permission).WithMany(permission => permission.RolePermissions).HasForeignKey(item => item.PermissionId);
        });

        modelBuilder.Entity<UserProjectAssignment>(entity =>
        {
            entity.ToTable("user_project_assignments");
            entity.HasKey(item => new { item.UserId, item.ProjectId });
            entity.HasOne(item => item.User).WithMany(user => user.ProjectAssignments).HasForeignKey(item => item.UserId);
            entity.HasIndex(item => item.ProjectId);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(250).IsRequired();
            entity.Property(item => item.SupplierType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(80);
            entity.Property(item => item.TaxNumber).HasMaxLength(100);
            entity.Property(item => item.Notes).HasMaxLength(2000);
        });

        modelBuilder.Entity<SupplierSupply>(entity =>
        {
            entity.ToTable("supplier_supplies");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SupplyType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ItemName).HasMaxLength(300).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.InvoiceNumber).HasMaxLength(120);
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired().HasDefaultValue("مسودة");
            entity.HasOne(item => item.Supplier).WithMany(supplier => supplier.Supplies).HasForeignKey(item => item.SupplierId);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Invoice).WithMany(invoice => invoice.Supplies).HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.SupplierId, item.ProjectId, item.SupplyDate });
        });

        modelBuilder.Entity<SupplierPayment>(entity =>
        {
            entity.ToTable("supplier_payments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.PaymentMethod).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Reference).HasMaxLength(150);
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired().HasDefaultValue("مسودة");
            entity.HasOne(item => item.Supplier).WithMany(supplier => supplier.Payments).HasForeignKey(item => item.SupplierId);
            entity.HasOne(item => item.Project).WithMany(project => project.SupplierPayments).HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.Supply).WithMany(supply => supply.Payments).HasForeignKey(item => item.SupplyId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.Invoice).WithMany(invoice => invoice.Payments).HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.SupplierId, item.ProjectId, item.PaymentDate });
        });

        modelBuilder.Entity<SupplierInvoice>(entity =>
        {
            entity.ToTable("supplier_invoices");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.InvoiceNumber).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Currency).HasMaxLength(10).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired();
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Supplier).WithMany().HasForeignKey(item => item.SupplierId);
            entity.HasOne(item => item.SiteMovement).WithMany().HasForeignKey(item => item.SiteMovementId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.ProjectId, item.InvoiceNumber }).IsUnique();
            entity.HasIndex(item => new { item.SupplierId, item.ProjectId, item.InvoiceDate });
        });

        modelBuilder.Entity<SupplierInvoiceLine>(entity =>
        {
            entity.ToTable("supplier_invoice_lines");
            entity.HasKey(item => item.Id);
            entity.HasOne(item => item.Invoice).WithMany(invoice => invoice.Lines).HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.SupplierSupply).WithMany().HasForeignKey(item => item.SupplierSupplyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.SupplierSupplyId).IsUnique();
        });

        modelBuilder.Entity<Contractor>(entity =>
        {
            entity.ToTable("contractors");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(250).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(80);
            entity.Property(item => item.TaxNumber).HasMaxLength(100);
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.HasIndex(item => item.Name);
        });

        modelBuilder.Entity<ContractorContract>(entity =>
        {
            entity.ToTable("contractor_contracts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ContractNumber).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Currency).HasMaxLength(10).IsRequired();
            entity.Property(item => item.Terms).HasMaxLength(4000);
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired().HasDefaultValue("مسودة");
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Contractor).WithMany().HasForeignKey(item => item.ContractorId);
            entity.HasIndex(item => new { item.ProjectId, item.ContractNumber }).IsUnique();
        });

        modelBuilder.Entity<ContractorContractItem>(entity =>
        {
            entity.ToTable("contractor_contract_items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ApprovedQuantity).HasPrecision(18, 3);
            entity.Property(item => item.UnitRate).HasPrecision(18, 2);
            entity.Property(item => item.SourceReference).HasMaxLength(500);
            entity.HasOne(item => item.Contract).WithMany(contract => contract.Items).HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.ContractId);
        });

        modelBuilder.Entity<ContractorStatement>(entity =>
        {
            entity.ToTable("contractor_statements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Currency).HasMaxLength(10).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired().HasDefaultValue("مسودة");
            entity.Property(item => item.GrossAmount).HasPrecision(18, 2);
            entity.Property(item => item.PreviousPaymentsAmount).HasPrecision(18, 2);
            entity.Property(item => item.DeductionsAmount).HasPrecision(18, 2);
            entity.Property(item => item.NetAmount).HasPrecision(18, 2);
            entity.Property(item => item.SourceReference).HasMaxLength(500);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Contractor).WithMany().HasForeignKey(item => item.ContractorId);
            entity.HasOne(item => item.Contract).WithMany(contract => contract.Statements).HasForeignKey(item => item.ContractId);
            entity.HasIndex(item => new { item.ProjectId, item.ContractorId, item.PeriodFrom, item.PeriodTo });
        });

        modelBuilder.Entity<ContractorStatementLine>(entity =>
        {
            entity.ToTable("contractor_statement_lines");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceEntityType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.UnitRate).HasPrecision(18, 2);
            entity.Property(item => item.GrossAmount).HasPrecision(18, 2);
            entity.Property(item => item.SourceReference).HasMaxLength(500).IsRequired();
            entity.HasOne(item => item.Statement).WithMany(statement => statement.Lines).HasForeignKey(item => item.StatementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ContractItem).WithMany().HasForeignKey(item => item.ContractItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.SourceEntityType, item.SourceEntityId, item.ContractItemId });
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.ToTable("attachments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.OriginalFileName).HasMaxLength(500).IsRequired();
            entity.Property(item => item.StoredFileName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.RelativePath).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.EntityType, item.EntityId });
        });

        modelBuilder.Entity<SiteMovement>(entity =>
        {
            entity.ToTable("site_movements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MovementType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Unit).HasMaxLength(80);
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Reference).HasMaxLength(150);
            entity.Property(item => item.Details).HasMaxLength(4000);
            entity.Property(item => item.ExpenseCategory).HasMaxLength(150);
            entity.Property(item => item.PaidTo).HasMaxLength(250);
            entity.Property(item => item.PaymentMethod).HasMaxLength(100);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Supplier).WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.ProjectId, item.MovementDate, item.MovementType });
        });

        modelBuilder.Entity<ExcavationRecord>(entity =>
        {
            entity.ToTable("excavation_records");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AccountCategory).HasMaxLength(120).IsRequired().HasDefaultValue("العرب");
            entity.Property(item => item.CalculationMethod).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CalculatedQuantity).HasPrecision(18, 3);
            entity.Property(item => item.Unit).HasMaxLength(80).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.SoilType).HasMaxLength(150);
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Supplier).WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.ProjectId, item.ExcavationDate });
        });

        modelBuilder.Entity<ConcreteRecord>(entity =>
        {
            entity.ToTable("concrete_records");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AccountCategory).HasMaxLength(120).IsRequired().HasDefaultValue("المقاول");
            entity.Property(item => item.ConcreteType).HasMaxLength(120).IsRequired();
            entity.Property(item => item.ElementType).HasMaxLength(150).IsRequired();
            entity.Property(item => item.MixGrade).HasMaxLength(100);
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.Unit).HasMaxLength(80).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.DeliveryNoteNumber).HasMaxLength(150);
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Supplier).WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.ProjectId, item.PourDate });
        });

        modelBuilder.Entity<SteelRecord>(entity =>
        {
            entity.ToTable("steel_records");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AccountCategory).HasMaxLength(120).IsRequired().HasDefaultValue("الحديد");
            entity.Property(item => item.OperationType).HasMaxLength(120).IsRequired();
            entity.Property(item => item.SteelType).HasMaxLength(120);
            entity.Property(item => item.BarDiameter).HasMaxLength(80);
            entity.Property(item => item.Grade).HasMaxLength(80);
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.Unit).HasMaxLength(80).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.DeliveryNoteNumber).HasMaxLength(150);
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Supplier).WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.ProjectId, item.OperationDate });
        });

        modelBuilder.Entity<FinishingRecord>(entity =>
        {
            entity.ToTable("finishing_records");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AccountCategory).HasMaxLength(120).IsRequired().HasDefaultValue("غير محدد");
            entity.Property(item => item.WorkType).HasMaxLength(150).IsRequired();
            entity.Property(item => item.AreaName).HasMaxLength(150);
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.Unit).HasMaxLength(80).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.TotalAmount).HasPrecision(18, 2);
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Supplier).WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.ProjectId, item.WorkDate });
        });

        modelBuilder.Entity<AdvancePayment>(entity =>
        {
            entity.ToTable("advance_payments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ClientName).HasMaxLength(250).IsRequired();
            entity.Property(item => item.PayerName).HasMaxLength(250).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.PaymentMethod).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Reference).HasMaxLength(150);
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasIndex(item => new { item.ProjectId, item.PaymentDate });
        });

        modelBuilder.Entity<AdvanceAllocation>(entity =>
        {
            entity.ToTable("advance_allocations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AccountCategory).HasMaxLength(120).IsRequired().HasDefaultValue("غير محدد");
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasOne(item => item.AdvancePayment).WithMany(payment => payment.Allocations).HasForeignKey(item => item.AdvancePaymentId);
            entity.HasOne(item => item.SiteMovement).WithMany().HasForeignKey(item => item.SiteMovementId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.SupplierPayment).WithMany().HasForeignKey(item => item.SupplierPaymentId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => item.AdvancePaymentId);
        });

        modelBuilder.Entity<WorkflowAction>(entity =>
        {
            entity.ToTable("workflow_actions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(100).IsRequired();
            entity.Property(item => item.FromStatus).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ToStatus).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.HasIndex(item => new { item.EntityType, item.EntityId, item.ActionDateUtc });
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("attendance_records");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AttendanceDate).HasColumnType("date");
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId);
            entity.HasIndex(item => new { item.UserId, item.AttendanceDate }).IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Type).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.EntityType).HasMaxLength(100);
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId);
            entity.HasIndex(item => new { item.UserId, item.IsRead, item.CreatedAtUtc });
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.TokenHash }).IsUnique();
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId);
        });

        modelBuilder.Entity<SecurityAuditLog>(entity =>
        {
            entity.ToTable("security_audit_logs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).HasMaxLength(100).IsRequired();
            entity.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Details).HasMaxLength(4000).IsRequired();
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.EntityType, item.EntityId, item.CreatedAtUtc });
        });

        modelBuilder.Entity<HistoricalImportBatch>(entity =>
        {
            entity.ToTable("historical_import_batches");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceSha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourceFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MappingVersion).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SourceApprovalReference).HasMaxLength(500);
            entity.Property(item => item.RejectionReason).HasMaxLength(2000);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasIndex(item => new { item.ProjectId, item.SourceSha256 }).IsUnique();
        });

        modelBuilder.Entity<HistoricalImportRecord>(entity =>
        {
            entity.ToTable("historical_import_records");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DeterministicIdentity).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SectionId).HasMaxLength(80).IsRequired();
            entity.Property(item => item.SheetName).HasMaxLength(250).IsRequired();
            entity.Property(item => item.SourceCoordinate).HasMaxLength(500).IsRequired();
            entity.Property(item => item.SourceCellsJson).HasColumnType("text").IsRequired();
            entity.Property(item => item.MappingFamily).HasMaxLength(80).IsRequired();
            entity.Property(item => item.MappingVersion).HasMaxLength(80).IsRequired();
            entity.Property(item => item.BusinessEventType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.OriginalAmount).HasPrecision(18, 2);
            entity.Property(item => item.Currency).HasMaxLength(10).IsRequired();
            entity.Property(item => item.SemanticValidationState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.LifecycleStatus).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ValueFingerprint).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ExclusionReason).HasMaxLength(2000);
            entity.HasOne(item => item.Batch).WithMany(batch => batch.Records).HasForeignKey(item => item.BatchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.PostedEntry).WithOne(entry => entry.HistoricalRecord).HasForeignKey<HistoricalPostedEntry>(entry => entry.HistoricalRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.DeterministicIdentity).IsUnique();
            entity.HasIndex(item => new { item.BatchId, item.LifecycleStatus });
        });

        modelBuilder.Entity<HistoricalImportAuditEvent>(entity =>
        {
            entity.ToTable("historical_import_audit_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).HasMaxLength(80).IsRequired();
            entity.Property(item => item.DetailsJson).HasColumnType("text").IsRequired();
            entity.HasOne(item => item.Batch).WithMany(batch => batch.AuditEvents).HasForeignKey(item => item.BatchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Record).WithMany().HasForeignKey(item => item.RecordId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.BatchId, item.CreatedAtUtc });
        });

        modelBuilder.Entity<HistoricalPostedEntry>(entity =>
        {
            entity.ToTable("historical_posted_entries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DeterministicIdentity).HasMaxLength(64).IsRequired();
            entity.Property(item => item.LedgerStream).HasMaxLength(100).IsRequired();
            entity.Property(item => item.BusinessEventType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Currency).HasMaxLength(10).IsRequired();
            entity.Property(item => item.SourceSha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ProvenanceJson).HasColumnType("text").IsRequired();
            entity.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId);
            entity.HasOne(item => item.Batch).WithMany().HasForeignKey(item => item.BatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.HistoricalRecord).WithOne(record => record.PostedEntry).HasForeignKey<HistoricalPostedEntry>(item => item.HistoricalRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.DeterministicIdentity).IsUnique();
            entity.HasIndex(item => new { item.ProjectId, item.Currency, item.LedgerStream });
        });
    }
}
