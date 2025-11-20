# Guida Sviluppatore Backend - MeepleAI

## Panoramica Generale

Il backend di MeepleAI implementa un'architettura **Domain-Driven Design (DDD)** con pattern **CQRS** utilizzando **MediatR**, organizzata in **7 bounded contexts**. La migrazione a DDD è **100% completa** con **224 handler CQRS** operativi (96 command + 87 query + 41 event handler), **5.387 righe** di codice legacy rimosso e **83+ endpoint** migrati a MediatR.

**Stack Tecnologico:**
- ASP.NET Core 9
- Entity Framework Core 9
- PostgreSQL (database principale)
- Qdrant (vector database per RAG)
- Redis (cache + background tasks)
- MediatR (CQRS pattern)
- Serilog (logging)
- OpenTelemetry (observability)

---

## Architettura DDD

### Struttura dei Bounded Contexts

```
apps/api/src/Api/BoundedContexts/
├── Authentication/         # Auth, sessioni, API key, OAuth, 2FA
├── GameManagement/         # Catalogo giochi, sessioni di gioco
├── KnowledgeBase/          # RAG, vettori, chat (Hybrid: vector+keyword RRF)
├── DocumentProcessing/     # Upload PDF, estrazione, validazione
├── WorkflowIntegration/    # Workflow n8n, logging errori
├── SystemConfiguration/    # Configurazione runtime, feature flags
└── Administration/         # Utenti, alert, audit, analytics
```

### Pattern Architetturale

Ogni bounded context segue questa struttura:

```
{Context}/
├── Domain/              # Logica di business pura
│   ├── Entities/       # Aggregati, entità
│   ├── ValueObjects/   # Oggetti valore (immutabili)
│   └── Services/       # Servizi di dominio
├── Application/         # Livello applicativo (CQRS)
│   ├── Commands/       # Comandi (scrittura)
│   ├── Queries/        # Query (lettura)
│   ├── Handlers/       # Handler MediatR
│   └── Events/         # Domain events
└── Infrastructure/      # Adattatori tecnici
    ├── Repositories/   # Persistenza
    └── Adapters/       # Servizi esterni
```

**Principio fondamentale:** Domain (logica pura) → Application (CQRS) → Infrastructure (adattatori) → HTTP (MediatR)

---

## 1. Bounded Context: Authentication

**Responsabilità:**
- Registrazione, login, logout utenti
- Gestione sessioni (cookie-based)
- Autenticazione API key (dual auth: cookie + header)
- Integrazione OAuth 2.0 (Google, Discord, GitHub)
- Autenticazione a due fattori (TOTP + backup codes)
- Flussi di reset password

### Entità e Aggregati Principali

#### **User** (Aggregate Root)
```csharp
public class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; }
    public PasswordHash PasswordHash { get; private set; }
    public Role Role { get; private set; }
    public bool IsTwoFactorEnabled { get; private set; }
    public TotpSecret? TotpSecret { get; private set; }

    // Collezioni di entità figlie
    public List<Session> Sessions { get; private set; }
    public List<ApiKey> ApiKeys { get; private set; }
    public List<OAuthAccount> OAuthAccounts { get; private set; }

    // Metodi di business logic
    public void ChangePassword(string oldPassword, string newPassword)
    {
        if (!PasswordHash.Verify(oldPassword))
            throw new DomainException("Password corrente non valida");

        PasswordHash = PasswordHash.Create(newPassword);
        DomainEvents.Raise(new PasswordChangedEvent(Id));
    }

    public void EnableTwoFactor(string totpSecret)
    {
        TotpSecret = new TotpSecret(totpSecret);
        IsTwoFactorEnabled = true;
        DomainEvents.Raise(new TwoFactorEnabledEvent(Id));
    }
}
```

#### **Session** (Entità)
```csharp
public class Session
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public SessionToken Token { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public void Extend(TimeSpan duration)
    {
        ExpiresAt = DateTime.UtcNow.Add(duration);
        DomainEvents.Raise(new SessionExtendedEvent(Id, UserId));
    }

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}
```

### Value Objects

#### **Email**
```csharp
public record Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email non può essere vuota");

        if (!IsValidEmail(value))
            throw new DomainException("Formato email non valido");

        Value = value.ToLowerInvariant().Trim();
    }

    private static bool IsValidEmail(string email)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }
}
```

#### **PasswordHash**
```csharp
public record PasswordHash
{
    public string Hash { get; }

    private PasswordHash(string hash) => Hash = hash;

    public static PasswordHash Create(string plainPassword)
    {
        // PBKDF2 con 210.000 iterazioni
        var hash = BCrypt.Net.BCrypt.HashPassword(plainPassword, 12);
        return new PasswordHash(hash);
    }

    public bool Verify(string plainPassword)
    {
        return BCrypt.Net.BCrypt.Verify(plainPassword, Hash);
    }
}
```

### Comandi Principali

