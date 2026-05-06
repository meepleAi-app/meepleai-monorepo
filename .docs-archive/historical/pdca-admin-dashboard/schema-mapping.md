# Backend DTO → Frontend Type Mapping

**Date**: 2026-02-12
**Purpose**: Map backend DTOs to frontend TypeScript types for API integration

---

## 📊 Admin Stats Endpoint

### **Backend DTO**: `DashboardStatsDto`

```csharp
DashboardStatsDto(
    DashboardMetrics Metrics,
    List<TimeSeriesDataPoint> UserTrend,
    List<TimeSeriesDataPoint> SessionTrend,
    List<TimeSeriesDataPoint> ApiRequestTrend,
    List<TimeSeriesDataPoint> PdfUploadTrend,
    List<TimeSeriesDataPoint> ChatMessageTrend,
    DateTime GeneratedAt
)

DashboardMetrics(
    int TotalUsers,
    int ActiveSessions,
    int ApiRequestsToday,
    int TotalPdfDocuments,
    int TotalChatMessages,
    double AverageConfidenceScore,
    int TotalRagRequests,
    long TotalTokensUsed,
    int TotalGames,
    int ApiRequests7d,
    int ApiRequests30d,
    double AverageLatency24h,
    double AverageLatency7d,
    double ErrorRate24h,
    int ActiveAlerts,
    int ResolvedAlerts,
    decimal TokenBalanceEur,
    decimal TokenLimitEur,
    decimal DbStorageGb,
    decimal DbStorageLimitGb,
    decimal DbGrowthMbPerDay,
    double CacheHitRatePercent,
    double CacheHitRateTrendPercent
)
```

### **Frontend Type**: `AdminStats`

```typescript
interface AdminStats {
  totalGames: number;
  publishedGames: number;    // ❌ NOT in backend
  pendingGames: number;      // ❌ NOT in backend
  totalUsers: number;
  activeUsers: number;       // ❌ NOT in backend
  newUsers: number;          // ❌ NOT in backend
  approvalRate: number;      // ❌ NOT in backend
  pendingApprovals: number;  // ❌ NOT in backend
  recentSubmissions: number; // ❌ NOT in backend
}
```

### **Mapping Strategy**

**Option A: Use Available Backend Fields**
```typescript
interface AdminStats {
  // From DashboardMetrics
  totalGames: number;        // ← Metrics.TotalGames
  totalUsers: number;        // ← Metrics.TotalUsers
  activeSessions: number;    // ← Metrics.ActiveSessions
  apiRequestsToday: number;  // ← Metrics.ApiRequestsToday

  // Calculated from trends
  activeUsers: number;       // ← UserTrend (last data point)
  apiRequests7d: number;     // ← Metrics.ApiRequests7d
}
```

**Option B: Create New Endpoint** (Better)
```csharp
// Create specific endpoint for dashboard needs
GET /api/v1/admin/dashboard/overview-stats

AdminDashboardOverviewDto(
    int TotalGames,
    int PublishedGames,     // FROM SharedGameCatalog
    int PendingGames,       // FROM SharedGameCatalog
    int TotalUsers,
    int ActiveUsers,        // Users active last 30 days
    int NewUsers,           // Users created last 30 days
    decimal ApprovalRate,   // Approved / Total submissions
    int PendingApprovals,   // Count from approval queue
    int RecentSubmissions   // Submissions last 7 days
)
```

**Recommendation**: Option B - Create dedicated overview stats endpoint

---

## 🎮 Approval Queue Endpoint

### **Backend DTO**: `ApprovalQueueItemDto`

```csharp
ApprovalQueueItemDto(
    Guid GameId,
    string Title,
    Guid SubmittedBy,
    DateTime SubmittedAt,
    int DaysPending,
    int PdfCount
)
```

### **Frontend Type**: `ApprovalQueueItem`

