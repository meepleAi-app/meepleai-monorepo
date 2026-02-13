# Tier Limits Analysis - Issue #9 Context

**Analysis Date**: 2026-02-13
**Purpose**: Document existing tier limit system for Issue #9 (Tier Limits Validation in User Wizard)

---

## Tier System Architecture

### 4 Subscription Tiers

| Tier | Enum Value | Target Users |
|------|------------|--------------|
| **Free** | 0 | Basic users, trial |
| **Basic** | 1 | Casual users (NOTE: Also called "Normal" in some DTOs) |
| **Pro** | 2 | Power users (NOTE: Also called "Premium" in some DTOs) |
| **Enterprise** | 3 | Organizations, unlimited |

**⚠️ Naming Inconsistency Found**:
- `TierName` enum: Free, Basic, Pro, Enterprise
- `PdfTierUploadLimitsDto`: Free, Normal, Premium (legacy names)
- Need to standardize in Issue #9 implementation

---

## PDF Upload Limits (Issue #9 Primary Concern)

### Global Tier Limits
**Source**: `TierLimits.cs` value object

| Tier | Daily Limit | Weekly Limit | Monthly Limit | Per-Game Limit |
|------|-------------|--------------|---------------|----------------|
| **Free** | 5 | 20 | **5** | **1 PDF/game** |
| **Basic** | 20 | 100 | **20** | **3 PDF/game** |
| **Pro** | 100 | 500 | **100** | **10 PDF/game** |
| **Enterprise** | Unlimited | Unlimited | Unlimited | Unlimited |

**Role Bypass**:
- Admin/Editor roles: **Unlimited uploads** (bypass all quota checks)
- User role: Subject to tier limits

### PDF Size Limits
**Source**: Configuration (need to verify in appsettings or DB)

**Assumed** (from wizard code):
```yaml
Free: 10MB max (?)  # Not confirmed, wizard has 50MB limit
Basic: 30MB max (?)
Pro: 50MB max      # Confirmed in wizard
Enterprise: 100MB max (?)
```

**⚠️ GAP**: PDF size limits per tier NOT found in code review. Need to investigate:
- `UploadPdfCommandHandler` validation
- `appsettings.json` configuration
- Database `PdfUploadLimits` table

---

## Other Tier Limits (Context)

### Agent Creation Limits
**Source**: `TierLimits.cs`

| Tier | Max Agents | Notes |
|------|------------|-------|
| Free | 1 | Single agent only |
| Basic | 3 | Multiple games |
| Pro | 10 | Power users |
| Enterprise | Unlimited | |

**Relevance to Issue #9**: User wizard Step 3 (Config Agent) should check:
- Has user reached max agents for their tier?
- Display: "You have 0/1 agents (Free tier). [Upgrade to create more]"

### Game Library Limits
**Source**: `TierLimits.cs` - `MaxCollectionSize`

| Tier | Max Games in Library |
|------|---------------------|
| Free | 20 |
| Basic | 50 |
| Pro | 200 |
| Enterprise | Unlimited |

**Relevance**: Minimal for Issue #9 (users adding to library, not viewing)

### Token/Message Limits
**Source**: `TierLimits.cs`

| Tier | Tokens/Month | Messages/Day |
|------|--------------|--------------|
| Free | 10,000 | 10 |
| Basic | 50,000 | 50 |
| Pro | 200,000 | 200 |
| Enterprise | Unlimited | Unlimited |

**Relevance**: Affects agent chat usage, not wizard flow

---

## Quota Enforcement Architecture

### Service Layer
```csharp
IPdfUploadQuotaService (Domain Service)
├─ CheckQuotaAsync(userId, userTier, userRole)
│  └─ Returns: PdfUploadQuotaResult (Allowed, ErrorMessage, Usage, Limits, ResetAt)
├─ ReserveQuotaAsync(userId, pdfId) // Two-phase commit
├─ ConfirmQuotaAsync(userId, pdfId)
├─ ReleaseQuotaAsync(userId, pdfId)
└─ CheckPerGameQuotaAsync(userId, gameId, userTier) // Per-game limits

IGameLibraryQuotaService
└─ Check if user can add game to library (MaxCollectionSize limit)
```

### Storage
- **Redis**: Upload counts (daily/weekly), reservations (TTL)
- **PostgreSQL**: Tier configuration, user tier assignment
- **Fail-Open**: If Redis unavailable, quota check succeeds (availability > strict enforcement)

---

## Issue #9 Implementation Requirements

