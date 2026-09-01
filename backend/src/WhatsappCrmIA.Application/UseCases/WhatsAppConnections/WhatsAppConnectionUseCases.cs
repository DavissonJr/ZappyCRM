using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Entities;

namespace WhatsappCrmIA.Application.UseCases.WhatsAppConnections;

public record GetWhatsAppConnectionsQuery : IRequest<IReadOnlyList<WhatsAppConnectionDto>>;

public class GetWhatsAppConnectionsHandler
    : IRequestHandler<GetWhatsAppConnectionsQuery, IReadOnlyList<WhatsAppConnectionDto>>
{
    private readonly IApplicationDbContext _db;
    public GetWhatsAppConnectionsHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<WhatsAppConnectionDto>> Handle(
        GetWhatsAppConnectionsQuery request, CancellationToken ct)
    {
        return await _db.WhatsAppConnections
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new WhatsAppConnectionDto(c.Id, c.Label, c.InstanceName, c.PhoneNumber, c.IsConnected))
            .ToListAsync(ct);
    }
}

public record CreateWhatsAppConnectionCommand(string Label) : IRequest<CreateConnectionResult>;

public record CreateConnectionResult(bool Success, WhatsAppConnectionDto? Connection, string? Error);

public class CreateWhatsAppConnectionHandler
    : IRequestHandler<CreateWhatsAppConnectionCommand, CreateConnectionResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IWhatsAppGateway _whatsApp;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IWebhookUrlBuilder _webhookUrlBuilder;

    public CreateWhatsAppConnectionHandler(
        IApplicationDbContext db,
        IWhatsAppGateway whatsApp,
        ICurrentTenantService currentTenant,
        IWebhookUrlBuilder webhookUrlBuilder)
    {
        _db = db;
        _whatsApp = whatsApp;
        _currentTenant = currentTenant;
        _webhookUrlBuilder = webhookUrlBuilder;
    }

    public async Task<CreateConnectionResult> Handle(
        CreateWhatsAppConnectionCommand request, CancellationToken ct)
    {
        if (_currentTenant.TenantId is not { } tenantId)
            return new CreateConnectionResult(false, null, "Tenant não identificado.");

        if (string.IsNullOrWhiteSpace(request.Label))
            return new CreateConnectionResult(false, null, "Dê um nome para o número (ex: Recepção).");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        var planLimit = Domain.Common.PlanCatalog.Get(tenant?.Plan ?? Domain.Enums.PlanTier.Trial).MaxWhatsAppConnections;
        if (planLimit != Domain.Common.PlanCatalog.UnlimitedMarker)
        {
            var currentCount = await _db.WhatsAppConnections.CountAsync(ct);
            if (currentCount >= planLimit)
                return new CreateConnectionResult(false, null,
                    $"Seu plano atual permite até {planLimit} número(s) de WhatsApp. " +
                    "Fale com o suporte pra liberar mais números no seu plano atual.");
        }

        // Nome de instância único e legível: tenant + label + sufixo curto.
        var slug = request.Label.Trim().ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD);
        slug = new string(slug.Where(c =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
            System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        slug = System.Text.RegularExpressions.Regex.Replace(slug, "[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug)) slug = "numero";

        var instanceName = $"{tenantId:N}-{slug}";
        if (instanceName.Length > 60) instanceName = instanceName[..60];

        var alreadyExists = await _db.WhatsAppConnections
            .AnyAsync(c => c.InstanceName == instanceName, ct);
        if (alreadyExists)
            return new CreateConnectionResult(false, null,
                "Já existe um número com um nome muito parecido. Tente um nome diferente.");

        try
        {
            await _whatsApp.CreateInstanceAsync(instanceName, ct);
        }
        catch (EvolutionApiException ex)
        {
            return new CreateConnectionResult(false, null,
                $"Não foi possível criar a conexão no WhatsApp ({ex.Message})");
        }

        try
        {
            // Já deixa o webhook configurado, para que mensagens recebidas cheguem
            // automaticamente na nossa API sem passo manual nenhum.
            var webhookUrl = _webhookUrlBuilder.Build(tenantId, instanceName);
            await _whatsApp.SetWebhookAsync(instanceName, webhookUrl, ct);
        }
        catch (EvolutionApiException ex)
        {
            // Desfaz a instância criada para não deixar lixo órfão na Evolution API.
            try { await _whatsApp.DeleteInstanceAsync(instanceName, ct); } catch { /* ignora */ }

            return new CreateConnectionResult(false, null,
                $"O número foi criado, mas não conseguimos configurar o recebimento de mensagens ({ex.Message}). Tente novamente.");
        }

        var connection = new WhatsAppConnection
        {
            Label = request.Label,
            InstanceName = instanceName,
            IsConnected = false
        };
        _db.WhatsAppConnections.Add(connection);
        await _db.SaveChangesAsync(ct);

        var dto = new WhatsAppConnectionDto(
            connection.Id, connection.Label, connection.InstanceName, connection.PhoneNumber, connection.IsConnected);
        return new CreateConnectionResult(true, dto, null);
    }
}

public record GetQrCodeQuery(Guid ConnectionId) : IRequest<QrCodeResult>;

public record QrCodeResult(bool Success, string? QrCodeBase64, string? Error);

public class GetQrCodeHandler : IRequestHandler<GetQrCodeQuery, QrCodeResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IWhatsAppGateway _whatsApp;

    public GetQrCodeHandler(IApplicationDbContext db, IWhatsAppGateway whatsApp)
    {
        _db = db;
        _whatsApp = whatsApp;
    }

    public async Task<QrCodeResult> Handle(GetQrCodeQuery request, CancellationToken ct)
    {
        var connection = await _db.WhatsAppConnections.FirstOrDefaultAsync(c => c.Id == request.ConnectionId, ct);
        if (connection is null) return new QrCodeResult(false, null, "Número não encontrado.");

        try
        {
            var qr = await _whatsApp.GetQrCodeAsync(connection.InstanceName, ct);
            connection.QrCodeBase64 = qr;
            await _db.SaveChangesAsync(ct);
            return new QrCodeResult(true, qr, null);
        }
        catch (EvolutionApiException ex)
        {
            return new QrCodeResult(false, null, $"Não foi possível gerar o QR code ({ex.Message})");
        }
    }
}

