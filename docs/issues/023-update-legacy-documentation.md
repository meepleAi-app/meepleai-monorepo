# Issue #023: Update Legacy Documentation References

**Priority:** 🟢 LOW
**Category:** Documentation
**Estimated Effort:** 1 day
**Sprint:** LONG-TERM (Post-Beta)

## Summary

Documentation may contain references to deprecated features, old endpoints, removed services, or outdated architecture patterns. After completing other legacy code cleanup issues, documentation should be updated to reflect current state.

## Documentation to Review

### 1. API Documentation

**Files:**
- `docs/03-api/board-game-ai-api-specification.md`
- `docs/03-api/ai-provider-configuration.md`

**Check for:**
- [ ] References to removed endpoints
- [ ] Old authentication patterns
- [ ] Deprecated request/response formats
- [ ] Legacy service mentions
- [ ] Outdated API versions

**Actions:**
- [ ] Remove references to `/profile` endpoint
- [ ] Update authentication examples (OAuth/2FA)
- [ ] Verify all endpoint examples are current
- [ ] Update response schemas to use DTOs

---

### 2. Architecture Documentation

**Files:**
- `docs/01-architecture/overview/system-architecture.md`
- `docs/01-architecture/patterns/*.md`
- All ADRs in `docs/01-architecture/adr/`

**Check for:**
- [ ] References to old service-based architecture
- [ ] Pre-DDD patterns
- [ ] Removed bounded contexts
- [ ] Outdated component diagrams

**Actions:**
- [ ] Update architecture diagrams (if DDD changes not reflected)
- [ ] Add note about completed DDD migration (99% → 100%)
- [ ] Archive superseded ADRs (move to `docs/archived/adr/`)
- [ ] Update component interaction diagrams

---

### 3. Development Guides

**Files:**
- `docs/02-development/**/*.md`
- Testing guides
- Contributing guidelines
- Setup instructions

**Check for:**
- [ ] References to removed services
- [ ] Old development workflows
- [ ] Deprecated tools or dependencies
- [ ] Outdated code examples

**Actions:**
- [ ] Update testing guide with current CQRS patterns
- [ ] Remove references to GameService, AuthService, etc.
- [ ] Update code examples to use MediatR
- [ ] Verify setup instructions are current

---

### 4. Frontend Documentation

**Files:**
- `docs/04-frontend/**/*.md`
- `docs/04-frontend/shadcn-ui-installation.md`

**Check for:**
- [ ] References to removed components
- [ ] Old state management patterns (pre-Zustand)
- [ ] Deprecated pages (`/profile`)
- [ ] Outdated component structure

**Actions:**
- [ ] Update component organization docs
- [ ] Remove references to profile page
- [ ] Document Zustand migration (if needed)
- [ ] Update component examples

---

### 5. Security Documentation

**Files:**
- `SECURITY.md`
- `docs/06-security/**/*.md`
- `docs/06-security/oauth-security.md`
- `docs/06-security/environment-variables-production.md`

**Check for:**
- [ ] References to old authentication methods
- [ ] Deprecated security headers
- [ ] Old OAuth flow descriptions
- [ ] Outdated environment variables

**Actions:**
- [ ] Verify OAuth documentation matches current implementation
- [ ] Update security headers list (remove X-XSS-Protection if removed)
- [ ] Document 2FA implementation if not already
- [ ] Update API key format documentation

---

### 6. CLAUDE.md (Project Instructions)

**File:** `CLAUDE.md`

**Check for:**
- [ ] "99% complete" → Should be 100% when all issues resolved
- [ ] References to pending migrations
- [ ] Outdated service listings
- [ ] Old architecture patterns

**Actions:**
- [ ] Update DDD completion percentage
- [ ] Update "Eliminated" services list if any removed
- [ ] Verify all commands/scripts still work
- [ ] Update "Last Verified" date

---

### 7. README Files

**Files:**
- `README.md` (root)
- `apps/api/README.md`
- `apps/web/README.md`

**Check for:**
- [ ] Outdated feature lists
- [ ] References to removed pages/endpoints
- [ ] Old architecture descriptions
- [ ] Deprecated setup instructions

**Actions:**
- [ ] Update feature lists
- [ ] Verify quick start guides work
- [ ] Update architecture overview
- [ ] Check all links are valid

---

## Tasks

### Phase 1: Documentation Audit (0.5 days)

#### 1.1 Automated Search
```bash
# Search for references to removed services
grep -r "GameService\|AuthService\|UserManagementService" docs/ --include="*.md"

# Search for deprecated endpoints
grep -r "/profile" docs/ --include="*.md"

# Search for old patterns
grep -r "service layer\|Service-based" docs/ --include="*.md" -i

# Find broken links
# Use tool like markdown-link-check or manually verify
```

#### 1.2 Manual Review
- [ ] Read through each major documentation file
- [ ] Check examples compile/work
- [ ] Verify diagrams match current architecture
- [ ] Note inconsistencies

#### 1.3 Create Checklist
- [ ] List all outdated references found
- [ ] Prioritize by visibility/importance
- [ ] Assign to categories (API, Architecture, Security, etc.)

