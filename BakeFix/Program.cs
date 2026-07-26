using BakeFix.Filters;
using BakeFix.Migrations;
using BakeFix.Repositories;
using BakeFix.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
Env.TraversePath().Load(".env");

builder.Configuration.AddEnvironmentVariables();
builder.Configuration["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("DB_CONNECTION");

// ── HTTP context (needed by TenantContext) ──────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ── Tenant context ──────────────────────────────────────────────────────────
builder.Services.AddScoped<ITenantContext, TenantContext>();

// ── Repositories ────────────────────────────────────────────────────────────
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<ExpenseRepository>();
builder.Services.AddScoped<IncomeRepository>();
builder.Services.AddScoped<DashboardRepository>();
builder.Services.AddScoped<EmployeeRepository>();
builder.Services.AddScoped<WageRepository>();
builder.Services.AddScoped<DivisionRepository>();
builder.Services.AddScoped<PushSubscriptionRepository>();
builder.Services.AddScoped<NotificationSettingsRepository>();
builder.Services.AddScoped<DebtRepository>();
builder.Services.AddScoped<TaskRepository>();
builder.Services.AddScoped<DailyNoteRepository>();
builder.Services.AddScoped<ProductCategoryRepository>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<StockTransactionRepository>();
builder.Services.AddScoped<PrayerRepository>();

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<IncomeService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<WageService>();
builder.Services.AddScoped<DivisionService>();
builder.Services.AddScoped<PushNotificationService>();
builder.Services.AddScoped<DebtService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<DailyNoteService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PrayerService>();
builder.Services.AddHostedService<NotificationSchedulerService>();
builder.Services.AddHostedService<TaskCarryForwardService>();
builder.Services.AddHostedService<PrayerSchedulerService>();
builder.Services.AddSingleton<DatabaseMigrator>();

// ── Controllers with global ModuleAccessFilter ───────────────────────────────
builder.Services.AddScoped<ModuleAccessFilter>();
builder.Services.AddControllers(opts =>
{
    opts.Filters.AddService<ModuleAccessFilter>();
});

string ResolveEnvToken(string? value)
{
    if (value is null) return string.Empty;
    if (value.StartsWith("ENV:"))
        return Environment.GetEnvironmentVariable(value.Replace("ENV:", "")) ?? string.Empty;
    return value;
}

builder.Configuration["Vapid:PublicKey"]             = ResolveEnvToken(builder.Configuration["Vapid:PublicKey"]);
builder.Configuration["Vapid:PrivateKey"]            = ResolveEnvToken(builder.Configuration["Vapid:PrivateKey"]);
builder.Configuration["AppSettings:RunMigrations"]   = ResolveEnvToken(builder.Configuration["AppSettings:RunMigrations"]);

var rawOrigins = builder.Configuration["AppSettings:AllowedOrigins"];
var corsOrigins = ResolveEnvToken(rawOrigins).Split(";", StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(key),
            ValidateLifetime         = true
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "BakeFix API",
        Version     = "v1",
        Description =
            "REST API for **BakeFix** — a multi-tenant bakery bookkeeping platform.\n\n" +
            "### Authentication\n" +
            "All endpoints except `POST /auth/login` require a **Bearer JWT** token. " +
            "Click **Authorize** and enter your token (without the `Bearer ` prefix).\n\n" +
            "### Module access\n" +
            "Endpoints tagged with a module name (Inventory, Debts, Wages, …) will return " +
            "`403 Forbidden` if that module is not enabled for the caller's organisation."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Paste the JWT token returned by POST /auth/login."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Run database migrations before accepting requests
var migrator = app.Services.GetRequiredService<DatabaseMigrator>();
await migrator.RunAsync();

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global exception handler — must be first so it wraps everything
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

    ctx.Response.ContentType = "application/json";
    ctx.Response.StatusCode = ex switch
    {
        UnauthorizedAccessException => StatusCodes.Status403Forbidden,
        ArgumentException           => StatusCodes.Status400BadRequest,
        _                           => StatusCodes.Status500InternalServerError
    };

    var isDev = app.Environment.IsDevelopment();
    var message = ex switch
    {
        UnauthorizedAccessException => ex.Message,
        ArgumentException           => ex.Message,
        _ when isDev                => $"{ex?.GetType().Name}: {ex?.Message}",
        _                           => "An unexpected error occurred."
    };

    await ctx.Response.WriteAsJsonAsync(new { message });
}));

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