#### **RegisterCommand**
```csharp
public record RegisterCommand(
    string Email,
    string Password,
    string DisplayName
) : ICommand<RegisterResponse>;

public class RegisterCommandHandler : ICommandHandler<RegisterCommand, RegisterResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public async Task<RegisterResponse> Handle(RegisterCommand command, CancellationToken ct)
    {
        // 1. Validazione univocità email
        var email = new Email(command.Email);
        var existingUser = await _userRepository.GetByEmailAsync(email, ct);
        if (existingUser != null)
            throw new DomainException("Email già registrata");

        // 2. Creazione utente (aggregato)
        var user = new User(
            id: Guid.NewGuid(),
            email: email,
            passwordHash: PasswordHash.Create(command.Password),
            displayName: command.DisplayName,
            role: Role.User
        );

        await _userRepository.AddAsync(user, ct);

        // 3. Creazione sessione automatica
        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            expiresAt: DateTime.UtcNow.AddHours(24)
        );

        await _sessionRepository.AddAsync(session, ct);

        // 4. Commit transazione
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Utente registrato: {Email}", email.Value);

        return new RegisterResponse(
            User: MapToDto(user),
            SessionToken: session.Token.Value
        );
    }
}
```

#### **LoginCommand** (con supporto 2FA)
```csharp
public record LoginCommand(
    string Email,
    string Password,
    string? TwoFactorCode = null,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<LoginResponse>;

public class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand command, CancellationToken ct)
    {
        // 1. Trova utente
        var email = new Email(command.Email);
        var user = await _userRepository.GetByEmailAsync(email, ct);

        if (user == null || !user.PasswordHash.Verify(command.Password))
            throw new DomainException("Email o password non validi");

        // 2. Controllo 2FA
        if (user.IsTwoFactorEnabled)
        {
            if (string.IsNullOrEmpty(command.TwoFactorCode))
            {
                // Crea sessione temporanea (5 minuti)
                var tempSession = await CreateTempSessionAsync(user, ct);
                return new LoginResponse(
                    RequiresTwoFactor: true,
                    TempSessionToken: tempSession.Token.Value
                );
            }

            // Verifica codice TOTP
            if (!user.VerifyTotpCode(command.TwoFactorCode))
                throw new DomainException("Codice 2FA non valido");
        }

        // 3. Crea sessione permanente
        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            expiresAt: DateTime.UtcNow.AddHours(24),
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent
        );

        await _sessionRepository.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new LoginResponse(
            User: MapToDto(user),
            SessionToken: session.Token.Value,
            ExpiresAt: session.ExpiresAt
        );
    }
}
```

### Query Principali

#### **GetUserSessionsQuery**
```csharp
public record GetUserSessionsQuery(Guid UserId) : IQuery<List<SessionDto>>;

public class GetUserSessionsQueryHandler
    : IQueryHandler<GetUserSessionsQuery, List<SessionDto>>
{
    private readonly ISessionRepository _sessionRepository;

    public async Task<List<SessionDto>> Handle(GetUserSessionsQuery query, CancellationToken ct)
    {
        var sessions = await _sessionRepository.GetByUserIdAsync(query.UserId, ct);

        return sessions
            .Where(s => !s.IsExpired())
            .OrderByDescending(s => s.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    private static SessionDto MapToDto(Session session)
    {
        return new SessionDto(
            Id: session.Id,
            IpAddress: session.IpAddress,
            UserAgent: ParseUserAgent(session.UserAgent),
            CreatedAt: session.CreatedAt,
            ExpiresAt: session.ExpiresAt
        );
    }
}
```

### Domain Events

```csharp
// Event
public record PasswordChangedEvent(Guid UserId) : DomainEventBase;

// Handler
public class PasswordChangedEventHandler
    : DomainEventHandlerBase<PasswordChangedEvent>
{
    private readonly IAlertingService _alertingService;

    protected override async Task HandleEventAsync(
        PasswordChangedEvent domainEvent,
        CancellationToken ct)
    {
        // Invia notifica email all'utente
        await _alertingService.SendPasswordChangeNotificationAsync(
            domainEvent.UserId,
            ct);

        Logger.LogInformation(
            "Password cambiata per utente {UserId}",
            domainEvent.UserId);
    }
}
```

### Endpoint HTTP

```csharp
// Routing/AuthEndpoints.cs
public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");

    // Registrazione
    group.MapPost("/register", async (
        HttpContext context,
        IMediator mediator,
        CancellationToken ct) =>
    {
        var payload = await context.Request.ReadFromJsonAsync<RegisterPayload>(ct);

        var command = new RegisterCommand(
            Email: payload.Email,
            Password: payload.Password,
            DisplayName: payload.DisplayName
        );

        var result = await mediator.Send(command, ct);

        // Scrivi cookie httpOnly
        CookieHelpers.WriteSessionCookie(
            context,
            result.SessionToken,
            result.ExpiresAt
        );

        return Results.Ok(new { user = result.User });
    });

    // Login
    group.MapPost("/login", async (
        HttpContext context,
        IMediator mediator,
        CancellationToken ct) =>
    {
        var payload = await context.Request.ReadFromJsonAsync<LoginPayload>(ct);

        var command = new LoginCommand(
            Email: payload.Email,
            Password: payload.Password,
            TwoFactorCode: payload.TwoFactorCode,
            IpAddress: context.Connection.RemoteIpAddress?.ToString(),
            UserAgent: context.Request.Headers.UserAgent.ToString()
        );

        var result = await mediator.Send(command, ct);

        if (result.RequiresTwoFactor)
        {
            return Results.Ok(new
            {
                requiresTwoFactor = true,
                tempSessionToken = result.TempSessionToken
            });
        }

        CookieHelpers.WriteSessionCookie(
            context,
            result.SessionToken,
            result.ExpiresAt
        );

        return Results.Ok(new { user = result.User });
    });

    // Logout
    group.MapPost("/logout", async (
        HttpContext context,
        IMediator mediator,
        CancellationToken ct) =>
    {
        var sessionToken = CookieHelpers.ReadSessionCookie(context);

        if (!string.IsNullOrEmpty(sessionToken))
        {
            await mediator.Send(new LogoutCommand(sessionToken), ct);
            CookieHelpers.DeleteSessionCookie(context);
        }

        return Results.Ok();
    });
}
```

