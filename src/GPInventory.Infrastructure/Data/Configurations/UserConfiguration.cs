using GPInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GPInventory.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
            
        builder.Property(e => e.Mail)
            .HasColumnName("mail")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.LastName)
            .HasColumnName("lastname")
            .HasMaxLength(255)
            .IsRequired();
            
        builder.Property(e => e.Gender)
            .HasColumnName("gender")
            .HasMaxLength(1);
            
        builder.Property(e => e.BirthDate)
            .HasColumnName("birthdate");
            
        builder.Property(e => e.Phone)
            .HasColumnName("phone");
            
        builder.Property(e => e.Password)
            .HasColumnName("password")
            .HasColumnType("text")
            .IsRequired();
            
        builder.Property(e => e.Salt)
            .HasColumnName("salt")
            .HasMaxLength(255);
            
        builder.Property(e => e.Active)
            .HasColumnName("active")
            .HasColumnType("bit(1)");
        
        builder.Property(e => e.SystemRole)
            .HasColumnName("system_role")
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("none");
        
        // BaseEntity properties - ignore since they don't exist in the database
        builder.Ignore(e => e.CreatedAt);
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.IsActive);
        
        // Indexes for performance
        builder.HasIndex(e => e.Mail).HasDatabaseName("IX_User_Mail").IsUnique();
        builder.HasIndex(e => e.Phone).HasDatabaseName("IX_User_Phone");
    }
}