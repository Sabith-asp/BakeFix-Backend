using BakeFix.Filters;
using BakeFix.Repositories;
using BakeFix.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<IncomeService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<WageService>();

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
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

    var message = ex switch
    {
        UnauthorizedAccessException => ex.Message,
        ArgumentException           => ex.Message,
        _                           => "An unexpected error occurred."
    };

    await ctx.Response.WriteAsJsonAsync(new { message });
}));

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
