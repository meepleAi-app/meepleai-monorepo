# Diagrammi Interazioni Bounded Contexts

## 1. AUTHENTICATION - Bounded Context

### Class Diagram

```mermaid
classDiagram
    class User {
        <<AggregateRoot>>
        +Guid Id
        +Email Email
        +string DisplayName
        +PasswordHash PasswordHash
        +Role Role
        +bool IsTwoFactorEnabled
        +string? TwoFactorSecret
        +DateTime CreatedAt
        +DateTime? LastLoginAt
        +ChangePassword(oldPassword, newPassword)
        +Enable2FA(secret)
        +Disable2FA()
        +UpdateLastLogin()
    }

    class Session {
        <<Entity>>
        +Guid Id
        +Guid UserId
        +SessionToken Token
        +string IpAddress
        +string UserAgent
        +DateTime CreatedAt
        +DateTime ExpiresAt
        +bool IsRevoked
        +Revoke()
        +IsExpired() bool
    }

    class ApiKey {
        <<Entity>>
        +Guid Id
        +Guid UserId
        +string Name
        +string KeyPrefix
        +string KeyHash
        +DateTime CreatedAt
        +DateTime? ExpiresAt
        +bool IsActive
        +Revoke()
    }

    class OAuthAccount {
        <<Entity>>
        +Guid Id
        +Guid UserId
        +string Provider
        +string ProviderUserId
        +string? AccessToken
        +DateTime? TokenExpiresAt
    }

    class Email {
        <<ValueObject>>
        +string Value
        +Validate()
    }

    class PasswordHash {
        <<ValueObject>>
        +string Hash
        +string Salt
        +Create(password)$
        +Verify(password) bool
    }

    class Role {
        <<ValueObject>>
        +string Value
        +Parse(role)$
        +IsAdmin() bool
    }

    class SessionToken {
        <<ValueObject>>
        +string Value
        +Generate()$
    }

    class RegisterCommandHandler {
        <<Handler>>
        +Handle(RegisterCommand) RegisterResponse
        -IUserRepository userRepo
        -ISessionRepository sessionRepo
        -IUnitOfWork unitOfWork
    }

    class LoginCommandHandler {
        <<Handler>>
        +Handle(LoginCommand) LoginResponse
        -IUserRepository userRepo
        -ISessionRepository sessionRepo
    }

    User "1" --> "1" Email
    User "1" --> "1" PasswordHash
    User "1" --> "1" Role
    User "1" --> "*" Session
    User "1" --> "*" ApiKey
    User "1" --> "*" OAuthAccount
    Session "1" --> "1" SessionToken

    RegisterCommandHandler ..> User : creates
    RegisterCommandHandler ..> Session : creates
    LoginCommandHandler ..> User : validates
    LoginCommandHandler ..> Session : creates
```

### Sequence: Login with 2FA

```mermaid
sequenceDiagram
    participant Client
    participant LoginHandler as LoginCommandHandler
    participant UserRepo as IUserRepository
    participant User as User Aggregate
    participant SessionRepo as ISessionRepository

    Client->>LoginHandler: LoginCommand<br/>{email, password}

    LoginHandler->>UserRepo: GetByEmailAsync(email)
    UserRepo-->>LoginHandler: User

    LoginHandler->>User: VerifyPassword(password)
    User-->>LoginHandler: Valid

    alt User has 2FA enabled
        User-->>LoginHandler: IsTwoFactorEnabled = true
        LoginHandler->>SessionRepo: CreateTemporarySession(user, 5min)
        SessionRepo-->>LoginHandler: TempSession
        LoginHandler-->>Client: Requires2FA<br/>{tempSessionToken}

        Note over Client: User enters TOTP code

        Client->>LoginHandler: Verify2FACommand<br/>{tempSessionToken, totpCode}
        LoginHandler->>User: Verify2FA(totpCode)
        User-->>LoginHandler: Valid
        LoginHandler->>SessionRepo: CreateFullSession(user, 30d)
        SessionRepo-->>LoginHandler: FullSession
        LoginHandler-->>Client: LoginResponse<br/>{user, sessionToken}

    else No 2FA
        LoginHandler->>SessionRepo: CreateSession(user, 30d)
        SessionRepo-->>LoginHandler: Session
        LoginHandler-->>Client: LoginResponse<br/>{user, sessionToken}
    end
```

