using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.UseCases.Contacts;

namespace WhatsappCrmIA.Api.Controllers;

public record UpdateContactRequest(string? Name, string? Notes, bool IsBlocked);

[ApiController]
[Route("api/contacts")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ContactsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactListItemDto>>> Get(
        [FromQuery] string? search,
        [FromQuery] int? noConversationInLastDays,
        [FromQuery] int? noAppointmentInLastDays,
        CancellationToken ct)
        => Ok(await _mediator.Send(
            new GetContactsQuery(search, noConversationInLastDays, noAppointmentInLastDays), ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactRequest request, CancellationToken ct)
    {
        var success = await _mediator.Send(new UpdateContactCommand(id, request.Name, request.Notes, request.IsBlocked), ct);
        return success ? Ok() : NotFound();
    }
}
