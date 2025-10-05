namespace Api.Infrastructure.Entities;

public class UserEntity
{
    required public string Id { get; set; }
    required public string Email { get; set; }
    public string? DisplayName { get; set; }
    required public string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<UserSessionEntity> Sessions { get; set; } = new List<UserSessionEntity>();
}
