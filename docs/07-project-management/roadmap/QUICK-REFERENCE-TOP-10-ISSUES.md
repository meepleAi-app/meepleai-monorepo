# 🎯 TOP 10 ISSUE PRIORITARIE - QUICK REFERENCE

**MeepleAI Monorepo** | **15 Novembre 2025** | **Focus: Code Quality**

---

## 🚨 AZIONE IMMEDIATA

### #1183 - Fix Deadlock Risk in RateLimitService [P0 CRITICAL]

**File**: `apps/api/src/Api/Services/RateLimitService.cs:160-161`

**Problema**:
```csharp
// ❌ DEADLOCK RISK
public RateLimitConfig GetConfigForRole(UserRole role)
{
    var config = _configService.GetRateLimitConfigAsync().Result;  // BLOCCA!
    return config.Configs[role];
}
```

**Soluzione**:
```csharp
// ✅ SAFE
public async Task<RateLimitConfig> GetConfigForRoleAsync(
    UserRole role,
    CancellationToken cancellationToken = default)
{
    var config = await _configService.GetRateLimitConfigAsync(cancellationToken);
    return config.Configs[role];
}
```

**Update Callers**: `AuthEndpoints.cs:347, 527-528, 579-580`

**Effort**: 2-3 ore | **Impact**: 🔥🔥🔥🔥🔥 System-wide auth

---

## 📋 TOP 10 - ORDINE DI ESECUZIONE

| # | Priority | Issue | Titolo | Effort | Week |
|---|----------|-------|--------|--------|------|
| 1 | **P0** | #1183 | Fix Deadlock in RateLimitService | 2-3h | **NOW** |
| 2 | **P1** | #1192 | Add AsNoTracking to Read-Only Queries | 4-6h | 1 |
| 3 | **P1** | #1187 | Replace Hardcoded Config Values | 3-4h | 1 |
| 4 | **P1** | #1184 | Migrate ChatService → CQRS | 2-3d | 2 |
| 5 | **P1** | #1188 | Migrate AgentService → CQRS | 2-3d | 2 |
| 6 | **P1** | #1189 | Migrate RuleSpec Services → CQRS | 2-3d | 3 |
| 7 | **P1** | #1185 | Migrate RuleSpecService → CQRS | 3-4d | 3 |
| 8 | **P2** | #1191 | Complete OAuth Callback → CQRS | 2d | 4 |
| 9 | **P2** | #1186 | Implement Streaming Query Handlers | 2-3d | 4 |
| 10 | **P2** | #1190 | Implement Domain Events | 2-3d | 4 |

---

## ⚡ QUICK WINS (Settimana 1)

### #1192 - Add AsNoTracking to Read-Only Queries

**Pattern**:
```csharp
// Add .AsNoTracking() to all read-only queries
var games = await _context.Games
    .AsNoTracking()  // ← Add this
    .Where(g => g.IsPublished)
    .ToListAsync();
```

**Impact**: ~30% query performance boost
**Effort**: 4-6 ore
**Files**: All Query handlers (`Get*Query`, `Search*Query`, `List*Query`)

---

### #1187 - Replace Hardcoded Configuration Values

**Pattern**:
```csharp
// BEFORE
const int MaxRetries = 3;  // ❌ Hardcoded

// AFTER
var maxRetries = _configuration.GetValue<int>("Retry:MaxAttempts");
```

**Impact**: Maintainability, runtime config
**Effort**: 3-4 ore
**Scope**: Scan codebase for `const` and magic numbers

---

## 🏗️ DDD MIGRATION (Settimane 2-4)

### Week 2: Service Migration (Part 1)

**#1184 - ChatService → CQRS**
- Commands: `SendMessageCommand`, `DeleteMessageCommand`
- Queries: `GetMessagesQuery`, `GetThreadQuery`
- Remove: `ChatService.cs`
- Effort: 2-3 giorni

**#1188 - AgentService → CQRS**
- Commands: `SelectAgentCommand`, `ConfigureAgentCommand`
- Queries: `GetAvailableAgentsQuery`, `GetAgentConfigQuery`
- Remove: `AgentService.cs`
- Effort: 2-3 giorni

---

### Week 3: Service Migration (Part 2)

**#1189 - RuleSpec Comment/Diff → CQRS**
- Services: `CommentService`, `DiffService`
- Pattern: Split into Commands/Queries
- Effort: 2-3 giorni

**#1185 - RuleSpecService → CQRS** (LARGE)
- Domain: RuleSpec aggregate
- Commands: Create, Update, Delete, Validate
- Queries: Get, Search, List
- Remove: `RuleSpecService.cs`
- Effort: 3-4 giorni

---

### Week 4: Advanced Patterns

**#1191 - OAuth Callback → CQRS**
- OAuth flow alignment
- Effort: 2 giorni

**#1186 - Streaming Query Handlers**
```csharp
public async IAsyncEnumerable<ChatMessage> Handle(
    StreamChatQuery request,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (var message in _ragService.StreamResponseAsync(request.Question))
    {
        yield return message;
    }
}
```
- Effort: 2-3 giorni

**#1190 - Domain Events**
- Infrastructure: Event dispatcher
- Pattern: `IDomainEvent`, `IDomainEventHandler`
- Effort: 2-3 giorni

---

## 📅 TIMELINE SETTIMANALE

```
Week 1 (18-22 Nov)
├─ #1183 - Deadlock fix (2-3h) ✓
├─ #1192 - AsNoTracking (4-6h)
└─ #1187 - Config values (3-4h)

Week 2 (25-29 Nov)
├─ #1184 - ChatService → CQRS (2-3d)
└─ #1188 - AgentService → CQRS (2-3d)

Week 3 (2-6 Dic)
├─ #1189 - RuleSpec Services → CQRS (2-3d)
└─ #1185 - RuleSpecService → CQRS (3-4d)

Week 4 (9-13 Dic)
├─ #1191 - OAuth → CQRS (2d)
├─ #1186 - Streaming Handlers (2-3d)
└─ #1190 - Domain Events (2-3d)
```

---

## ✅ CHECKLIST COMPLETAMENTO

### Week 1: Critical & Performance
- [ ] #1183 - Deadlock fixed + load tested
- [ ] #1192 - AsNoTracking added to all read queries
- [ ] #1187 - All hardcoded values moved to config

### Week 2-3: DDD Services Migration
- [ ] #1184 - ChatService removed, CQRS implemented
- [ ] #1188 - AgentService removed, CQRS implemented
- [ ] #1189 - Comment/Diff services removed, CQRS implemented
- [ ] #1185 - RuleSpecService removed, CQRS implemented

### Week 4: Advanced Patterns
- [ ] #1191 - OAuth flow aligned with CQRS
- [ ] #1186 - Streaming queries operational
- [ ] #1190 - Domain events infrastructure complete

### Milestone: DDD 100% Complete! 🎉

---

## 🎯 SUCCESS METRICS

| Metric | Before | After | Target |
|--------|--------|-------|--------|
| DDD Migration | 99% | 100% | ✅ |
| Query Performance | Baseline | +30% | ✅ |
| Auth Stability | Risk deadlock | Safe async | ✅ |
| Config Flexibility | Hardcoded | Runtime | ✅ |
| Legacy Services | 4 remaining | 0 | ✅ |

---

## 📞 SUPPORT

- **Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues
- **Docs**: `docs/ROADMAP-CODE-QUALITY-2025.md`
- **Architecture**: `docs/architecture/board-game-ai-architecture-overview.md`

---

**Last Updated**: 15 Novembre 2025
**Next Review**: Dopo Week 4 (13 Dicembre 2025)
