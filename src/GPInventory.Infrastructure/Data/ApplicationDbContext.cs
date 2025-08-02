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
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductType> ProductTypes { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<FlowType> FlowTypes { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleDetail> SaleDetails { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<Store> Stores { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
    }
}