---

## 2. Bounded Context: GameManagement

**Responsabilità:**
- Gestione catalogo giochi da tavolo
- Lifecycle sessioni di gioco (start, pause, resume, complete, abandon)
- Gestione giocatori nelle sessioni
- Integrazione con BoardGameGeek (BGG)
- Generazione RuleSpec, versioning, diff
- Commenti alle regole e editing collaborativo

### Entità Principali

#### **Game** (Aggregate Root)
```csharp
public class Game
{
    public Guid Id { get; private set; }
    public GameTitle Title { get; private set; }
    public Publisher Publisher { get; private set; }
    public YearPublished YearPublished { get; private set; }
    public PlayerCount MinPlayers { get; private set; }
    public PlayerCount MaxPlayers { get; private set; }
    public PlayTime MinPlayTime { get; private set; }
    public PlayTime MaxPlayTime { get; private set; }
    public int? BggId { get; private set; }

    public void UpdateFromBgg(BggGameData bggData)
    {
        Title = new GameTitle(bggData.Name);
        Publisher = new Publisher(bggData.Publisher);
        YearPublished = new YearPublished(bggData.Year);
        BggId = bggData.Id;

        DomainEvents.Raise(new GameLinkedToBggEvent(Id, bggData.Id));
    }
}
```

#### **GameSession** (Aggregate Root)
```csharp
public class GameSession
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public SessionStatus Status { get; private set; }
    public List<SessionPlayer> Players { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public void Pause()
    {
        if (Status != SessionStatus.Active)
            throw new DomainException("Può essere messa in pausa solo una sessione attiva");

        Status = SessionStatus.Paused;
        DomainEvents.Raise(new GameSessionPausedEvent(Id, GameId));
    }

    public void Resume()
    {
        if (Status != SessionStatus.Paused)
            throw new DomainException("Può essere ripresa solo una sessione in pausa");

        Status = SessionStatus.Active;
        DomainEvents.Raise(new GameSessionResumedEvent(Id, GameId));
    }

    public void Complete()
    {
        if (Status == SessionStatus.Completed)
            throw new DomainException("Sessione già completata");

        Status = SessionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        DomainEvents.Raise(new GameSessionCompletedEvent(Id, GameId, CompletedAt.Value));
    }
}
```

### Value Objects

```csharp
public record GameTitle
{
    public string Value { get; }

    public GameTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Il titolo del gioco non può essere vuoto");

        if (value.Length > 200)
            throw new DomainException("Il titolo del gioco non può superare 200 caratteri");

        Value = value.Trim();
    }
}

public record PlayerCount
{
    public int Value { get; }

    public PlayerCount(int value)
    {
        if (value < 1)
            throw new DomainException("Il numero di giocatori deve essere almeno 1");

        if (value > 100)
            throw new DomainException("Il numero di giocatori non può superare 100");

        Value = value;
    }
}

public enum SessionStatus
{
    Active,
    Paused,
    Completed,
    Abandoned
}
```

### Comandi

```csharp
public record StartGameSessionCommand(
    Guid GameId,
    List<string> PlayerNames
) : ICommand<GameSessionDto>;

public class StartGameSessionCommandHandler
    : ICommandHandler<StartGameSessionCommand, GameSessionDto>
{
    public async Task<GameSessionDto> Handle(StartGameSessionCommand command, CancellationToken ct)
    {
        // 1. Verifica esistenza gioco
        var game = await _gameRepository.GetByIdAsync(command.GameId, ct);
        if (game == null)
            throw new DomainException("Gioco non trovato");

        // 2. Validazione numero giocatori
        var playerCount = command.PlayerNames.Count;
        if (playerCount < game.MinPlayers.Value || playerCount > game.MaxPlayers.Value)
            throw new DomainException(
                $"Il gioco richiede tra {game.MinPlayers.Value} e {game.MaxPlayers.Value} giocatori");

        // 3. Crea sessione
        var session = new GameSession(
            id: Guid.NewGuid(),
            gameId: command.GameId,
            players: command.PlayerNames.Select(name => new SessionPlayer(name)).ToList(),
            startedAt: DateTime.UtcNow
        );

        await _sessionRepository.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(session);
    }
}
```

### Domain Services

```csharp
public class RuleSpecDiffDomainService
{
    public RuleSpecDiff ComputeDiff(RuleSpec oldVersion, RuleSpec newVersion)
    {
        var diff = new RuleSpecDiff
        {
            OldVersion = oldVersion.Version,
            NewVersion = newVersion.Version,
            Changes = new List<DiffChange>()
        };

        // Calcola differenze riga per riga
        var oldLines = oldVersion.Content.Split('\n');
        var newLines = newVersion.Content.Split('\n');

        // Algoritmo diff (es. Myers diff)
        var changes = DiffAlgorithm.Compute(oldLines, newLines);

        diff.Changes.AddRange(changes);

        return diff;
    }
}
```

---

## 3. Bounded Context: KnowledgeBase

**Responsabilità:**
- Pipeline RAG (Retrieval-Augmented Generation) con hybrid search
- Gestione thread di chat con storico Q&A
- Indicizzazione documenti vettoriali (Qdrant)
- Orchestrazione agenti AI (Chess, Feedback, FollowUp)
- Tracking costi LLM e alerting
- Validazione multi-modello (GPT-4 + Claude consensus)
- Tracking metriche di qualità

