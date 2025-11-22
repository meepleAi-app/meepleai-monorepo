# Diagramma Flusso CQRS/MediatR

## Pattern CQRS - Flow Generale

```mermaid
sequenceDiagram
    participant Client as HTTP Client
    participant Endpoint as HTTP Endpoint
    participant Mediator as IMediator<br/>(MediatR)
    participant Handler as Command/Query<br/>Handler
    participant Domain as Domain Layer<br/>(Aggregates, VOs)
    participant Repo as Repository<br/>(Infrastructure)
    participant DB as Database<br/>(PostgreSQL)

    Client->>Endpoint: HTTP Request<br/>(POST/GET/PUT/DELETE)

    Note over Endpoint: Autorizzazione<br/>[Authorize] attribute

    Endpoint->>Endpoint: Valida DTO input
    Endpoint->>Endpoint: Crea Command/Query

    Endpoint->>Mediator: Send(command/query)

    Note over Mediator: Pipeline Behaviors<br/>- Validation<br/>- Logging<br/>- Transaction

    Mediator->>Handler: Handle(request, ct)

    alt Command (Write Operation)
        Handler->>Repo: Query existing entity
        Repo->>DB: SELECT query
        DB-->>Repo: Entity data
        Repo-->>Handler: Domain entity

        Handler->>Domain: Business logic<br/>Aggregate methods
        Note over Domain: Domain validation<br/>Value Objects<br/>Domain events
        Domain-->>Handler: Updated entity

        Handler->>Repo: SaveAsync(entity)
        Repo->>DB: INSERT/UPDATE
        DB-->>Repo: Success
        Repo-->>Handler: Saved entity

        Handler->>Handler: Map to DTO
        Handler-->>Mediator: Response DTO

    else Query (Read Operation)
        Handler->>Repo: Query data<br/>(AsNoTracking)
        Repo->>DB: SELECT query
        DB-->>Repo: Raw data
        Repo-->>Handler: Domain entities

        Handler->>Handler: Map to DTO<br/>Projection
        Handler-->>Mediator: Response DTO
    end

    Mediator-->>Endpoint: Result<T>

    Endpoint->>Endpoint: Map to HTTP response
    Endpoint-->>Client: HTTP Response<br/>(200/201/400/404/500)
```

## Esempio Concreto: User Registration

```mermaid
sequenceDiagram
    participant Client
    participant Endpoint as POST /api/v1/auth/register
    participant Mediator as IMediator
    participant Handler as RegisterCommandHandler
    participant User as User Aggregate
    participant Session as Session Entity
    participant URepo as IUserRepository
    participant SRepo as ISessionRepository
    participant UoW as IUnitOfWork
    participant DB as PostgreSQL

    Client->>Endpoint: POST<br/>{email, password, displayName}

    Endpoint->>Endpoint: Valida input<br/>[FromBody] validation

    Endpoint->>Endpoint: Crea RegisterCommand
    Note over Endpoint: new RegisterCommand(<br/>  Email: dto.Email,<br/>  Password: dto.Password,<br/>  DisplayName: dto.DisplayName<br/>)

    Endpoint->>Mediator: Send(registerCommand)

    Note over Mediator: ValidationBehavior<br/>checks command

    Mediator->>Handler: Handle(command, ct)

    Handler->>Handler: Crea Email ValueObject
    Note over Handler: var email = new Email(command.Email);<br/>Validation in constructor

    Handler->>URepo: GetByEmailAsync(email)
    URepo->>DB: SELECT * FROM Users<br/>WHERE Email = @email
    DB-->>URepo: null (not found)
    URepo-->>Handler: null

    Handler->>Handler: Crea PasswordHash ValueObject
    Note over Handler: var passwordHash =<br/>PasswordHash.Create(command.Password);<br/>PBKDF2 210k iterations

    Handler->>Handler: Crea Role ValueObject
    Note over Handler: var role = Role.Parse("User")

    Handler->>User: new User(<br/>id, email, displayName,<br/>passwordHash, role)
    Note over User: Domain validation<br/>in constructor
    User-->>Handler: User aggregate

    Handler->>Session: CreateSession(user,<br/>ipAddress, userAgent)
    Note over Session: SessionToken generated<br/>ExpiresAt = Now + 30d
    Session-->>Handler: Session entity

    Handler->>URepo: AddAsync(user, ct)
    URepo->>DB: INSERT INTO Users
    DB-->>URepo: UserId
    URepo-->>Handler: Success

    Handler->>SRepo: AddAsync(session, ct)
    SRepo->>DB: INSERT INTO Sessions
    DB-->>SRepo: SessionId
    SRepo-->>Handler: Success

    Handler->>UoW: SaveChangesAsync(ct)
    Note over UoW: Transaction commit
    UoW->>DB: COMMIT
    DB-->>UoW: Success
    UoW-->>Handler: Saved

    Handler->>Handler: Map to RegisterResponse DTO
    Note over Handler: MapToUserDto(user)<br/>+ sessionToken + expiresAt

    Handler-->>Mediator: RegisterResponse
    Mediator-->>Endpoint: RegisterResponse

    Endpoint->>Endpoint: Set-Cookie header<br/>(httpOnly, secure)

    Endpoint-->>Client: 200 OK<br/>{user, sessionToken, expiresAt}
```

