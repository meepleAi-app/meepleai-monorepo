# Issue #013: Remove Obsolete Data Models

**Priority:** 🟡 MEDIUM
**Category:** Backend / Technical Debt
**Estimated Effort:** 1-2 days
**Sprint:** SHORT-TERM (1-2 months)

## Summary

Legacy model versions exist in the codebase that may no longer be used. Most notably `RuleSpecV0.cs` appears to be superseded by newer `RuleSpec` model. These should be verified and removed if obsolete.

## Obsolete Models Identified

### 1. RuleSpecV0 (Primary Target)

**File:** `apps/api/src/Api/Models/RuleSpecV0.cs`

**Description:** Original version of game rule specification format

**Current Model:** `apps/api/src/Api/Models/RuleSpec.cs`

**Status:** Unknown if still referenced

**Properties Include:**
- Game metadata (title, players, duration)
- Setup instructions
- Game phases
- Available actions
- Scoring rules
- End conditions
- Edge cases
- Glossary

---

## Investigation Tasks

### Phase 1: Usage Analysis (0.5 days)

#### 1.1 Search for References
```bash
# Search for RuleSpecV0 usage in code
grep -r "RuleSpecV0" apps/api/src --include="*.cs"
grep -r "RuleSpecV0" apps/api/tests --include="*.cs"

# Search for references in comments/docs
grep -r "RuleSpecV0" docs/
grep -r "rule.?spec.*v0" docs/ -i
```

#### 1.2 Database Check
```sql
-- Check if any serialized data contains v0 format
SELECT COUNT(*) FROM "RuleSpecs" WHERE "Content"::text LIKE '%RuleSpecV0%';
SELECT COUNT(*) FROM "Games" WHERE "Metadata"::text LIKE '%RuleSpecV0%';

-- Check for version indicators
SELECT "Id", "Version" FROM "RuleSpecs" WHERE "Version" = '0' OR "Version" = 'v0';
```

#### 1.3 Migration History
- [ ] Check git history: `git log --all --full-history -- "**/RuleSpecV0.cs"`
- [ ] Identify when RuleSpec replaced RuleSpecV0
- [ ] Find migration commits or documentation

#### 1.4 Dependency Analysis
- [ ] Check if any serializers/deserializers reference V0
- [ ] Check if API endpoints accept/return V0 format
- [ ] Look for version negotiation logic

---

## Scenarios & Actions

### Scenario A: No Usage Found ✅

**Action:** Safe to delete immediately

**Steps:**
1. [ ] Delete `apps/api/src/Api/Models/RuleSpecV0.cs`
2. [ ] Remove any related test files
3. [ ] Remove from documentation
4. [ ] Commit with message: "Remove obsolete RuleSpecV0 model"

---

### Scenario B: Used Only in Tests 🟡

**Action:** Update tests, then delete

**Steps:**
1. [ ] Update tests to use current RuleSpec model
2. [ ] Verify test coverage maintained
3. [ ] Delete RuleSpecV0
4. [ ] Run full test suite

---

### Scenario C: Used in Data Migration ⚠️

**Example:**
```csharp
// Hypothetical migration code
if (data.Version == "v0") {
    var v0Spec = JsonSerializer.Deserialize<RuleSpecV0>(data.Content);
    var currentSpec = MigrateFromV0(v0Spec);
    // ...
}
```

**Action:** Keep temporarily, add deprecation notice

**Steps:**
1. [ ] Verify migration is still needed
2. [ ] Check if legacy v0 data still exists in production
3. [ ] If yes: Keep model, add `[Obsolete]` attribute
4. [ ] If no: Can delete
5. [ ] Document retention decision

**Add to model:**
```csharp
[Obsolete("RuleSpecV0 is deprecated. Use RuleSpec instead. Kept only for legacy data migration.")]
public class RuleSpecV0
{
    // ...
}
```

---

### Scenario D: Used in Production API 🔴

**Example:**
```csharp
// Version negotiation
if (acceptHeader.Contains("v0")) {
    return JsonSerializer.Serialize<RuleSpecV0>(ruleSpec);
}
```