### Architettura RAG

```
Query Utente
    ↓
Hybrid Search (RRF Fusion)
    ├─ Vector Search (Qdrant) - 70%
    └─ Keyword Search (PostgreSQL FTS) - 30%
    ↓
Top K Risultati (default: 10)
    ↓
LLM Generation (OpenRouter)
    ↓
5-Layer Validation Pipeline
    ├─ 1. Confidence ≥ 0.70
    ├─ 2. Citation Validation (page + snippet)
    ├─ 3. Hallucination Detection (forbidden keywords)
    ├─ 4. Multi-Model Consensus (GPT-4 + Claude)
    └─ 5. Quality Metrics (P@10, MRR)
    ↓
Risposta Validata
```

### Entità Principali

#### **ChatThread** (Aggregate Root)
```csharp
public class ChatThread
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public Guid UserId { get; private set; }
    public ThreadStatus Status { get; private set; }
    public List<ChatMessage> Messages { get; private set; }

    public void AddMessage(ChatMessage message)
    {
        if (Status == ThreadStatus.Closed)
            throw new DomainException("Impossibile aggiungere messaggi a un thread chiuso");

        // Invalida messaggi precedenti dell'assistente se l'utente modifica
        if (message.Role == MessageRole.User)
        {
            var lastUserMessageIndex = Messages.FindLastIndex(m => m.Role == MessageRole.User);
            if (lastUserMessageIndex >= 0)
            {
                for (int i = lastUserMessageIndex + 1; i < Messages.Count; i++)
                {
                    Messages[i].Invalidate();
                }
            }
        }

        Messages.Add(message);
        DomainEvents.Raise(new MessageAddedEvent(Id, message.Id));
    }

    public void Close()
    {
        Status = ThreadStatus.Closed;
        DomainEvents.Raise(new ThreadClosedEvent(Id));
    }
}
```

#### **VectorDocument**
```csharp
public class VectorDocument
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public string Content { get; private set; }
    public int PageNumber { get; private set; }
    public Vector Embedding { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; }

    public void UpdateMetadata(string key, object value)
    {
        Metadata[key] = value;
        DomainEvents.Raise(new VectorDocumentMetadataUpdatedEvent(Id, key));
    }
}
```

### Query con Streaming (SSE)

```csharp
public record StreamQaQuery(
    string GameId,
    string Question,
    string Language = "it"
) : IStreamingQuery<RagStreamingEvent>;

public class StreamQaQueryHandler
    : IStreamingQueryHandler<StreamQaQuery, RagStreamingEvent>
{
    private readonly IRagService _ragService;

    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        StreamQaQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // 1. Invia evento di inizio
        yield return new RagStreamingEvent
        {
            Type = StreamingEventType.Started,
            Data = "Ricerca in corso..."
        };

        // 2. Hybrid search
        var searchResults = await _ragService.SearchAsync(
            query.GameId,
            query.Question,
            topK: 10,
            ct);

        yield return new RagStreamingEvent
        {
            Type = StreamingEventType.Context,
            Data = JsonSerializer.Serialize(searchResults)
        };

        // 3. Stream LLM response token by token
        await foreach (var token in _ragService.StreamCompletionAsync(
            query.Question,
            searchResults,
            query.Language,
            ct))
        {
            yield return new RagStreamingEvent
            {
                Type = StreamingEventType.Chunk,
                Data = token
            };
        }

        // 4. Validazione finale
        var validation = await _ragService.ValidateResponseAsync(...);

        yield return new RagStreamingEvent
        {
            Type = StreamingEventType.Validation,
            Data = JsonSerializer.Serialize(validation)
        };

        // 5. Evento di completamento
        yield return new RagStreamingEvent
        {
            Type = StreamingEventType.Completed
        };
    }
}
```

### Domain Services

#### **5-Layer Validation Pipeline**

```csharp
public class RagValidationPipelineService
{
    private readonly ConfidenceValidationService _confidenceValidator;
    private readonly CitationValidationService _citationValidator;
    private readonly HallucinationDetectionService _hallucinationDetector;
    private readonly MultiModelValidationService _multiModelValidator;
    private readonly QualityTrackingDomainService _qualityTracker;

    public async Task<ValidationResult> ValidateAsync(
        string answer,
        List<SearchResult> sources,
        CancellationToken ct)
    {
        var result = new ValidationResult();

        // Layer 1: Confidence threshold (≥0.70)
        var confidence = await _confidenceValidator.CalculateAsync(answer, sources, ct);
        result.Confidence = confidence;
        if (confidence < 0.70)
        {
            result.Passed = false;
            result.Errors.Add("Confidence troppo bassa");
            return result;
        }

        // Layer 2: Citation validation
        var citationsValid = await _citationValidator.ValidateAsync(answer, sources, ct);
        if (!citationsValid)
        {
            result.Passed = false;
            result.Errors.Add("Citazioni non valide");
            return result;
        }

        // Layer 3: Hallucination detection
        var hasHallucination = await _hallucinationDetector.DetectAsync(answer, ct);
        if (hasHallucination)
        {
            result.Passed = false;
            result.Errors.Add("Rilevata possibile allucinazione");
            return result;
        }

        // Layer 4: Multi-model consensus
        var consensus = await _multiModelValidator.ValidateAsync(answer, sources, ct);
        if (!consensus.Agreed)
        {
            result.Warnings.Add("Modelli in disaccordo");
        }

        // Layer 5: Quality metrics
        await _qualityTracker.TrackAsync(answer, sources, result, ct);

        result.Passed = true;
        return result;
    }
}
```