### Frontend Integration Points

#### 1. Fetch User Tier (Wizard Init)
```typescript
// GET /api/v1/users/me
interface UserProfile {
  id: string;
  role: string;
  tier: 'Free' | 'Basic' | 'Pro' | 'Enterprise';
  // ... other fields
}

// Store in wizard state
const [userTier, setUserTier] = useState<string>('Free');
```

#### 2. Step 2 (PDF Upload) - Size Validation
```typescript
// Client-side validation BEFORE upload
const maxSizeByTier = {
  Free: 10 * 1024 * 1024,      // 10MB (verify!)
  Basic: 30 * 1024 * 1024,     // 30MB (verify!)
  Pro: 50 * 1024 * 1024,       // 50MB (confirmed)
  Enterprise: 100 * 1024 * 1024 // 100MB (verify!)
};

if (file.size > maxSizeByTier[userTier]) {
  toast.error(`Your ${userTier} plan allows max ${maxSizeByTier[userTier]/1024/1024}MB PDFs. [Upgrade to Pro]`);
  return;
}
```

#### 3. Step 2 (PDF Upload) - Quota Check
```typescript
// Fetch quota info before showing upload UI
// GET /api/v1/pdf/quota (create new endpoint OR extend existing)
interface PdfQuotaInfo {
  dailyUsed: number;
  dailyLimit: number;
  dailyRemaining: number;
  monthlyUsed: number;
  monthlyLimit: number;
  monthlyRemaining: number;
  resetAt: string;
}

// Display in UI
<div className="text-sm text-slate-600">
  PDF this month: {quotaInfo.monthlyUsed}/{quotaInfo.monthlyLimit}
  {quotaInfo.monthlyRemaining === 0 && (
    <span className="text-red-600">Quota reached. [Upgrade]</span>
  )}
</div>
```

#### 4. Step 3 (Agent Config) - Strategy Filtering
```typescript
// Filter strategies based on tier
const allowedStrategiesByTier = {
  Free: ['Fast', 'Balanced'],  // Only simple strategies
  Basic: ['Fast', 'Balanced', 'SentenceWindow', 'StepBack'],
  Pro: '*', // All except Custom
  Enterprise: '*' // All including Custom
};

const filteredStrategies = strategies.filter(s => {
  if (userTier === 'Free' || userTier === 'Basic') {
    return allowedStrategiesByTier[userTier].includes(s.name);
  }
  if (s.name === 'Custom' && userTier !== 'Enterprise') {
    return false; // Custom only for Enterprise
  }
  return true;
});

// UI for restricted strategies
<option disabled={!isAllowed}>
  {strategy.displayName} {!isAllowed && '🔒 Pro only'}
</option>
```

---

## Backend API Gaps (For Issue #9)

### Missing Endpoints (Need to Create or Verify)

#### 1. User Tier Info
```
GET /api/v1/users/me
Response: { ..., tier: "Free", tierLimits: { ... } }

Status: ❓ Verify if tier field exists in current endpoint
```

#### 2. PDF Quota Info
```
GET /api/v1/pdf/quota
Response: {
  dailyUsed, dailyLimit, dailyRemaining,
  monthlyUsed, monthlyLimit, monthlyRemaining,
  resetAt
}

Status: ❓ Likely doesn't exist, need to create
Alternative: Call IPdfUploadQuotaService.GetQuotaInfoAsync() in new endpoint
```

#### 3. Agent Quota Info
```
GET /api/v1/agents/quota
Response: {
  agentsCreated, maxAgents, remaining
}

Status: ❓ Likely doesn't exist
Alternative: Query AgentDefinition count + compare to TierLimits.MaxAgentsCreated
```

---

## Validation Rules Summary (For Issue #9)

### Client-Side Validation (UX)
```yaml
PDF Upload:
  ✅ File size check (prevent upload if exceeds tier limit)
  ✅ Quota check (display remaining uploads)
  ✅ File type check (PDF only)

Agent Config:
  ✅ Strategy filtering (hide unavailable strategies)
  ✅ Agent count check (has user reached max agents?)
  ✅ Badge display ("Pro only" for costly strategies)
```

### Server-Side Validation (Security)
```yaml
PDF Upload:
  ✅ IPdfUploadQuotaService.CheckQuotaAsync() (already implemented)
  ✅ File size validation in UploadPdfCommandHandler
  ✅ Quota reserve/confirm/release (two-phase commit)

Agent Creation:
  ❓ Need to add TierLimits.MaxAgentsCreated validation in CreateGameAgentCommand
  ❓ Check user's current agent count vs tier limit
```

