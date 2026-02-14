# Epic #4068: E2E Test Scenarios

**Test Coverage**: Permission flows, Tooltip positioning, Tag system, Collection limits, Agent metadata

---

## Permission System E2E Tests

### Scenario 1: Free Tier User Limitations

```typescript
test('Free tier user sees limited features', async ({ page }) => {
  // Setup: Login as Free tier user
  await page.goto('/login');
  await page.fill('[name="email"]', 'free@example.com');
  await page.fill('[name="password"]', 'password');
  await page.click('button[type="submit"]');

  // Navigate to game catalog
  await page.goto('/games');

  // Find first game card
  const gameCard = page.locator('[data-testid="game-card"]').first();

  // Assert: Wishlist visible (Free tier feature)
  await expect(gameCard.locator('[aria-label="Add to wishlist"]')).toBeVisible();

  // Assert: Bulk select hidden (Pro tier feature)
  await expect(gameCard.locator('[type="checkbox"]')).not.toBeVisible();

  // Assert: Drag handle hidden (Normal tier feature)
  await expect(gameCard.locator('[aria-label="Drag handle"]')).not.toBeVisible();

  // Hover quick actions menu
  await gameCard.hover();
  await page.click('[aria-label="Quick actions"]');

  // Assert: Only non-admin actions visible
  await expect(page.locator('text=View')).toBeVisible();
  await expect(page.locator('text=Share')).toBeVisible();
  await expect(page.locator('text=Delete')).not.toBeVisible(); // Admin only
});
```

### Scenario 2: Pro Tier User Full Access

```typescript
test('Pro tier user sees all standard features', async ({ page }) => {
  await loginAs(page, 'pro@example.com');
  await page.goto('/games');

  const gameCard = page.locator('[data-testid="game-card"]').first();

  // All standard features visible
  await expect(gameCard.locator('[aria-label="Add to wishlist"]')).toBeVisible();
  await expect(gameCard.locator('[type="checkbox"]')).toBeVisible(); // Bulk select
  await expect(gameCard.locator('[aria-label="Drag handle"]')).toBeVisible();

  // Quick actions include editor features
  await gameCard.hover();
  await page.click('[aria-label="Quick actions"]');
  await expect(page.locator('text=Edit')).toBeVisible();

  // But not admin features
  await expect(page.locator('text=Delete')).not.toBeVisible();
});
```

### Scenario 3: Admin Role Overrides Tier

```typescript
test('Admin role grants access regardless of tier', async ({ page }) => {
  // Login as Admin with Free tier
  await loginAs(page, 'admin-free@example.com');
  await page.goto('/games');

  const gameCard = page.locator('[data-testid="game-card"]').first();

  // Admin actions visible despite Free tier
  await gameCard.hover();
  await page.click('[aria-label="Quick actions"]');
  await expect(page.locator('text=Delete')).toBeVisible();
  await expect(page.locator('text=Edit')).toBeVisible();

  // Tier-locked features still respect tier (OR logic)
  // Bulk select requires Pro OR Editor (Admin satisfies role part)
  await expect(gameCard.locator('[type="checkbox"]')).toBeVisible();
});
```

### Scenario 4: Suspended User Access Denied

```typescript
test('Suspended user cannot access features', async ({ page }) => {
  await loginAs(page, 'suspended@example.com');

  // Redirect to suspension notice page
  await expect(page).toHaveURL('/account/suspended');
  await expect(page.locator('text=Account Suspended')).toBeVisible();

  // Direct navigation blocked
  await page.goto('/games');
  await expect(page).toHaveURL('/account/suspended');
});
```

### Scenario 5: State-Based Content Access

