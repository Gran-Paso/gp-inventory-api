using GPInventory.Application.Interfaces;
using GPInventory.Application.Mappings;
using GPInventory.Application.Services;
using GPInventory.Infrastructure.Data;
using GPInventory.Infrastructure.Repositories;
using GPInventory.Infrastructure.Services;
using GPInventory.Api.Middleware;
using GPInventory.Api.Converters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON to handle camelCase from frontend
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        
        // Add custom DateTime converters to preserve local time
        options.JsonSerializerOptions.Converters.Add(new LocalDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new LocalDateTimeConverterNonNullable());
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT Authentication
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "GP Inventory API", 
        Version = "v1",
        Description = "API para el sistema de inventario Gran Paso"
    });

    // Configure JWT Authentication for Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173", 
                "http://localhost:5174",  // GP Factory old
                "http://localhost:3000",  // Gran Paso main
                "http://localhost:3001",  // GP Factory
                "http://localhost:3002",  // GP Expenses
                "http://localhost:3003",  // GP Inventory  
                "http://localhost:3004",  // GP Auth
                "http://localhost:5175",  // Gran Paso website dev
                "https://localhost:5001", 
                "https://localhost:5173", 
                "http://localhost:5000",
                "https://inventory.granpasochile.cl",  // GP Inventory producción
                "https://expenses.granpasochile.cl",   // GP Expenses producción
                "https://factory.granpasochile.cl",    // GP Factory producción
                "https://auth.granpasochile.cl",       // GP Auth producción
                "https://granpasochile.cl",            // Gran Paso website producción
                "https://www.granpasochile.cl"         // Gran Paso website producción con www
               )
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials()
               .SetPreflightMaxAge(TimeSpan.FromMinutes(30)); // Cache preflight requests
    });
    
    // Política más permisiva para desarrollo
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");

if (useInMemoryDatabase)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("GPInventoryInMemory"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.Parse("8.0.33-mysql")));
}

// AutoMapper
builder.Services.AddAutoMapper(typeof(AuthMappingProfile), typeof(NotificationMappingProfile), typeof(ExpenseMappingProfile));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Repository pattern
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Expense repositories
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IFixedExpenseRepository, FixedExpenseRepository>();
builder.Services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
builder.Services.AddScoped<IExpenseSubcategoryRepository, ExpenseSubcategoryRepository>();
builder.Services.AddScoped<IRecurrenceTypeRepository, RecurrenceTypeRepository>();

// Production repositories
builder.Services.AddScoped<ISupplyRepository, SupplyRepository>();
builder.Services.AddScoped<IProcessRepository, ProcessRepository>();
builder.Services.AddScoped<IProcessDoneRepository, ProcessDoneRepository>();
builder.Services.AddScoped<IUnitMeasureRepository, UnitMeasureRepository>();
builder.Services.AddScoped<ISupplyEntryRepository, SupplyEntryRepository>();

// Gran Paso repositories
builder.Services.AddScoped<IProspectRepository, ProspectRepository>();

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Expense services
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();

// Production services
builder.Services.AddScoped<ISupplyService, SupplyService>();
builder.Services.AddScoped<IProcessService, ProcessService>();
builder.Services.AddScoped<IProcessDoneService, ProcessDoneService>();
builder.Services.AddScoped<IUnitMeasureService, UnitMeasureService>();
builder.Services.AddScoped<ISupplyEntryService, SupplyEntryService>();

// Gran Paso services
builder.Services.AddScoped<IProspectService, ProspectService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add logging middleware para debug de CORS
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    // Log request details for CORS debugging
    if (context.Request.Method == "OPTIONS")
    {
        var origin = context.Request.Headers["Origin"].ToString();
        logger.LogInformation("CORS Preflight request from origin: {Origin}", origin);
        logger.LogInformation("Request headers: {Headers}", string.Join(", ", context.Request.Headers.Keys));
    }
    
    await next();
    
    // Log response headers for CORS debugging
    if (context.Request.Method == "OPTIONS")
    {
        logger.LogInformation("CORS Response headers: {Headers}", 
            string.Join(", ", context.Response.Headers.Where(h => h.Key.StartsWith("Access-Control")).Select(h => $"{h.Key}: {h.Value}")));
    }
});

// Add error handling middleware
app.UseMiddleware<ErrorHandlingMiddleware>();

// CORS debe ir lo más arriba posible en el pipeline
app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

// Configure static files middleware for serving uploaded images
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed data
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DataSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "An error occurred while seeding the database. The application will continue to run, but some features may not work correctly.");
    }
}

app.Run();

// Make Program class public for integration tests
public partial class Program { }
