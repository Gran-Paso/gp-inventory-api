using GPInventory.Application.Interfaces;
using GPInventory.Application.Mappings;
using GPInventory.Application.Services;
using GPInventory.Infrastructure.Data;
using GPInventory.Infrastructure.Repositories;
using GPInventory.Infrastructure.Services;
using GPInventory.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on all network interfaces
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8080); // Listen on 0.0.0.0:8080
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON to handle camelCase from frontend
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
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
                "http://localhost:5174",  // GP Factory - Puerto específico
                "http://localhost:3000",  // Gran Paso main
                "http://localhost:3001",  // GP Factory
                "http://localhost:3002",  // GP Expenses
                "http://localhost:3003",  // GP Inventory  
                "http://localhost:3004",  // GP Auth
                "http://localhost:3005",  // GP Admin
                "http://localhost:5175",  // Gran Paso website dev
                "http://localhost:4173",  // Vite preview mode
                "http://localhost:4174",  // Vite preview mode alternate
                "https://localhost:5001", 
                "https://localhost:5173", 
                "https://localhost:5174", // GP Factory HTTPS
                "https://localhost:4173", // Vite preview HTTPS
                "http://localhost:8080",
                "http://127.0.0.1:5173",  // Local IP variant
                "http://127.0.0.1:8080",  // Local IP variant
                "http://192.168.1.83:8080", // Local network for mobile testing
                // Producción
                "https://inventory.granpasochile.cl",  // GP Inventory producción
                "https://expenses.granpasochile.cl",   // GP Expenses producción
                "https://factory.granpasochile.cl",    // GP Factory producción
                "https://auth.granpasochile.cl",       // GP Auth producción
                "https://admin.granpasochile.cl",      // GP Admin producción
                "https://granpasochile.cl",            // Gran Paso website producción
                "https://www.granpasochile.cl",        // Gran Paso website producción con www
                // QA
                "https://qa.inventory.granpasochile.cl",  // GP Inventory QA
                "https://qa.expenses.granpasochile.cl",   // GP Expenses QA
                "https://qa.factory.granpasochile.cl",    // GP Factory QA
                "https://qa.auth.granpasochile.cl",       // GP Auth QA
                "https://qa.admin.granpasochile.cl",      // GP Admin QA
                // Dev
                "https://dev.inventory.granpasochile.cl",  // GP Inventory Dev
                "https://dev.expenses.granpasochile.cl",   // GP Expenses Dev
                "https://dev.factory.granpasochile.cl",    // GP Factory Dev
                "https://dev.auth.granpasochile.cl",       // GP Auth Dev
                "https://dev.admin.granpasochile.cl"       // GP Admin Dev
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
builder.Services.AddAutoMapper(typeof(AuthMappingProfile), typeof(NotificationMappingProfile), typeof(ExpenseMappingProfile), typeof(PaymentMappingProfile));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

// Deshabilitar el mapeo automático de claims para preservar los nombres originales
Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

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
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "email" // Usar email como identificador principal
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
builder.Services.AddScoped<IExpenseSqlRepository, ExpenseSqlRepository>();
builder.Services.AddScoped<IFixedExpenseRepository, FixedExpenseRepository>();
builder.Services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
builder.Services.AddScoped<IExpenseSubcategoryRepository, ExpenseSubcategoryRepository>();
builder.Services.AddScoped<IRecurrenceTypeRepository, RecurrenceTypeRepository>();
builder.Services.AddScoped<IBudgetRepository, BudgetRepository>();

// Payment repositories
builder.Services.AddScoped<IPaymentCatalogRepository, PaymentCatalogRepository>();
builder.Services.AddScoped<IPaymentPlanRepository, PaymentPlanRepository>();
builder.Services.AddScoped<IPaymentInstallmentRepository, PaymentInstallmentRepository>();
builder.Services.AddScoped<IInstallmentDocumentRepository, InstallmentDocumentRepository>();

// Production repositories
builder.Services.AddScoped<ISupplyRepository, SupplyRepository>();
builder.Services.AddScoped<ISupplyCategoryRepository, SupplyCategoryRepository>();
builder.Services.AddScoped<IProcessRepository, ProcessRepository>();
builder.Services.AddScoped<IProcessDoneRepository, ProcessDoneRepository>();
builder.Services.AddScoped<IUnitMeasureRepository, UnitMeasureRepository>();
builder.Services.AddScoped<ISupplyEntryRepository, SupplyEntryRepository>();
builder.Services.AddScoped<IComponentProductionRepository, ComponentProductionRepository>();
builder.Services.AddScoped<IProviderRepository, ProviderRepository>();
builder.Services.AddScoped<IManufactureRepository, ManufactureRepository>();
builder.Services.AddScoped<IComponentRepository>(provider => 
    new ComponentRepository(builder.Configuration.GetConnectionString("DefaultConnection")!));

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IProductAuditService, ProductAuditService>();

// Expense services
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();

// Payment services
builder.Services.AddScoped<IPaymentCatalogService, PaymentCatalogService>();
builder.Services.AddScoped<IPaymentPlanService, PaymentPlanService>();
builder.Services.AddScoped<IPaymentInstallmentService, PaymentInstallmentService>();
builder.Services.AddScoped<IInstallmentDocumentService, InstallmentDocumentService>();

// Production services
builder.Services.AddScoped<ISupplyService, SupplyService>();
builder.Services.AddScoped<ISupplyCategoryService, SupplyCategoryService>();
builder.Services.AddScoped<IProcessService, ProcessService>();
builder.Services.AddScoped<IProcessDoneService, ProcessDoneService>();
builder.Services.AddScoped<IUnitMeasureService, UnitMeasureService>();
builder.Services.AddScoped<ISupplyEntryService, SupplyEntryService>();
builder.Services.AddScoped<IComponentService, ComponentService>();
builder.Services.AddScoped<IProviderService, ProviderService>();
builder.Services.AddScoped<IManufactureService, ManufactureService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Add logging middleware para debug de CORS
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    // Log ALL requests for debugging
    logger.LogInformation("Incoming request: {Method} {Path} from Origin: {Origin}", 
        context.Request.Method, 
        context.Request.Path, 
        context.Request.Headers["Origin"].ToString());
    
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
var corsLogger = app.Services.GetRequiredService<ILogger<Program>>();
corsLogger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
corsLogger.LogInformation("Is Development: {IsDevelopment}", app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    // En desarrollo, usar política más permisiva para debugging
    corsLogger.LogInformation("Using CORS policy: AllowAll (Development)");
    app.UseCors("AllowAll");
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // En producción, usar política restrictiva
    corsLogger.LogInformation("Using CORS policy: AllowFrontend (Production)");
    app.UseCors("AllowFrontend");
}

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
