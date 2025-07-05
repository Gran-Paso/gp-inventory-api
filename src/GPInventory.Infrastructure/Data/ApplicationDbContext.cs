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
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.Cost).HasColumnName("cost");
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
            entity.Property(e => e.Type).HasColumnName("type").HasMaxLength(255);
            
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
            entity.Property(e => e.FlowId).HasColumnName("flow");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.AuctionPrice).HasColumnName("auction_price");

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Stocks)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Flow)
                .WithMany(f => f.Stocks)
                .HasForeignKey(e => e.FlowId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // BaseEntity properties - ignore since they don't exist in the database
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.IsActive);
        });

        // Seed data for FlowType
        modelBuilder.Entity<FlowType>().HasData(
            new FlowType { Id = 1, Type = "entrada" },
            new FlowType { Id = 2, Type = "salida" }
        );
    }
}