#### **Hybrid Search con RRF Fusion**

```csharp
public class RrfFusionDomainService
{
    private const double VectorWeight = 0.7;  // 70%
    private const double KeywordWeight = 0.3; // 30%
    private const int K = 60; // RRF constant

    public List<SearchResult> Fuse(
        List<VectorSearchResult> vectorResults,
        List<KeywordSearchResult> keywordResults)
    {
        var fusedScores = new Dictionary<Guid, double>();

        // Score vettoriali (70%)
        for (int i = 0; i < vectorResults.Count; i++)
        {
            var docId = vectorResults[i].DocumentId;
            var rrfScore = VectorWeight / (K + i + 1);

            if (!fusedScores.ContainsKey(docId))
                fusedScores[docId] = 0;

            fusedScores[docId] += rrfScore;
        }

        // Score keyword (30%)
        for (int i = 0; i < keywordResults.Count; i++)
        {
            var docId = keywordResults[i].DocumentId;
            var rrfScore = KeywordWeight / (K + i + 1);

            if (!fusedScores.ContainsKey(docId))
                fusedScores[docId] = 0;

            fusedScores[docId] += rrfScore;
        }

        // Ordina per score finale
        return fusedScores
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => CreateSearchResult(kvp.Key, kvp.Value))
            .ToList();
    }
}
```

---

## 4. Bounded Context: DocumentProcessing

**Responsabilità:**
- Upload, validazione e storage PDF
- Pipeline di estrazione testo a 3 stadi (Unstructured → SmolDocling → Docnet)
- Validazione qualità PDF (scoring a 4 metriche)
- Chunking del testo per indicizzazione vettoriale
- Estrazione tabelle e conversione in regole atomiche

### Pipeline PDF a 3 Stadi

```
PDF Upload
    ↓
EnhancedPdfProcessingOrchestrator
    ↓
Stage 1: Unstructured (≥0.80 qualità)
    ├─ Successo: 80% dei casi
    ├─ Tempo medio: 1.3s
    └─ Se fallisce ↓
Stage 2: SmolDocling VLM (≥0.70 qualità)
    ├─ Successo: 15% dei casi
    ├─ Tempo medio: 3-5s
    ├─ Layout complessi, tabelle
    └─ Se fallisce ↓
Stage 3: Docnet (best effort)
    ├─ Successo: 5% dei casi
    ├─ Veloce, locale
    └─ Nessuna garanzia qualità
    ↓
PdfQualityValidationDomainService
    ↓
Quality Report + Raccomandazioni
```

### Domain Services

#### **PdfQualityValidationDomainService**

```csharp
public class PdfQualityValidationDomainService
{
    public QualityReport ValidateExtraction(PdfDocument pdf, string extractedText)
    {
        var report = new QualityReport { DocumentId = pdf.Id };

        // Metrica 1: Text Coverage (40%)
        var textCoverage = CalculateTextCoverage(extractedText, pdf.PageCount);
        report.TextCoverageScore = textCoverage * 0.4;

        // Metrica 2: Structure Detection (20%)
        var structureScore = DetectStructure(extractedText);
        report.StructureScore = structureScore * 0.2;

        // Metrica 3: Table Detection (20%)
        var tableScore = DetectTables(extractedText);
        report.TableScore = tableScore * 0.2;

        // Metrica 4: Page Coverage (20%)
        var pageCoverage = CalculatePageCoverage(extractedText, pdf.PageCount);
        report.PageCoverageScore = pageCoverage * 0.2;

        // Score totale
        report.TotalScore =
            report.TextCoverageScore +
            report.StructureScore +
            report.TableScore +
            report.PageCoverageScore;

        // Raccomandazioni
        if (report.TotalScore < 0.80)
            report.Recommendations.Add("Considera OCR per PDF scansionato");

        if (report.TableScore < 0.5)
            report.Recommendations.Add("Tabelle non ben estratte, considera SmolDocling");

        return report;
    }

    private double CalculateTextCoverage(string text, int pageCount)
    {
        var charsPerPage = text.Length / (double)pageCount;

        // Soglia: 500 caratteri/pagina
        if (charsPerPage >= 500)
            return 1.0;

        return charsPerPage / 500.0;
    }

    private double DetectStructure(string text)
    {
        var score = 0.0;

        // Titoli (H1, H2, ecc.)
        if (Regex.IsMatch(text, @"^#{1,3}\s+.+$", RegexOptions.Multiline))
            score += 0.4;

        // Liste puntate
        if (Regex.IsMatch(text, @"^[\-\*]\s+.+$", RegexOptions.Multiline))
            score += 0.3;

        // Numerazioni
        if (Regex.IsMatch(text, @"^\d+\.\s+.+$", RegexOptions.Multiline))
            score += 0.3;

        return score;
    }
}
```

### Comandi