---

## 2. GAMEMANAGEMENT - Bounded Context

### Class Diagram

```mermaid
classDiagram
    class Game {
        <<AggregateRoot>>
        +Guid Id
        +GameTitle Title
        +Publisher Publisher
        +YearPublished YearPublished
        +PlayerCount PlayerCount
        +PlayTime PlayTime
        +string? Description
        +string? BggId
        +Version Version
        +UpdateDetails(title, publisher, ...)
    }

    class GameSession {
        <<AggregateRoot>>
        +Guid Id
        +Guid GameId
        +List~SessionPlayer~ Players
        +SessionStatus Status
        +DateTime StartedAt
        +DateTime? CompletedAt
        +string? Winner
        +Start()
        +Complete(winner)
        +Abandon()
    }

    class GameTitle {
        <<ValueObject>>
        +string Value
        +Validate()
    }

    class PlayerCount {
        <<ValueObject>>
        +int MinPlayers
        +int MaxPlayers
        +Validate()
    }

    class SessionStatus {
        <<Enum>>
        Setup
        InProgress
        Completed
        Abandoned
    }

    class SessionPlayer {
        <<ValueObject>>
        +string Name
        +int Position
    }

    class CreateGameCommandHandler {
        <<Handler>>
        +Handle(CreateGameCommand) GameDto
        -IGameRepository gameRepo
    }

    class StartGameSessionCommandHandler {
        <<Handler>>
        +Handle(StartGameSessionCommand) SessionDto
        -IGameSessionRepository sessionRepo
    }

    Game "1" --> "1" GameTitle
    Game "1" --> "1" PlayerCount
    GameSession "1" --> "1" SessionStatus
    GameSession "1" --> "*" SessionPlayer
    GameSession "*" --> "1" Game

    CreateGameCommandHandler ..> Game : creates
    StartGameSessionCommandHandler ..> GameSession : creates
```

---

## 3. DOCUMENTPROCESSING - Bounded Context

### Class Diagram: PDF Pipeline

```mermaid
classDiagram
    class PdfDocument {
        <<AggregateRoot>>
        +Guid Id
        +Guid GameId
        +FileName FileName
        +FileSize FileSize
        +PageCount PageCount
        +string? ExtractedText
        +ProcessingStatus Status
        +ExtractionQuality Quality
        +UpdateExtractedText(text, quality)
    }

    class EnhancedPdfProcessingOrchestrator {
        <<Service>>
        +ExtractTextWithFallbackAsync(stream) EnhancedExtractionResult
        -IPdfTextExtractor unstructuredExtractor
        -IPdfTextExtractor smolDoclingExtractor
        -IPdfTextExtractor docnetExtractor
    }

    class IPdfTextExtractor {
        <<Interface>>
        +ExtractTextAsync(stream) TextExtractionResult
        +ExtractPagedTextAsync(stream) PagedTextExtractionResult
    }

    class UnstructuredPdfTextExtractor {
        +ExtractTextAsync(stream) TextExtractionResult
        -HttpClient httpClient
        -PdfTextProcessingDomainService processingService
    }

    class SmolDoclingPdfTextExtractor {
        +ExtractTextAsync(stream) TextExtractionResult
        -HttpClient httpClient
        -CircuitBreaker circuitBreaker
    }

    class DocnetPdfTextExtractor {
        +ExtractTextAsync(stream) TextExtractionResult
        -IOcrService ocrService
        -SemaphoreSlim docnetSemaphore
    }

    class PdfQualityValidationDomainService {
        <<DomainService>>
        +ValidateExtractionQuality(result) PdfQualityReport
        +GenerateQualityReport(result) PdfQualityReport
        +CalculateTextCoverage(chars, pages) double
    }

    class PdfTextProcessingDomainService {
        <<DomainService>>
        +NormalizeText(rawText) string
        +AssessQuality(text, pageCount) ExtractionQuality
        +ShouldTriggerOcr(text, pageCount) bool
    }

    class IndexPdfCommandHandler {
        <<Handler>>
        +Handle(IndexPdfCommand) IndexingResultDto
        -IPdfTextExtractor extractor
        -ITextChunkingService chunking
        -IEmbeddingService embedding
        -IQdrantService qdrant
    }

    EnhancedPdfProcessingOrchestrator --> UnstructuredPdfTextExtractor
    EnhancedPdfProcessingOrchestrator --> SmolDoclingPdfTextExtractor
    EnhancedPdfProcessingOrchestrator --> DocnetPdfTextExtractor

    UnstructuredPdfTextExtractor ..|> IPdfTextExtractor
    SmolDoclingPdfTextExtractor ..|> IPdfTextExtractor
    DocnetPdfTextExtractor ..|> IPdfTextExtractor

    UnstructuredPdfTextExtractor ..> PdfTextProcessingDomainService
    SmolDoclingPdfTextExtractor ..> PdfTextProcessingDomainService
    DocnetPdfTextExtractor ..> PdfTextProcessingDomainService

    EnhancedPdfProcessingOrchestrator ..> PdfQualityValidationDomainService

    IndexPdfCommandHandler ..> PdfDocument : updates
    IndexPdfCommandHandler ..> IPdfTextExtractor : uses
```