public record RefreshConnectionStatusCommand(Guid ConnectionId) : IRequest<bool>;

public class RefreshConnectionStatusHandler : IRequestHandler<RefreshConnectionStatusCommand, bool>
{
    private readonly IApplicationDbContext _db;
    private readonly IWhatsAppGateway _whatsApp;

    public RefreshConnectionStatusHandler(IApplicationDbContext db, IWhatsAppGateway whatsApp)
    {
        _db = db;
        _whatsApp = whatsApp;
    }

    public async Task<bool> Handle(RefreshConnectionStatusCommand request, CancellationToken ct)
    {
        var connection = await _db.WhatsAppConnections.FirstOrDefaultAsync(c => c.Id == request.ConnectionId, ct);
        if (connection is null) return false;

        var isConnected = await _whatsApp.IsConnectedAsync(connection.InstanceName, ct);
        connection.IsConnected = isConnected;
        if (isConnected && connection.ConnectedAtUtc is null)
            connection.ConnectedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return isConnected;
    }
}

public record DisconnectWhatsAppConnectionCommand(Guid ConnectionId) : IRequest<DisconnectResult>;

public record DisconnectResult(bool Success, string? Error);

public class DisconnectWhatsAppConnectionHandler : IRequestHandler<DisconnectWhatsAppConnectionCommand, DisconnectResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IWhatsAppGateway _whatsApp;

    public DisconnectWhatsAppConnectionHandler(IApplicationDbContext db, IWhatsAppGateway whatsApp)
    {
        _db = db;
        _whatsApp = whatsApp;
    }

    public async Task<DisconnectResult> Handle(DisconnectWhatsAppConnectionCommand request, CancellationToken ct)
    {
        var connection = await _db.WhatsAppConnections.FirstOrDefaultAsync(c => c.Id == request.ConnectionId, ct);
        if (connection is null) return new DisconnectResult(false, "Número não encontrado.");

        try
        {
            await _whatsApp.LogoutAsync(connection.InstanceName, ct);
        }
        catch (EvolutionApiException ex)
        {
            return new DisconnectResult(false, $"Não foi possível desconectar ({ex.Message})");
        }

        connection.IsConnected = false;
        connection.PhoneNumber = null;
        connection.ConnectedAtUtc = null;
        connection.QrCodeBase64 = null;

        await _db.SaveChangesAsync(ct);
        return new DisconnectResult(true, null);
    }
}

public record DeleteWhatsAppConnectionCommand(Guid ConnectionId) : IRequest<DeleteConnectionResult>;

public record DeleteConnectionResult(bool Success, string? Error);

public class DeleteWhatsAppConnectionHandler : IRequestHandler<DeleteWhatsAppConnectionCommand, DeleteConnectionResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IWhatsAppGateway _whatsApp;

    public DeleteWhatsAppConnectionHandler(IApplicationDbContext db, IWhatsAppGateway whatsApp)
    {
        _db = db;
        _whatsApp = whatsApp;
    }

    public async Task<DeleteConnectionResult> Handle(DeleteWhatsAppConnectionCommand request, CancellationToken ct)
    {
        var connection = await _db.WhatsAppConnections.FirstOrDefaultAsync(c => c.Id == request.ConnectionId, ct);
        if (connection is null) return new DeleteConnectionResult(false, "Número não encontrado.");

        // Tenta remover na Evolution API também — se falhar (ex: já não existia lá),
        // ainda assim removemos do nosso banco para não deixar lixo na tela.
        try
        {
            await _whatsApp.DeleteInstanceAsync(connection.InstanceName, ct);
        }
        catch
        {
            // segue o fluxo mesmo assim
        }

        _db.WhatsAppConnections.Remove(connection);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return new DeleteConnectionResult(false,
                "Não foi possível remover esse número por um erro no banco de dados. Tente novamente.");
        }

        return new DeleteConnectionResult(true, null);
    }
}