---

## Implementation Checklist (Issue #9)

### Prerequisites (Verify Before Implementation)
- [ ] Verify: GET /api/v1/users/me returns `tier` field
- [ ] Verify: PDF size limits per tier (configuration source)
- [ ] Verify: Strategy complexity tier restrictions (need to define?)
- [ ] Create: GET /api/v1/pdf/quota endpoint (if missing)
- [ ] Create: GET /api/v1/agents/quota endpoint (if missing)

### Frontend Tasks (Issue #9)
- [ ] Fetch user tier on wizard mount
- [ ] Display tier badge in wizard header
- [ ] Step 2: Client PDF size validation
- [ ] Step 2: Display quota info (used/limit/remaining)
- [ ] Step 2: Block upload if quota exceeded + upgrade CTA
- [ ] Step 3: Filter strategies by tier
- [ ] Step 3: Display "Pro only" badges
- [ ] Step 3: Check agent creation quota

### Backend Tasks (Issue #9)
- [ ] Extend GET /api/v1/users/me with tier + limits (if missing)
- [ ] Create GET /api/v1/pdf/quota endpoint
- [ ] Create GET /api/v1/agents/quota endpoint
- [ ] Add tier validation in CreateGameAgentCommand (Issue #5)
- [ ] Document tier-strategy mapping (which tiers can use which strategies)

### Testing (Issue #9)
- [ ] Unit: Quota validation logic
- [ ] Integration: Quota endpoints return correct data
- [ ] E2E: Free user blocked by PDF size limit
- [ ] E2E: Free user blocked by costly strategy
- [ ] E2E: Pro user can upload larger PDF
- [ ] E2E: Upgrade CTA displayed correctly

---

## Effort Estimate (Issue #9)

### Original Estimate: 4-5h
### Revised Estimate: 6-8h (given backend endpoint gaps)

**Breakdown**:
- Backend endpoints (quota info): 2-3h
- Frontend validation logic: 2h
- UI feedback (quota display, upgrade CTA): 2h
- Testing (unit + E2E): 2h

**Total**: 8-9h (slightly over original 5h estimate)

---

## Recommendations

### Priority 1: Define Strategy-Tier Mapping
**Question**: Which tiers can use which RAG strategies?

**Proposed**:
```yaml
Free (complexity 0-1):
  - Fast (0)
  - Balanced (1)

Basic (complexity 0-5):
  - Free strategies +
  - SentenceWindow (5)
  - StepBack (9)
  - QueryExpansion (10)

Pro (complexity 0-11, except Custom):
  - Basic strategies +
  - Precise (2)
  - Expert (3)
  - Consensus (4)
  - Iterative (6)
  - MultiAgent (8)
  - RagFusion (11)

Enterprise (all including Custom):
  - All strategies (0-11)
```

**Rationale**: Cost-based (higher complexity = more expensive)

### Priority 2: Expose Tier Limits to Frontend
Create GET /api/v1/users/me/limits endpoint:
```json
{
  "tier": "Free",
  "limits": {
    "pdfUploads": { "daily": 5, "monthly": 5, "used": 2, "remaining": 3 },
    "pdfSize": { "maxBytes": 10485760 },
    "agents": { "max": 1, "created": 0, "remaining": 1 },
    "strategies": ["Fast", "Balanced"]
  }
}
```

### Priority 3: Graceful Degradation
- If quota service fails → Allow operation (fail-open pattern already implemented ✅)
- If tier info missing → Default to Free tier (safest assumption)

---

## Next Steps for Issue #9

1. **Verify APIs** (30 min)
   - Check if GET /api/v1/users/me returns tier
   - Check if quota endpoints exist

2. **Define Strategy Mapping** (15 min)
   - Document which tiers can use which strategies
   - Get stakeholder approval

3. **Implement** (6-8h)
   - Backend: Quota endpoints (if missing)
   - Frontend: Validation + UI feedback
   - Tests: Unit + E2E

4. **Integrate** with Issue #4 (User Wizard)
   - Not a separate PR, same feature branch

---

## References

- TierName enum: `Administration/Domain/Enums/TierName.cs`
- TierLimits value object: `Administration/Domain/ValueObjects/TierLimits.cs`
- PDF quota service: `DocumentProcessing/Domain/Services/IPdfUploadQuotaService.cs`
- Game quota service: `UserLibrary/Domain/Services/IGameLibraryQuotaService.cs`
