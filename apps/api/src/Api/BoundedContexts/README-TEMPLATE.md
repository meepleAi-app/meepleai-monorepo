# {BoundedContext} Bounded Context

> **Living Documentation**: Questo README è auto-documentato. Riferimenti estratti da codice sorgente.

## Overview

**Responsabilità**: [Breve descrizione del dominio e responsabilità del bounded context]

**Strategia DDD**: [Core Domain | Supporting Domain | Generic Domain]

## Domain Model

### Aggregates
- **{AggregateName}**: [Descrizione root aggregate con invarianti business]
  - Domain File: `Domain/Entities/{AggregateName}.cs`
  - Value Objects: [Lista value objects associati]
  - Domain Events: [Eventi dominio emessi]

### Value Objects
| Nome | Validazioni | Uso |
|------|-------------|-----|
| {ValueObjectName} | [Regole validazione] | [Dove usato] |

### Domain Services
- **{ServiceName}**: [Operazioni che non appartengono a singola entità]
  - File: `Domain/Services/{ServiceName}.cs`

## Application Layer (CQRS)

### Commands
| Command | Handler | Descrizione |
|---------|---------|-------------|
| {CommandName} | {HandlerName} | [Cosa fa il comando] |

**Pattern**:
```csharp
// Esempio comando pattern
public record CreateXCommand(Guid Id, string Name) : IRequest<XDto>;

public class CreateXCommandHandler : IRequestHandler<CreateXCommand, XDto>
{
    public async Task<XDto> Handle(CreateXCommand request, CancellationToken ct)
    {
        // Validazione → Domain logic → Persistence → Mapping
    }
}
```

### Queries
| Query | Handler | Descrizione |
|-------|---------|-------------|
| {QueryName} | {HandlerName} | [Cosa restituisce la query] |

**Pattern**:
```csharp
// Esempio query pattern
public record GetXByIdQuery(Guid Id) : IRequest<XDto>;

public class GetXByIdQueryHandler : IRequestHandler<GetXByIdQuery, XDto>
{
    public async Task<XDto> Handle(GetXByIdQuery request, CancellationToken ct)
    {
        // Fetch → Map → Return (NO business logic)
    }
}
```

## Infrastructure

### Persistence
- **DbContext**: `Infrastructure/Data/{Context}DbContext.cs` (se bounded context specifico)
- **Entity Configuration**: `Infrastructure/Data/Configurations/`
- **Repositories**: [Se pattern repository utilizzato]

### External Services
- **{ServiceName}**: [Integrazioni esterne (API, message brokers, etc.)]
  - Implementation: `Infrastructure/Services/{ServiceName}.cs`
  - Interface: `Application/Interfaces/I{ServiceName}.cs`

## HTTP API

### Endpoints
**Base Path**: `/api/v1/{resource}`

**Riferimento Completo**: [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1)

| Method | Path | Command/Query | Auth |
|--------|------|---------------|------|
| POST | `/api/v1/{resource}` | {CommandName} | API Key/Cookie |
| GET | `/api/v1/{resource}/{id}` | {QueryName} | API Key/Cookie |

**Pattern Routing**:
```csharp
// Minimal API endpoint con MediatR
app.MapPost("/api/v1/{resource}", async (
    [FromBody] CreateXRequest request,
    IMediator mediator,
    CancellationToken ct) =>
{
    var command = new CreateXCommand(request.Id, request.Name);
    var result = await mediator.Send(command, ct);
    return Results.Ok(result);
})
.RequireAuthorization()
.WithName("CreateX")
.WithOpenApi();
```

## Dependencies

### Internal (Bounded Contexts)
- **{ContextName}**: [Perché dipende da questo context]

### External (NuGet)
- **{PackageName}**: [Scopo utilizzo package]

**Riferimento**: `Api.csproj` PackageReference

## Testing

### Unit Tests
**Path**: `tests/Api.Tests/BoundedContexts/{Context}/`

**Coverage**: [Target ≥90%]

**Pattern**:
```csharp
// Example: Command handler test
[Fact]
public async Task Handle_ValidCommand_ReturnsExpectedResult()
{
    // Arrange: Setup mocks, dependencies
    var handler = new CreateXCommandHandler(mockRepo.Object);
    var command = new CreateXCommand(Guid.NewGuid(), "Test");

    // Act: Execute handler
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert: Verify outcome
    Assert.NotNull(result);
    mockRepo.Verify(r => r.AddAsync(It.IsAny<X>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

### Integration Tests
**Pattern**: Testcontainers + WebApplicationFactory

```csharp
// Example: E2E endpoint test
[Fact]
public async Task POST_Resource_Returns201Created()
{
    // Arrange: Spin up Testcontainers
    await using var factory = new CustomWebApplicationFactory();
    var client = factory.CreateClient();

    // Act: HTTP request
    var response = await client.PostAsJsonAsync("/api/v1/resource", new { Name = "Test" });

    // Assert: Status + response
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

## Configuration

**appsettings.json**:
```json
{
  "{Context}": {
    "Setting1": "value",
    "Setting2": 42
  }
}
```

**Dynamic Config**: Gestito tramite `SystemConfiguration` bounded context.

## Observability

### Logging
**Pattern**: Serilog structured logging
```csharp
_logger.LogInformation(
    "Processing {CommandName} for {EntityId}",
    nameof(CreateXCommand),
    command.Id
);
```

### Metrics
**Prometheus**: [Metriche custom se presenti]

### Tracing
**OpenTelemetry**: Automatic tracing via `ActivitySource`

## Migration Guide

### From Legacy (se applicabile)
[Passi migrazione da vecchia architettura a bounded context pattern]

### Breaking Changes
[Changelog breaking changes in questo bounded context]

## FAQs

**Q: Quando usare Command vs Query?**
A: Command = side effects (write). Query = no side effects (read).

**Q: Come gestire transazioni cross-context?**
A: Evitare. Usare eventual consistency con domain events.

**Q: Dove mettere business logic?**
A: Domain entities/services. Application handlers = orchestration.

## References

- **ADR**: `docs/01-architecture/adr/` (decisioni architetturali)
- **DDD Patterns**: `docs/01-architecture/ddd/`
- **OpenAPI Spec**: [http://localhost:8080/openapi/v1.json](http://localhost:8080/openapi/v1.json)

---

**Last Updated**: [Auto-generated on code change]
**Maintainer**: {Context} Team
