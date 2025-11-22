# Issue: Standardize Backend Test Naming Convention

**ID**: TEST-001
**Category**: Backend Testing - Code Quality
**Priority**: 🔴 **CRITICAL**
**Status**: 🔴 Open
**Created**: 2025-11-20

---

## 📋 Summary

Standardizzare la naming convention per i test backend. Attualmente coesistono due pattern: test con prefisso `Test##_` e test senza prefisso, creando inconsistenza e difficoltà di lettura.

---

## 🎯 Problem Statement

### Current Inconsistency

Esempio da `CitationValidationServiceTests.cs`:
```csharp
// ✅ Pattern A: Test prefix (numbered)
[Fact]
public async Task Test01_ValidateCitations_AllValid_ReturnsValid() { }

[Fact]
public async Task Test02_ValidateCitations_Empty_ReturnsValid() { }

[Fact]
public async Task Test13_ValidateCitations_NegativePage_ReturnsInvalid() { }
```

Esempio da `EmailTests.cs` e `UpdateGameCommandHandlerTests.cs`:
```csharp
// ✅ Pattern B: No prefix (descriptive only)
[Fact]
public void Email_WithValidEmail_CreatesSuccessfully() { }

[Fact]
public async Task Handle_NonExistentGame_ThrowsInvalidOperationException() { }
```

### Issues
- ⚠️ **Inconsistent patterns** across 187 test files
- ⚠️ **Confusion for new developers** su quale pattern usare
- ⚠️ **Mixed patterns** nello stesso bounded context
- ⚠️ **Difficile navigazione** con IDE (sorting diverso)

### Files Affected
- `CitationValidationServiceTests.cs` (24 tests with Test## prefix)
- `EmailTests.cs` (6 tests without prefix)
- `UpdateGameCommandHandlerTests.cs` (13 tests without prefix)
- `StreamQaQueryHandlerTests.cs` (multiple tests without prefix)
- **~187 test files total**

---

## 🔧 Solution: Adopt Pattern B (Descriptive Without Prefix)

### Recommended Pattern

```csharp
// ✅ RECOMMENDED: Method_Scenario_ExpectedBehavior
[Fact]
public async Task ValidateCitations_AllValid_ReturnsValid()
{
    // Arrange
    var snippets = CreateValidSnippets();

    // Act
    var result = await _service.ValidateCitationsAsync(snippets, gameId, cancellationToken);

    // Assert
    Assert.True(result.IsValid);
    Assert.Equal(3, result.ValidCitations);
}

[Fact]
public async Task ValidateCitations_InvalidPageNumber_ReturnsInvalid()
{
    // Arrange
    var snippets = CreateSnippetsWithInvalidPage();

    // Act
    var result = await _service.ValidateCitationsAsync(snippets, gameId, cancellationToken);

    // Assert
    Assert.False(result.IsValid);
    Assert.Single(result.Errors);
    Assert.Equal(CitationErrorType.InvalidPageNumber, result.Errors[0].ErrorType);
}
```

### Rationale
1. **Self-documenting**: Nome descrive completamente il test
2. **IDE-friendly**: Alphabetical sorting by method name
3. **xUnit standard**: Pattern raccomandato da Microsoft e xUnit docs
4. **Refactoring-safe**: Numeri diventano obsoleti quando test vengono riordinati
5. **Readability**: Più facile capire cosa testa senza vedere il codice

### Migration Strategy

#### Phase 1: Update Documentation (1 hour)
1. Aggiornare `docs/02-development/testing/backend/testing-guide.md` con il pattern raccomandato
2. Aggiungere esempi chiari e counter-esempi
3. Aggiornare CONTRIBUTING.md con naming rules

#### Phase 2: Migrate High-Impact Files (2-3 hours)
Priorità ai file con più test e/o più modifiche recenti:

```bash
# Files to migrate first (highest impact)
apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/CitationValidationServiceTests.cs (24 tests)
apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/RagValidationPipelineServiceTests.cs (15+ tests)
apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/LlmCostLogRepositoryTests.cs
```

**Migration script** (semi-automated):
```csharp
// Before
[Fact]
public async Task Test01_ValidateCitations_AllValid_ReturnsValid() { }

// After (remove Test## prefix)
[Fact]
public async Task ValidateCitations_AllValid_ReturnsValid() { }
```

#### Phase 3: Enforce with Code Review (ongoing)
1. Add checklist item to PR template: "Test names follow Method_Scenario_ExpectedBehavior pattern"
2. Configure git hook to warn about Test## pattern (optional)

---

## 📝 Implementation Checklist

### Phase 1: Documentation (1 hour)
- [ ] Update `docs/02-development/testing/backend/testing-guide.md`
- [ ] Add naming convention section with examples
- [ ] Update CONTRIBUTING.md
- [ ] Create migration guide for existing tests

### Phase 2: High-Impact Migration (2-3 hours)
- [ ] Migrate CitationValidationServiceTests.cs (24 tests)
- [ ] Migrate RagValidationPipelineServiceTests.cs (15+ tests)
- [ ] Migrate LlmCostLogRepositoryTests.cs
- [ ] Migrate other files with Test## prefix (search codebase)
- [ ] Run full test suite to verify no breakage
- [ ] Update test names in related documentation

### Phase 3: Enforcement (ongoing)
- [ ] Add PR template checklist item
- [ ] Configure EditorConfig/Analyzer rule (if possible)
- [ ] Train team on new convention
- [ ] Review existing PRs for compliance

---

## ✅ Acceptance Criteria

- [ ] Single consistent naming pattern across all test files
- [ ] No mixing of Test## prefix and non-prefixed in same bounded context
- [ ] Documentation clearly states the recommended pattern
- [ ] All tests pass after migration
- [ ] PR template includes naming convention checklist
- [ ] Team trained and aware of new convention

---

## 📊 Impact Analysis

### Before
- 187 test files with mixed naming
- ~30% use Test## prefix
- ~70% use descriptive-only names
- Confusion in code reviews

### After
- 187 test files with consistent naming
- 100% use Method_Scenario_ExpectedBehavior pattern
- Clear documentation and guidelines
- Reduced code review friction

### Risks
- **Low risk**: Naming change doesn't affect test execution
- **CI impact**: None (test names in reports will change but still pass)
- **Team adoption**: Requires communication and training

---

## 🔗 Related Issues

- [TEST-002](./backend-test-isolation-fixes.md) - Fix Test Isolation Issues
- [TEST-003](./backend-test-data-factories.md) - Create Test Data Factories
- [TEST-008](./backend-consolidate-theory-tests.md) - Consolidate Theory Tests

---

## 📚 References

- [xUnit Best Practices](https://xunit.net/docs/comparisons#note2)
- [Microsoft Testing Guidelines](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Roy Osherove's Naming Standard](https://osherove.com/blog/2005/4/3/naming-standards-for-unit-tests.html)

---

## 📈 Effort Estimate

**Total: 3-4 hours**

| Phase | Effort | Description |
|-------|--------|-------------|
| Documentation | 1h | Update guides, CONTRIBUTING.md |
| High-Impact Migration | 2-3h | Migrate 30-40 test files with prefix |
| Review & Training | <1h | Team communication |

---

**Last Updated**: 2025-11-20
**Status**: 🔴 Open - Ready for Implementation
**Assignee**: TBD
