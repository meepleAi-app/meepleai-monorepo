# Issue #011: Implement Missing Frontend Backend APIs

**Priority:** 🟡 MEDIUM
**Category:** Backend + Frontend / Feature Completion
**Estimated Effort:** 5-7 days
**Sprint:** SHORT-TERM (1-2 months)
**Related:** SPRINT-1, SPRINT-2

## Summary

Multiple frontend components have placeholder TODOs indicating missing backend API implementations. These need to be completed for full feature functionality.

## Missing Backend APIs

### 1. Settings: UpdatePreferencesCommand
**Frontend File:** `apps/web/src/app/settings/page.tsx:272`
**Status:** Settings page UI complete, backend pending

```typescript
// TODO: Implement UpdatePreferencesCommand when backend is ready
const handlePreferencesSubmit = async (preferences: UserPreferences) => {
  // Mock implementation - needs backend
}
```

**Backend Requirements:**
- Create `UpdatePreferencesCommand` in Authentication or SystemConfiguration context
- Fields needed:
  - Language preference (enum: IT, EN, etc.)
  - Theme (enum: Light, Dark, System)
  - Email notifications (boolean)
  - Data retention period (integer, days)
- Add validation
- Persist to User entity
- Return updated preferences

**API Endpoint:**
```
PATCH /api/v1/users/{userId}/preferences
Body: { language, theme, emailNotifications, dataRetentionDays }
Response: UserPreferencesDto
```

---

### 2. Games: GetRuleSpecsQuery
**Frontend File:** `apps/web/src/app/games/[id]/page.tsx`
**Status:** Placeholder for game rules display

```typescript
// Placeholder for GetRuleSpecsQuery (backend TODO)
const ruleSpecs = []; // Should fetch from API
```

**Backend Requirements:**
- Query to fetch all RuleSpecs for a Game
- Filter by GameId
- Return formatted rule specifications
- Include version history if applicable

**API Endpoint:**
```
GET /api/v1/games/{gameId}/rule-specs
Response: RuleSpecDto[]
```

**Note:** Verify if this overlaps with existing endpoints in `RuleSpecEndpoints.cs`

---

### 3. Games: GetGameSessionsQuery
**Frontend File:** `apps/web/src/hooks/useGames.ts`
**Status:** Hook implemented, API missing

```typescript
// TODO: FE-IMP-005 - api.games.getSessions not implemented yet
const { data: sessions } = useQuery({
  queryKey: ['game-sessions', gameId],
  queryFn: () => api.games.getSessions(gameId), // ❌ Not implemented
})
```

**Backend Requirements:**
- Create `GetGameSessionsQuery` in GameManagement context
- Return all play sessions for a specific game
- Include session metadata (players, date, duration, outcomes)
- Support pagination

**API Endpoint:**
```
GET /api/v1/games/{gameId}/sessions?page=1&pageSize=20
Response: PagedResult<GameSessionDto>
```

---

### 4. Search: PDF Language Filter
**Frontend File:** `apps/web/src/components/search/SearchFilters.tsx`
**Status:** UI placeholder, backend support needed

```typescript
// TODO: PDF Language Filter (requires backend support)
<Select disabled>
  <SelectTrigger>Language Filter</SelectTrigger>
</Select>
```

**Backend Requirements:**
- Extract language from PDF metadata or content
- Add Language field to PdfDocument entity
- Support filtering in SearchQuery
- Auto-detect language during PDF processing

**API Enhancement:**
```
GET /api/v1/search?query=...&language=it,en
Response: SearchResultDto with language metadata
```

---

### 5. Chat Context in Search
**Frontend File:** `apps/web/src/hooks/useSearch.ts`
**Multiple TODOs:**

```typescript
// TODO: Add chat context to search query
// TODO: Filter by agent when agent filtering implemented
// TODO: Add proper chat context handling
```

**Backend Requirements:**
- Extend SearchQuery to accept chat context
- Add agent filtering capability
- Support contextual search based on previous messages
- Integrate with ChatThread for conversation context

**API Enhancement:**
```
GET /api/v1/search?query=...&chatThreadId=...&agentId=...
Response: Contextual search results
```

---

## Tasks

### Phase 1: Analysis & Design (1 day)
- [ ] Review each missing API with product/design
- [ ] Define data models and DTOs
- [ ] Create API specifications
- [ ] Design database schema changes if needed
- [ ] Prioritize by user impact