**Action:** Plan deprecation timeline

**Steps:**
1. [ ] Identify API clients using v0 format
2. [ ] Communicate deprecation (3-6 months notice)
3. [ ] Add deprecation headers to API responses:
   ```
   Deprecation: true
   Sunset: 2025-12-31
   Link: <https://api.meepleai.dev/docs/migration>; rel="sunset"
   ```
4. [ ] Monitor usage metrics
5. [ ] Remove when usage drops to zero

---

## Additional Models to Check

### 2. Other Version-Suffixed Models

Search for other versioned models:
```bash
grep -r "class.*V[0-9]" apps/api/src/Api/Models --include="*.cs"
```

### 3. Legacy Response/Request Models

Check for models with "Legacy" in name:
```bash
grep -r "class.*Legacy" apps/api/src --include="*.cs"
grep -r "class.*Old" apps/api/src --include="*.cs"
grep -r "class.*Deprecated" apps/api/src --include="*.cs"
```

### 4. Migration-Specific DTOs

**File:** `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/ChatThreadRepository.cs:271-286`

**Found:** `LegacyChatMessageDto`

**Status:** ACTIVE - Still used for legacy data migration (Issue #1215)

**Action:** Keep (documented as active migration code in Section 6)

---

## Testing After Removal

### Unit Tests
- [ ] Verify all tests pass
- [ ] Check no tests reference removed models
- [ ] Coverage maintained at 90%+

### Integration Tests
- [ ] Test rule spec CRUD operations
- [ ] Verify serialization/deserialization
- [ ] Test API endpoints returning rule specs

### Production Data Test
- [ ] Query production for any v0 format data
- [ ] Test migration path if needed
- [ ] Verify backward compatibility if required

---

## Documentation Updates

If RuleSpecV0 removed:
- [ ] Update API documentation
- [ ] Remove from data model diagrams
- [ ] Update CHANGELOG.md
- [ ] Check ADRs for references
- [ ] Update migration guides if applicable

---

## Success Criteria

- [ ] All obsolete models identified
- [ ] Usage verified for each model
- [ ] Models with zero usage deleted
- [ ] Models with legacy usage marked `[Obsolete]` and documented
- [ ] All tests pass
- [ ] Documentation updated
- [ ] No production errors

---

## Decision Matrix

| Model | Usage Found? | Keep/Delete | Action |
|-------|--------------|-------------|--------|
| RuleSpecV0 | TBD | TBD | Investigate first |
| LegacyChatMessageDto | Yes (migration) | Keep | Already documented |
| *Others* | TBD | TBD | Search and analyze |

---

## Related Issues

- Issue #012: Remove Backward Compatibility Layers
- Issue #010: Resolve Backend TODOs
- Legacy data migration (Section 6 of analysis)

## References

- RuleSpecV0: `apps/api/src/Api/Models/RuleSpecV0.cs`
- RuleSpec (current): `apps/api/src/Api/Models/RuleSpec.cs`
- Legacy code analysis: Section 7 (Old Model Versions)
- Migration patterns: See ChatThreadRepository legacy handling

## Estimated Impact

**If RuleSpecV0 unused:**
- Lines removed: ~100-200
- Risk: LOW
- Time: 1 hour

**If RuleSpecV0 used in migration:**
- Lines removed: 0 (keep with deprecation notice)
- Risk: LOW
- Time: 2 hours (documentation)

**If RuleSpecV0 used in API:**
- Lines removed: TBD (requires deprecation timeline)
- Risk: MEDIUM-HIGH
- Time: Several months (coordinated deprecation)

## Notes

**Priority:** This should be done AFTER more critical issues (duplicate components, profile page) but BEFORE long-term refactoring.

**Command to start investigation:**
```bash
# Quick check
grep -r "RuleSpecV0" apps/api/src apps/api/tests --include="*.cs" -c

# If zero hits, safe to delete
# If non-zero, investigate each hit
```
