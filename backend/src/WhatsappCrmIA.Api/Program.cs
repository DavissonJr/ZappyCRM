using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Application.UseCases.Messaging;
using WhatsappCrmIA.Infrastructure.Persistence;
using WhatsappCrmIA.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Serviços de aplicação ----
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ProcessIncomingMessageCommand).Assembly));

// ---- Persistência ----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// ---- Data Protection persistente ----
// IMPORTANTE: sem isso, toda vez que o container reiniciar as chaves de
// criptografia mudam, e qualquer segredo salvo antes (como a chave da
// Anthropic de cada tenant) fica permanentemente ilegível. O volume
// "dataprotection_keys" no docker-compose garante que a chave sobrevive
// a rebuilds/restarts do container.
builder.Services.AddDataProtection()
    .SetApplicationName("WhatsappCrmIA")
    .PersistKeysToFileSystem(new DirectoryInfo("/keys"));
builder.Services.AddScoped<ISecretProtector, WhatsappCrmIA.Api.Services.SecretProtector>();

// ---- Tenant atual (resolvido a partir do JWT) ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---- Integrações externas ----
builder.Services.AddHttpClient<IWhatsAppGateway, EvolutionApiWhatsAppGateway>();
builder.Services.AddHttpClient<IAiAgentService, ClaudeAiAgentService>();

// ---- Autenticação (hash de senha + emissão de JWT próprio) ----
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IWebhookUrlBuilder, WebhookUrlBuilder>();
builder.Services.AddScoped<INotificationService, WhatsappCrmIA.Api.Services.SignalRNotificationService>();
builder.Services.AddScoped<IReminderScheduler, WhatsappCrmIA.Api.Services.HangfireReminderScheduler>();
builder.Services.AddScoped<WhatsappCrmIA.Api.Services.SendReminderJob>();
builder.Services.AddScoped<IBulkCampaignRunner, WhatsappCrmIA.Api.Services.HangfireBulkCampaignRunner>();
builder.Services.AddScoped<WhatsappCrmIA.Api.Services.BulkCampaignJob>();

// ---- Jobs agendados (lembretes) ----
builder.Services.AddHangfire(cfg => cfg
    .UsePostgreSqlStorage(opt =>
        opt.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Default"))));
builder.Services.AddHangfireServer();

// ---- Auth (JWT emitido pela própria API) ----
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret não configurado.");

builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sem isso, o .NET renomeia "sub" para um nome de claim enorme por baixo dos
        // panos (comportamento legado), e User.FindFirst("sub") nunca encontra nada.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        // O cliente JS do SignalR não consegue mandar header Authorization no handshake
        // do WebSocket, então ele manda o token via query string — aqui a gente aceita isso
        // só para as chamadas do hub, sem abrir brecha para o resto da API.
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdmin", policy => policy.RequireClaim("platform_admin", "true"));
});

// ---- CORS para o Angular ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Cole aqui só o token, sem a palavra 'Bearer' na frente."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware global: qualquer exceção não tratada em qualquer endpoint vira uma
// resposta JSON limpa, em vez de vazar stack trace para o cliente.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var message = app.Environment.IsDevelopment()
            ? ex.Message
            : "Ocorreu um erro inesperado. Tente novamente em instantes.";
        await context.Response.WriteAsJsonAsync(new { message });
    }
});

app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/jobs"); // proteger com auth em produção
app.MapControllers();
app.MapHub<WhatsappCrmIA.Api.Hubs.ConversationHub>("/hubs/conversations");

app.Run();