---

### Phase 2: Content Updates (0.5 days)

#### 2.1 Remove References to Deprecated Features
- [ ] Delete mentions of `/profile` page
- [ ] Remove old service examples
- [ ] Update authentication flow descriptions
- [ ] Fix API endpoint examples

#### 2.2 Update Code Examples
```markdown
<!-- Before -->
```csharp
var result = await _gameService.GetGamesAsync();
```

<!-- After -->
```csharp
var result = await _mediator.Send(new GetAllGamesQuery());
```
```

#### 2.3 Update Diagrams
- [ ] Architecture diagrams (if not already updated)
- [ ] Component interaction diagrams
- [ ] Authentication flow diagrams
- [ ] Data flow diagrams

**Tools:**
- Mermaid (embedded in markdown)
- Draw.io / Lucidchart
- PlantUML

#### 2.4 Archive Superseded Content
Create `docs/archived/` directory for:
- [ ] Old ADRs (if superseded by new ones)
- [ ] Pre-DDD architecture docs
- [ ] Deprecated guides

**Add README in archived:**
```markdown
# Archived Documentation

This directory contains historical documentation that is no longer current
but preserved for reference.

## Why archived?
- Superseded by newer documentation
- Describes removed features
- Outdated architecture patterns

## Current documentation
See `docs/INDEX.md` for up-to-date content.
```

---

### Phase 3: Verification (0.25 days)

#### 3.1 Link Checking
```bash
# Install markdown-link-check
npm install -g markdown-link-check

# Check all markdown files
find docs -name "*.md" -exec markdown-link-check {} \;
```

#### 3.2 Code Example Testing
- [ ] Extract code examples from docs
- [ ] Test they compile/run
- [ ] Fix any broken examples

#### 3.3 Peer Review
- [ ] Have another developer review docs
- [ ] Check for clarity and accuracy
- [ ] Verify examples make sense

---

### Phase 4: INDEX.md Update (0.25 days)

**File:** `docs/INDEX.md`

**Actions:**
- [ ] Update document count (currently 115)
- [ ] Remove archived documents from main index
- [ ] Add new documents if any created
- [ ] Verify all links work
- [ ] Update categories if structure changed

---

## Specific Updates Needed

### Based on Legacy Code Cleanup

After completing previous issues, update docs for:

1. **Issue #001-003 (Duplicate Components):**
   - [ ] Update frontend component organization docs
   - [ ] Document new import paths

2. **Issue #002 (Profile Page Removal):**
   - [ ] Remove all references to `/profile`
   - [ ] Update navigation docs to show `/settings`

3. **Issue #010-011 (TODOs/APIs):**
   - [ ] Update API specification with new endpoints
   - [ ] Document new commands/queries

4. **Issue #012 (Backward Compat):**
   - [ ] Update API response format documentation
   - [ ] Document DTO structure

5. **Issue #013 (Obsolete Models):**
   - [ ] Remove RuleSpecV0 from data model docs
   - [ ] Update entity relationship diagrams

6. **Issue #020 (Test Infrastructure):**
   - [ ] Update testing guide with fixes
   - [ ] Document test utilities

7. **Issue #021 (Legacy Comments):**
   - [ ] Remove deprecated API patterns from docs
   - [ ] Update security headers documentation

8. **Issue #022 (Infrastructure Services):**
   - [ ] Add new infrastructure services catalog
   - [ ] Update architecture to reflect service categorization

---

## Success Criteria

- [ ] Zero references to removed features
- [ ] All code examples work
- [ ] All links valid (no 404s)
- [ ] Diagrams match current architecture
- [ ] CLAUDE.md updated to 100% DDD complete
- [ ] INDEX.md accurate
- [ ] Archived content properly organized

---

## Related Issues

- All previous issues (#001-#022)
- DDD migration completion
- Documentation debt reduction

## References

- Documentation index: `docs/INDEX.md`
- CLAUDE.md: `/CLAUDE.md`
- Legacy code analysis: All sections

## Tools

**Markdown Linting:**
```bash
npm install -g markdownlint-cli
markdownlint docs/**/*.md
```

**Link Checking:**
```bash
npm install -g markdown-link-check
markdown-link-check docs/**/*.md
```

**Diagram Tools:**
- Mermaid: Built into GitHub/GitLab markdown
- Draw.io: https://app.diagrams.net/
- PlantUML: https://plantuml.com/

---

## Estimated Impact

**Documentation Health:**
- Current: References to removed features, outdated examples
- After: Accurate, up-to-date, verified documentation

**Developer Experience:**
- Reduced confusion from outdated docs
- Faster onboarding
- Correct examples to copy/paste

**Maintenance:**
- Easier to keep docs current
- Clear archive for historical reference
- Better organization

**Risk Level:** Very Low
- Only documentation changes
- No code impact
- Can be done incrementally

---

## Notes

**This should be the LAST issue completed:**
- Wait until all code cleanup issues resolved
- Ensures documentation matches final state
- Avoids documenting temporary states

**Ongoing Maintenance:**
- Add docs review to PR checklist
- Update docs when features change
- Annual docs audit
- Version docs with releases
