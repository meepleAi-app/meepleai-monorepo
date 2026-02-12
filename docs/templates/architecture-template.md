# [Feature/Module Name] - Architecture

## Quick Reference

| Aspect | Value |
|--------|-------|
| **Bounded Context** | [Context Name] |
| **Layer** | Domain / Application / Infrastructure |
| **Dependencies** | [List key dependencies] |
| **Entry Point** | [API endpoint or component] |
| **Responsibility** | [One-line description] |

## Architecture Diagram

```mermaid
flowchart TD
    A[Client Request] --> B[Endpoint/Component]
    B --> C{Validation}
    C -->|Valid| D[Command/Query Handler]
    C -->|Invalid| E[Error Response]
    D --> F[Domain Logic]
    F --> G[Repository]
    G --> H[(Database)]
    D --> I[External Service]
    F --> J[Event Publisher]

    style B fill:#e1f5ff
    style D fill:#fff4e1
    style F fill:#e8f5e9
    style G fill:#f3e5f5
```

## Component Responsibilities

| Component | Responsibility | Input | Output |
|-----------|---------------|-------|--------|
| **Endpoint** | HTTP routing, DTO mapping | HTTP Request | IResult |
| **Validator** | Business rule validation | Command/Query | ValidationResult |
| **Handler** | Orchestration logic | Command/Query | Response DTO |
| **Domain Entity** | Core business logic | Method calls | State changes |
| **Repository** | Data persistence | Entity | Entity/bool |
| **Service** | Cross-cutting concerns | Varies | Varies |

## Data Flow

```mermaid
sequenceDiagram
    participant C as Client
    participant E as Endpoint
    participant V as Validator
    participant H as Handler
    participant D as Domain
    participant R as Repository

    C->>E: HTTP Request
    E->>V: Validate(command)
    V-->>E: ValidationResult
    alt Valid
        E->>H: Send(command)
        H->>D: ExecuteBusinessLogic()
        D->>R: Save(entity)
        R-->>D: Success
        D-->>H: Result
        H-->>E: Response DTO
        E-->>C: 200 OK
    else Invalid
        E-->>C: 400 BadRequest
    end
```

## Layer Interaction Rules

```
┌─────────────────────────────────────────┐
│         Presentation Layer              │
│  (Endpoints, Controllers, Components)   │
└─────────────────┬───────────────────────┘
                  ↓ (DTOs only)
┌─────────────────────────────────────────┐
│        Application Layer                │
│   (Commands, Queries, Handlers)         │
└─────────────────┬───────────────────────┘
                  ↓ (Domain entities)
┌─────────────────────────────────────────┐
│          Domain Layer                   │
│    (Entities, Value Objects, Events)    │
└─────────────────┬───────────────────────┘
                  ↑ (Repository interfaces)
┌─────────────────────────────────────────┐
│       Infrastructure Layer              │
│  (Repositories, External Services)      │
└─────────────────────────────────────────┘
```

**✅ ALLOWED**:
- Presentation → Application (via DTOs)
- Application → Domain (via entities)
- Infrastructure → Domain (implements interfaces)

**❌ FORBIDDEN**:
- Presentation → Domain (bypass Application)
- Domain → Infrastructure (direct dependency)
- Infrastructure → Application (upward dependency)

## Key Design Patterns

| Pattern | Usage | Location |
|---------|-------|----------|
| **CQRS** | Separate read/write models | Commands/Queries |
| **Repository** | Data access abstraction | Infrastructure |
| **Factory Method** | Entity creation | Domain entities |
| **Value Object** | Immutable domain concepts | Domain |
| **Domain Event** | Async side effects | Domain → Handlers |

## Decision Tree: When to Use

```
Need data persistence?
├─ Yes → Repository pattern
│   └─ Multiple data sources?
│       ├─ Yes → Unit of Work pattern
│       └─ No → Single repository
└─ No → In-memory/stateless service

Need validation?
├─ Yes → FluentValidation in Application layer
│   └─ Domain rules? → Also in Domain entity
└─ No → Simple DTO mapping

Need async communication?
├─ Yes → Domain Events + MediatR
└─ No → Direct method calls
```

## File Organization

```
BoundedContext/
├── Domain/
│   ├── Entities/
│   │   └── [EntityName].cs
│   ├── ValueObjects/
│   │   └── [ValueObjectName].cs
│   ├── Events/
│   │   └── [EventName].cs
│   └── Repositories/
│       └── I[EntityName]Repository.cs
├── Application/
│   ├── Commands/
│   │   ├── [Action]Command.cs
│   │   ├── [Action]CommandValidator.cs
│   │   └── [Action]CommandHandler.cs
│   └── Queries/
│       ├── [Query]Query.cs
│       ├── [Query]QueryValidator.cs
│       └── [Query]QueryHandler.cs
└── Infrastructure/
    └── Repositories/
        └── [EntityName]Repository.cs
```

## Testing Strategy

| Test Type | Target | Tool | Coverage Goal |
|-----------|--------|------|---------------|
| **Unit** | Domain entities, validators | xUnit | 90%+ |
| **Integration** | Handlers + DB | Testcontainers | 85%+ |
| **E2E** | Full request flow | API tests | 70%+ |

## Common Pitfalls

| ❌ Anti-Pattern | ✅ Correct Pattern |
|----------------|-------------------|
| Direct service injection in endpoints | Use `IMediator.Send()` only |
| `InvalidOperationException` for business rules | Use domain-specific exceptions |
| Public setters on entities | Private setters + factory methods |
| Shared DTOs between commands/queries | Separate DTO per operation |
| Fat handlers with business logic | Logic in Domain, orchestration in Handler |

## Performance Considerations

| Concern | Solution | Implementation |
|---------|----------|----------------|
| **Caching** | HybridCache for queries | `IHybridCacheService` |
| **N+1 Queries** | Eager loading | `.Include()` in repositories |
| **Large result sets** | Pagination | `Skip().Take()` pattern |
| **Expensive operations** | Background jobs | Quartz.NET |

## Dependencies

```mermaid
graph LR
    A[This Module] --> B[SharedGameCatalog BC]
    A --> C[Authentication BC]
    A --> D[External: Qdrant]
    A --> E[External: Redis]

    style A fill:#e1f5ff
    style B fill:#fff4e1
    style C fill:#fff4e1
    style D fill:#ffe8e8
    style E fill:#ffe8e8
```

## Configuration

| Setting | Environment Variable | Default | Required |
|---------|---------------------|---------|----------|
| [Setting 1] | `ENV_VAR_NAME` | `default_value` | ✅ Yes |
| [Setting 2] | `ENV_VAR_NAME` | `default_value` | ❌ No |

## Related Documentation

- **API Reference**: [Link to API template]
- **Testing Guide**: [Link to testing template]
- **Troubleshooting**: [Link to troubleshooting template]
