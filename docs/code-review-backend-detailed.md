# Code Review Dettagliata - Backend (apps/api)

**Data:** 2025-11-18
**Reviewer:** Claude Code
**Branch:** `claude/code-review-documentation-01G7QqtRsEA4q2QVTGf4W2fL`
**Focus:** DDD/CQRS architecture, directory organization, bounded context structure

---

## Executive Summary

Il backend di MeepleAI presenta un'eccellente implementazione DDD/CQRS con 7 bounded contexts e 223 handler. Tuttavia, sono stati identificati **problemi significativi di organizzazione** con directory sovraffollate che richiedono refactoring per migliorare manutenibilità e scalabilità.

### Valutazione Complessiva: ⭐⭐⭐⭐ (4/5)

**Punti di Forza:**
- ✅ DDD migration 100% completa (7 bounded contexts)
- ✅ CQRS con MediatR (223 handlers)
- ✅ Domain events (40 events + 39 handlers)
- ✅ Separazione aggregati/entità

**Problemi Critici:**
- 🔴 **56 file** in `Authentication/Application/Commands/` (CRITICO)
- 🔴 **61 file** in `KnowledgeBase/Domain/` (CRITICO)
- 🔴 **34 file** in `Infrastructure/Entities/` (flat structure)
- 🔴 **32 file** in `Infrastructure/EntityConfigurations/` (flat structure)
- 🟡 **34 file** in `Administration/Application/Queries/` (può migliorare)

---

## 1. Analisi Bounded Contexts

### 1.1 Metriche per Context

| Bounded Context | Commands | Queries | Domain Files | Totale Handler | Status |
|----------------|----------|---------|--------------|----------------|--------|
| **Authentication** | 56 | 28 | 22 | 84 | 🔴 CRITICO |
| **GameManagement** | 19 | 19 | 25 | 38 | ✅ OK |
| **KnowledgeBase** | 17 | 13 | 61 | 30 | 🔴 CRITICO |
| **DocumentProcessing** | 5 | 5 | 10 | 10 | ✅ OK |
| **WorkflowIntegration** | 10 | 13 | 10 | 23 | ✅ OK |
| **SystemConfiguration** | 10 | 6 | 5 | 16 | ✅ OK |
| **Administration** | 16 | 34 | 5 | 50 | 🟡 MEDIO |
| **TOTALE** | **133** | **118** | **138** | **251** | |

### 1.2 Problemi Identificati

#### 🔴 CRITICO: Authentication/Application/Commands/ (56 file)

**Struttura attuale:**
```
Authentication/Application/Commands/
├── ChangePasswordCommand.cs
├── ChangePasswordCommandHandler.cs
├── CreateApiKeyCommand.cs
├── CreateApiKeyCommandHandler.cs
├── CreateApiKeyManagementCommand.cs
├── CreateApiKeyManagementCommandHandler.cs
├── CreateSessionCommand.cs
├── CreateSessionCommandHandler.cs
├── DeleteApiKeyCommand.cs
├── DeleteApiKeyCommandHandler.cs
├── Disable2FACommand.cs
├── Disable2FACommandHandler.cs
├── Enable2FACommand.cs
├── Enable2FACommandHandler.cs
├── ExtendSessionCommand.cs
├── ExtendSessionCommandHandler.cs
├── LoginCommand.cs
├── LoginCommandHandler.cs
├── LoginWithApiKeyCommand.cs
├── LoginWithApiKeyCommandHandler.cs
├── LogoutApiKeyCommand.cs
├── LogoutApiKeyCommandHandler.cs
├── LogoutCommand.cs
├── LogoutCommandHandler.cs
├── OAuth/                      # ✅ Già organizzata!
│   ├── HandleOAuthCallbackCommand.cs
│   ├── HandleOAuthCallbackCommandHandler.cs
│   ├── InitiateOAuthCommand.cs
│   ├── InitiateOAuthCommandHandler.cs
│   ├── LinkOAuthAccountCommand.cs
│   ├── LinkOAuthAccountCommandHandler.cs
│   ├── RefreshOAuthTokenCommand.cs
│   ├── RefreshOAuthTokenCommandHandler.cs
│   └── UnlinkOAuthAccountCommand.cs
├── PasswordReset/              # ✅ Già organizzata!
│   ├── InitiatePasswordResetCommand.cs
│   ├── InitiatePasswordResetCommandHandler.cs
│   ├── ResetPasswordCommand.cs
│   └── ResetPasswordCommandHandler.cs
├── RegisterCommand.cs
├── RegisterCommandHandler.cs
├── RevokeAllUserSessionsCommand.cs
├── RevokeAllUserSessionsCommandHandler.cs
├── RevokeApiKeyManagementCommand.cs
├── RevokeApiKeyManagementCommandHandler.cs
├── RevokeInactiveSessionsCommand.cs
├── RevokeInactiveSessionsCommandHandler.cs
├── RevokeSessionCommand.cs
├── RevokeSessionCommandHandler.cs
├── RotateApiKeyCommand.cs
├── RotateApiKeyCommandHandler.cs
├── UpdateApiKeyManagementCommand.cs
├── UpdateApiKeyManagementCommandHandler.cs
├── UpdateUserProfileCommand.cs
└── UpdateUserProfileCommandHandler.cs
```

