# Epic #3327: Handoff & Maintenance Guide

> Quick reference for maintaining and extending Epic #3327 deliverables

**Created**: 2026-02-06
**Epic Status**: 96% Complete (1 PR auto-merging)

---

## 🎯 What Was Delivered

### Security Middleware Stack

**Location**: `apps/api/src/Api/Middleware/`

```
Request Flow:
  → RateLimitingMiddleware (existing)
  → SessionQuotaMiddleware (NEW - #3671)
  → AccountLockoutMiddleware (NEW - #3676)
  → EmailVerificationMiddleware (NEW - #3672)
  → Endpoint
```

**Pattern**: All use fail-open pattern (log warning, allow request on infrastructure failure)

**Maintenance**:
- Middleware order matters (defined in Program.cs)
- Test each middleware independently before pipeline changes
- Monitor logs for fail-open events (indicates infrastructure issues)

---

### Admin Configuration Endpoints

**New Admin Capabilities**:

| Feature | Endpoint | Files | Issue |
|---------|----------|-------|-------|
| **PDF Tier Limits** | `PUT /admin/config/pdf-limits/{tier}` | `AdminConfigEndpoints.cs`, `UpdatePdfLimitsCommand.cs` | #3673 |
| **Feature Flags** | `POST /admin/feature-flags/{key}/tier/{tier}/enable` | `FeatureFlagEndpoints.cs` | #3073, #3674 |
| **Session Limits** | `PUT /admin/config/session-limits` | `SessionLimitsConfigEndpoints.cs` | #3070 |

**Pattern**: All use CQRS with IMediator.Send()

**Extending**:
1. Create Command + Validator in SystemConfiguration
2. Create Handler (use existing as template)
3. Add endpoint in appropriate AdminEndpoints file
4. Register in Program.cs

---

### User Self-Service Endpoints

**New User Capabilities**:

| Feature | Endpoint | Response | Issue |
|---------|----------|----------|-------|
| **Device Management** | `GET /users/me/devices` | List of active devices | #3677 |
| **Available Features** | `GET /users/me/features` | Features with access status | #3674 |
| **AI Usage** | `GET /users/me/ai-usage?days=30` | Token usage + cost | #3074, #3338 |

**Pattern**: All require session, return user-scoped data

---

## 🔧 Maintenance Tasks

### Quota Limits Configuration

**How to Update PDF Limits**:
```bash
# Via Scalar UI
PUT http://localhost:8080/api/v1/admin/config/pdf-limits/premium
{
  "maxPerDay": 200,
  "maxPerWeek": 1000,
  "maxPerGame": 20
}

# Or via database (SystemConfigurations table)
UPDATE system_configurations
SET value = '200'
WHERE key = 'UploadLimits:premium:DailyLimit';
```

**Configuration Keys**:
```
UploadLimits:{tier}:DailyLimit
UploadLimits:{tier}:WeeklyLimit
UploadLimits:{tier}:PerGameLimit
```

### Feature Flag Management

**How to Enable Feature for Tier**:
```bash
# Enable Advanced RAG for Normal tier
POST http://localhost:8080/api/v1/admin/feature-flags/Features.AdvancedRAG/tier/normal/enable

# Disable for Free tier
POST http://localhost:8080/api/v1/admin/feature-flags/Features.AdvancedRAG/tier/free/disable
```

**Check User Access**:
```bash
# As user
GET http://localhost:8080/api/v1/users/me/features
→ Returns list with hasAccess boolean for each feature
```

### Email Verification Grace Period

**Current**: 7 days for existing users
**Location**: User.VerificationGracePeriodEndsAt field

**How to Adjust**:
1. Modify migration: `20260206071644_AddEmailVerificationGracePeriod.cs`
2. Change: `INTERVAL '7 days'` to desired period
3. Re-run migration: `dotnet ef database update`

---

## 🐛 Troubleshooting

### Middleware Issues

**Problem**: Users blocked unexpectedly

**Debug Steps**:
```bash
# Check middleware logs
docker logs meepleai-api --tail=100 | grep "Middleware\|fail-open"

# Verify middleware order
grep -A 5 "app.UseMiddleware" apps/api/src/Api/Program.cs

# Test middleware individually
# Temporarily comment out other middlewares in Program.cs
```

**Common Causes**:
- Redis connection failure (session quota)
- Database timeout (email verification check)
- Rate limit misconfiguration

**Solution**: Middleware fails open (allows request), check logs for root cause

### Feature Flag Not Working

**Problem**: User can't access feature despite correct tier

**Debug Steps**:
```bash
# 1. Check feature flag configuration
GET /admin/feature-flags/{featureName}

# 2. Check tier-specific flag
GET /admin/feature-flags/{featureName}?tier=premium

# 3. Check user's features
GET /users/me/features (as that user)

# 4. Check logs
grep "Feature.*denied" logs/api.log
```