### Sequence: 3-Stage PDF Extraction

```mermaid
sequenceDiagram
    participant Handler as IndexPdfCommandHandler
    participant Orch as EnhancedPdfProcessingOrchestrator
    participant Stage1 as UnstructuredExtractor
    participant Stage2 as SmolDoclingExtractor
    participant Stage3 as DocnetExtractor
    participant Quality as QualityValidationService

    Handler->>Orch: ExtractTextWithFallbackAsync(pdfStream)

    Note over Orch: Copy stream to byte[]<br/>for reuse across stages

    Orch->>Stage1: ExtractTextAsync(stream)
    Stage1->>Stage1: HTTP POST to Unstructured service
    Stage1-->>Orch: TextExtractionResult<br/>{text, quality: High, score: 0.85}

    alt Quality >= 0.80 (Stage 1 Success)
        Orch-->>Handler: EnhancedExtractionResult<br/>{stageUsed: 1, stageName: "Unstructured"}

    else Quality < 0.80 (Fallback to Stage 2)
        Note over Orch: Stage 1 insufficient<br/>trying Stage 2

        Orch->>Stage2: ExtractTextAsync(stream)
        Stage2->>Stage2: HTTP POST to SmolDocling VLM
        Stage2-->>Orch: TextExtractionResult<br/>{text, quality: Medium, score: 0.72}

        alt Quality >= 0.70 (Stage 2 Success)
            Orch-->>Handler: EnhancedExtractionResult<br/>{stageUsed: 2, stageName: "SmolDocling"}

        else Quality < 0.70 (Fallback to Stage 3)
            Note over Orch: Stage 2 insufficient<br/>trying Stage 3

            Orch->>Stage3: ExtractTextAsync(stream)
            Stage3->>Stage3: Docnet local extraction

            alt Low quality detected
                Stage3->>Stage3: OCR fallback (Tesseract)
            end

            Stage3-->>Orch: TextExtractionResult<br/>{text, quality: bestEffort}
            Orch-->>Handler: EnhancedExtractionResult<br/>{stageUsed: 3, stageName: "Docnet"}
        end
    end

    Handler->>Quality: ValidateExtractionQuality(result)
    Quality-->>Handler: PdfQualityReport<br/>{metrics, recommendations}

    Handler->>Handler: Update PdfDocument entity<br/>with extracted text + quality
```

---

## 4. KNOWLEDGEBASE - Bounded Context (RAG System)

### Class Diagram: RAG Components