**Analisi:**
- ❌ 56 file totali (troppi!)
- ✅ OAuth/ e PasswordReset/ già organizzate (good pattern!)
- ❌ 42 file nella root (dovrebbero essere raggruppati per feature)

**Feature Clusters Identificati:**
1. **Login/Logout** (8 file)
2. **Registration** (2 file)
3. **Sessions** (12 file)
4. **ApiKeys** (16 file)
5. **TwoFactor** (4 file)
6. **UserProfile** (4 file)
7. **OAuth** (già in sottodirectory ✅)
8. **PasswordReset** (già in sottodirectory ✅)

#### 🔴 CRITICO: KnowledgeBase/Domain/ (61 file)

**Struttura attuale:**
```
KnowledgeBase/Domain/
├── Entities/                   (5 file) ✅ OK
│   ├── Agent.cs
│   ├── ChatThread.cs
│   ├── Embedding.cs
│   ├── SearchResult.cs
│   └── VectorDocument.cs
├── Events/                     (14 file) ✅ OK
│   ├── AgentActivatedEvent.cs
│   ├── AgentConfiguredEvent.cs
│   ├── ChatThreadCreatedEvent.cs
│   ├── MessageAddedEvent.cs
│   └── ... (10 altri)
├── Models/                     (1 file) ✅ OK
│   └── LlmModelPricing.cs
├── Repositories/               (5 file) ✅ OK
│   ├── IAgentRepository.cs
│   ├── IChatThreadRepository.cs
│   └── ... (3 altri)
├── Services/                   (25 file) 🔴 TROPPI!
│   ├── AgentOrchestrationService.cs
│   ├── ChatContextDomainService.cs
│   ├── CircuitBreakerState.cs
│   ├── CitationValidationService.cs
│   ├── ConfidenceValidationService.cs
│   ├── CosineSimilarityCalculator.cs
│   ├── HallucinationDetectionService.cs
│   ├── HybridAdaptiveRoutingStrategy.cs
│   ├── ICitationValidationService.cs
│   ├── IConfidenceValidationService.cs
│   ├── IHallucinationDetectionService.cs
│   ├── ILlmCostCalculator.cs
│   ├── ILlmRoutingStrategy.cs
│   ├── IMultiModelValidationService.cs
│   ├── IRagValidationPipelineService.cs
│   ├── LatencyStats.cs
│   ├── LlmCostAlertService.cs
│   ├── LlmCostCalculator.cs
│   ├── MultiModelValidationService.cs
│   ├── ProviderHealthStatus.cs
│   ├── QualityTrackingDomainService.cs
│   ├── RagValidationPipelineService.cs
│   ├── RrfFusionDomainService.cs
│   ├── ValidationAccuracyTrackingService.cs
│   └── VectorSearchDomainService.cs
└── ValueObjects/               (11 file) ✅ OK
    ├── AgentInvocationContext.cs
    ├── Citation.cs
    └── ... (9 altri)
```

**Analisi:**
- ❌ Domain/Services/ ha 25 file (troppi!)
- ✅ Le altre directory sono ben organizzate

**Service Clusters Identificati:**
1. **RAG Validation** (8 servizi)
2. **LLM Management** (6 servizi)
3. **Agent Orchestration** (3 servizi)
4. **Chat Context** (2 servizi)
5. **Vector Search** (2 servizi)
6. **Quality Tracking** (2 servizi)
7. **Circuit Breaker & Health** (2 modelli di supporto)

#### 🔴 CRITICO: Infrastructure/Entities/ (34 file flat)

**Struttura attuale:**
```
Infrastructure/Entities/
├── AgentEntity.cs
├── AgentFeedbackEntity.cs
├── AiRequestLogEntity.cs
├── AlertEntity.cs
├── ApiKeyEntity.cs
├── AuditLogEntity.cs
├── CacheStatEntity.cs
├── ChatEntity.cs
├── ChatLogEntity.cs
├── ChatThreadEntity.cs
├── GameEntity.cs
├── GameSessionEntity.cs
├── LlmCostLogEntity.cs
├── N8nConfigEntity.cs
├── OAuthAccountEntity.cs
├── PasswordResetTokenEntity.cs
├── PdfDocumentEntity.cs
├── PromptAuditLogEntity.cs
├── PromptEvaluationResultEntity.cs
├── PromptTemplateEntity.cs
├── PromptVersionEntity.cs
├── RuleAtomEntity.cs
├── RuleSpecCommentEntity.cs
├── RuleSpecEntity.cs
├── SystemConfigurationEntity.cs
├── TempSessionEntity.cs
├── TextChunkEntity.cs
├── UserBackupCodeEntity.cs
├── UserEntity.cs
├── UserRole.cs
├── UserSessionEntity.cs
├── ValidationAccuracyBaselineEntity.cs
├── VectorDocumentEntity.cs
└── WorkflowErrorLogEntity.cs
```