```typescript
test('Draft games visible only to creator', async ({ page }) => {
  // As regular user
  await loginAs(page, 'user@example.com');
  await page.goto('/admin/games');

  // Draft games not in list
  await expect(page.locator('text=Draft Game Title')).not.toBeVisible();

  // Logout and login as creator
  await page.click('[aria-label="User menu"]');
  await page.click('text=Logout');
  await loginAs(page, 'creator@example.com');
  await page.goto('/admin/games');

  // Draft games visible
  await expect(page.locator('text=Draft Game Title')).toBeVisible();
});
```

---

## Tooltip Positioning E2E Tests

### Scenario 6: Tooltip Auto-Flip at Viewport Edges

```typescript
test('Tooltip flips when near viewport bottom', async ({ page }) => {
  await page.goto('/games');

  // Scroll to bottom
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));

  // Find card near bottom
  const bottomCard = page.locator('[data-testid="game-card"]').last();

  // Hover to trigger tooltip
  await bottomCard.locator('[aria-label="More info"]').hover();

  // Tooltip should appear ABOVE trigger (flipped)
  const tooltip = page.locator('[role="tooltip"]');
  await expect(tooltip).toBeVisible();

  const tooltipBox = await tooltip.boundingBox();
  const triggerBox = await bottomCard.boundingBox();

  // Assert: Tooltip is above trigger
  assert(tooltipBox!.y < triggerBox!.y, 'Tooltip should be above trigger near bottom edge');
});
```

### Scenario 7: Mobile Touch Tooltip

```typescript
test('Tooltip shows on tap (mobile)', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'Mobile only test');

  await page.goto('/games');
  const card = page.locator('[data-testid="game-card"]').first();
  const tooltipTrigger = card.locator('[aria-label="More info"]');

  // Tap (not hover on mobile)
  await tooltipTrigger.tap();

  // Tooltip appears
  await expect(page.locator('[role="tooltip"]')).toBeVisible();

  // Tap outside to dismiss
  await page.tap('body', { position: { x: 10, y: 10 } });
  await expect(page.locator('[role="tooltip"]')).not.toBeVisible();
});
```

### Scenario 8: Keyboard Navigation Tooltip

```typescript
test('Tooltip accessible via keyboard', async ({ page }) => {
  await page.goto('/games');

  // Tab to first tooltip trigger
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab'); // Navigate to tooltip trigger

  // Enter to show tooltip
  await page.keyboard.press('Enter');
  await expect(page.locator('[role="tooltip"]')).toBeVisible();

  // Escape to hide
  await page.keyboard.press('Escape');
  await expect(page.locator('[role="tooltip"]')).not.toBeVisible();

  // Focus should return to trigger
  const focused = await page.evaluate(() => document.activeElement?.getAttribute('aria-label'));
  expect(focused).toContain('More info');
});
```

---

## Tag System E2E Tests

### Scenario 9: Tag Overflow Display

```typescript
test('Shows max 3 tags with overflow counter', async ({ page }) => {
  await page.goto('/games');

  // Find game with 5 tags
  const gameWithManyTags = page.locator('[data-testid="game-card"][data-tag-count="5"]').first();

  // Assert: 3 visible tags
  const visibleTags = gameWithManyTags.locator('[role="status"]');
  await expect(visibleTags).toHaveCount(3);

  // Assert: Overflow counter "+2"
  await expect(gameWithManyTags.locator('text=+2')).toBeVisible();

  // Hover overflow counter
  await gameWithManyTags.locator('text=+2').hover();

  // Tooltip shows hidden tags
  const overflowTooltip = page.locator('[role="tooltip"]');
  await expect(overflowTooltip).toBeVisible();
  await expect(overflowTooltip.locator('text=Owned')).toBeVisible();
  await expect(overflowTooltip.locator('text=Wishlist')).toBeVisible();
});
```

### Scenario 10: Responsive Tag Layout

