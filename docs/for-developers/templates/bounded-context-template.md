# {ContextName} Bounded Context

**{Brief description of context responsibilities}**

---

## 📋 Responsabilità

- {Responsibility 1}
- {Responsibility 2}
- {Responsibility 3}
- {Responsibility 4}

---

## 🏗️ Domain Model

### Aggregates

**{AggregateName}** (Aggregate Root):
```csharp
public class {AggregateName}
{
    public Guid Id { get; private set; }
    // Key properties
    public {Type} {PropertyName} { get; private set; }

    // Collections
    public List<{ChildEntity}> {CollectionName} { get; private set; }

    // Domain methods
    public void {DomainMethod}() { }
}
```

**{ChildEntity}**:
```csharp
public class {ChildEntity}
{
    public Guid Id { get; private set; }
    public Guid {ParentId} { get; private set; }
    // Properties
}
```

### Value Objects

**{ValueObjectName}**:
```csharp
public record {ValueObjectName}
{
    public {Type} Value { get; init; }

    public static {ValueObjectName} Create({Type} value)
    {
        // Validation logic
        return new {ValueObjectName} { Value = value };
    }
}
```

---

## 📡 Application Layer (CQRS)

### Commands (Write Operations)

#### {Category} Commands

| Command | HTTP Method | Endpoint | Auth | Request | Response |
|---------|-------------|----------|------|---------|----------|
| `{CommandName}` | POST/PUT/DELETE | `/api/v1/{path}` | None/Cookie/API Key | `{RequestDTO}` | `{ResponseDTO}` |

**{CommandName}**:
- **Purpose**: {Brief description}
- **Request Schema**:
  ```json
  {
    "field1": "value",
    "field2": 123
  }
  ```
- **Response Schema**:
  ```json
  {
    "id": "guid",
    "message": "string"
  }
  ```
- **Validation Rules**:
  - {Rule 1}
  - {Rule 2}
- **Error Codes**:
  - `400`: {Validation error scenario}
  - `404`: {Not found scenario}
  - `409`: {Conflict scenario}
- **Domain Events Raised**:
  - `{EventName}`: {When raised}

---

### Queries (Read Operations)

#### {Category} Queries

| Query | HTTP Method | Endpoint | Auth | Query Params | Response |
|-------|-------------|----------|------|--------------|----------|
| `{QueryName}` | GET | `/api/v1/{path}` | Cookie | `?param=value` | `{ResponseDTO}` |

**{QueryName}**:
- **Purpose**: {Brief description}
- **Query Parameters**:
  - `param1` (optional): {Description}
  - `param2` (required): {Description}
- **Response Schema**:
  ```json
  {
    "data": [],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 100
    }
  }
  ```
- **Filters**:
  - {Filter option 1}
  - {Filter option 2}
- **Sorting**:
  - Default: {sort field} {direction}
  - Available: {other sort options}

---

## 🔄 Domain Events

| Event | When Raised | Payload | Subscribers |
|-------|-------------|---------|-------------|
| `{EventName}` | {Trigger condition} | `{EventPayloadClass}` | {Other contexts} |

**{EventName}**:
```csharp
public class {EventName} : INotification
{
    public Guid {EntityId} { get; init; }
    public {Type} {Property} { get; init; }
}
```

**Event Handlers** (Cross-Context Integration):
- **{OtherContext}**: {What it does with this event}

---

## 🔗 Integration Points

### Inbound Dependencies

**{OtherContext}**:
- {What it needs from this context}
- Example: Fetch {entity} metadata for {purpose}

### Outbound Dependencies

**{OtherContext}**:
- {What this context needs from other context}
- Example: Query {entity} for {purpose}

### Event-Driven Communication

```mermaid
graph LR
    A[{ThisContext}] -->|{EventName}| B[{OtherContext}]
    B -->|{ResponseEvent}| A
```

---

## 🔐 Security & Authorization

### Authentication Requirements

| Endpoint Pattern | Auth Method | Required Role |
|------------------|-------------|---------------|
| `/api/v1/{context}/*` | Cookie OR API Key | User |
| `/api/v1/{context}/admin/*` | Cookie | Admin |
| `/api/v1/{context}/public/*` | None | - |

### Data Access Policies

- **User Isolation**: Users can only access their own {entities}
- **Admin Override**: Admins can access any {entities}
- **Soft-Delete**: Deleted {entities} hidden from queries (global filter)

### Rate Limiting

| Endpoint | Limit | Window |
|----------|-------|--------|
| `{endpoint-pattern}` | {n} requests | per {timeframe} |

---

## 🎯 Common Usage Examples

### Example 1: {Common Operation}

**Scenario**: {User story}

**API Call**:
```bash
curl -X POST http://localhost:8080/api/v1/{context}/{operation} \
  -H "Content-Type: application/json" \
  -H "Cookie: session_token={token}" \
  -d '{
    "field": "value"
  }'
```

**Response**:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "message": "Success"
}
```

---

## 📊 Performance Characteristics

### Caching Strategy

| Operation | Cache | TTL | Invalidation |
|-----------|-------|-----|--------------|
| `{QueryName}` | Redis | {duration} | On {EventName} |

### Database Indexes

```sql
-- Key indexes for performance
CREATE INDEX idx_{table}_{field} ON {Table}({Field});
```

---

## 🧪 Testing Strategy

### Unit Tests
- Location: `tests/Api.Tests/{Context}/`
- Coverage: 90%+ target
- Focus: Domain logic, validators, command/query handlers

### Integration Tests
- Database: Testcontainers PostgreSQL
- External Services: Mock {external dependencies}
- Focus: Repository operations, event handlers

### E2E Tests
- Location: `apps/web/__tests__/e2e/{context}/`
- Focus: Complete user workflows
- Examples: {workflow 1}, {workflow 2}

---

## 📂 Code Location

```
apps/api/src/Api/BoundedContexts/{Context}/
├── Domain/
│   ├── Entities/              # Aggregates and entities
│   ├── ValueObjects/          # Immutable value objects
│   ├── Repositories/          # Repository interfaces
│   ├── Services/              # Domain services
│   └── Events/                # Domain events
├── Application/
│   ├── Commands/              # Write operations
│   ├── Queries/               # Read operations
│   ├── Handlers/              # MediatR handlers
│   ├── DTOs/                  # Data transfer objects
│   └── Validators/            # FluentValidation rules
└── Infrastructure/
    ├── Persistence/           # EF Core repositories
    ├── Services/              # External API clients
    └── DependencyInjection/   # Service registration
```

---

## 🔗 Related Documentation

### Architecture
- [ADR-{number}: {Title}](../architecture/adr/adr-{number}-{slug}.md) - {Brief description}

### Other Bounded Contexts
- [{RelatedContext}](./{related-context}.md) - {Relationship description}

### API Reference
- [Scalar API Docs](http://localhost:8080/scalar/v1) - Interactive API explorer

---

**Status**: ✅ Production / 🚧 Beta / 🔴 Planning
**Last Updated**: {Date}
**Total Commands**: {N}
**Total Queries**: {N}
**Test Coverage**: {percentage}%