**Common Causes**:
- Feature not configured (defaults to false for role flags)
- Tier-specific flag overriding global
- Cache not invalidated after config change

---

## 📈 Monitoring

### Key Metrics to Watch

**Session Quota Enforcement**:
```sql
-- Sessions terminated by quota enforcement
SELECT COUNT(*) FROM game_sessions
WHERE status = 'Terminated'
AND updated_at > NOW() - INTERVAL '1 day';
```

**Email Verification**:
```sql
-- Users pending verification
SELECT COUNT(*) FROM users
WHERE email_verified = false
AND verification_grace_period_ends_at > NOW();

-- Users past grace period
SELECT COUNT(*) FROM users
WHERE email_verified = false
AND verification_grace_period_ends_at < NOW();
```

**Account Lockouts**:
```sql
-- Currently locked accounts
SELECT COUNT(*) FROM users
WHERE account_locked_until > NOW();
```

**Device Tracking**:
```sql
-- Users at device limit
SELECT user_id, COUNT(*) as device_count
FROM sessions
WHERE device_fingerprint IS NOT NULL
GROUP BY user_id
HAVING COUNT(*) >= 5;
```

---

## 🔄 Extension Points

### Adding New Tier-Based Features

**Steps**:
1. **Configure Feature Flag**:
   ```bash
   POST /admin/feature-flags
   {
     "key": "Features.NewFeature",
     "enabled": true
   }

   POST /admin/feature-flags/Features.NewFeature/tier/premium/enable
   ```

2. **Use in Code**:
   ```csharp
   var canAccess = await _featureFlagService.CanAccessFeatureAsync(user, "Features.NewFeature");
   if (!canAccess)
       throw new ForbiddenException("Requires Premium tier");
   ```

3. **Document**:
   - Add to `docs/features/feature-flags-tier-matrix.md`
   - Update feature list in UserFeatureDto handler

### Adding New Quota Types

**Pattern** (based on SessionQuotaMiddleware):
1. Create Service (e.g., `IXyzQuotaService`)
2. Implement with Redis tracking (daily/weekly keys)
3. Create middleware using quota service
4. Register in Program.cs middleware pipeline
5. Add admin config endpoints
6. Create unit tests

**Files to Create**:
```
BoundedContext/Infrastructure/Services/XyzQuotaService.cs
BoundedContext/Domain/Services/IXyzQuotaService.cs
Middleware/XyzQuotaMiddleware.cs
Routing/XyzQuotaConfigEndpoints.cs
```

---

## 📞 Support Contacts

### Code Ownership

| Component | Primary Contact | Files |
|-----------|----------------|-------|
| **Middleware** | Backend Team | `Middleware/Session*, Email*, Account*` |
| **Feature Flags** | Platform Team | `SystemConfiguration/` |
| **Admin UI** | Frontend Team | `apps/web/src/app/(authenticated)/admin/` |
| **Quotas** | Backend Team | `*/Services/*QuotaService.cs` |

### Common Questions

**Q: How do I add a new tier?**
A: Modify `UserTier` value object, add to database enum, update all tier-based queries

**Q: Can I change grace period after deployment?**
A: Yes, via migration or manual database update (affects only new users)

**Q: How do I temporarily disable enforcement?**
A: Set feature flag to false globally (don't modify middleware - use feature flags)

**Q: Where are quota limits stored?**
A: Redis (current usage) + SystemConfigurations table (limits)

---

## 🔗 Related Documentation

- **Architecture**: `docs/01-architecture/middleware-stack.md`
- **PDCA Process**: `docs/pdca/epic-3327*/`
- **Patterns**: `docs/patterns/`
- **Testing**: `docs/05-testing/backend/`
- **Feature Flags**: `docs/features/feature-flags-tier-matrix.md`

---

## ✅ Handoff Checklist

- [x] All PRs created (6 total)
- [x] 5 PRs merged successfully
- [x] 1 PR awaiting auto-merge (#3738)
- [x] All issues addressed (7/7)
- [x] Documentation complete (PDCA + guides)
- [x] Patterns extracted (6 patterns)
- [x] Tests created (37+ tests)
- [x] Zero regressions verified
- [ ] PR #3738 merged (auto-merge in progress)
- [ ] Epic #3327 closed (after PR merge)

---

**Epic #3327**: Ready for automatic closure upon PR #3738 merge ✅

**Maintainer**: Refer to this guide for extending or troubleshooting Epic #3327 deliverables

---

*Handoff Document Created: 2026-02-06*
*PM Agent: Epic Coordination Complete*
