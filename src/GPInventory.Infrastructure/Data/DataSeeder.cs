using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed FlowTypes if they don't exist
        if (!await context.FlowTypes.AnyAsync())
        {
            context.FlowTypes.AddRange(
                new FlowType { Id = 1, Type = "entrada" },
                new FlowType { Id = 2, Type = "salida" }
            );
            await context.SaveChangesAsync();
        }

        // Seed Roles if they don't exist
        if (!await context.Roles.AnyAsync())
        {
            context.Roles.AddRange(
                new Role("Admin"),
                new Role("Manager"),
                new Role("Employee")
            );
            await context.SaveChangesAsync();
        }

        // Seed ProductTypes if they don't exist
        if (!await context.ProductTypes.AnyAsync())
        {
            context.ProductTypes.AddRange(
                new ProductType("Electronics"),
                new ProductType("Clothing"),
                new ProductType("Food"),
                new ProductType("Books"),
                new ProductType("Other")
            );
            await context.SaveChangesAsync();
        }

        // Seed a default business if none exist
        if (!await context.Businesses.AnyAsync())
        {
            context.Businesses.Add(new Business("Default Business", 1, "#007bff"));
            await context.SaveChangesAsync();
        }
    }
}