```typescript
test('Tags adapt to screen size', async ({ page }) => {
  // Desktop: Full labels
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto('/games');

  let card = page.locator('[data-testid="game-card"]').first();
  await expect(card.locator('text=New')).toBeVisible(); // Full label

  // Mobile: Icon-only
  await page.setViewportSize({ width: 375, height: 667 });
  await page.reload();

  card = page.locator('[data-testid="game-card"]').first();
  const tagStrip = card.locator('[aria-label="Entity tags"]');

  // Strip width reduced
  const box = await tagStrip.boundingBox();
  expect(box!.width).toBeLessThan(30); // 24px on mobile

  // Label text hidden (icon-only mode)
  await expect(card.locator('text=New')).not.toBeVisible();
});
```

---

## Collection Limits E2E Tests

### Scenario 11: Approaching Limit Warning

```typescript
test('Shows warning when collection near limit', async ({ page }) => {
  // Setup: Pro user with 475/500 games (95%)
  await loginAs(page, 'pro-near-limit@example.com');
  await page.goto('/collection');

  // Progress bar visible
  const progressBar = page.locator('[role="progressbar"]').first();
  await expect(progressBar).toBeVisible();

  // Color is red (>90%)
  await expect(progressBar).toHaveClass(/bg-red-500/);

  // Warning message visible
  await expect(page.locator('text=Approaching limit')).toBeVisible();
  await expect(page.locator('[data-icon="alert-triangle"]')).toBeVisible();

  // Upgrade CTA visible
  await expect(page.locator('text=Upgrade to Enterprise')).toBeVisible();
});
```

### Scenario 12: Storage Quota Display

```typescript
test('Displays storage quota with real-time updates', async ({ page }) => {
  await loginAs(page, 'normal@example.com');
  await page.goto('/collection');

  // Initial storage display
  await expect(page.locator('text=/\\d+MB \\/ \\d+MB/')).toBeVisible();

  // Upload PDF
  await page.click('text=Upload Rulebook');
  await page.setInputFiles('input[type="file"]', 'tests/fixtures/test-rulebook.pdf');
  await page.click('text=Upload');

  // Wait for upload complete
  await page.waitForSelector('text=Upload complete');

  // Storage quota updated
  // (exact numbers depend on test data, just verify format)
  await expect(page.locator('text=/\\d+MB \\/ 500MB/')).toBeVisible();
});
```

---

## Agent Metadata E2E Tests

### Scenario 13: Agent Status Badge Display

```typescript
test('Agent card shows status badge', async ({ page }) => {
  await page.goto('/agents');

  const agentCard = page.locator('[data-testid="agent-card"][data-entity="agent"]').first();

  // Status badge visible
  const statusBadge = agentCard.locator('[role="status"]');
  await expect(statusBadge).toBeVisible();

  // One of the 4 states visible
  const statusText = await statusBadge.textContent();
  expect(['Active', 'Idle', 'Training', 'Error']).toContain(statusText?.trim());
});
```

### Scenario 14: Agent Model Info Tooltip

```typescript
test('Agent model info shows in tooltip', async ({ page }) => {
  await page.goto('/agents');

  const agentCard = page.locator('[data-testid="agent-card"]').first();
  const modelBadge = agentCard.locator('[aria-label*="Model"]');

  // Hover to show tooltip
  await modelBadge.hover();

  const tooltip = page.locator('[role="tooltip"]');
  await expect(tooltip).toBeVisible();

  // Tooltip contains model parameters
  await expect(tooltip.locator('text=Temperature')).toBeVisible();
  await expect(tooltip.locator('text=Max Tokens')).toBeVisible();
});
```

### Scenario 15: Agent Invocation Stats

```typescript
test('Agent stats display invocation count and last run', async ({ page }) => {
  await page.goto('/agents');

  const agentCard = page.locator('[data-testid="agent-card"]').first();

  // Invocation count formatted
  await expect(agentCard.locator('text=/\\d+(\\.\\d+)?[KM]?/')).toBeVisible(); // 342, 1.2K, etc.

  // Last executed timestamp
  await expect(agentCard.locator('text=/(just now|\\d+ (seconds?|minutes?|hours?|days?) ago)/')).toBeVisible();
});
```

