using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.UseCases.Admin;
using WhatsappCrmIA.Domain.Common;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Api.Controllers;

public record SetTenantActiveRequest(bool IsActive);

public record CreateTenantRequest(
    string CompanyName, string Segment, PlanTier Plan,
    string OwnerFullName, string OwnerEmail, string TemporaryPassword);

public record UpdateTenantPlanRequest(PlanTier Plan);

/// <summary>
/// Painel exclusivo de quem administra o sistema (você) — enxerga todas as
/// empresas cadastradas, não só a própria. Protegido pela policy
/// "PlatformAdmin", que exige a claim platform_admin=true no JWT (ver
/// README para como ativar isso pro seu usuário).
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "PlatformAdmin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdminController(IMediator mediator) => _mediator = mediator;

    [HttpGet("tenants")]
    public async Task<ActionResult<IReadOnlyList<AdminTenantSummaryDto>>> GetTenants(CancellationToken ct)
        => Ok(await _mediator.Send(new GetAdminTenantsQuery(), ct));

    [HttpGet("plans")]
    public ActionResult<IReadOnlyList<AdminPlanOptionDto>> GetPlans()
        => Ok(PlanCatalog.Plans.Values.Select(p => new AdminPlanOptionDto(
            p.Tier.ToString(), p.DisplayName,
            p.MaxWhatsAppConnections == PlanCatalog.UnlimitedMarker ? -1 : p.MaxWhatsAppConnections,
            p.MaxUsers == PlanCatalog.UnlimitedMarker ? -1 : p.MaxUsers)).ToList());

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        var (tenantId, error) = await _mediator.Send(new AdminCreateTenantCommand(
            request.CompanyName, request.Segment, request.Plan,
            request.OwnerFullName, request.OwnerEmail, request.TemporaryPassword), ct);

        return tenantId is null ? BadRequest(new { message = error }) : Ok(new { id = tenantId });
    }

    [HttpPut("tenants/{id:guid}/plan")]
    public async Task<IActionResult> UpdateTenantPlan(Guid id, [FromBody] UpdateTenantPlanRequest request, CancellationToken ct)
    {
        var success = await _mediator.Send(new AdminUpdateTenantPlanCommand(id, request.Plan), ct);
        return success ? Ok() : NotFound();
    }

    [HttpPut("tenants/{id:guid}/active")]
    public async Task<IActionResult> SetTenantActive(Guid id, [FromBody] SetTenantActiveRequest request, CancellationToken ct)
    {
        var success = await _mediator.Send(new SetTenantActiveCommand(id, request.IsActive), ct);
        return success ? Ok() : NotFound();
    }
}