## Layered Architecture (DDD + CQRS)

```mermaid
graph TB
    subgraph "HTTP Layer"
        Endpoints[HTTP Endpoints<br/>Routing/*.cs<br/>Minimal APIs]
    end

    subgraph "Application Layer"
        Commands[Commands<br/>Write operations]
        Queries[Queries<br/>Read operations]
        Handlers[Handlers<br/>Business orchestration]
        DTOs[DTOs<br/>Data Transfer Objects]
    end

    subgraph "Domain Layer - Pure Business Logic"
        Aggregates[Aggregates<br/>User, Game, PdfDocument]
        ValueObjects[Value Objects<br/>Email, PasswordHash, Confidence]
        DomainServices[Domain Services<br/>Pure domain logic]
        Repositories[Repository Interfaces<br/>Contracts only]
    end

    subgraph "Infrastructure Layer"
        RepoImpl[Repository Implementations<br/>EF Core]
        DbContext[AppDbContext<br/>Entity mappings]
        ExternalServices[External Services<br/>Qdrant, Ollama, etc.]
    end

    subgraph "Cross-Cutting"
        Mediator[MediatR Pipeline]
        Validation[FluentValidation]
        Logging[Serilog]
        Telemetry[OpenTelemetry]
    end

    Endpoints -->|IMediator.Send| Mediator
    Mediator -->|Dispatch| Commands
    Mediator -->|Dispatch| Queries

    Commands --> Handlers
    Queries --> Handlers

    Handlers -->|Use| Aggregates
    Handlers -->|Use| DomainServices
    Handlers -->|Depend on| Repositories

    Aggregates -->|Contain| ValueObjects
    DomainServices -->|Operate on| Aggregates

    Repositories -->|Implemented by| RepoImpl
    RepoImpl -->|Use| DbContext
    RepoImpl -->|Call| ExternalServices

    Mediator -.->|Behaviors| Validation
    Mediator -.->|Behaviors| Logging
    Mediator -.->|Behaviors| Telemetry

    Handlers -->|Return| DTOs
    DTOs -->|Mapped to| Endpoints

    style Endpoints fill:#4fc3f7
    style Commands fill:#66bb6a
    style Queries fill:#81c784
    style Handlers fill:#29b6f6
    style Aggregates fill:#ab47bc
    style ValueObjects fill:#ba68c8
    style DomainServices fill:#9c27b0
    style RepoImpl fill:#ef5350
    style DbContext fill:#f44336
    style Mediator fill:#ffa726
```

## Command vs Query Pattern

### Command Pattern (Write)

```mermaid
graph LR
    Command[Command Object<br/>Immutable record] -->|IRequest&lt;TResponse&gt;| Handler[CommandHandler]
    Handler -->|1. Load| Repo[Repository]
    Repo -->|Entity| Handler
    Handler -->|2. Execute| Domain[Domain Logic<br/>Aggregate methods]
    Domain -->|Modified Entity| Handler
    Handler -->|3. Save| Repo
    Repo -->|Persist| DB[(Database)]
    Handler -->|4. Return| Response[Response DTO]

    style Command fill:#66bb6a
    style Handler fill:#29b6f6
    style Domain fill:#ab47bc
    style Repo fill:#ef5350
```

**Caratteristiche**:
- Modifica stato
- Transazionale (UnitOfWork)
- Validazione completa
- Domain events
- Audit logging

### Query Pattern (Read)