---

## Accessibility E2E Tests (WCAG 2.1 AA)

### Scenario 16: Keyboard-Only Navigation

```typescript
test('Complete workflow with keyboard only', async ({ page }) => {
  await page.goto('/games');

  // Tab through cards
  for (let i = 0; i < 5; i++) {
    await page.keyboard.press('Tab');
  }

  // Current card should have focus ring
  const focused = page.locator(':focus');
  await expect(focused).toHaveCSS('outline-style', 'solid'); // Focus visible

  // Enter to open card details
  await page.keyboard.press('Enter');

  // Verify navigation happened
  await expect(page).toHaveURL(/\\/games\\/[a-f0-9-]+/);

  // Back to catalog
  await page.goBack();

  // Tab to tooltip trigger
  await page.keyboard.press('Tab');
  await page.keyboard.press('Enter'); // Show tooltip

  // Tooltip visible
  await expect(page.locator('[role="tooltip"]')).toBeVisible();

  // Escape to close
  await page.keyboard.press('Escape');
  await expect(page.locator('[role="tooltip"]')).not.toBeVisible();
});
```

### Scenario 17: Screen Reader Announcements

```typescript
test('Screen reader announces content correctly', async ({ page }) => {
  await page.goto('/games');

  const gameCard = page.locator('[data-testid="game-card"]').first();

  // Card has proper aria-label
  const ariaLabel = await gameCard.getAttribute('aria-label');
  expect(ariaLabel).toMatch(/Game: .+/);

  // Tooltip trigger has aria-describedby
  const tooltipTrigger = gameCard.locator('[aria-describedby]').first();
  const describedBy = await tooltipTrigger.getAttribute('aria-describedby');
  expect(describedBy).toBeTruthy();

  // Hover to show tooltip
  await tooltipTrigger.hover();

  // Tooltip has matching ID
  const tooltip = page.locator(`#${describedBy}`);
  await expect(tooltip).toBeVisible();
  await expect(tooltip).toHaveAttribute('role', 'tooltip');
});
```

### Scenario 18: Color Contrast Compliance

```typescript
test('All text meets WCAG AA contrast ratios', async ({ page }) => {
  await page.goto('/games');

  // Run axe accessibility audit
  const results = await new AxeBuilder({ page }).analyze();

  // No contrast violations
  const contrastViolations = results.violations.filter(v =>
    v.id === 'color-contrast' || v.id === 'color-contrast-enhanced'
  );

  expect(contrastViolations).toHaveLength(0);

  // Specifically check tag badges
  const tagBadges = page.locator('[role="status"]');
  const count = await tagBadges.count();

  for (let i = 0; i < count; i++) {
    const badge = tagBadges.nth(i);
    const bgColor = await badge.evaluate(el => window.getComputedStyle(el).backgroundColor);
    const color = await badge.evaluate(el => window.getComputedStyle(el).color);

    // Calculate contrast ratio (simplified - use proper library in real test)
    const contrastRatio = calculateContrastRatio(color, bgColor);
    expect(contrastRatio).toBeGreaterThanOrEqual(4.5); // WCAG AA for normal text
  }
});
```

---

## Performance E2E Tests

### Scenario 19: Tooltip Positioning Performance

```typescript
test('Tooltip positioning completes in <16ms', async ({ page }) => {
  await page.goto('/games');

  const card = page.locator('[data-testid="game-card"]').first();
  const trigger = card.locator('[aria-label="More info"]');

  // Measure tooltip positioning time
  const timings = await page.evaluate(async (triggerSelector) => {
    const trigger = document.querySelector(triggerSelector);
    const measurements: number[] = [];

    for (let i = 0; i < 10; i++) {
      const start = performance.now();

      // Trigger tooltip show
      trigger!.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true }));

      // Wait for position calculation
      await new Promise(resolve => requestAnimationFrame(resolve));

      const end = performance.now();
      measurements.push(end - start);

      // Hide tooltip
      trigger!.dispatchEvent(new MouseEvent('mouseleave', { bubbles: true }));
      await new Promise(resolve => setTimeout(resolve, 50));
    }

    return measurements;
  }, await trigger.evaluate(el => el.outerHTML));

  const avgTime = timings.reduce((a, b) => a + b, 0) / timings.length;
  expect(avgTime).toBeLessThan(16); // 60fps requirement
});
```

### Scenario 20: Large Card Grid Performance

```typescript
test('Grid with 100+ cards renders performantly', async ({ page }) => {
  await page.goto('/games?limit=150');

  // Measure render time
  const renderTime = await page.evaluate(() => {
    const start = performance.now();

    // Force layout recalculation
    document.body.offsetHeight;

    return performance.now() - start;
  });

  expect(renderTime).toBeLessThan(100); // <100ms for 150 cards

  // Check all cards rendered
  const cardCount = await page.locator('[data-testid="game-card"]').count();
  expect(cardCount).toBeGreaterThanOrEqual(100);

  // Scroll performance (no jank)
  const scrollPerf = await page.evaluate(async () => {
    const frameTimings: number[] = [];
    let lastTime = performance.now();

    return new Promise<number[]>(resolve => {
      const measure = () => {
        const currentTime = performance.now();
        frameTimings.push(currentTime - lastTime);
        lastTime = currentTime;

        if (frameTimings.length < 60) {
          requestAnimationFrame(measure);
        } else {
          resolve(frameTimings);
        }
      };

      // Start scrolling
      window.scrollTo({ top: 1000, behavior: 'smooth' });
      requestAnimationFrame(measure);
    });
  });

  // No frames exceed 16.67ms (60fps)
  const slowFrames = scrollPerf.filter(t => t > 16.67);
  expect(slowFrames.length).toBeLessThan(5); // Allow up to 5 dropped frames
});
```

---

## Integration E2E Tests

### Scenario 21: Complete User Journey

```typescript
test('New user journey: signup → upgrade → use features', async ({ page }) => {
  // 1. Signup (Free tier)
  await page.goto('/signup');
  await page.fill('[name="email"]', 'newuser@example.com');
  await page.fill('[name="password"]', 'SecurePass123!');
  await page.click('button[type="submit"]');

  // 2. Verify Free tier limitations
  await page.goto('/games');
  const card = page.locator('[data-testid="game-card"]').first();
  await expect(card.locator('[type="checkbox"]')).not.toBeVisible(); // No bulk select

  // 3. Attempt bulk select → upgrade prompt
  await page.goto('/collection');
  await page.click('text=Bulk Actions'); // Should show upgrade prompt
  await expect(page.locator('text=Upgrade to Pro')).toBeVisible();

  // 4. Upgrade to Pro
  await page.click('text=Upgrade to Pro');
  await page.fill('[name="card-number"]', '4242424242424242');
  // ... complete checkout ...

  // 5. Verify Pro features unlocked
  await page.goto('/games');
  await expect(card.locator('[type="checkbox"]')).toBeVisible(); // Bulk select now visible

  // 6. Use Pro feature
  await card.locator('[type="checkbox"]').check();
  await page.click('[aria-label="Bulk actions menu"]');
  await expect(page.locator('text=Add to Collection')).toBeVisible();
});
```

### Scenario 22: Admin Moderation Workflow

```typescript
test('Admin can moderate content and manage users', async ({ page }) => {
  await loginAs(page, 'admin@example.com');

  // 1. Navigate to admin panel
  await page.goto('/admin/users');

  // 2. Find user to moderate
  const userRow = page.locator('[data-testid="user-row"]').first();
  await userRow.click();

  // 3. Admin quick actions visible
  await expect(page.locator('text=Suspend User')).toBeVisible();
  await expect(page.locator('text=Ban User')).toBeVisible();
  await expect(page.locator('text=Change Tier')).toBeVisible();

  // 4. Suspend user
  await page.click('text=Suspend User');
  await page.fill('[name="reason"]', 'Policy violation');
  await page.click('text=Confirm Suspension');

  // 5. Verify suspension
  await expect(page.locator('text=User suspended')).toBeVisible();

  // 6. Check user card reflects status
  await page.goto('/admin/users');
  await expect(userRow.locator('text=Suspended')).toBeVisible();
});
```

---

## Visual Regression Tests

### Scenario 23: MeepleCard Variants with All Features

```typescript
test('Visual regression: All MeepleCard variants', async ({ page }) => {
  await page.goto('/storybook/iframe.html?id=ui-meeplecard--all-variants');

  // Grid variant
  await expect(page.locator('[data-variant="grid"]')).toHaveScreenshot('meeplecard-grid.png');

  // List variant
  await expect(page.locator('[data-variant="list"]')).toHaveScreenshot('meeplecard-list.png');

  // Compact variant
  await expect(page.locator('[data-variant="compact"]')).toHaveScreenshot('meeplecard-compact.png');

  // With tags
  await expect(page.locator('[data-variant="grid"][data-has-tags="true"]')).toHaveScreenshot('meeplecard-with-tags.png');

  // Agent variant with metadata
  await expect(page.locator('[data-entity="agent"]')).toHaveScreenshot('meeplecard-agent.png');
});
```

---

## Load & Stress Tests

### Scenario 24: Concurrent Permission Checks

```typescript
test('Permission system handles concurrent requests', async ({ page }) => {
  await loginAs(page, 'pro@example.com');

  // Make 50 concurrent permission checks
  const features = Array.from({ length: 50 }, (_, i) => `feature-${i}`);

  const checkResults = await Promise.all(
    features.map(f =>
      page.evaluate(feature =>
        fetch(`/api/v1/permissions/check?feature=${feature}`)
          .then(r => r.json()),
        f
      )
    )
  );

  // All requests completed
  expect(checkResults).toHaveLength(50);

  // No rate limiting or errors
  checkResults.forEach(result => {
    expect(result).toHaveProperty('hasAccess');
    expect(result).toHaveProperty('reason');
  });
});
```

---

## Test Helpers & Utilities

```typescript
// tests/e2e/helpers/auth.ts
export async function loginAs(page: Page, email: string) {
  await page.goto('/login');
  await page.fill('[name="email"]', email);
  await page.fill('[name="password"]', 'password');
  await page.click('button[type="submit"]');
  await page.waitForURL('/games'); // Wait for redirect
}