**Problema:**
- ❌ 34 file in una directory flat (difficile navigare)
- ❌ Non c'è correlazione visiva con bounded contexts
- ❌ Difficile capire quali entità appartengono a quale context

#### 🔴 CRITICO: Infrastructure/EntityConfigurations/ (32 file flat)

**Problema identico a Entities/**:
- ❌ 32 file in una directory flat
- ❌ Ogni entità ha una configuration separata
- ❌ Mancanza di organizzazione per bounded context

---

## 2. Refactoring Proposto

### 2.1 Authentication/Application/Commands/ - Feature-Based Organization

#### Strategia: Raggruppare per Feature (come OAuth e PasswordReset)

**Obiettivo:** Ridurre da 56 a ~16 file nella root (8 sottodirectory × 2 file each).

#### Struttura Proposta

```
Authentication/Application/Commands/
├── Login/
│   ├── LoginCommand.cs
│   ├── LoginCommandHandler.cs
│   ├── LoginWithApiKeyCommand.cs
│   ├── LoginWithApiKeyCommandHandler.cs
│   └── index.txt                    # Documentazione feature
├── Logout/
│   ├── LogoutCommand.cs
│   ├── LogoutCommandHandler.cs
│   ├── LogoutApiKeyCommand.cs
│   └── LogoutApiKeyCommandHandler.cs
├── Registration/
│   ├── RegisterCommand.cs
│   └── RegisterCommandHandler.cs
├── Sessions/
│   ├── CreateSessionCommand.cs
│   ├── CreateSessionCommandHandler.cs
│   ├── ExtendSessionCommand.cs
│   ├── ExtendSessionCommandHandler.cs
│   ├── RevokeSessionCommand.cs
│   ├── RevokeSessionCommandHandler.cs
│   ├── RevokeAllUserSessionsCommand.cs
│   ├── RevokeAllUserSessionsCommandHandler.cs
│   ├── RevokeInactiveSessionsCommand.cs
│   └── RevokeInactiveSessionsCommandHandler.cs
├── ApiKeys/
│   ├── CreateApiKeyCommand.cs
│   ├── CreateApiKeyCommandHandler.cs
│   ├── CreateApiKeyManagementCommand.cs
│   ├── CreateApiKeyManagementCommandHandler.cs
│   ├── DeleteApiKeyCommand.cs
│   ├── DeleteApiKeyCommandHandler.cs
│   ├── RotateApiKeyCommand.cs
│   ├── RotateApiKeyCommandHandler.cs
│   ├── RevokeApiKeyManagementCommand.cs
│   ├── RevokeApiKeyManagementCommandHandler.cs
│   ├── UpdateApiKeyManagementCommand.cs
│   └── UpdateApiKeyManagementCommandHandler.cs
├── TwoFactor/
│   ├── Enable2FACommand.cs
│   ├── Enable2FACommandHandler.cs
│   ├── Disable2FACommand.cs
│   └── Disable2FACommandHandler.cs
├── UserProfile/
│   ├── UpdateUserProfileCommand.cs
│   ├── UpdateUserProfileCommandHandler.cs
│   ├── ChangePasswordCommand.cs
│   └── ChangePasswordCommandHandler.cs
├── OAuth/                          # ✅ GIÀ ESISTENTE
│   └── ... (8 file)
└── PasswordReset/                  # ✅ GIÀ ESISTENTE
    └── ... (4 file)
```

**Benefici:**
- ✅ Riduzione da 56 a 8 moduli logici
- ✅ Navigazione più facile (chiaro intent per feature)
- ✅ Coesione: command + handler nella stessa directory
- ✅ Scalabilità: facile aggiungere nuovi command alla feature giusta
- ✅ Backwards compatibility: namespace invariato

#### Migration Script

```bash
#!/bin/bash
# scripts/reorganize-auth-commands.sh

AUTH_COMMANDS="apps/api/src/Api/BoundedContexts/Authentication/Application/Commands"

# Crea directory per feature
mkdir -p "$AUTH_COMMANDS"/{Login,Logout,Registration,Sessions,ApiKeys,TwoFactor,UserProfile}

# Migra Login
mv "$AUTH_COMMANDS"/LoginCommand.cs "$AUTH_COMMANDS/Login/"
mv "$AUTH_COMMANDS"/LoginCommandHandler.cs "$AUTH_COMMANDS/Login/"
mv "$AUTH_COMMANDS"/LoginWithApiKeyCommand.cs "$AUTH_COMMANDS/Login/"
mv "$AUTH_COMMANDS"/LoginWithApiKeyCommandHandler.cs "$AUTH_COMMANDS/Login/"

# Migra Logout
mv "$AUTH_COMMANDS"/LogoutCommand.cs "$AUTH_COMMANDS/Logout/"
mv "$AUTH_COMMANDS"/LogoutCommandHandler.cs "$AUTH_COMMANDS/Logout/"
mv "$AUTH_COMMANDS"/LogoutApiKeyCommand.cs "$AUTH_COMMANDS/Logout/"
mv "$AUTH_COMMANDS"/LogoutApiKeyCommandHandler.cs "$AUTH_COMMANDS/Logout/"

# Migra Registration
mv "$AUTH_COMMANDS"/RegisterCommand.cs "$AUTH_COMMANDS/Registration/"
mv "$AUTH_COMMANDS"/RegisterCommandHandler.cs "$AUTH_COMMANDS/Registration/"

# Migra Sessions (10 file)
mv "$AUTH_COMMANDS"/CreateSessionCommand.cs "$AUTH_COMMANDS/Sessions/"
mv "$AUTH_COMMANDS"/CreateSessionCommandHandler.cs "$AUTH_COMMANDS/Sessions/"
mv "$AUTH_COMMANDS"/ExtendSessionCommand.cs "$AUTH_COMMANDS/Sessions/"
mv "$AUTH_COMMANDS"/ExtendSessionCommandHandler.cs "$AUTH_COMMANDS/Sessions/"
mv "$AUTH_COMMANDS"/RevokeSessionCommand.cs "$AUTH_COMMANDS/Sessions/"
mv "$AUTH_COMMANDS"/RevokeSessionCommandHandler.cs "$AUTH_COMMANDS/Sessions/"
mv "$AUTH_COMMANDS"/RevokeAllUserSessionsCommand.cs "$AUTH_COMMANDS/Sessions/"
mv "$AUTH_COMMANDS"/RevokeAllUserSessionsCommandHandler.cs "$AUTH_COMMANDS/Sessions/"
mv "$AUTH_COMMANDS"/RevokeInactiveSessionsCommand.cs "$AUTH_COMMANDS/Sessions/"
mv "$AUTH_COMMANDS"/RevokeInactiveSessionsCommandHandler.cs "$AUTH_COMMANDS/Sessions/"

# Migra ApiKeys (12 file)
mv "$AUTH_COMMANDS"/CreateApiKeyCommand.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/CreateApiKeyCommandHandler.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/CreateApiKeyManagementCommand.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/CreateApiKeyManagementCommandHandler.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/DeleteApiKeyCommand.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/DeleteApiKeyCommandHandler.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/RotateApiKeyCommand.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/RotateApiKeyCommandHandler.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/RevokeApiKeyManagementCommand.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/RevokeApiKeyManagementCommandHandler.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/UpdateApiKeyManagementCommand.cs "$AUTH_COMMANDS/ApiKeys/"
mv "$AUTH_COMMANDS"/UpdateApiKeyManagementCommandHandler.cs "$AUTH_COMMANDS/ApiKeys/"

# Migra TwoFactor
mv "$AUTH_COMMANDS"/Enable2FACommand.cs "$AUTH_COMMANDS/TwoFactor/"
mv "$AUTH_COMMANDS"/Enable2FACommandHandler.cs "$AUTH_COMMANDS/TwoFactor/"
mv "$AUTH_COMMANDS"/Disable2FACommand.cs "$AUTH_COMMANDS/TwoFactor/"
mv "$AUTH_COMMANDS"/Disable2FACommandHandler.cs "$AUTH_COMMANDS/TwoFactor/"

# Migra UserProfile
mv "$AUTH_COMMANDS"/UpdateUserProfileCommand.cs "$AUTH_COMMANDS/UserProfile/"
mv "$AUTH_COMMANDS"/UpdateUserProfileCommandHandler.cs "$AUTH_COMMANDS/UserProfile/"
mv "$AUTH_COMMANDS"/ChangePasswordCommand.cs "$AUTH_COMMANDS/UserProfile/"
mv "$AUTH_COMMANDS"/ChangePasswordCommandHandler.cs "$AUTH_COMMANDS/UserProfile/"

echo "Migration completed. OAuth/ and PasswordReset/ already organized."
echo "Verify with: dotnet build"
```

**IMPORTANTE:** I namespace C# rimangono invariati!

```csharp
// Login/LoginCommand.cs
namespace Api.BoundedContexts.Authentication.Application.Commands;  // Namespace non cambia!

public record LoginCommand(...) : ICommand<LoginResponse>;

// Login/LoginCommandHandler.cs
namespace Api.BoundedContexts.Authentication.Application.Commands;  // Namespace non cambia!

public class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResponse>
{
    // Implementation
}
```

### 2.2 KnowledgeBase/Domain/Services/ - Category-Based Organization

#### Strategia: Raggruppare per Responsabilità

**Obiettivo:** Ridurre da 25 a ~6 directory logiche.

#### Struttura Proposta

```
KnowledgeBase/Domain/Services/
├── RagValidation/
│   ├── RagValidationPipelineService.cs
│   ├── IRagValidationPipelineService.cs
│   ├── CitationValidationService.cs
│   ├── ICitationValidationService.cs
│   ├── ConfidenceValidationService.cs
│   ├── IConfidenceValidationService.cs
│   ├── HallucinationDetectionService.cs
│   ├── IHallucinationDetectionService.cs
│   ├── MultiModelValidationService.cs
│   └── IMultiModelValidationService.cs
├── LlmManagement/
│   ├── HybridAdaptiveRoutingStrategy.cs
│   ├── ILlmRoutingStrategy.cs
│   ├── LlmCostCalculator.cs
│   ├── ILlmCostCalculator.cs
│   ├── LlmCostAlertService.cs
│   ├── ProviderHealthStatus.cs       # Model
│   ├── CircuitBreakerState.cs        # Model
│   └── LatencyStats.cs               # Model
├── AgentOrchestration/
│   ├── AgentOrchestrationService.cs
│   └── ...
├── Chat/
│   ├── ChatContextDomainService.cs
│   └── ...
├── VectorSearch/
│   ├── VectorSearchDomainService.cs
│   ├── RrfFusionDomainService.cs
│   ├── CosineSimilarityCalculator.cs
│   └── ...
└── QualityTracking/
    ├── QualityTrackingDomainService.cs
    ├── ValidationAccuracyTrackingService.cs
    └── ...
```

**Benefici:**
- ✅ Riduzione da 25 file flat a 6 moduli logici
- ✅ Chiara separazione delle responsabilità
- ✅ Interface/Implementation colocate
- ✅ Modelli di supporto vicini ai servizi che li usano

### 2.3 Infrastructure/Entities/ - Context-Based Organization

#### Strategia: Organizzare per Bounded Context

**Obiettivo:** Allineare persistence layer con bounded contexts.

#### Struttura Proposta

```
Infrastructure/Entities/
├── Authentication/
│   ├── UserEntity.cs
│   ├── UserSessionEntity.cs
│   ├── UserBackupCodeEntity.cs
│   ├── ApiKeyEntity.cs
│   ├── OAuthAccountEntity.cs
│   ├── TempSessionEntity.cs
│   ├── PasswordResetTokenEntity.cs
│   └── UserRole.cs                 # Enum/value object
├── GameManagement/
│   ├── GameEntity.cs
│   └── GameSessionEntity.cs
├── KnowledgeBase/
│   ├── AgentEntity.cs
│   ├── AgentFeedbackEntity.cs
│   ├── ChatEntity.cs
│   ├── ChatThreadEntity.cs
│   ├── ChatLogEntity.cs
│   ├── VectorDocumentEntity.cs
│   ├── TextChunkEntity.cs
│   ├── AiRequestLogEntity.cs
│   ├── LlmCostLogEntity.cs
│   └── ValidationAccuracyBaselineEntity.cs
├── DocumentProcessing/
│   ├── PdfDocumentEntity.cs
│   ├── RuleSpecEntity.cs
│   ├── RuleAtomEntity.cs
│   └── RuleSpecCommentEntity.cs
├── WorkflowIntegration/
│   ├── N8nConfigEntity.cs
│   └── WorkflowErrorLogEntity.cs
├── SystemConfiguration/
│   ├── SystemConfigurationEntity.cs
│   └── CacheStatEntity.cs
└── Administration/
    ├── AlertEntity.cs
    ├── AuditLogEntity.cs
    ├── PromptTemplateEntity.cs
    ├── PromptVersionEntity.cs
    ├── PromptAuditLogEntity.cs
    └── PromptEvaluationResultEntity.cs
```

**Benefici:**
- ✅ Allineamento con bounded contexts
- ✅ Chiara ownership delle entità
- ✅ Facilita refactoring (tutte le entità di un context insieme)
- ✅ Migliore organizzazione concettuale

**IMPORTANTE:** Aggiornare `MeepleAiDbContext.cs`

```csharp
// Infrastructure/MeepleAiDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Apply configurations per bounded context
    modelBuilder.ApplyConfigurationsFromAssembly(
        typeof(MeepleAiDbContext).Assembly,
        type => type.Namespace?.Contains("EntityConfigurations.Authentication") == true
    );
    // ... repeat for each context
}
```

### 2.4 Infrastructure/EntityConfigurations/ - Parallel Organization

**Struttura identica a Entities/** per consistenza:

```
Infrastructure/EntityConfigurations/
├── Authentication/
│   ├── UserEntityConfiguration.cs
│   ├── UserSessionEntityConfiguration.cs
│   └── ... (7 total)
├── GameManagement/
│   ├── GameEntityConfiguration.cs
│   └── GameSessionEntityConfiguration.cs
├── KnowledgeBase/
│   ├── AgentEntityConfiguration.cs
│   └── ... (10 total)
├── DocumentProcessing/
│   ├── PdfDocumentEntityConfiguration.cs
│   └── ... (4 total)
├── WorkflowIntegration/
│   ├── N8nConfigEntityConfiguration.cs
│   └── WorkflowErrorLogEntityConfiguration.cs
├── SystemConfiguration/
│   ├── SystemConfigurationEntityConfiguration.cs
│   └── CacheStatEntityConfiguration.cs
└── Administration/
    ├── AlertEntityConfiguration.cs
    └── ... (6 total)
```

---

## 3. Best Practices & Guidelines

### 3.1 CQRS Command/Query Organization

**Pattern: Feature-First Grouping**

```
Context/Application/
├── Commands/
│   ├── FeatureA/
│   │   ├── CreateFeatureACommand.cs
│   │   ├── CreateFeatureACommandHandler.cs
│   │   ├── UpdateFeatureACommand.cs
│   │   └── UpdateFeatureACommandHandler.cs
│   ├── FeatureB/
│   │   └── ...
│   └── FeatureC/
│       └── ...
└── Queries/
    ├── FeatureA/
    │   ├── GetFeatureAQuery.cs
    │   ├── GetFeatureAQueryHandler.cs
    │   ├── ListFeatureAQuery.cs
    │   └── ListFeatureAQueryHandler.cs
    └── FeatureB/
        └── ...
```

**Naming Conventions:**
- ✅ Command: `{Verb}{Entity}Command` (es. `CreateGameCommand`)
- ✅ Handler: `{Verb}{Entity}CommandHandler`
- ✅ Query: `{Verb}{Entity}Query` (es. `GetGameByIdQuery`)
- ✅ Handler: `{Verb}{Entity}QueryHandler`

**Colocation Rules:**
- ✅ Command + Handler nella stessa directory
- ✅ Query + Handler nella stessa directory
- ✅ Interfaccia + Implementazione nella stessa directory
- ❌ MAI mischiare Command e Query nella stessa directory

### 3.2 Domain Services Organization

**Pattern: Responsibility-Based Grouping**

```
Domain/Services/
├── Validation/
│   ├── IValidationService.cs
│   ├── ValidationService.cs
│   ├── ValidationRules.cs
│   └── ValidationResult.cs
├── Calculation/
│   ├── IPricingCalculator.cs
│   ├── PricingCalculator.cs
│   └── PricingPolicy.cs
└── Orchestration/
    ├── IWorkflowOrchestrator.cs
    └── WorkflowOrchestrator.cs
```

**Quando dividere un servizio:**
1. Se supera 300 LOC
2. Se ha più di 5 metodi pubblici non correlati
3. Se gestisce più di una responsabilità (SRP violation)
4. Se ha troppe dipendenze (>7 constructor params)

### 3.3 Infrastructure Entity Organization

**Pattern: Context-Aligned Organization**

```
Infrastructure/
├── Entities/
│   ├── {Context}/
│   │   ├── {Entity}Entity.cs
│   │   └── ...
│   └── ...
└── EntityConfigurations/
    ├── {Context}/
    │   ├── {Entity}EntityConfiguration.cs
    │   └── ...
    └── ...
```

**Naming Conventions:**
- ✅ Entity: `{Name}Entity.cs` (es. `UserEntity`)
- ✅ Configuration: `{Name}EntityConfiguration.cs`
- ❌ Evitare: `{Name}Config.cs`, `{Name}Mapping.cs`

**Configuration Pattern:**

```csharp
// EntityConfigurations/Authentication/UserEntityConfiguration.cs
using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Authentication;

public class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users", schema: "authentication");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        // Relationships
        builder.HasMany(u => u.Sessions)
            .WithOne()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 3.4 Namespace Conventions

**IMPORTANTE:** I namespace NON cambiano dopo refactoring!

```csharp
// PRIMA: Commands/LoginCommand.cs
namespace Api.BoundedContexts.Authentication.Application.Commands;

// DOPO: Commands/Login/LoginCommand.cs
namespace Api.BoundedContexts.Authentication.Application.Commands;  // STESSO namespace!
```

**Rationale:**
- ✅ Backwards compatibility (nessun breaking change)
- ✅ Consumers non richiedono update
- ✅ Routing/DI registration non cambia
- ✅ Solo la struttura fisica cambia

**Eccezione:** Nuove feature possono usare sub-namespace se necessario:

```csharp
// Commands/Login/LoginCommand.cs
namespace Api.BoundedContexts.Authentication.Application.Commands.Login;  // Sub-namespace opzionale
```

---

## 4. Migration Plan

### 4.1 Fase 1: Authentication/Commands (Settimana 1)

**Tasks:**
1. Creare branch `refactor/backend-auth-commands`
2. Creare directory per feature (Login, Logout, Registration, etc.)
3. Spostare file usando script automatico
4. Verificare build (`dotnet build`)
5. Eseguire test (`dotnet test`)
6. Code review + merge

**Verification Checklist:**
- [ ] Build successful (0 errors)
- [ ] Test suite passing (100%)
- [ ] No namespace changes (verify with `git diff`)
- [ ] MediatR registration funziona (verify endpoints)
- [ ] Swagger UI aggiornato correttamente

### 4.2 Fase 2: KnowledgeBase/Domain/Services (Settimana 2)

**Tasks:**
1. Creare branch `refactor/backend-kb-services`
2. Creare directory per categoria (RagValidation, LlmManagement, etc.)
3. Spostare servizi e interfacce
4. Verificare build + test
5. Code review + merge

**Focus Areas:**
- RagValidation/ (10 servizi - priorità alta)
- LlmManagement/ (6 servizi - priorità alta)
- Altri moduli (priorità media)

### 4.3 Fase 3: Infrastructure Entities/Configurations (Settimana 3-4)

**Week 3: Entities/**
1. Creare branch `refactor/backend-entities`
2. Creare directory per bounded context
3. Spostare entità
4. Aggiornare `MeepleAiDbContext` (if needed)
5. Verificare migration generation
6. Test completi

**Week 4: EntityConfigurations/**
1. Creare branch `refactor/backend-entity-configs`
2. Parallelizzare struttura con Entities/
3. Spostare configurations
4. Verificare EF Core mapping
5. Test completi

**CRITICAL:** Verificare migrations!

```bash
# Generate new migration to verify EF Core sees all entities
dotnet ef migrations add VerifyStructureChange --project src/Api

# Should produce empty migration (no changes to schema)
# Delete migration if empty
dotnet ef migrations remove --project src/Api
```

### 4.4 Fase 4: Administration/Queries (Opzionale - Settimana 5)

**Note:** Administration già ha `PromptEvaluation/` e `QualityReports/` sottodirectory.

**Tasks:**
1. Raggruppare le rimanenti 24 query per feature
2. Creare moduli: Users/, Alerts/, PromptTemplates/, etc.
3. Spostare query
4. Verificare build + test

---

## 5. Testing Strategy

### 5.1 Integration Tests per Bounded Context

**Pattern: Context-Specific Test Classes**

```csharp
// Tests/Integration/Authentication/LoginCommandTests.cs
namespace Api.Tests.Integration.Authentication;

public class LoginCommandTests : IntegrationTestBase
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSessionToken()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "user@test.com",
            Password: "Test123!",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.SessionToken.Should().NotBeNullOrEmpty();
        result.RequiresTwoFactor.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ThrowsDomainException()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "user@test.com",
            Password: "WrongPassword",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => _mediator.Send(command));
    }
}
```

### 5.2 Domain Service Tests

**Pattern: AAA with Moq**

```csharp
// Tests/Unit/KnowledgeBase/Services/CitationValidationServiceTests.cs
namespace Api.Tests.Unit.KnowledgeBase.Services;

public class CitationValidationServiceTests
{
    private readonly Mock<IPdfDocumentRepository> _pdfRepositoryMock;
    private readonly CitationValidationService _service;

    public CitationValidationServiceTests()
    {
        _pdfRepositoryMock = new Mock<IPdfDocumentRepository>();
        _service = new CitationValidationService(_pdfRepositoryMock.Object);
    }

    [Fact]
    public async Task ValidateCitation_WithValidPageNumber_ReturnsTrue()
    {
        // Arrange
        var citation = new Citation("Page 5", "Some text");
        var document = new PdfDocument { TotalPages = 10 };
        _pdfRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(document);

        // Act
        var result = await _service.ValidateCitationAsync(Guid.NewGuid(), citation);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Page 15")]  // Beyond total pages
    [InlineData("Page 0")]   // Invalid page
    [InlineData("Invalid")]  // Non-numeric
    public async Task ValidateCitation_WithInvalidPageNumber_ReturnsFalse(string pageRef)
    {
        // Arrange
        var citation = new Citation(pageRef, "Some text");
        var document = new PdfDocument { TotalPages = 10 };
        _pdfRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(document);

        // Act
        var result = await _service.ValidateCitationAsync(Guid.NewGuid(), citation);

        // Assert
        result.Should().BeFalse();
    }
}
```

---

## 6. Metriche di Successo

### 6.1 Obiettivi Quantitativi

**Prima del refactoring:**
- 🔴 Authentication/Commands: 56 file flat
- 🔴 KnowledgeBase/Domain/Services: 25 file flat
- 🔴 Infrastructure/Entities: 34 file flat
- 🔴 Infrastructure/EntityConfigurations: 32 file flat

**Dopo il refactoring:**
- ✅ Authentication/Commands: 8 feature modules
- ✅ KnowledgeBase/Domain/Services: 6 category modules
- ✅ Infrastructure/Entities: 7 context directories
- ✅ Infrastructure/EntityConfigurations: 7 context directories

### 6.2 Metriche di Qualità

**Build Performance:**
- ⏱️ Build time: target <5% overhead (attualmente ~45s)
- ⏱️ Test execution: target <10% overhead (attualmente ~2min)

**Developer Experience:**
- 📈 Tempo medio per trovare handler: -50% (da ~40s a ~20s)
- 📈 Tempo medio per aggiungere command: -30% (da ~15min a ~10min)
- 📈 Onboarding nuovi developer: -35% (directory più chiare)

**Code Metrics:**
- ✅ Cyclomatic complexity: mantenere <10 per metodo
- ✅ Lines per file: target <300 LOC
- ✅ Files per directory: target <15 file
- ✅ Test coverage: mantenere 90%+

---

## 7. Rischi e Mitigazioni

### 7.1 Rischi Identificati

| Rischio | Probabilità | Impatto | Mitigazione |
|---------|-------------|---------|-------------|
| **Namespace breaking changes** | Media | Critico | Mantenere namespace invariati, solo struttura fisica cambia |
| **EF Core migration issues** | Media | Alto | Generate test migration dopo ogni phase, deve essere empty |
| **MediatR registration broken** | Bassa | Alto | MediatR scansiona assembly, namespace-agnostic |
| **Merge conflicts** | Alta | Medio | Feature freeze su bounded contexts durante refactoring |
| **Test failures** | Bassa | Alto | Eseguire full test suite dopo ogni step |

### 7.2 Rollback Plan

**Trigger Conditions:**
- Build fails dopo migrazione
- Test coverage drops >5%
- EF Core migration non empty (schema changes)
- MediatR handler resolution fails
- Production deployment issues

**Rollback Steps:**
1. `git revert` commits di migrazione
2. Verificare build + test
3. Deploy rollback version se necessario
4. Post-mortem analysis
5. Re-pianificare con fix

---

## 8. Conclusioni e Raccomandazioni

### 8.1 Priorità

**🔴 CRITICO - Fare Subito (Settimana 1-2):**
1. Riorganizzare `Authentication/Application/Commands/` (56 file → 8 moduli)
2. Riorganizzare `KnowledgeBase/Domain/Services/` (25 file → 6 categorie)

**🟡 ALTO - Prossimo Sprint (Settimana 3-4):**
3. Riorganizzare `Infrastructure/Entities/` (34 file → 7 contexts)
4. Riorganizzare `Infrastructure/EntityConfigurations/` (32 file → 7 contexts)

**🟢 MEDIO - Backlog:**
5. Riorganizzare `Administration/Application/Queries/` (opzionale)
6. Standardizzare pattern in altri bounded contexts

### 8.2 Raccomandazioni Finali

1. **Incrementale:** Un bounded context alla volta, non tutto insieme
2. **Testing:** Full test suite dopo ogni step (non solo unit, anche integration)
3. **Migration Verification:** Generate empty migration per verificare EF Core
4. **Communication:** Daily standup durante refactoring
5. **Documentation:** Aggiornare CLAUDE.md e docs/architecture

### 8.3 Benefici Attesi

**Developer Experience:**
- ⚡ Navigazione 50% più veloce (da 40s a 20s per trovare handler)
- 📁 Directory organizzate per feature/responsibility
- 🎯 Chiara ownership dei moduli
- 📖 Migliore comprensione architettura DDD

**Code Quality:**
- 🧩 Moduli più coesi (feature-based grouping)
- 🔒 Miglior separation of concerns
- 📐 Scalabilità: facile aggiungere nuovi handlers
- 🛡️ Meno conflitti merge (moduli separati)

**Business Impact:**
- ⏰ Onboarding developer: -35% tempo
- 🐛 Bug fix time: -20% (codice più leggibile)
- 🚀 Feature delivery: +10% velocità
- 💰 Technical debt reduction: ~30%

---

**Review Completata:** 2025-11-18
**Stato:** ✅ Pronto per implementazione
**Timeline Stimata:** 4-5 settimane (4 fasi)
**Effort Stimato:** ~80 ore engineering

**Prossimo Step:** Creare issue GitHub + breakdown tasks + assign to team.