### Phase 2: Backend Implementation (3-4 days)

#### 2.1 User Preferences (Priority: HIGH)
- [ ] Create `UserPreferences` value object
- [ ] Add preferences to User entity
- [ ] Create `UpdatePreferencesCommand` and handler
- [ ] Create `GetPreferencesQuery` and handler
- [ ] Add migration for preferences fields
- [ ] Write unit + integration tests
- [ ] Document in API spec

#### 2.2 Game Sessions (Priority: MEDIUM)
- [ ] Create `GetGameSessionsQuery`
- [ ] Create `GameSessionDto`
- [ ] Implement query handler with pagination
- [ ] Add endpoint in GameManagementEndpoints
- [ ] Write tests
- [ ] Document

#### 2.3 Rule Specs by Game (Priority: LOW)
- [ ] Verify if endpoint already exists
- [ ] If not, create `GetRuleSpecsByGameIdQuery`
- [ ] Implement handler
- [ ] Add endpoint
- [ ] Write tests

#### 2.4 Search Enhancements (Priority: MEDIUM)
- [ ] Add language detection to PDF processing
- [ ] Extend SearchQuery for context/agent filtering
- [ ] Update Qdrant/Postgres search logic
- [ ] Add filtering to search endpoint
- [ ] Write tests

### Phase 3: Frontend Integration (2 days)
- [ ] Update `lib/api/index.ts` with new endpoints
- [ ] Implement settings preferences save
- [ ] Implement games session loading
- [ ] Enable search filters
- [ ] Update hooks to use real APIs
- [ ] Remove TODO comments

### Phase 4: Testing (1 day)
- [ ] Test each API endpoint with Postman/curl
- [ ] Integration tests with Testcontainers
- [ ] Frontend E2E tests
- [ ] Verify error handling
- [ ] Test edge cases

### Phase 5: Documentation
- [ ] Update API specification
- [ ] Add OpenAPI/Swagger docs
- [ ] Update CLAUDE.md if needed
- [ ] Create migration guide for preferences

## Database Changes

### User Preferences Schema
```sql
ALTER TABLE "Users" ADD COLUMN "Language" VARCHAR(10) DEFAULT 'it';
ALTER TABLE "Users" ADD COLUMN "Theme" VARCHAR(20) DEFAULT 'system';
ALTER TABLE "Users" ADD COLUMN "EmailNotifications" BOOLEAN DEFAULT true;
ALTER TABLE "Users" ADD COLUMN "DataRetentionDays" INTEGER DEFAULT 90;
```

### PDF Language Metadata
```sql
ALTER TABLE "PdfDocuments" ADD COLUMN "DetectedLanguage" VARCHAR(10);
ALTER TABLE "PdfDocuments" ADD COLUMN "LanguageConfidence" DECIMAL(3,2);
```

## API Client Updates

**Before:**
```typescript
// Mock/placeholder
const preferences = { language: 'it', theme: 'dark' }
```

**After:**
```typescript
const preferences = await api.users.updatePreferences(userId, {
  language: 'it',
  theme: 'dark',
  emailNotifications: true,
  dataRetentionDays: 90
})
```

## Success Criteria

- [ ] All frontend TODO comments resolved
- [ ] Settings preferences fully functional
- [ ] Game sessions display real data
- [ ] Search filters operational
- [ ] All new endpoints documented
- [ ] 90%+ test coverage maintained
- [ ] No regressions in existing features

## Related Issues

- Issue #010: Resolve Backend TODOs
- SPRINT-1 completion
- FE-IMP-005: Game Sessions API

## References

- Frontend TODOs: Legacy code analysis Section 1.B
- API spec: `docs/03-api/board-game-ai-api-specification.md`
- Settings page: `apps/web/src/app/settings/page.tsx`
- Hooks: `apps/web/src/hooks/`
- CQRS pattern: Follow existing command/query structure

## Notes

**Priority Order:**
1. User Preferences (HIGH) - Settings page is user-facing
2. Search Enhancements (MEDIUM) - Improves core functionality
3. Game Sessions (MEDIUM) - Nice to have
4. Rule Specs (LOW) - May already exist

**Estimated API Endpoints:** 4-5 new endpoints
**Estimated Lines of Code:** ~800-1000 (backend + frontend)
