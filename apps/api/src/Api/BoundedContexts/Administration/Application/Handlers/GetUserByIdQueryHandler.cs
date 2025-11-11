using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

public class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetUserByIdQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserDto?> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(query.UserId);
        var user = await _dbContext.Users
            .Include(u => u.Sessions)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

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
