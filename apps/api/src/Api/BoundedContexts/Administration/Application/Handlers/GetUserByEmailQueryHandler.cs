using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

public class GetUserByEmailQueryHandler : IQueryHandler<GetUserByEmailQuery, UserDto?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetUserByEmailQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<UserDto?> Handle(GetUserByEmailQuery query, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Sessions)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == query.Email, cancellationToken).ConfigureAwait(false);

        return user == null ? null : MapToDto(user);
    }

    private static UserDto MapToDto(UserEntity user)
    {
        var lastSeenAt = user.Sessions
            .Where(s => s.RevokedAt == null)
            .OrderByDescending(s => s.LastSeenAt ?? s.CreatedAt)
            .FirstOrDefault()
            ?.LastSeenAt;

        return new UserDto(
            Id: user.Id.ToString(),
            Email: user.Email,
            DisplayName: user.DisplayName ?? string.Empty,
            Role: user.Role,
            CreatedAt: user.CreatedAt,
            LastSeenAt: lastSeenAt
        );
    }
}