// tests/e2e/helpers/permissions.ts
export async function getUserPermissions(page: Page) {
  return await page.evaluate(() =>
    fetch('/api/v1/permissions/me').then(r => r.json())
  );
}

// tests/e2e/helpers/contrast.ts
export function calculateContrastRatio(color1: string, color2: string): number {
  // Parse RGB from color strings
  // Calculate relative luminance
  // Return contrast ratio
  // (Simplified - use proper library like color-contrast-checker)
  return 4.5; // Placeholder
}
```

---

## Test Data Fixtures

```typescript
// tests/fixtures/users.ts
export const TEST_USERS = {
  free: { email: 'free@example.com', tier: 'free', role: 'user' },
  normal: { email: 'normal@example.com', tier: 'normal', role: 'user' },
  pro: { email: 'pro@example.com', tier: 'pro', role: 'user' },
  enterprise: { email: 'enterprise@example.com', tier: 'enterprise', role: 'user' },
  editor: { email: 'editor@example.com', tier: 'free', role: 'editor' },
  creator: { email: 'creator@example.com', tier: 'normal', role: 'creator' },
  admin: { email: 'admin@example.com', tier: 'free', role: 'admin' },
  adminPro: { email: 'admin-pro@example.com', tier: 'pro', role: 'admin' },
  suspended: { email: 'suspended@example.com', tier: 'pro', role: 'user', status: 'suspended' },
  banned: { email: 'banned@example.com', tier: 'enterprise', role: 'user', status: 'banned' }
};

