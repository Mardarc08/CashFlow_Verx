using Consolidado.Api.Endpoints;
using Consolidado.Application.Events;
using Consolidado.Domain.Interface;
using Consolidado.Infrastructure.Cache;
using Consolidado.Infrastructure.Persistence;
using Google.Cloud.PubSub.V1;
using HealthChecks.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Banco de Dados
var sqlConnectionString = String.Empty;
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddEnvironmentVariables().AddJsonFile("appsettings.Development.json");
    sqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}
else
{
    //Alterar para ler a string de conexão do ambiente de produção, por exemplo, usando variáveis de ambiente
    sqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}


// PostegreSQL
//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseNpgsql(sqlConnectionString));


// MSSQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(sqlConnectionString,
    sqlServerOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null); // You can specify specific error numbers to retry on if needed
    }));

// Repositório
builder.Services.AddScoped<IConsolidadoRepository, ConsolidadoRepository>();

// Redis Cache 
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));
builder.Services.AddSingleton<IConsolidadoCache, RedisConsolidadoCache>();


// MediatR (CQRS)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));


// Pub/Sub Subscriber
builder.Services.AddSingleton(sp =>
{
    var projectId = builder.Configuration["PubSub:ProjectId"];
    var subscriptionId = builder.Configuration["PubSub:SubscriptionId"];
    var subscriptionName = SubscriptionName.FromProjectSubscription(projectId!, subscriptionId!);
    return SubscriberClient.CreateAsync(subscriptionName).GetAwaiter().GetResult();
});

builder.Services.AddHostedService<LancamentoRegistradoConsumer>();

// ── Autenticação JWT ─────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// ── Swagger ──────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CashFlow - Consolidado Diário", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Insira: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(document => new() { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
});

// ── Health Checks ────────────────────────────────────────────────────────────
//builder.Services.AddHealthChecks()
//    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!)
//    .AddRedis(builder.Configuration["Redis:ConnectionString"]!);

builder.Services.AddHealthChecks()
    .AddSqlServer(sqlConnectionString).AddRedis(builder.Configuration["Redis:ConnectionString"]!);

var app = builder.Build();

// ── Middlewares ──────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
app.UseSwagger();
app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────────────────────
app.MapConsolidadoEndpoints();
app.MapHealthChecks("/health");

// ── Migrations automáticas (dev) ─────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();

public partial class Program { }