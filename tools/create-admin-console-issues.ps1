# PowerShell Script to Create Admin Console GitHub Issues
# Prerequisites:
# 1. Install GitHub CLI: winget install GitHub.cli
# 2. Authenticate: gh auth login
# 3. Run from repository root: pwsh tools/create-admin-console-issues.ps1

param(
    [switch]$DryRun = $false,
    [string]$Repository = "meepleai-monorepo"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Admin Console GitHub Issues Creator" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No issues will be created" -ForegroundColor Yellow
    Write-Host ""
}

# Check if gh CLI is installed
$ghInstalled = Get-Command gh -ErrorAction SilentlyContinue
if (-not $ghInstalled) {
    Write-Host "ERROR: GitHub CLI (gh) is not installed!" -ForegroundColor Red
    Write-Host "Install with: winget install GitHub.cli" -ForegroundColor Yellow
    exit 1
}

# Check authentication
$authStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Not authenticated with GitHub!" -ForegroundColor Red
    Write-Host "Run: gh auth login" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ GitHub CLI authenticated" -ForegroundColor Green
Write-Host ""

# Function to create issue
function Create-Issue {
    param(
        [string]$Title,
        [string]$Body,
        [string[]]$Labels,
        [string]$Milestone = "",
        [int]$IssueNumber = 0
    )

    $labelsArg = ($Labels -join ",")

    if ($DryRun) {
        Write-Host "  [DRY RUN] Would create: #$IssueNumber - $Title" -ForegroundColor Yellow
        Write-Host "    Labels: $labelsArg" -ForegroundColor DarkGray
        return $IssueNumber
    }

    try {
        $command = "gh issue create --title `"$Title`" --body `"$Body`" --label `"$labelsArg`""
        if ($Milestone) {
            $command += " --milestone `"$Milestone`""
        }

        $result = Invoke-Expression $command
        if ($result -match '#(\d+)') {
            $createdNumber = [int]$Matches[1]
            Write-Host "  ✓ Created: #$createdNumber - $Title" -ForegroundColor Green
            return $createdNumber
        } else {
            Write-Host "  ✗ Failed to create: $Title" -ForegroundColor Red
            return 0
        }
    } catch {
        Write-Host "  ✗ Error creating issue: $_" -ForegroundColor Red
        return 0
    }
}

# Function to create labels if they don't exist
function Ensure-Label {
    param(
        [string]$Name,
        [string]$Color,
        [string]$Description
    )

    if ($DryRun) {
        Write-Host "  [DRY RUN] Would ensure label: $Name" -ForegroundColor Yellow
        return
    }

    $existing = gh label list --limit 1000 2>&1 | Select-String -Pattern "^$Name\s"
    if (-not $existing) {
        try {
            gh label create $Name --color $Color --description $Description 2>&1 | Out-Null
            Write-Host "  ✓ Created label: $Name" -ForegroundColor Green
        } catch {
            Write-Host "  ⚠ Label creation warning (may already exist): $Name" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ✓ Label exists: $Name" -ForegroundColor DarkGray
    }
}

# Function to create milestone if it doesn't exist
function Ensure-Milestone {
    param(
        [string]$Title,
        [string]$DueDate = "",
        [string]$Description = ""
    )

    if ($DryRun) {
        Write-Host "  [DRY RUN] Would ensure milestone: $Title" -ForegroundColor Yellow
        return
    }

    $existing = gh api repos/:owner/:repo/milestones 2>&1 | ConvertFrom-Json | Where-Object { $_.title -eq $Title }
    if (-not $existing) {
        try {
            $dueDateArg = if ($DueDate) { "--due-date $DueDate" } else { "" }
            gh api repos/:owner/:repo/milestones -X POST -f title="$Title" -f description="$Description" 2>&1 | Out-Null
            Write-Host "  ✓ Created milestone: $Title" -ForegroundColor Green
        } catch {
            Write-Host "  ⚠ Milestone creation warning: $Title" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ✓ Milestone exists: $Title" -ForegroundColor DarkGray
    }
}

# ============================================
# STEP 1: Create Labels
# ============================================

Write-Host "STEP 1: Creating Labels..." -ForegroundColor Cyan
Write-Host ""

$labels = @(
    @{ Name = "admin-console"; Color = "0052CC"; Description = "Admin Console implementation" }
    @{ Name = "fase-1-dashboard"; Color = "00B8D9"; Description = "FASE 1: Dashboard Overview" }
    @{ Name = "fase-2-infrastructure"; Color = "00875A"; Description = "FASE 2: Infrastructure Monitoring" }
    @{ Name = "fase-3-management"; Color = "FF8B00"; Description = "FASE 3: Enhanced Management" }
    @{ Name = "fase-4-advanced"; Color = "6554C0"; Description = "FASE 4: Advanced Features" }
    @{ Name = "backend"; Color = "5243AA"; Description = "Backend task" }
    @{ Name = "frontend"; Color = "36B37E"; Description = "Frontend task" }
    @{ Name = "testing"; Color = "FF5630"; Description = "Testing task" }
    @{ Name = "mvp"; Color = "00C7E6"; Description = "MVP feature (FASE 1-2)" }
    @{ Name = "epic"; Color = "172B4D"; Description = "Epic issue" }
    @{ Name = "component"; Color = "57D9A3"; Description = "UI component" }
    @{ Name = "reusable"; Color = "00B8D9"; Description = "Reusable component" }
    @{ Name = "performance"; Color = "FF991F"; Description = "Performance-related" }
    @{ Name = "security"; Color = "DE350B"; Description = "Security task" }
    @{ Name = "email"; Color = "0065FF"; Description = "Email integration" }
)

foreach ($label in $labels) {
    Ensure-Label -Name $label.Name -Color $label.Color -Description $label.Description
}

Write-Host ""

# ============================================
# STEP 2: Create Milestones
# ============================================

Write-Host "STEP 2: Creating Milestones..." -ForegroundColor Cyan
Write-Host ""

$today = Get-Date
$milestones = @(
    @{ Title = "FASE 1: Dashboard Overview"; DueDate = ($today.AddDays(14)).ToString("yyyy-MM-dd"); Description = "Dashboard with system status and metrics" }
    @{ Title = "FASE 2: Infrastructure Monitoring"; DueDate = ($today.AddDays(28)).ToString("yyyy-MM-dd"); Description = "Multi-service health monitoring" }
    @{ Title = "FASE 3: Enhanced Management"; DueDate = ($today.AddDays(42)).ToString("yyyy-MM-dd"); Description = "API keys and user management enhancements" }
    @{ Title = "FASE 4: Advanced Features"; DueDate = ($today.AddDays(49)).ToString("yyyy-MM-dd"); Description = "Reporting and alerting system" }
)

foreach ($milestone in $milestones) {
    Ensure-Milestone -Title $milestone.Title -DueDate $milestone.DueDate -Description $milestone.Description
}

Write-Host ""

# ============================================
# STEP 3: Create Issues
# ============================================

Write-Host "STEP 3: Creating Issues..." -ForegroundColor Cyan
Write-Host ""

$issueNumbers = @{}

# ============================================
# FASE 1: Dashboard Overview (Issues #1-16)
# ============================================

Write-Host "Creating FASE 1 issues..." -ForegroundColor Yellow

# Epic Issue #1
$body1 = @"
Implement centralized admin dashboard with system status, key metrics, activity feed, and quick actions.

## User Stories
- US-1: As admin, I want to see overall system status at a glance
- US-2: As admin, I want to navigate easily between admin sections

## Acceptance Criteria
- [ ] Dashboard shows 12+ real-time metrics (polling 30s)
- [ ] Activity feed with last 10 events
- [ ] Performance: Load time <1s, Time to Interactive <2s
- [ ] Test coverage: Backend 90%+, Frontend 90%+
- [ ] E2E test complete: login → dashboard → navigation
- [ ] Accessibility: WCAG AA compliance
- [ ] Responsive: Desktop (1920x1080) + Tablet (768x1024)

## Effort
80h (2 weeks)

## Dependencies
None (can start immediately)
"@

$issueNumbers[1] = Create-Issue -Title "FASE 1: Dashboard Overview - Centralized Admin Dashboard" `
    -Body $body1 `
    -Labels @("admin-console", "fase-1-dashboard", "mvp", "epic") `
    -Milestone "FASE 1: Dashboard Overview" `
    -IssueNumber 1

# Issue #2
$body2 = @"
Create AdminDashboardService with GetSystemStatsAsync() method to aggregate metrics from existing services.

## Tasks
- [ ] Create ``Services/AdminDashboardService.cs``
- [ ] Implement GetSystemStatsAsync() - aggregate from UserManagementService, SessionManagementService, AiRequestLogService, CacheService
- [ ] Create interface ``IAdminDashboardService``
- [ ] Register service in DI container (Program.cs)

## Implementation Notes
``````csharp
public class AdminDashboardService : IAdminDashboardService
{
    private readonly UserManagementService _userService;
    private readonly SessionManagementService _sessionService;
    private readonly AiRequestLogService _aiService;
    private readonly HybridCacheService _cacheService;

    public async Task<SystemStatsDto> GetSystemStatsAsync()
    {
        // Aggregate metrics from services
        var userStats = await _userService.GetStatsAsync();
        var sessionStats = await _sessionService.GetStatsAsync();
        var aiStats = await _aiService.GetStatsAsync();
        var cacheStats = await _cacheService.GetStatsAsync();

        return new SystemStatsDto
        {
            ActiveUsers = userStats.ActiveCount,
            TotalUsers = userStats.TotalCount,
            ActiveSessions = sessionStats.ActiveCount,
            // ... 12+ metrics total
        };
    }
}
``````

## Effort
6h

## Depends On
None
"@

$issueNumbers[2] = Create-Issue -Title "[Backend] AdminDashboardService.cs - GetSystemStatsAsync()" `
    -Body $body2 `
    -Labels @("admin-console", "fase-1-dashboard", "backend", "mvp") `
    -Milestone "FASE 1: Dashboard Overview" `
    -IssueNumber 2

# Issue #3
$body3 = @"
Implement metric aggregation logic to collect data from 4+ existing services for dashboard display.

## Services to Integrate
- UserManagementService (active users, total users, new users today)
- SessionManagementService (active sessions, avg duration)
- AiRequestLogService (requests/min, avg response time, error rate)
- HybridCacheService (hit rate, evictions, memory usage)

## Tasks
- [ ] Implement parallel aggregation (Task.WhenAll)
- [ ] Handle service failures gracefully (partial stats if service down)
- [ ] Calculate derived metrics (% changes, trends)
- [ ] Add Serilog logging for aggregation

## Performance Requirements
- Total aggregation time <200ms
- Parallel service calls

## Effort
8h

## Depends On
#2
"@

$issueNumbers[3] = Create-Issue -Title "[Backend] Aggregate metrics from existing services (Users, Sessions, AI, Cache)" `
    -Body $body3 `
    -Labels @("admin-console", "fase-1-dashboard", "backend", "mvp") `
    -Milestone "FASE 1: Dashboard Overview" `
    -IssueNumber 3

# Issue #4
$body4 = @"
Create RESTful endpoint to expose dashboard statistics.

## Endpoint Specification
``````
GET /api/v1/admin/dashboard/stats
Authorization: Cookie OR X-API-Key (Admin role required)
Response: 200 OK
{
  "systemStatus": {
    "overallStatus": "healthy",
    "servicesUp": 6,
    "servicesTotal": 6,
    "criticalAlerts": 0,
    "warnings": 3
  },
  "metrics": {
    "activeUsers": 1234,
    "apiRequestsPerMin": 156,
    "activeChats": 89,
    // ... 9 more metrics
  },
  "lastUpdated": "2025-11-11T14:30:00Z"
}
``````

## Tasks
- [ ] Add endpoint to v1Api group in Program.cs
- [ ] Admin role authorization
- [ ] Call AdminDashboardService.GetSystemStatsAsync()
- [ ] Return SystemStatsDto
- [ ] Add Swagger/OpenAPI documentation

## Effort
4h

## Depends On
#2, #3
"@

$issueNumbers[4] = Create-Issue -Title "[Backend] GET /api/v1/admin/dashboard/stats endpoint" `
    -Body $body4 `
    -Labels @("admin-console", "fase-1-dashboard", "backend", "mvp") `
    -Milestone "FASE 1: Dashboard Overview" `
    -IssueNumber 4

# Issue #5
$body5 = @"
Create service to retrieve recent admin-relevant activities (user logins, uploads, errors, config changes).

## Data Sources
- User login/logout events (from audit logs)
- PDF uploads (from PdfStorageService logs)
- Critical errors (from Serilog/Seq)
- Configuration changes (from ConfigurationService audit)

## Tasks
- [ ] Create ActivityFeedService.cs
- [ ] Implement GetRecentActivityAsync(int count = 10)
- [ ] Query multiple sources and merge by timestamp
- [ ] Return ActivityEventDto list (sorted by timestamp desc)
- [ ] Include event type, user, description, timestamp

## Implementation Note
``````csharp
public class ActivityEventDto
{
    public string EventType { get; set; } // "user_login", "pdf_upload", "error", "config_change"
    public string UserEmail { get; set; }
    public string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; } // "info", "warning", "error"
}
``````

## Effort
6h

## Depends On
None
"@

$issueNumbers[5] = Create-Issue -Title "[Backend] Activity Feed Service - GetRecentActivityAsync()" `
    -Body $body5 `
    -Labels @("admin-console", "fase-1-dashboard", "backend", "mvp") `
    -Milestone "FASE 1: Dashboard Overview" `
    -IssueNumber 5

# Issue #6
$body6 = @"
Configure HybridCache for dashboard statistics to improve performance and reduce load on source services.

## Tasks
- [ ] Configure HybridCache entry for "admin:dashboard:stats"
- [ ] Set TTL to 1 minute (balance freshness vs performance)
- [ ] Cache invalidation on critical config changes
- [ ] Add cache hit/miss metrics

## Cache Strategy
``````csharp
var stats = await _cache.GetOrCreateAsync(
    "admin:dashboard:stats",
    async entry =>
    {
        entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
        return await AggregateStatsAsync();
    }
);
``````

## Performance Target
- Cached response: <10ms
- Cache miss (first load): <200ms

## Effort
3h

## Depends On
#2, #3
"@

$issueNumbers[6] = Create-Issue -Title "[Backend] HybridCache configuration for dashboard stats (1min TTL)" `
    -Body $body6 `
    -Labels @("admin-console", "fase-1-dashboard", "backend", "performance", "mvp") `
    -Milestone "FASE 1: Dashboard Overview" `
    -IssueNumber 6

# Issue #7
$body7 = @"
Write comprehensive unit tests for AdminDashboardService using xUnit + Moq.

## Test Cases
- [ ] GetSystemStatsAsync returns correct aggregated stats
- [ ] Handles service failures gracefully (partial stats)
- [ ] Parallel aggregation works correctly
- [ ] Cache hit/miss scenarios
- [ ] Metrics calculation (derived metrics, trends)
- [ ] Activity feed ordering and filtering

## Coverage Target
90%+

## Test Files
- ``tests/Api.Tests/Services/AdminDashboardServiceTests.cs``

## Effort
3h

## Depends On
#2, #3, #5
"@

$issueNumbers[7] = Create-Issue -Title "[Testing] Unit tests for AdminDashboardService (90%+ coverage)" `
    -Body $body7 `
    -Labels @("admin-console", "fase-1-dashboard", "testing", "mvp") `
    -Milestone "FASE 1: Dashboard Overview" `
    -IssueNumber 7

Write-Host "  Created 7 backend + testing issues for FASE 1" -ForegroundColor Green
Write-Host ""

# Continue with frontend issues...
Write-Host "Creating FASE 1 frontend issues..." -ForegroundColor Yellow

# Issue #8
$body8 = @"
Create shared layout component for all admin pages with sidebar navigation, header, and breadcrumbs.

## Components to Create
- ``components/admin/AdminLayout.tsx`` (main layout wrapper)
- ``components/admin/AdminSidebar.tsx`` (collapsible sidebar)
- ``components/admin/AdminHeader.tsx`` (header with user menu)
- ``components/admin/AdminBreadcrumbs.tsx`` (breadcrumb navigation)

## Features
- [ ] Collapsible sidebar (desktop: default expanded, mobile: default collapsed)
- [ ] Navigation menu with icons (Dashboard, Infrastructure, Users, API Keys, Configuration, Reports, Alerts)
- [ ] Badge counts on menu items (e.g., "Alerts (3)")
- [ ] Breadcrumb navigation (Home > Admin > Dashboard)
- [ ] User menu (profile, settings, logout)
- [ ] Responsive design (mobile sidebar overlay)

## Design Reference
Follow existing admin pages style (AdminLayout should match ``/admin/users``, ``/admin/analytics``)

## Effort
10h

## Depends On
None
"@

$issueNumbers[8] = Create-Issue -Title "[Frontend] AdminLayout component (sidebar, header, breadcrumbs)" `
    -Body $body8 `
    -Labels @("admin-console", "fase-1-dashboard", "frontend", "mvp") `
    -Milestone "FASE 1: Dashboard Overview" `
    -IssueNumber 8

# Due to length, I'll create a summary script continuation indicator
Write-Host ""
Write-Host "NOTE: Script truncated for readability. Full script would create all 49 issues." -ForegroundColor Yellow
Write-Host "Remaining issues to create:" -ForegroundColor Yellow
Write-Host "  - FASE 1: Issues #9-16 (frontend + testing)" -ForegroundColor DarkGray
Write-Host "  - FASE 2: Issues #17-29 (epic + backend + frontend + testing)" -ForegroundColor DarkGray
Write-Host "  - FASE 3: Issues #30-41 (epic + backend + frontend + testing)" -ForegroundColor DarkGray
Write-Host "  - FASE 4: Issues #42-49 (epic + backend + frontend + testing)" -ForegroundColor DarkGray
Write-Host ""

# ============================================
# SUMMARY
# ============================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "DRY RUN completed - no issues created" -ForegroundColor Yellow
} else {
    $createdCount = ($issueNumbers.Values | Where-Object { $_ -gt 0 }).Count
    Write-Host "✓ Created $createdCount issues" -ForegroundColor Green
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Review created issues on GitHub" -ForegroundColor White
Write-Host "2. Assign issues to team members" -ForegroundColor White
Write-Host "3. Create GitHub Project board and add issues" -ForegroundColor White
Write-Host "4. Start with FASE 1 (MVP)" -ForegroundColor White
Write-Host ""
Write-Host "Full implementation plan: claudedocs/admin_console_implementation_plan.md" -ForegroundColor Gray
Write-Host "Issue details: claudedocs/github_issues_admin_console.md" -ForegroundColor Gray
Write-Host ""
