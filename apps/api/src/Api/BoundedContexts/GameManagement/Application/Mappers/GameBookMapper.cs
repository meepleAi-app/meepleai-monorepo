using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Application.Mappers;

internal static class GameBookMapper
{
    public static GameBookDto ToDto(this GameBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        return new GameBookDto(
            book.Id,
            book.GameRef.Id,
            (int)book.GameRef.Kind,
            book.OwnerUserId,
            book.DisplayName,
            (int)book.Roles,
            (int)book.ParagraphScheme,
            book.Language,
            book.SequentialRead,
            book.KbSourceDocId,
            book.PhysicalOnly,
            book.CreatedAt);
    }
}
