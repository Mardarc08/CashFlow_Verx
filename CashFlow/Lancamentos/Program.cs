using Lancamentos.Api.Endpoints;
using Lancamentos.Api.Middlewares;
using Lancamentos.Domain.Interfaces;
using Lancamentos.Infrastructure.Data;
using Lancamentos.Infrastructure.Messaging;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using Confluent.Kafka;

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
            errorNumbersToAdd: null);
    }));

// Repositórios
builder.Services.AddScoped<ILancamentoRepository, LancamentoRepository>();

// MediatR (CQRS)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Kafka Producer
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

// Autenticação JWT
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

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CashFlow - Lançamentos", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Insira: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(document => new() { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(sqlConnectionString);

var app = builder.Build();


// Middlewares
//app.UseMiddleware<ExceptionMiddleware>();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Endpoints 
app.MapLancamentoEndpoints();
app.MapHealthChecks("/health");

// Migrations automáticas (dev) 
if (app.Environment.IsDevelopment())
{
    try{
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }
    catch(Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erro ao aplicar migrações automáticas");
    }
}

app.Run();

public partial class Program { } // necessário para integration tests