```csharp
public record UploadPdfCommand(
    string FileName,
    Stream FileStream,
    Guid GameId,
    Guid UserId
) : ICommand<PdfDocumentDto>;

public class UploadPdfCommandHandler
    : ICommandHandler<UploadPdfCommand, PdfDocumentDto>
{
    private readonly IPdfValidationDomainService _validator;
    private readonly IPdfDocumentRepository _repository;
    private readonly IFileStorage _storage;

    public async Task<PdfDocumentDto> Handle(UploadPdfCommand command, CancellationToken ct)
    {
        // 1. Validazione
        var validationResult = await _validator.ValidateAsync(
            command.FileName,
            command.FileStream,
            ct);

        if (!validationResult.IsValid)
            throw new DomainException(validationResult.ErrorMessage);

        // 2. Storage
        var storageKey = await _storage.SaveAsync(
            command.FileStream,
            $"pdfs/{command.GameId}/{Guid.NewGuid()}.pdf",
            ct);

        // 3. Creazione entità
        var pdf = new PdfDocument(
            id: Guid.NewGuid(),
            gameId: command.GameId,
            userId: command.UserId,
            fileName: new FileName(command.FileName),
            storageKey: storageKey,
            fileSize: new FileSize(command.FileStream.Length),
            pageCount: validationResult.PageCount
        );

        await _repository.AddAsync(pdf, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(pdf);
    }
}
```

---

## 5. Pattern CQRS con MediatR

### Struttura Base

#### **Command**
```csharp
// Interfaccia marker
public interface ICommand<TResponse> : IRequest<TResponse> { }

// Esempio
public record CreateGameCommand(string Title, int MinPlayers) : ICommand<GameDto>;
```

#### **Query**
```csharp
// Interfaccia marker
public interface IQuery<TResponse> : IRequest<TResponse> { }

// Esempio
public record GetGameByIdQuery(Guid GameId) : IQuery<GameDto>;
```

#### **Handler**
```csharp
public interface ICommandHandler<TCommand, TResponse>
    : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}

public interface IQueryHandler<TQuery, TResponse>
    : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
```

### Pipeline Behaviors

#### **Logging Behavior**
```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        _logger.LogInformation(
            "Handled {RequestName} in {ElapsedMs}ms",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
```

#### **Validation Behavior** (FluentValidation)
```csharp
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = validationResults
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Any())
            throw new ValidationException(failures);

        return await next();
    }
}
```

---

## 6. Domain Events

### Architettura Eventi

```csharp
// Interfaccia base
public interface IDomainEvent : INotification
{
    DateTime OccurredAt { get; }
    Guid EventId { get; }
}

// Classe base
public abstract class DomainEventBase : IDomainEvent
{
    public DateTime OccurredAt { get; }
    public Guid EventId { get; }

    protected DomainEventBase()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
    }
}

// Esempio evento
public record PasswordChangedEvent(Guid UserId) : DomainEventBase;
```

### Handler Base con Auto-Audit

```csharp
public abstract class DomainEventHandlerBase<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent
{
    private readonly MeepleAiDbContext _dbContext;
    protected readonly ILogger<DomainEventHandlerBase<TEvent>> Logger;

    public async Task Handle(TEvent notification, CancellationToken ct)
    {
        Logger.LogInformation("Handling domain event {EventType}", typeof(TEvent).Name);

        // Auto-audit: TUTTI gli eventi creano un audit log
        await CreateAuditLogAsync(notification, ct);

        // Logica specifica dell'handler
        await HandleEventAsync(notification, ct);
    }

    protected abstract Task HandleEventAsync(TEvent domainEvent, CancellationToken ct);

    private async Task CreateAuditLogAsync(TEvent domainEvent, CancellationToken ct)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EventType = typeof(TEvent).Name,
            EventData = JsonSerializer.Serialize(domainEvent),
            OccurredAt = domainEvent.OccurredAt,
            EventId = domainEvent.EventId
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(ct);
    }
}
```

### Integration Events (Cross-Context)

```csharp
// Event di integrazione
public record GameCreatedIntegrationEvent(
    Guid GameId,
    string GameTitle
) : DomainEventBase;

// Handler in GameManagement (publisher)
public class GameCreatedEventHandler
    : DomainEventHandlerBase<GameCreatedEvent>
{
    private readonly IMediator _mediator;

    protected override async Task HandleEventAsync(GameCreatedEvent domainEvent, CancellationToken ct)
    {
        // Pubblica integration event
        await _mediator.Publish(new GameCreatedIntegrationEvent(
            domainEvent.GameId,
            domainEvent.GameTitle
        ), ct);
    }
}

// Handler in WorkflowIntegration (consumer)
public class GameCreatedIntegrationEventHandler
    : INotificationHandler<GameCreatedIntegrationEvent>
{
    public async Task Handle(GameCreatedIntegrationEvent notification, CancellationToken ct)
    {
        // Trigger n8n workflow per nuovo gioco
        await _n8nClient.TriggerWorkflowAsync("new-game", new
        {
            gameId = notification.GameId,
            gameTitle = notification.GameTitle
        }, ct);
    }
}
```

---

## 7. Background Task Orchestration

### IBackgroundTaskOrchestrator

```csharp
public interface IBackgroundTaskOrchestrator
{
    // Esecuzione immediata
    Task ScheduleAsync(
        string taskId,
        string taskName,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken ct);

    // Esecuzione ritardata
    Task ScheduleDelayedAsync(
        string taskId,
        string taskName,
        TimeSpan delay,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken ct);

    // Esecuzione ricorrente
    Task ScheduleRecurringAsync(
        string taskId,
        string taskName,
        TimeSpan interval,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken ct);

    // Gestione task
    Task<bool> CancelAsync(string taskId);
    Task<BackgroundTaskStatus?> GetStatusAsync(string taskId);

    // Distributed lock
    Task<bool> ExecuteWithDistributedLockAsync(
        string lockKey,
        Func<CancellationToken, Task> taskFactory,
        TimeSpan lockTimeout,
        CancellationToken ct);
}

public enum BackgroundTaskStatus
{
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled
}
```

