# SharedKernel

Common domain primitives and infrastructure shared across all bounded contexts.

## Purpose

The SharedKernel contains building blocks used by multiple bounded contexts:
- Base classes for entities, aggregates, and value objects
- Common interfaces for repositories and unit of work
- CQRS interfaces (commands, queries, handlers)
- Common domain exceptions
- Cross-cutting infrastructure abstractions

## Structure

```
SharedKernel/
├── Domain/                     Domain layer primitives
│   ├── Entities/              Base entity and aggregate root classes
│   │   ├── Entity<TId>        Base class with identity equality
│   │   └── AggregateRoot<TId> Base class with domain event management
│   ├── ValueObjects/          Base value object class
│   │   └── ValueObject        Base class with value equality
│   ├── Validation/            Validation framework (NEW)
│   │   ├── ValidationExtensions     Core validation methods
│   │   ├── CommonValidators         Domain-specific validators
│   │   ├── ValidationHelpers        Helper utilities
│   │   └── README.md               Complete documentation
│   ├── Results/               Result pattern for functional error handling
│   │   ├── Result<T>          Success/failure result wrapper
│   │   └── Error              Error information record
│   ├── Interfaces/            Core domain interfaces
│   │   ├── IEntity<TId>       Entity marker interface
│   │   ├── IAggregateRoot     Aggregate root marker interface
│   │   └── IDomainEvent       Domain event interface
│   └── Exceptions/            Domain exceptions
│       ├── DomainException    Base domain exception
│       └── ValidationException Validation failure exception
│
├── Application/               Application layer primitives
│   ├── Interfaces/            CQRS interfaces
│   │   ├── ICommand           Command marker interface
│   │   ├── ICommand<T>        Command with result interface
│   │   ├── IQuery<T>          Query interface
│   │   ├── ICommandHandler    Command handler interface
│   │   └── IQueryHandler      Query handler interface
│   └── Behaviors/             MediatR pipeline behaviors (future)
│
└── Infrastructure/            Infrastructure layer primitives
    ├── Persistence/           Data access abstractions
    │   ├── IRepository<T,TId> Generic repository interface
    │   └── IUnitOfWork        Unit of work interface
    ├── Caching/               Caching abstractions (future)
    └── Messaging/             Domain event publishing (future)
```

## Usage Patterns

### Entities

Entities have identity and are distinguished by their ID, not their attributes.

```csharp
public class User : AggregateRoot<Guid>
{
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }

    public User(Guid id, string email, string passwordHash) : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
    }

    public void UpdateEmail(string newEmail)
    {
        Email = newEmail;
        AddDomainEvent(new UserEmailChangedEvent(Id, newEmail));
    }
}
```

### Value Objects

Value objects are immutable and have no identity. Equality is based on all properties.

**Modern approach using ValidationExtensions framework:**

```csharp
public class Email : ValueObject
{
    public string Value { get; }

    public Email(string value)
    {
        Value = value
            .NotNullOrWhiteSpace(nameof(Email), "Email cannot be empty")
            .Then(e => e.IsValidEmail())
            .ThrowIfFailure(nameof(Email))
            .ToLowerInvariant();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**Legacy approach (deprecated):**

```csharp
// ❌ Old pattern - avoid in new code
if (string.IsNullOrWhiteSpace(value))
    throw new ValidationException(nameof(Email), "Email cannot be empty");

// ✓ New pattern - use ValidationExtensions
Value = value
    .NotNullOrWhiteSpace(nameof(Email), "Email cannot be empty")
    .ThrowIfFailure(nameof(Email));
```

See [Validation Framework Documentation](Domain/Validation/README.md) for complete usage guide.

### Domain Events

Domain events represent something that happened in the domain.

```csharp
public record UserEmailChangedEvent(Guid UserId, string NewEmail) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public Guid EventId { get; init; } = Guid.NewGuid();
}
```

### Commands (CQRS)

Commands represent intent to change system state.

```csharp
public record CreateUserCommand(string Email, string Password) : ICommand<Guid>;

public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Guid>
{
    private readonly IRepository<User, Guid> _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(IRepository<User, Guid> userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var userId = Guid.NewGuid();
        var user = new User(userId, command.Email, command.Password);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return userId;
    }
}
```

### Queries (CQRS)

Queries represent intent to retrieve data without changing state.

```csharp
public record GetUserByIdQuery(Guid UserId) : IQuery<UserDto?>;

public class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetUserByIdQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserDto?> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == query.UserId)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

### Repository Pattern

Repositories encapsulate data access for aggregate roots.

```csharp
public class UserRepository : IRepository<User, Guid>
{
    private readonly MeepleAiDbContext _dbContext;

    public UserRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(entity, cancellationToken);
    }

    // ... other CRUD operations
}
```

## DDD Principles

### Ubiquitous Language
- Use domain terminology consistently in code, tests, and documentation
- Each bounded context has its own ubiquitous language
- Avoid technical jargon in domain layer (e.g., "User" not "UserEntity")

### Aggregates
- Aggregate roots control access to their child entities
- All changes go through the aggregate root
- One transaction = one aggregate
- Keep aggregates small (2-3 entities max)

### Domain Events
- Use domain events to communicate between aggregates
- Events are immutable (use `record` with `init` properties)
- Events use past tense (e.g., `UserCreated`, not `CreateUser`)

### Bounded Contexts
- Each context has its own domain model
- Contexts communicate via domain events or application services
- No direct database access across contexts (use repositories)

## Dependencies

- **MediatR** (12.4.1): CQRS implementation (commands, queries, handlers)
- **EF Core** (9.0+): Persistence via DbContext and repositories

## Best Practices

### DO
✅ Keep domain layer pure (no infrastructure dependencies)
✅ Make value objects immutable
✅ Validate in constructors (fail fast)
✅ Use domain events for cross-aggregate communication
✅ Keep aggregates small and focused
✅ Use repositories only for aggregate roots

### DON'T
❌ Put business logic in application services (belongs in domain)
❌ Access child entities directly (go through aggregate root)
❌ Create large aggregates (performance issues)
❌ Use domain events for local operations (just call methods)
❌ Share entities across bounded contexts (use DTOs)

## Testing

### Domain Layer Tests
- Test entity business logic in isolation
- Test value object validation and equality
- Test domain services with mocked dependencies
- No database required (pure unit tests)

### Application Layer Tests
- Test command/query handlers with mocked repositories
- Test application services with mocked domain services
- Verify correct domain events raised

### Integration Tests
- Test repositories with real database (Testcontainers)
- Test full use case flows (command → handler → repository → DB)
- Test domain event publishing and handling

## Future Enhancements

### Planned (Phase 1)
- Domain event dispatcher implementation
- MediatR pipeline behaviors (logging, validation, caching)
- Unit of work EF Core implementation

### Planned (Future Phases)
- Domain event sourcing support
- CQRS read models (separate from write models)
- Saga pattern for cross-context workflows
- Outbox pattern for reliable event publishing

## References

- [Domain-Driven Design (Eric Evans)](https://www.domainlanguage.com/ddd/)
- [Implementing DDD (Vaughn Vernon)](https://vaughnvernon.co/)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [EF Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