```mermaid
classDiagram
    class ChatThread {
        <<AggregateRoot>>
        +Guid Id
        +Guid GameId
        +Guid UserId
        +List~ChatMessage~ Messages
        +DateTime CreatedAt
        +AddMessage(message)
    }

    class VectorDocument {
        <<AggregateRoot>>
        +Guid Id
        +Guid GameId
        +string PdfId
        +string TextContent
        +int PageNumber
        +int ChunkIndex
        +Language Language
    }

    class Embedding {
        <<Entity>>
        +Guid Id
        +Guid VectorDocumentId
        +Vector EmbeddingVector
        +string Language
    }

    class SearchResult {
        <<Entity>>
        +Guid Id
        +Guid VectorDocumentId
        +string TextContent
        +int PageNumber
        +Confidence RelevanceScore
        +int Rank
        +string SearchMethod
    }

    class RagService {
        <<ApplicationService>>
        +AskWithHybridSearchAsync(query) QaResponse
        -IHybridSearchService hybridSearch
        -IEmbeddingService embedding
        -IHybridLlmService llm
        -IQueryExpansionService queryExpansion
    }

    class HybridSearchService {
        <<Service>>
        +SearchAsync(query, mode) HybridSearchResult
        -IQdrantService qdrant
        -IKeywordSearchService keyword
        -RrfFusionDomainService rrfFusion
    }

    class RrfFusionDomainService {
        <<DomainService>>
        +FuseResults(vectorResults, keywordResults) RankedResults
        +CalculateRrfScore(rank, k) double
    }

    class HybridLlmService {
        <<Service>>
        +GenerateCompletionAsync(prompt) LlmResult
        -ILlmRoutingStrategy routingStrategy
        -CircuitBreakerState circuitBreaker
        -ProviderHealthCheckService healthCheck
    }

    class ConfidenceValidationService {
        <<DomainService>>
        +ValidateConfidence(score) ValidationResult
    }

    class CitationValidationService {
        <<DomainService>>
        +ValidateCitations(citations, gameId) ValidationResult
    }

    class HallucinationDetectionService {
        <<DomainService>>
        +DetectHallucination(text, language) HallucinationResult
    }

    class AskQuestionQueryHandler {
        <<Handler>>
        +Handle(AskQuestionQuery) QaResponseDto
        -RagService ragService
    }

    ChatThread "1" --> "*" ChatMessage
    VectorDocument "1" --> "1" Embedding
    SearchResult "1" --> "1" Confidence

    RagService --> HybridSearchService
    RagService --> HybridLlmService
    HybridSearchService --> RrfFusionDomainService

    RagService ..> ConfidenceValidationService
    RagService ..> CitationValidationService
    RagService ..> HallucinationDetectionService

    AskQuestionQueryHandler ..> RagService
    AskQuestionQueryHandler ..> SearchResult : creates
```

### Sequence: RAG Query with Hybrid Search

```mermaid
sequenceDiagram
    participant Client
    participant Handler as AskQuestionQueryHandler
    participant RAG as RagService
    participant QueryExp as QueryExpansionService
    participant Hybrid as HybridSearchService
    participant Vector as QdrantService
    participant Keyword as KeywordSearchService
    participant RRF as RrfFusionDomainService
    participant LLM as HybridLlmService
    participant Validation as ValidationLayer

    Client->>Handler: AskQuestionQuery<br/>{gameId, query}

    Handler->>RAG: AskWithHybridSearchAsync(query)

    Note over RAG: Check cache first

    RAG->>QueryExp: GenerateQueryVariations(query)
    QueryExp-->>RAG: [query1, query2, query3, query4]

    par Parallel Search
        RAG->>Hybrid: SearchAsync(variations, mode: Hybrid)

        Hybrid->>Vector: SearchAsync(vectorQueries)
        Vector-->>Hybrid: VectorResults (top 10)

        Hybrid->>Keyword: SearchAsync(textQueries)
        Keyword-->>Hybrid: KeywordResults (top 10)
    end

    Hybrid->>RRF: FuseResults(vectorResults, keywordResults)
    Note over RRF: RRF_score = Σ(weight / (k + rank))<br/>vectorWeight=0.7, keywordWeight=0.3
    RRF-->>Hybrid: FusedResults (ranked)

    Hybrid-->>RAG: Top 5 HybridSearchResults

    RAG->>RAG: Build context from results<br/>[Page 1] text1\n[Page 2] text2...

    RAG->>RAG: Render prompt template<br/>(system + user prompt)

    RAG->>LLM: GenerateCompletionAsync(prompt, context)

    Note over LLM: Route to provider<br/>Circuit breaker check<br/>Health monitoring

    LLM-->>RAG: LlmCompletionResult<br/>{answer, tokens, metadata}

    RAG->>Validation: 5-Layer Validation

    par Validation Layers
        Validation->>Validation: 1. Confidence >= 0.70
        Validation->>Validation: 2. Citations valid
        Validation->>Validation: 3. No hallucination keywords
        Validation->>Validation: 4. Quality tracking
        Validation->>Validation: 5. Response quality
    end

    Validation-->>RAG: ValidationResult

    RAG->>RAG: Build QaResponse<br/>{answer, snippets, confidence}

    RAG->>RAG: Cache response (TTL: 24h)

    RAG-->>Handler: QaResponse

    Handler->>Handler: Map to QaResponseDto

    Handler-->>Client: 200 OK<br/>{answer, sources, confidence}
```

