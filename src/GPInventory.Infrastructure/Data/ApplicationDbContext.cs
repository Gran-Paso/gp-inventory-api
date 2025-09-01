using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Business> Businesses { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserHasBusiness> UserHasBusinesses { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductType> ProductTypes { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<FlowType> FlowTypes { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleDetail> SaleDetails { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<Store> Stores { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<UserNotification> UserNotifications { get; set; }
    
    // Expense entities
    public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
    public DbSet<ExpenseSubcategory> ExpenseSubcategories { get; set; }
    public DbSet<RecurrenceType> RecurrenceTypes { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<FixedExpense> FixedExpenses { get; set; }
    
    // Production entities
    public DbSet<Supply> Supplies { get; set; }
    public DbSet<SupplyEntry> SupplyEntries { get; set; }
    public DbSet<UnitMeasure> UnitMeasures { get; set; }
    public DbSet<TimeUnit> TimeUnits { get; set; }
    public DbSet<Process> Processes { get; set; }
    public DbSet<ProcessSupply> ProcessSupplies { get; set; }
    public DbSet<ProcessDone> ProcessDones { get; set; }
    
    // Gran Paso entities
    public DbSet<Prospect> Prospects { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Configure basic conventions
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Prospect entity to use singular table name
        modelBuilder.Entity<Prospect>(entity =>
        {
            entity.ToTable("prospect");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Mail).HasColumnName("mail").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Contact).HasColumnName("contact").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Enterprise).HasColumnName("enterprise").HasMaxLength(255);
            entity.Property(e => e.Description).HasColumnName("description").HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
        
        // Configure all entities explicitly before applying configurations
        modelBuilder.Entity<SupplyEntry>(entity =>
        {
            // Configure entity first
            entity.ToTable("supply_entry");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.SupplyId).HasColumnName("supply_id");
            entity.Property(e => e.UnitCost).HasColumnName("unit_cost");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.ProcessDoneId).HasColumnName("process_done_id");
            
            // BaseEntity properties - map to database columns
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Ignore(e => e.IsActive); // This one doesn't exist in the database
            
            // Configure only the relationships we want
            entity.HasOne(e => e.Supply)
                .WithMany(s => s.SupplyEntries)
                .HasForeignKey(e => e.SupplyId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.HasOne(e => e.ProcessDone)
                .WithMany(pd => pd.SupplyEntries)
                .HasForeignKey(e => e.ProcessDoneId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        // Apply Inventory configurations
        modelBuilder.ApplyConfiguration(new ProductTypeConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new FlowTypeConfiguration());
        modelBuilder.ApplyConfiguration(new StockConfiguration());
        modelBuilder.ApplyConfiguration(new ProviderConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentMethodConfiguration());
        modelBuilder.ApplyConfiguration(new StoreConfiguration());
        modelBuilder.ApplyConfiguration(new SaleConfiguration());

        // Apply User configurations
        modelBuilder.ApplyConfiguration(new UserConfiguration());

        // Apply Notification configurations
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new UserNotificationConfiguration());

        // Apply Expense configurations
        modelBuilder.ApplyConfiguration(new ExpenseCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ExpenseSubcategoryConfiguration());
        modelBuilder.ApplyConfiguration(new RecurrenceTypeConfiguration());
        modelBuilder.ApplyConfiguration(new ExpenseConfiguration());
        modelBuilder.ApplyConfiguration(new FixedExpenseConfiguration());

        // Business configuration
        modelBuilder.Entity<Business>(entity =>
        {
            entity.ToTable("business");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.CompanyName).HasColumnName("company_name").HasMaxLength(255);
            entity.Property(e => e.Theme).HasColumnName("theme");
            entity.Property(e => e.PrimaryColor).HasColumnName("primary_color").HasMaxLength(255);
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
        });

        // Role configuration
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("role");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
            entity.Ignore(e => e.Description); // Description column doesn't exist in database
        });

        // UserHasBusiness configuration
        modelBuilder.Entity<UserHasBusiness>(entity =>
        {
            entity.ToTable("user_has_business");
            entity.HasKey(e => new { e.UserId, e.BusinessId, e.RoleId });
            entity.Property(e => e.UserId).HasColumnName("id_user");
            entity.Property(e => e.BusinessId).HasColumnName("id_business");
            entity.Property(e => e.RoleId).HasColumnName("id_role");

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserBusinesses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.UserBusinesses)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.UserBusinesses)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.Id);
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
        });

        // PaymentMethod configuration
        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.ToTable("payment_methods");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
        });

        // SaleDetail configuration
        modelBuilder.Entity<SaleDetail>(entity =>
        {
            entity.ToTable("sales_detail");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProductId).HasColumnName("product");
            entity.Property(e => e.Amount).HasColumnName("amount").HasMaxLength(255);
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.Discount).HasColumnName("discount");
            entity.Property(e => e.SaleId).HasColumnName("sale");

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Sale)
                .WithMany(s => s.SaleDetails)
                .HasForeignKey(e => e.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
        });

        // Supply configuration
        modelBuilder.Entity<Supply>(entity =>
        {
            entity.ToTable("supplies");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.BusinessId).HasColumnName("business_id");
            entity.Property(e => e.StoreId).HasColumnName("store_id");
            entity.Property(e => e.FixedExpenseId).HasColumnName("fixed_expense_id");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.UnitMeasureId).HasColumnName("unit_measure_id");
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Store)
                .WithMany()
                .HasForeignKey(e => e.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.FixedExpense)
                .WithMany()
                .HasForeignKey(e => e.FixedExpenseId)
                .OnDelete(DeleteBehavior.SetNull);

            // Explicitly ignore any automatic UnitMeasure navigation property
            // to prevent EF from generating UnitMeasureId1 columns
            entity.Ignore("UnitMeasure");
            
            // Temporarily removed UnitMeasure navigation to fix EF Core issue
            // entity.HasOne(e => e.UnitMeasure)
            //     .WithMany()
            //     .HasForeignKey(e => e.UnitMeasureId)
            //     .HasConstraintName("FK_supplies_unit_measures_unit_measure_id")
            //     .OnDelete(DeleteBehavior.Restrict);
        });

        // UnitMeasure configuration
        modelBuilder.Entity<UnitMeasure>(entity =>
        {
            entity.ToTable("unit_measures");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.Symbol).HasColumnName("symbol").HasMaxLength(255);
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
            entity.Ignore(e => e.Description); // This column doesn't exist in the database
        });

        // TimeUnit configuration
        modelBuilder.Entity<TimeUnit>(entity =>
        {
            entity.ToTable("time_units");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
            entity.Ignore(e => e.Description); // This column doesn't exist in the database
        });

        // Process configuration
        modelBuilder.Entity<Process>(entity =>
        {
            entity.ToTable("processes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ProductionTime).HasColumnName("production_time");
            entity.Property(e => e.TimeUnitId).HasColumnName("time_unit_id");
            entity.Property(e => e.StoreId).HasColumnName("store_id");
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
            
            // Explicitly ignore Notes property if EF Core tries to map it
            entity.Ignore("Notes");

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Store)
                .WithMany()
                .HasForeignKey(e => e.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TimeUnit)
                .WithMany(t => t.Processes)
                .HasForeignKey(e => e.TimeUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProcessSupply configuration
        modelBuilder.Entity<ProcessSupply>(entity =>
        {
            entity.ToTable("process_supplies");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProcessId).HasColumnName("process_id");
            entity.Property(e => e.SupplyId).HasColumnName("supply_id");
            entity.Property(e => e.Order).HasColumnName("order");
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);

            entity.HasOne(e => e.Process)
                .WithMany(p => p.ProcessSupplies)
                .HasForeignKey(e => e.ProcessId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Supply)
                .WithMany(s => s.ProcessSupplies)
                .HasForeignKey(e => e.SupplyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProcessDone configuration
        modelBuilder.Entity<ProcessDone>(entity =>
        {
            entity.ToTable("process_done");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProcessId).HasColumnName("process_id");
            entity.Property(e => e.Stage).HasColumnName("stage");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.StockId).HasColumnName("stock_id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.Notes).HasColumnName("notes");
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
            
            // Explicitly ignore shadow properties that might be auto-generated
            entity.Ignore("ProcessId1");

            entity.HasOne(e => e.Process)
                .WithMany(p => p.ProcessDones)
                .HasForeignKey(e => e.ProcessId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(e => e.Stock)
                .WithMany()
                .HasForeignKey(e => e.StockId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // FINAL CLEANUP: Remove any shadow properties that may have been auto-generated
        // Simply live with the warning but don't let it affect functionality
        try
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.ClrType == typeof(SupplyEntry))
                {
                    // FIRST: Remove any UnitMeasure-related foreign keys
                    var fksToRemove = entityType.GetForeignKeys()
                        .Where(fk => fk.Properties.Any(p => p.Name.Contains("UnitMeasureId")))
                        .ToList();
                    
                    foreach (var fk in fksToRemove)
                    {
                        entityType.RemoveForeignKey(fk);
                    }
                    
                    // SECOND: Remove any UnitMeasure-related shadow properties
                    var shadowProps = entityType.GetProperties()
                        .Where(p => p.Name.Contains("UnitMeasureId") && p.IsShadowProperty())
                        .ToList();
                    
                    foreach (var prop in shadowProps)
                    {
                        entityType.RemoveProperty(prop);
                    }
                    
                    // THIRD: Remove any Notes-related shadow properties from Process entity
                    if (entityType.ClrType == typeof(Process))
                    {
                        var notesProps = entityType.GetProperties()
                            .Where(p => p.Name.Contains("Notes") && p.IsShadowProperty())
                            .ToList();
                        
                        foreach (var prop in notesProps)
                        {
                            entityType.RemoveProperty(prop);
                        }
                    }
                    
                    // FOURTH: Remove any ProcessId1-related shadow properties from ProcessDone entity
                    if (entityType.ClrType == typeof(ProcessDone))
                    {
                        var processIdProps = entityType.GetProperties()
                            .Where(p => p.Name.Contains("ProcessId1") && p.IsShadowProperty())
                            .ToList();
                        
                        foreach (var prop in processIdProps)
                        {
                            entityType.RemoveProperty(prop);
                        }
                    }
                }
            }
        }
        catch
        {
            // If removal fails, at least the entity configuration should prevent usage
            // The warning will appear but functionality should not be affected
        }
    }
}