```typescript
interface ApprovalQueueItem {
  gameId: string;          // ✅ COMPATIBLE
  title: string;           // ✅ COMPATIBLE
  submittedBy: string;     // ⚠️ TYPE MISMATCH: Backend = Guid, Frontend = string (email)
  submittedAt: string;     // ✅ COMPATIBLE
  daysPending: number;     // ✅ COMPATIBLE
  pdfCount: number;        // ✅ COMPATIBLE
}
```

### **Issues**:

**1. SubmittedBy Type Mismatch**
- **Backend**: Returns `Guid` (user ID)
- **Frontend**: Expects `string` (email or display name)

**Fix Options**:
- **Option A**: Backend returns user email instead of Guid
- **Option B**: Frontend resolves Guid → email client-side
- **Option C**: Backend DTO includes both userId and userEmail

**Recommendation**: Option C - Extend DTO
```csharp
ApprovalQueueItemDto(
    Guid GameId,
    string Title,
    Guid SubmittedByUserId,      // User ID
    string SubmittedByEmail,     // User email/name
    DateTime SubmittedAt,
    int DaysPending,
    int PdfCount
)
```

---

## 👥 User Management Endpoints

### **Backend DTO**: `UserDto`

```csharp
UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Tier,
    DateTime CreatedAt,
    bool IsTwoFactorEnabled,
    DateTime? TwoFactorEnabledAt,
    int Level,
    int ExperiencePoints,
    bool EmailVerified,
    DateTime? EmailVerifiedAt,
    DateTime? VerificationGracePeriodEndsAt
)
```

### **Frontend Type**: `User`

```typescript
interface User {
  id: string;                    // ✅ COMPATIBLE
  displayName: string;           // ✅ COMPATIBLE
  email: string;                 // ✅ COMPATIBLE
  role: 'user' | 'admin';        // ✅ COMPATIBLE
  tier: 'free' | 'normal' | 'premium'; // ✅ COMPATIBLE
  level: number;                 // ✅ COMPATIBLE
  experiencePoints: number;      // ✅ COMPATIBLE
  createdAt: string;             // ✅ COMPATIBLE
  isActive: boolean;             // ❌ NOT in backend (need suspension status)
  isTwoFactorEnabled: boolean;   // ✅ COMPATIBLE
  emailVerified: boolean;        // ✅ COMPATIBLE
}
```

### **Issues**:

**Missing Field**: `isActive`
- **Frontend**: Needs to show Active/Suspended status
- **Backend**: UserDto doesn't include suspension/active status

**Fix**: Backend should include suspension status in UserDto or provide separate field

---

## 📚 User Library Stats

### **Need to verify DTO schema**

Check: `GetUserLibraryStatsQueryHandler` return type

---

## 🏆 User Badges

### **Need to verify DTO schema**

Check: `GetUserBadgesQueryHandler` return type

---

## 🎯 Schema Compatibility Summary

| Endpoint | Backend DTO | Frontend Type | Compatibility |
|----------|-------------|---------------|---------------|
| Admin Stats | DashboardStatsDto | AdminStats | ⚠️ **Schema mismatch** |
| Approval Queue | ApprovalQueueItemDto | ApprovalQueueItem | ⚠️ **submittedBy type mismatch** |
| Users | UserDto | User | ⚠️ **Missing isActive field** |
| Library Stats | TBD | UserLibraryStats | 🔍 **Needs verification** |
| Badges | TBD | UserBadge | 🔍 **Needs verification** |

---

## 📋 Action Items

**Immediate**:
1. Create new `/admin/dashboard/overview-stats` endpoint with simple metrics
2. Extend ApprovalQueueItemDto to include user email
3. Add suspension status to UserDto
4. Verify library stats and badges DTOs

**Alternative**:
- Update frontend types to match complex backend DashboardStatsDto
- Extract needed fields from Metrics object

**Recommendation**: Backend adjustments (simpler than frontend refactor)
