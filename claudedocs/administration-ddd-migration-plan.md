# Administration Bounded Context - DDD/CQRS Migration Plan

## Status: IN PROGRESS

### Overview
Complete DDD/CQRS migration for Administration bounded context following the pattern established in Authentication and GameManagement contexts.

### Current State (2025-11-11)

**Completed**:
- ✅ Domain Layer (Alert, AuditLog aggregates + value objects)
- ✅ Infrastructure Layer (AlertRepository, AuditLogRepository)
- ✅ Application Layer Structure (Commands/Queries created)

**Commands Created** (11 total):
1. ✅ CreateUserCommand
2. ✅ UpdateUserCommand
3. ✅ DeleteUserCommand
4. ✅ ChangeUserRoleCommand
5. ✅ ResetUserPasswordCommand
6. ✅ ExportStatsCommand
7. ✅ SendAlertCommand
8. ✅ ResolveAlertCommand

**Queries Created** (7 total):
1. ✅ GetAllUsersQuery
2. ✅ GetUserByIdQuery
3. ✅ GetUserByEmailQuery
4. ✅ GetAdminStatsQuery
5. ✅ GetActiveAlertsQuery
6. ✅ GetAlertHistoryQuery

**Handlers Needed** (15 total):
- ⏳ User Management: 8 handlers
- ⏳ Statistics: 2 handlers (GetAdminStats, ExportStats)
- ⏳ Alerting: 4 handlers (SendAlert, ResolveAlert, GetActiveAlerts, GetAlertHistory)
- ⏳ Audit: 1 handler (implicit in other operations)

### Implementation Strategy

#### Phase 1: User Management Handlers (Priority 1)
**Dependencies**: IUserRepository (from Authentication context), IPasswordHashingService

1. **CreateUserCommandHandler**
   - Reuse: Authentication.IUserRepository
   - Logic: Email validation, password hashing, role assignment
   - Source: UserManagementService.CreateUserAsync() lines 92-133

2. **UpdateUserCommandHandler**
   - Reuse: Authentication.IUserRepository
   - Logic: Email uniqueness check, partial updates
   - Source: UserManagementService.UpdateUserAsync() lines 138-179

3. **DeleteUserCommandHandler**
   - Reuse: Authentication.IUserRepository
   - Logic: Self-deletion prevention, last admin protection
   - Source: UserManagementService.DeleteUserAsync() lines 185-215

4. **GetAllUsersQueryHandler**
   - Reuse: Authentication.IUserRepository
   - Logic: Pagination, filtering, sorting
   - Source: UserManagementService.GetUsersAsync() lines 34-86

5. **GetUserByIdQueryHandler**
   - Reuse: Authentication.IUserRepository
   - Logic: Simple ID lookup with session mapping
   - New implementation

6. **GetUserByEmailQueryHandler**
   - Reuse: Authentication.IUserRepository.GetByEmailAsync()
   - Logic: Email lookup
   - New implementation

7. **ChangeUserRoleCommandHandler**
   - Reuse: Authentication.IUserRepository
   - Logic: Role update with validation
   - Part of UpdateUserCommandHandler logic

8. **ResetUserPasswordCommandHandler**
   - Reuse: Authentication.IUserRepository, IPasswordHashingService
   - Logic: Password hashing and update
   - New implementation

#### Phase 2: Statistics Handlers (Priority 2)
**Dependencies**: DbContext (direct access for analytics), HybridCache

1. **GetAdminStatsQueryHandler**
   - Reuse: AdminStatsService logic entirely
   - Logic: Parallel metric queries, time-series aggregation, caching
   - Source: AdminStatsService.GetDashboardStatsAsync() lines 40-84
   - Note: Requires direct DbContext access (not domain repositories) for analytics

2. **ExportStatsCommandHandler**
   - Reuse: AdminStatsService.ExportDashboardDataAsync() lines 313-332
   - Logic: CSV/JSON export generation
   - Delegates to GetAdminStatsQueryHandler

#### Phase 3: Alerting Handlers (Priority 3)
**Dependencies**: IAlertRepository, IAlertChannel[], AlertingConfiguration

1. **SendAlertCommandHandler**
   - Repository: IAlertRepository (Administration.Infrastructure)
   - Logic: Throttling, multi-channel sending, channel result tracking
   - Source: AlertingService.SendAlertAsync() lines 42-151

2. **ResolveAlertCommandHandler**
   - Repository: IAlertRepository
   - Logic: Deactivate alerts by type
   - Source: AlertingService.ResolveAlertAsync() lines 153-181

3. **GetActiveAlertsQueryHandler**
   - Repository: IAlertRepository
   - Logic: Query active alerts with ordering
   - Source: AlertingService.GetActiveAlertsAsync() lines 183-193

4. **GetAlertHistoryQueryHandler**
   - Repository: IAlertRepository
   - Logic: Date range query
   - Source: AlertingService.GetAlertHistoryAsync() lines 195-207

### Handler Implementation Pattern