---

## 5. SYSTEMCONFIGURATION - Bounded Context

### Class Diagram

```mermaid
classDiagram
    class SystemConfiguration {
        <<AggregateRoot>>
        +Guid Id
        +ConfigKey Key
        +string Value
        +string Category
        +string Environment
        +int Version
        +DateTime UpdatedAt
        +UpdateValue(newValue)
        +Rollback(previousVersion)
    }

    class FeatureFlag {
        <<AggregateRoot>>
        +Guid Id
        +string Name
        +bool IsEnabled
        +string? Description
        +Toggle()
    }

    class ConfigKey {
        <<ValueObject>>
        +string Value
        +Validate()
    }

    class UpdateConfigValueCommandHandler {
        <<Handler>>
        +Handle(UpdateConfigValueCommand) ConfigDto
        -ISystemConfigurationRepository repo
        -ICacheInvalidationService cache
    }

    class GetConfigByKeyQueryHandler {
        <<Handler>>
        +Handle(GetConfigByKeyQuery) ConfigDto
        -ISystemConfigurationRepository repo
    }

    SystemConfiguration "1" --> "1" ConfigKey

    UpdateConfigValueCommandHandler ..> SystemConfiguration
    GetConfigByKeyQueryHandler ..> SystemConfiguration
```

---

## 6. Relazioni Cross-Context

```mermaid
graph TB
    subgraph Auth["AUTHENTICATION"]
        User[User Aggregate]
        Session[Session Entity]
    end

    subgraph Game["GAMEMANAGEMENT"]
        GameAgg[Game Aggregate]
        SessionAgg[GameSession Aggregate]
    end

    subgraph Doc["DOCUMENTPROCESSING"]
        PdfDoc[PdfDocument Aggregate]
        Pipeline[PDF Processing Pipeline]
    end

    subgraph KB["KNOWLEDGEBASE"]
        Chat[ChatThread Aggregate]
        Vector[VectorDocument Aggregate]
        RAG[RAG Service]
    end

    subgraph Config["SYSTEMCONFIGURATION"]
        SysConfig[SystemConfiguration Aggregate]
    end

    User -->|UserId| Chat
    User -->|UserId| SessionAgg
    User -->|UserId| PdfDoc

    GameAgg -->|GameId| PdfDoc
    GameAgg -->|GameId| Chat
    GameAgg -->|GameId| Vector

    PdfDoc -->|Extracted Text| Pipeline
    Pipeline -->|Chunks| Vector
    Vector -->|Indexed Content| RAG

    SysConfig -.->|Dynamic Config| RAG
    SysConfig -.->|Dynamic Config| Pipeline

    style Auth fill:#ab47bc
    style Game fill:#66bb6a
    style Doc fill:#ffa726
    style KB fill:#29b6f6
    style Config fill:#ef5350
```

---

**Totale Bounded Contexts**: 7
**Totale Aggregates**: 12
**Totale Handlers**: 72+
**Pattern**: DDD + CQRS + Clean Architecture

**Versione**: 1.0
**Data**: 2025-11-13