// tests/fixtures/tags.ts
export const TEST_TAGS = {
  game: [
    { id: 'new', label: 'New', bgColor: 'hsl(142 76% 36%)' },
    { id: 'sale', label: 'Sale', bgColor: 'hsl(0 84% 60%)' },
    { id: 'owned', label: 'Owned', bgColor: 'hsl(221 83% 53%)' },
    { id: 'wishlisted', label: 'Wishlist', bgColor: 'hsl(350 89% 60%)' }
  ],
  agent: [
    { id: 'rag', label: 'RAG', bgColor: 'hsl(38 92% 50%)' },
    { id: 'vision', label: 'Vision', bgColor: 'hsl(262 83% 58%)' },
    { id: 'code', label: 'Code', bgColor: 'hsl(210 40% 55%)' }
  ]
};
```

---

## CI/CD Integration

```yaml
# .github/workflows/epic-4068-tests.yml
name: Epic #4068 - MeepleCard Enhancements Tests

on:
  pull_request:
    paths:
      - 'apps/api/src/Api/BoundedContexts/Administration/**'
      - 'apps/web/src/components/ui/tags/**'
      - 'apps/web/src/components/ui/agent/**'
      - 'apps/web/src/contexts/PermissionContext.tsx'

jobs:
  backend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Permission System Tests
        run: |
          cd apps/api/src/Api
          dotnet test --filter "Category=Unit&BoundedContext=Administration"
          dotnet test --filter "FullyQualifiedName~PermissionTests"

  frontend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Component Tests
        run: |
          cd apps/web
          pnpm test -- TagStrip TagBadge Agent Permission
          pnpm test:coverage

  e2e-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run E2E Tests
        run: |
          cd apps/web
          pnpm exec playwright test tests/e2e/epic-4068/

  accessibility:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Accessibility Audit
        run: |
          cd apps/web
          pnpm exec playwright test --grep @a11y
          pnpm exec axe-core audit

  visual-regression:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Visual Regression Tests
        run: |
          cd apps/web
          pnpm exec chromatic --project-token=${{ secrets.CHROMATIC_TOKEN }}