```mermaid
graph LR
    Query[Query Object<br/>Immutable record] -->|IRequest&lt;TResponse&gt;| Handler[QueryHandler]
    Handler -->|Read-only<br/>AsNoTracking| Repo[Repository]
    Repo -->|Raw data| DB[(Database)]
    DB -->|Entities| Repo
    Repo -->|Entities| Handler
    Handler -->|Projection| DTO[Response DTO]
    Handler -->|Return| Response[Response]

    style Query fill:#81c784
    style Handler fill:#29b6f6
    style Repo fill:#ef5350
    style DTO fill:#ffa726
```

**Caratteristiche**:
- Nessuna modifica stato
- AsNoTracking (30% più veloce)
- Proiezione diretta a DTO
- Caching possibile
- Nessuna transazione

## MediatR Pipeline Behaviors

```mermaid
graph TB
    Request[IRequest] -->|Enter| Pipeline{MediatR Pipeline}

    Pipeline -->|1| Auth[Authorization Behavior]
    Auth -->|Authorized| Validation
    Auth -->|Unauthorized| Error1[403 Forbidden]

    Validation[Validation Behavior<br/>FluentValidation] -->|Valid| Logging
    Validation -->|Invalid| Error2[400 Bad Request]

    Logging[Logging Behavior<br/>Serilog] -->|Log request| Transaction

    Transaction[Transaction Behavior<br/>Commands only] -->|Begin| Handler[Actual Handler]

    Handler -->|Success| Commit[Commit Transaction]
    Handler -->|Exception| Rollback[Rollback Transaction]

    Commit --> LogSuccess[Log success]
    Rollback --> LogError[Log error]

    LogSuccess --> Response[Return Response]
    LogError --> Error3[500 Internal Error]

    style Pipeline fill:#ffa726
    style Auth fill:#ab47bc
    style Validation fill:#66bb6a
    style Logging fill:#29b6f6
    style Transaction fill:#ef5350
    style Handler fill:#4fc3f7
```

## Bounded Context Interactions

```mermaid
graph TB
    subgraph Authentication
        AuthCmd[Commands/Queries]
        AuthDomain[User, Session, ApiKey]
    end

    subgraph GameManagement
        GameCmd[Commands/Queries]
        GameDomain[Game, GameSession]
    end

    subgraph DocumentProcessing
        DocCmd[Commands/Queries]
        DocDomain[PdfDocument]
    end

    subgraph KnowledgeBase
        KBCmd[Commands/Queries]
        KBDomain[ChatThread, VectorDocument]
    end

    AuthCmd -->|UserId| GameCmd
    AuthCmd -->|UserId| DocCmd
    AuthCmd -->|UserId| KBCmd

    GameCmd -->|GameId| DocCmd
    GameCmd -->|GameId| KBCmd

    DocCmd -->|Extracted Text| KBCmd
    DocCmd -->|PDF Chunks| KBCmd

    style Authentication fill:#ab47bc
    style GameManagement fill:#66bb6a
    style DocumentProcessing fill:#ffa726
    style KnowledgeBase fill:#29b6f6
```

## Dependency Injection Flow

```mermaid
graph TB
    Program[Program.cs<br/>Application startup] -->|Register| Services{Service Collection}

    Services -->|AddMediatR| MediatR[MediatR Services]
    Services -->|AddBoundedContext| Auth[Authentication Context]
    Services -->|AddBoundedContext| Game[GameManagement Context]
    Services -->|AddBoundedContext| Doc[DocumentProcessing Context]
    Services -->|AddBoundedContext| KB[KnowledgeBase Context]
    Services -->|AddBoundedContext| SysConfig[SystemConfiguration Context]
    Services -->|AddBoundedContext| Workflow[WorkflowIntegration Context]
    Services -->|AddBoundedContext| Admin[Administration Context]

    Services -->|AddDbContext| EF[Entity Framework]
    Services -->|AddInfrastructure| Infra[Infrastructure Services]

    Infra --> Qdrant[Qdrant Client]
    Infra --> Redis[Redis Client]
    Infra --> Ollama[Ollama Client]

    Services -->|Build| Provider[Service Provider]

    Provider -->|Resolve at runtime| Endpoint[HTTP Endpoints]

    style Program fill:#4fc3f7
    style Services fill:#ffa726
    style Provider fill:#66bb6a
```

---

**Pattern**: Clean Architecture + DDD + CQRS + Event Sourcing (partial)

**Versione**: 1.0
**Data**: 2025-11-13