```csharp
// Example: CreateUserCommandHandler
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly ILogger<CreateUserCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHashingService passwordHashingService,
        ILogger<CreateUserCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _passwordHashingService = passwordHashingService;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate email uniqueness
        var emailVo = new Email(request.Email);
        if (await _userRepository.ExistsByEmailAsync(emailVo, cancellationToken))
        {
            throw new InvalidOperationException($"User with email {request.Email} already exists");
        }

        // 2. Create User domain entity
        var user = User.Create(
            emailVo,
            new PasswordHash(_passwordHashingService.HashSecret(request.Password)),
            new Role(request.Role.ToLower()),
            request.DisplayName,
            _timeProvider.GetUtcNow().UtcDateTime
        );

        // 3. Save via repository
        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin created user {UserId} with role {Role}", user.Id, request.Role);

        // 4. Map to DTO
        return MapToDto(user);
    }

    private static UserDto MapToDto(User user)
    {
        // Map domain entity to DTO
        return new UserDto(
            Id: user.Id.ToString(),
            Email: user.Email.Value,
            DisplayName: user.DisplayName ?? string.Empty,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            LastSeenAt: null // Requires session repository
        );
    }
}
```

### Endpoint Migration Pattern

**Before** (Legacy Service):
```csharp
group.MapPost("/admin/users", async (CreateUserRequest request, UserManagementService service) =>
{
    var user = await service.CreateUserAsync(request, ct);
    return Results.Ok(user);
});
```

**After** (CQRS):
```csharp
group.MapPost("/admin/users", async (CreateUserRequest request, IMediator mediator) =>
{
    var command = new CreateUserCommand(
        request.Email,
        request.Password,
        request.DisplayName,
        request.Role
    );
    var user = await mediator.Send(command, ct);
    return Results.Ok(user);
});
```

### Repository Additions Needed

**IUserRepository Extensions** (if not already present):
```csharp
// Add to Authentication.IUserRepository
Task<PagedResult<User>> GetPagedAsync(
    string? searchTerm,
    string? roleFilter,
    string? sortBy,
    string? sortOrder,
    int page,
    int limit,
    CancellationToken cancellationToken = default);

Task<int> CountAdminUsersAsync(CancellationToken cancellationToken = default);
```

**IAlertRepository** (already exists in Administration.Infrastructure):
- ✅ GetActiveAlertsAsync()
- ✅ GetAlertHistoryAsync()
- ✅ ResolveAlertsByTypeAsync()
- ✅ IsThrottledAsync()

### Testing Strategy

**Unit Tests** (per handler):
- Command validation
- Business logic correctness
- Error handling (duplicates, not found, etc.)
- Domain entity creation

**Integration Tests** (per endpoint):
- Full request/response cycle
- Authentication/authorization
- Database persistence
- Error responses

**Existing Tests to Verify**:
- UserManagementService: 75 tests
- AdminStatsService: 20 tests
- AlertingService: 11 tests
- **Total: 106 tests must pass after migration**

### Service Removal Checklist

**Before Deletion**:
1. ✅ All handlers implemented
2. ✅ All endpoints migrated
3. ✅ All tests passing (106/106)
4. ✅ Build succeeds with 0 errors
5. ✅ No references to legacy services

**Services to Remove** (940 lines total):
- ❌ UserManagementService.cs (243 lines)
- ❌ AdminStatsService.cs (410 lines)
- ❌ AlertingService.cs (287 lines)

### DI Registration

**AdministrationServiceExtensions.cs**:
```csharp
public static class AdministrationServiceExtensions
{
    public static IServiceCollection AddAdministrationContext(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // MediatR handlers auto-registered from assembly
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(AdministrationServiceExtensions).Assembly));

        // Alert channels (existing)
        services.AddSingleton<IAlertChannel, EmailAlertChannel>();
        services.AddSingleton<IAlertChannel, SlackAlertChannel>();
        services.AddSingleton<IAlertChannel, PagerDutyAlertChannel>();

        return services;
    }
}
```

### Acceptance Criteria

- [⏳] 15 handlers implemented
- [⏳] All AdminEndpoints.cs migrated to CQRS
- [⏳] 106/106 existing tests passing
- [⏳] Build succeeds (0 errors)
- [⏳] 940 lines removed (legacy services)
- [⏳] Documentation updated

### Timeline Estimate

- **Phase 1** (User Management): 3-4 hours (8 handlers)
- **Phase 2** (Statistics): 1-2 hours (2 handlers + DbContext analytics)
- **Phase 3** (Alerting): 2-3 hours (4 handlers)
- **Testing & Migration**: 2-3 hours (endpoint migration + test verification)
- **Total**: 8-12 hours

### Next Session Handoff

**Ready to Implement**:
1. Start with CreateUserCommandHandler (highest priority)
2. Follow pattern: Handler → Test → Endpoint migration → Verify
3. Reuse Authentication.IUserRepository (already tested)
4. Keep legacy services until all tests pass
5. Final step: Build → Test → Remove services → Build → Test

**Critical Files**:
- Handlers: `Administration/Application/Handlers/*.cs`
- Endpoints: `AdminEndpoints.cs` (lines 70-500 approximately)
- Tests: `tests/Api.Tests/Services/UserManagementServiceTests.cs`
- Tests: `tests/Api.Tests/Services/AdminStatsServiceTests.cs`
- Tests: `tests/Api.Tests/Services/AlertingServiceTests.cs`
