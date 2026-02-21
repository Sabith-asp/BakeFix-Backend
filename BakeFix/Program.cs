using BakeFix.Repositories;
using BakeFix.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
Env.TraversePath().Load(".env");

// Now resolve ENV:* patterns
builder.Configuration.AddEnvironmentVariables();
builder.Configuration["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("DB_CONNECTION");
// Add services to the container.
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ExpenseRepository>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<IncomeRepository>();
builder.Services.AddScoped<IncomeService>();
builder.Services.AddScoped<WageRepository>();
builder.Services.AddScoped<WageService>();

string ResolveEnvToken(string? value)
{
    if (value is null) return string.Empty;

    if (value.StartsWith("ENV:"))
    {
        var key = value.Replace("ENV:", "");
        return Environment.GetEnvironmentVariable(key) ?? string.Empty;
    }

    return value;
}

var rawOrigins = builder.Configuration["AppSettings:AllowedOrigins"];
var resolvedOrigins = ResolveEnvToken(rawOrigins);
var corsOrigins = resolvedOrigins.Split(";", StringSplitOptions.RemoveEmptyEntries);

// 4. Add CORS policy
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


var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateLifetime = true
        };
    });

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
