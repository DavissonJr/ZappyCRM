using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.Interfaces;

namespace WhatsappCrmIA.Application.UseCases.Contacts;

public record GetContactsQuery(
    string? Search,
    int? NoConversationInLastDays,
    int? NoAppointmentInLastDays
) : IRequest<IReadOnlyList<ContactListItemDto>>;

public class GetContactsHandler : IRequestHandler<GetContactsQuery, IReadOnlyList<ContactListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public GetContactsHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ContactListItemDto>> Handle(GetContactsQuery request, CancellationToken ct)
    {
        var query = _db.Contacts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c =>
                (c.Name != null && c.Name.ToLower().Contains(term)) ||
                c.PhoneNumber.Contains(term));
        }

        var contacts = await query.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);
        var contactIds = contacts.Select(c => c.Id).ToList();

        var conversationInfo = await _db.Conversations
            .Where(c => contactIds.Contains(c.ContactId))
            .GroupBy(c => c.ContactId)
            .Select(g => new { ContactId = g.Key, Count = g.Count(), LastAt = g.Max(c => c.LastMessageAtUtc) })
            .ToDictionaryAsync(x => x.ContactId, x => x, ct);

        var appointmentInfo = await _db.Appointments
            .Where(a => contactIds.Contains(a.ContactId))
            .GroupBy(a => a.ContactId)
            .Select(g => new { ContactId = g.Key, Count = g.Count(), LastCreatedAt = g.Max(a => a.CreatedAtUtc) })
            .ToDictionaryAsync(x => x.ContactId, x => x, ct);

        var proposalCounts = await _db.Proposals
            .Where(p => contactIds.Contains(p.ContactId))
            .GroupBy(p => p.ContactId)
            .Select(g => new { ContactId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ContactId, x => x.Count, ct);

        // Filtro: sem NENHUMA mensagem/conversa nos últimos X dias (inclui quem
        // nunca conversou — esses também "não retornam há X dias", por definição).
        if (request.NoConversationInLastDays is { } convDays)
        {
            var cutoff = DateTime.UtcNow.AddDays(-convDays);
            contacts = contacts
                .Where(c => !conversationInfo.TryGetValue(c.Id, out var info) || info.LastAt < cutoff)
                .ToList();
        }

        // Filtro: sem NENHUM agendamento criado nos últimos X dias (inclui quem nunca agendou).
        if (request.NoAppointmentInLastDays is { } apptDays)
        {
            var cutoff = DateTime.UtcNow.AddDays(-apptDays);
            contacts = contacts
                .Where(c => !appointmentInfo.TryGetValue(c.Id, out var info) || info.LastCreatedAt < cutoff)
                .ToList();
        }

        return contacts.Select(c =>
        {
            conversationInfo.TryGetValue(c.Id, out var conv);
            appointmentInfo.TryGetValue(c.Id, out var appt);
            return new ContactListItemDto(
                c.Id, c.Name, c.PhoneNumber, c.ProfilePictureUrl, c.Notes, c.IsBlocked, c.CreatedAtUtc,
                conv?.LastAt, conv?.Count ?? 0,
                appt?.Count ?? 0,
                proposalCounts.GetValueOrDefault(c.Id, 0));
        }).ToList();
    }
}