### Implementazione Redis

```csharp
public class RedisBackgroundTaskOrchestrator : IBackgroundTaskOrchestrator
{
    private readonly IConnectionMultiplexer _redis;
    private const string TaskStatusKeyPrefix = "meepleai:tasks:status:";
    private const string TaskLockKeyPrefix = "meepleai:tasks:lock:";

    public async Task ScheduleAsync(
        string taskId,
        string taskName,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken ct)
    {
        var db = _redis.GetDatabase();

        // Imposta stato Scheduled
        await db.StringSetAsync(
            $"{TaskStatusKeyPrefix}{taskId}",
            BackgroundTaskStatus.Scheduled.ToString(),
            expiry: TimeSpan.FromHours(24));

        // Esegui in background
        _ = Task.Run(async () =>
        {
            try
            {
                // Stato Running
                await db.StringSetAsync(
                    $"{TaskStatusKeyPrefix}{taskId}",
                    BackgroundTaskStatus.Running.ToString(),
                    expiry: TimeSpan.FromHours(24));

                await taskFactory(ct);

                // Stato Completed
                await db.StringSetAsync(
                    $"{TaskStatusKeyPrefix}{taskId}",
                    BackgroundTaskStatus.Completed.ToString(),
                    expiry: TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                // Stato Failed
                await db.StringSetAsync(
                    $"{TaskStatusKeyPrefix}{taskId}",
                    BackgroundTaskStatus.Failed.ToString(),
                    expiry: TimeSpan.FromHours(24));

                _logger.LogError(ex, "Background task {TaskId} failed", taskId);
            }
        }, ct);
    }

    public async Task<bool> ExecuteWithDistributedLockAsync(
        string lockKey,
        Func<CancellationToken, Task> taskFactory,
        TimeSpan lockTimeout,
        CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var lockKeyFull = $"{TaskLockKeyPrefix}{lockKey}";
        var lockValue = Guid.NewGuid().ToString();

        // Acquisisci lock (Lua script atomico)
        var acquired = await db.StringSetAsync(
            lockKeyFull,
            lockValue,
            lockTimeout,
            When.NotExists);

        if (!acquired)
            return false;

        try
        {
            await taskFactory(ct);
            return true;
        }
        finally
        {
            // Rilascia lock (solo se ancora posseduto)
            await db.ScriptEvaluateAsync(@"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end",
                new RedisKey[] { lockKeyFull },
                new RedisValue[] { lockValue });
        }
    }
}
```

### Utilizzo

```csharp
// Nel command handler
public async Task<Result> Handle(IndexPdfCommand command, CancellationToken ct)
{
    // Schedula task di indicizzazione PDF
    await _orchestrator.ScheduleAsync(
        taskId: $"index-pdf-{command.PdfId}",
        taskName: "Index PDF Document",
        taskFactory: async ct => await IndexPdfAsync(command.PdfId, ct),
        cancellationToken: ct);

    return Result.Success();
}

// Cleanup schedulato con distributed lock
await _orchestrator.ScheduleRecurringAsync(
    taskId: "cleanup-old-sessions",
    taskName: "Cleanup Old Sessions",
    interval: TimeSpan.FromHours(24),
    taskFactory: async ct =>
    {
        await _orchestrator.ExecuteWithDistributedLockAsync(
            lockKey: "cleanup-sessions-lock",
            taskFactory: async ct => await CleanupSessionsAsync(ct),
            lockTimeout: TimeSpan.FromMinutes(10),
            cancellationToken: ct);
    },
    cancellationToken: CancellationToken.None);
```

---

## 8. Repository Pattern

### Interfaccia

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct);
    Task<List<User>> GetAllAsync(CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
```

### Implementazione EF Core

```csharp
public class UserRepository : IUserRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public UserRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _dbContext.Users
            .Include(u => u.Sessions)
            .Include(u => u.ApiKeys)
            .Include(u => u.OAuthAccounts)
            .AsNoTracking()  // 30% più veloce per read-only
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        await _dbContext.Users.AddAsync(user, ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        _dbContext.Users.Update(user);
    }
}
```

### Unit of Work

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly MeepleAiDbContext _dbContext;

    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        // Dispatch domain events BEFORE SaveChanges
        await DispatchDomainEventsAsync(ct);

        return await _dbContext.SaveChangesAsync(ct);
    }

    private async Task DispatchDomainEventsAsync(CancellationToken ct)
    {
        var domainEntities = _dbContext.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // Clear events
        domainEntities.ForEach(e => e.ClearDomainEvents());

        // Publish events
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, ct);
        }
    }
}
```

---

## 9. Testing

### Struttura Test

```csharp
public class RegisterCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new RegisterCommandHandler(
            _userRepositoryMock.Object,
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object,
            Mock.Of<ILogger<RegisterCommandHandler>>());
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesUserAndSession()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "Test123!",
            DisplayName: "Test User");

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);  // Email non esiste

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.User.Email.Should().Be("test@example.com");

        _userRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _sessionRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsDomainException()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "existing@example.com",
            Password: "Test123!",
            DisplayName: "Test User");

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User(...));  // Email già esiste

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
```

