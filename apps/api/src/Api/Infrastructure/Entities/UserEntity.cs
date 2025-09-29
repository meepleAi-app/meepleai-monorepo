namespace Api.Infrastructure.Entities;

public class UserEntity
{
    public string Id { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? DisplayName { get; set; }
        = null;
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.User;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TenantEntity Tenant { get; set; } = default!;
    public ICollection<UserSessionEntity> Sessions { get; set; } = new List<UserSessionEntity>();
}