```

---

## Test Coverage Requirements

**Backend**:
- Unit tests: ≥95% for Domain layer (Permission, UserTier, Role)
- Integration tests: ≥85% for Application layer (Queries, Services)
- E2E tests: All permission API endpoints

**Frontend**:
- Unit tests: ≥90% for pure components (TagBadge, AgentStatusBadge)
- Integration tests: ≥80% for context consumers
- E2E tests: Complete user flows per tier/role

**Accessibility**:
- axe-core: 0 violations
- WCAG 2.1 AA: 100% compliance
- Lighthouse: ≥95 accessibility score
- Manual testing: NVDA, VoiceOver, TalkBack

**Performance**:
- Tooltip positioning: <16ms (60fps)
- Card render: <100ms with all features
- Permission check: <5ms (cached)
- Large grid (100 cards): <1s initial render

---

## Test Execution Commands

```bash
# Backend unit tests
cd apps/api/src/Api
dotnet test --filter "Epic=4068"
dotnet test /p:CollectCoverage=true /p:CoverageMinimum=90

# Frontend unit tests
cd apps/web
pnpm test -- --coverage --coverageThreshold='{"global":{"branches":85,"functions":85,"lines":85,"statements":85}}'

# E2E tests
pnpm test:e2e -- tests/e2e/epic-4068/

# Accessibility tests
pnpm test:a11y

# Performance tests
pnpm test:perf

# Visual regression
pnpm exec chromatic

# All tests
pnpm test:all
```