### Integration Tests con Testcontainers

```csharp
[Collection("Integration")]
public class AuthenticationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    private WebApplicationFactory<Program> _factory;

    public AuthenticationIntegrationTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // Replace con container connections
                    services.Configure<DatabaseOptions>(options =>
                    {
                        options.ConnectionString = _postgresContainer.GetConnectionString();
                    });

                    services.Configure<RedisOptions>(options =>
                    {
                        options.ConnectionString = _redisContainer.GetConnectionString();
                    });
                });
            });
    }

    [Fact]
    public async Task RegisterUser_ValidPayload_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new
        {
            email = "test@example.com",
            password = "Test123!",
            displayName = "Test User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        result.User.Email.Should().Be("test@example.com");

        // Verifica cookie impostato
        response.Headers.Should().Contain(h => h.Key == "Set-Cookie");
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.StopAsync();
        await _redisContainer.StopAsync();
        await _factory.DisposeAsync();
    }
}
```

---

## 10. Osservabilità

### OpenTelemetry

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddRedisInstrumentation()
        .AddSource("MeepleAI.Rag")
        .AddSource("MeepleAI.Pdf")
        .AddJaegerExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

// Custom Activity Sources
public static class MeepleAiActivitySources
{
    public static readonly ActivitySource Rag = new("MeepleAI.Rag");
    public static readonly ActivitySource Pdf = new("MeepleAI.Pdf");
}

// Utilizzo
public async Task<RagResponse> AskAsync(string gameId, string query, CancellationToken ct)
{
    using var activity = MeepleAiActivitySources.Rag.StartActivity("RagService.Ask");
    activity?.SetTag("game.id", gameId);
    activity?.SetTag("query.length", query.Length);

    try
    {
        var result = await PerformRagAsync(gameId, query, ct);

        activity?.SetTag("result.confidence", result.Confidence);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return result;
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        throw;
    }
}
```

### Serilog

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "MeepleAI")
    .Enrich.WithProperty("Environment", env)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

// Utilizzo strutturato
_logger.LogInformation(
    "User {UserId} logged in from {IpAddress}",
    userId,
    ipAddress);

_logger.LogError(
    exception,
    "Failed to process PDF {PdfId} for game {GameId}",
    pdfId,
    gameId);
```

---

## 11. Best Practices

### 1. Separazione delle Responsabilità

- **Domain**: Logica di business PURA, zero dipendenze esterne
- **Application**: Orchestrazione, usa domain + repositories
- **Infrastructure**: Implementazioni tecniche (EF, Redis, Qdrant)
- **HTTP**: Thin layer, solo MediatR

### 2. Immutabilità

- **Value Objects**: Sempre `record` immutabili
- **Commands/Queries**: `record` immutabili
- **Entities**: Mutabili ma solo tramite metodi di business logic

### 3. Validazione

- **Value Objects**: Validazione nel costruttore
- **Commands**: FluentValidation in pipeline behavior
- **Domain**: Business rules validate in entity methods

### 4. Error Handling

```csharp
// Domain exceptions
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

// Validation exceptions
public class ValidationException : Exception
{
    public IEnumerable<ValidationFailure> Errors { get; }
}

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            DomainException ex => (400, ex.Message),
            ValidationException ex => (400, FormatValidationErrors(ex.Errors)),
            UnauthorizedAccessException => (401, "Non autorizzato"),
            _ => (500, "Errore interno del server")
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message });
    });
});
```

### 5. Dependency Injection

```csharp
// Scoped: Per-request (repositories, DbContext)
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IUnitOfWork, UnitOfWork>();

// Singleton: Shared state (orchestrator, cache)
services.AddSingleton<IBackgroundTaskOrchestrator, RedisBackgroundTaskOrchestrator>();

// Transient: Stateless (domain services)
services.AddTransient<RuleSpecDiffDomainService>();
```

---

## 12. Comandi Utili

```bash
# Build
dotnet build

# Test
dotnet test
dotnet test --filter "Category=Integration"
dotnet test --filter "FullyQualifiedName~RagEvaluationIntegrationTests"

# Coverage
./tools/run-backend-coverage.sh --html --open
./tools/run-backend-coverage-docker.sh

# Migrations
dotnet ef migrations add <Name> --project src/Api
dotnet ef database update --project src/Api
dotnet ef migrations remove --project src/Api

# Run
cd apps/api/src/Api && dotnet run

# Docker
docker compose up meepleai-postgres meepleai-qdrant meepleai-redis
```

---

## 13. Risorse

| Documento | Path |
|-----------|------|
| **Architettura Sistema** | `docs/01-architecture/overview/system-architecture.md` |
| **Specifica API** | `docs/03-api/board-game-ai-api-specification.md` |
| **ADR Hybrid RAG** | `docs/01-architecture/adr/adr-001-hybrid-rag.md` |
| **ADR Multi-Layer Validation** | `docs/01-architecture/adr/adr-006-multi-layer-validation.md` |
| **ADR PDF Processing** | `docs/01-architecture/adr/adr-003b-unstructured-pdf.md` |
| **Security** | `SECURITY.md` |
| **OAuth Security** | `docs/06-security/oauth-security.md` |
| **Testing Guide** | `docs/02-development/testing/README.md` |
| **Indice Completo** | `docs/INDEX.md` (160+ docs, 900+ pagine) |

---

**Versione:** 1.0
**Ultimo Aggiornamento:** 2025-11-19
**Autore:** Engineering Lead
