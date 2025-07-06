using GPInventory.Domain.Entities;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("user");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Mail).HasColumnName("mail").HasMaxLength(255);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.LastName).HasColumnName("lastname").HasMaxLength(255);
            entity.Property(e => e.Gender).HasColumnName("gender").HasMaxLength(1);
            entity.Property(e => e.BirthDate).HasColumnName("birthdate");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.Password).HasColumnName("password").HasColumnType("text");
            entity.Property(e => e.Salt).HasColumnName("salt").HasMaxLength(255);
            entity.Property(e => e.Active).HasColumnName("active").HasColumnType("bit(1)");
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
        });

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

        // ProductType configuration
        modelBuilder.Entity<ProductType>(entity =>
        {
            entity.ToTable("product_type");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
            entity.Ignore(e => e.Description); // Description column doesn't exist in database
        });

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("product");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.Image).HasColumnName("image").HasMaxLength(255);
            entity.Property(e => e.ProductTypeId).HasColumnName("product_type");
            entity.Property(e => e.Price).HasColumnName("price").HasMaxLength(255);
            entity.Property(e => e.Cost).HasColumnName("cost").HasMaxLength(255);
            entity.Property(e => e.Sku).HasColumnName("sku").HasMaxLength(255);
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.BusinessId).HasColumnName("business");

            entity.HasOne(e => e.ProductType)
                .WithMany(pt => pt.Products)
                .HasForeignKey(e => e.ProductTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Products)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
        });

        // FlowType configuration
        modelBuilder.Entity<FlowType>(entity =>
        {
            entity.ToTable("flow_type");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("type").HasMaxLength(100);
            
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
        });

        // Stock configuration
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("stock");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProductId).HasColumnName("product");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.FlowTypeId).HasColumnName("flow");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.AuctionPrice).HasColumnName("auction_price");
            entity.Property(e => e.Cost).HasColumnName("cost");
            entity.Property(e => e.ProviderId).HasColumnName("provider");
            entity.Property(e => e.Notes).HasColumnName("notes");

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Stocks)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.FlowType)
                .WithMany(f => f.Stocks)
                .HasForeignKey(e => e.FlowTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Provider)
                .WithMany(p => p.StockMovements)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
        });

        // Seed data for FlowType
        modelBuilder.Entity<FlowType>().HasData(
            new FlowType("Compra") { Id = 1 },
            new FlowType("Venta") { Id = 2 },
            new FlowType("Producción") { Id = 3 },
            new FlowType("Devolución") { Id = 4 },
            new FlowType("Ajuste Positivo") { Id = 5 },
            new FlowType("Ajuste Negativo") { Id = 6 },
            new FlowType("Merma") { Id = 7 },
            new FlowType("Transferencia") { Id = 8 }
        );
        
        // Provider configuration
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ToTable("provider");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.BusinessId).HasColumnName("business");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // BaseEntity properties - ignore since they don't exist in the database
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

        // Sale configuration
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.BusinessId).HasColumnName("business");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.CustomerName).HasColumnName("customer_name").HasMaxLength(255);
            entity.Property(e => e.CustomerRut).HasColumnName("customer_rut").HasMaxLength(255);
            entity.Property(e => e.Total).HasColumnName("total");
            entity.Property(e => e.PaymentMethodId).HasColumnName("payment_method");
            entity.Property(e => e.Notes).HasColumnName("notes").HasColumnType("text");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PaymentMethod)
                .WithMany(pm => pm.Sales)
                .HasForeignKey(e => e.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
                
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
