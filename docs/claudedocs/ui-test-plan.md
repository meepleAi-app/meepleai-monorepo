# MeepleAI - Piano Test UI Completo

> **Data**: 2026-02-11 | **Copertura**: 91 issues across 6 epics + extras
> **Framework**: Vitest + @testing-library/react + jest-axe + userEvent
> **Target**: 85%+ copertura frontend, WCAG 2.1 AA compliance

---

## Convenzioni Test

```
File: ComponentName.test.tsx        → Unit tests
File: ComponentName.a11y.test.tsx   → Accessibility tests
File: hookName.test.ts              → Hook tests
Location: __tests__/ parallel to source
```

**Pattern Standard**:
```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { describe, it, expect, vi, beforeEach } from 'vitest';
```

---

## Epic #1: MeepleCard Enhancements (#4068)

### 1.1 Permission System Integration (#4074)

**File**: `components/ui/data-display/meeple-card/__tests__/MeepleCardPermissions.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Renders card when user has `view` permission | Unit | P0 |
| 2 | Hides action buttons without `edit` permission | Unit | P0 |
| 3 | Shows delete button only with `delete` permission | Unit | P0 |
| 4 | Disables interactions for `guest` role | Unit | P0 |
| 5 | Admin role sees all actions | Unit | P1 |
| 6 | Permission changes update UI reactively | Unit | P1 |
| 7 | Renders skeleton when permissions loading | Unit | P1 |
| 8 | Handles permission fetch error gracefully | Unit | P2 |
| 9 | No axe violations with restricted permissions | A11y | P1 |
| 10 | aria-disabled on permission-blocked buttons | A11y | P1 |

```typescript
// Test skeleton
describe('MeepleCard Permissions', () => {
  describe('Role-based visibility', () => {
    it('hides action buttons without edit permission', () => {
      render(<MeepleCard entity="game" permissions={{ view: true, edit: false }} />);
      expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
    });
  });
  describe('Accessibility', () => {
    it('has no axe violations with restricted permissions', async () => {
      const { container } = render(<MeepleCard entity="game" permissions={{ view: true }} />);
      expect(await axe(container)).toHaveNoViolations();
    });
  });
});
```

### 1.2 WCAG 2.1 AA Accessibility (#4073)

**File**: `components/ui/data-display/meeple-card/__tests__/MeepleCard.a11y.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | No axe violations - grid variant | A11y | P0 |
| 2 | No axe violations - list variant | A11y | P0 |
| 3 | No axe violations - compact variant | A11y | P0 |
| 4 | No axe violations - featured variant | A11y | P0 |
| 5 | No axe violations - hero variant | A11y | P0 |
| 6 | All images have alt text | A11y | P0 |
| 7 | Color contrast ratio >= 4.5:1 for text | A11y | P0 |
| 8 | Focus indicators visible on keyboard nav | A11y | P1 |
| 9 | Card navigable via Tab key | A11y | P1 |
| 10 | Screen reader announces entity type | A11y | P1 |
| 11 | Rating stars have aria-label | A11y | P1 |
| 12 | Interactive elements have min 44x44px touch target | A11y | P2 |

### 1.3 Smart Tooltip Positioning (#4072)

**File**: `components/ui/data-display/meeple-card/__tests__/MeepleCardTooltip.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Tooltip appears on hover | Unit | P0 |
| 2 | Tooltip disappears on mouse leave | Unit | P0 |
| 3 | Tooltip appears on keyboard focus | Unit | P1 |
| 4 | Tooltip positions top when space available | Unit | P1 |
| 5 | Tooltip repositions when near viewport edge | Unit | P1 |
| 6 | Tooltip has role="tooltip" | A11y | P1 |
| 7 | aria-describedby links trigger to tooltip | A11y | P1 |
| 8 | Tooltip dismissable with Escape key | A11y | P1 |

### 1.4 Tag System Vertical Layout (#4075)

**File**: `components/ui/data-display/meeple-card/__tests__/MeepleCardTags.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Tags render in vertical stack layout | Unit | P0 |
| 2 | Tags truncate with "+N more" when > maxTags | Unit | P0 |
| 3 | Expanded state shows all tags | Unit | P1 |
| 4 | Mobile: tags collapse to 2 visible | Unit | P1 |
| 5 | Tag click triggers onTagClick callback | Unit | P1 |
| 6 | Tags have appropriate contrast | A11y | P1 |

### 1.5 Ownership State Logic (#4078)

**File**: `components/ui/data-display/meeple-card/__tests__/MeepleCardOwnership.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows "In Library" badge when owned | Unit | P0 |
| 2 | Shows "Add to Library" button when not owned | Unit | P0 |
| 3 | Shows "Wishlist" indicator when wishlisted | Unit | P1 |
| 4 | Ownership toggle calls onToggleOwnership | Unit | P1 |
| 5 | Loading state during ownership change | Unit | P1 |
| 6 | Error state on ownership toggle failure | Unit | P2 |

### 1.6 Collection Limits Management (#4077)

**File**: `components/ui/data-display/meeple-card/__tests__/MeepleCardCollectionLimits.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows quota usage (e.g., "45/50 games") | Unit | P0 |
| 2 | Warning state when > 80% quota | Unit | P0 |
| 3 | Blocked state at 100% quota | Unit | P0 |
| 4 | "Upgrade" CTA when limit reached | Unit | P1 |
| 5 | Progress bar shows correct percentage | Unit | P1 |
| 6 | Progress bar has aria-label | A11y | P1 |

### 1.7 Agent Type Support (#4079)

**File**: `components/ui/data-display/meeple-card/__tests__/MeepleCardAgentType.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Renders agent entity type with brain icon | Unit | P0 |
| 2 | Agent card shows strategy badge | Unit | P1 |
| 3 | Agent card shows status (active/paused) | Unit | P1 |
| 4 | Click navigates to /agents/{id} | Unit | P1 |
| 5 | No axe violations for agent variant | A11y | P1 |

### 1.8 Mobile Tag Optimization (#4076) & Performance (#4081)

**File**: `components/ui/data-display/meeple-card/__tests__/MeepleCardPerformance.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Mobile viewport: tags collapse correctly | Unit | P1 |
| 2 | Image lazy loading attribute present | Unit | P1 |
| 3 | Card renders within 16ms (no layout thrashing) | Perf | P2 |
| 4 | 100 cards render without memory leaks | Perf | P2 |
| 5 | Skeleton loads immediately on slow data | Unit | P2 |

### 1.9 Context-Aware Tests (#4080)

**File**: `components/ui/data-display/meeple-card/__tests__/MeepleCardContextAware.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Card in library context shows remove action | Unit | P1 |
| 2 | Card in catalog context shows add action | Unit | P1 |
| 3 | Card in search results shows relevance score | Unit | P1 |
| 4 | Card in agent context shows chat button | Unit | P1 |
| 5 | Card in dashboard shows quick stats | Unit | P2 |

**Totale Epic #1**: ~55 test cases

---

## Epic #2: Agent System (#4069)

### 2.1 Chat UI Base Component (#4085)

**File**: `components/agent/chat/__tests__/ChatUI.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Renders empty chat with welcome message | Unit | P0 |
| 2 | Displays user message bubble (right-aligned) | Unit | P0 |
| 3 | Displays agent message bubble (left-aligned) | Unit | P0 |
| 4 | Input field accepts text and submits on Enter | Unit | P0 |
| 5 | Submit button disabled when input empty | Unit | P0 |
| 6 | Shows typing indicator during agent response | Unit | P1 |
| 7 | Auto-scrolls to latest message | Unit | P1 |
| 8 | Markdown rendering in agent messages | Unit | P1 |
| 9 | Code block syntax highlighting | Unit | P2 |
| 10 | Message timestamps displayed | Unit | P2 |
| 11 | Copy message to clipboard | Unit | P2 |
| 12 | No axe violations in chat UI | A11y | P0 |
| 13 | aria-live region announces new messages | A11y | P1 |
| 14 | Chat input has accessible label | A11y | P1 |
| 15 | Keyboard navigation between messages | A11y | P1 |

### 2.2 Chat Persistence (#4086)

**File**: `hooks/__tests__/useChatPersistence.test.ts`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Saves messages to localStorage on send | Unit | P0 |
| 2 | Restores messages from localStorage on mount | Unit | P0 |
| 3 | Syncs to server when online | Unit | P1 |
| 4 | Queues messages when offline | Unit | P1 |
| 5 | Resolves conflicts (server wins) | Unit | P1 |
| 6 | Clears chat history on explicit delete | Unit | P1 |
| 7 | Handles localStorage quota exceeded | Unit | P2 |

### 2.3 Chat History Page (#4087)

**File**: `components/agent/chat/__tests__/ChatHistoryPage.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Renders timeline of past conversations | Unit | P0 |
| 2 | Groups conversations by date | Unit | P0 |
| 3 | Filters by agent type | Unit | P1 |
| 4 | Filters by game name | Unit | P1 |
| 5 | Search by message content | Unit | P1 |
| 6 | Pagination loads more conversations | Unit | P1 |
| 7 | Click opens conversation detail | Unit | P1 |
| 8 | Delete conversation with confirmation | Unit | P1 |
| 9 | Empty state when no conversations | Unit | P2 |

### 2.4 Resume Chat (#4088)

**File**: `components/agent/chat/__tests__/ResumeChat.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Resume from chat history page | Unit | P0 |
| 2 | Resume from notification deep link | Unit | P1 |
| 3 | Resume from game page agent section | Unit | P1 |
| 4 | Loads full conversation context | Unit | P1 |
| 5 | Shows "Continuing conversation..." indicator | Unit | P2 |

### 2.5 Agent List Page (#4090)

**File**: `components/agent/__tests__/AgentListPage.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Renders list of user's agents | Unit | P0 |
| 2 | Each agent shows name, game, status | Unit | P0 |
| 3 | Filter by agent status (active/paused) | Unit | P1 |
| 4 | Sort by last activity date | Unit | P1 |
| 5 | "Create Agent" CTA button | Unit | P1 |
| 6 | Empty state with onboarding prompt | Unit | P2 |
| 7 | No axe violations | A11y | P1 |

### 2.6 Dashboard Widget (#4091)

**File**: `components/agent/__tests__/AgentDashboardWidget.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows top 3 recent agents | Unit | P0 |
| 2 | Shows agent activity summary | Unit | P1 |
| 3 | "View All" link navigates to /agents | Unit | P1 |
| 4 | Empty state shows "Create your first agent" | Unit | P2 |

### 2.7 Game Page Agent Section (#4092)

**File**: `components/agent/__tests__/GamePageAgentSection.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows agent for current game | Unit | P0 |
| 2 | "Start Chat" button opens chat UI | Unit | P0 |
| 3 | "Create Agent" if none exists for game | Unit | P1 |
| 4 | Shows KB status (ready/processing) | Unit | P1 |
| 5 | Multiple agents shown when available | Unit | P2 |

### 2.8 Strategy Builder UI (#4093)

**File**: `components/agent/strategy/__tests__/StrategyBuilder.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Renders strategy configuration form | Unit | P0 |
| 2 | Model selection dropdown | Unit | P0 |
| 3 | Temperature slider with preview | Unit | P1 |
| 4 | Token limit configuration | Unit | P1 |
| 5 | System prompt editor | Unit | P1 |
| 6 | Save strategy calls API | Unit | P1 |
| 7 | Load existing strategy on edit | Unit | P1 |
| 8 | Reset to defaults button | Unit | P2 |
| 9 | Strategy preview/test panel | Unit | P2 |
| 10 | No axe violations | A11y | P1 |

### 2.9 Semi-Auto Creation Flow (#4084)

**File**: `components/agent/__tests__/AgentCreationFlow.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Step 1: Select game from library | Unit | P0 |
| 2 | Step 2: Choose strategy template | Unit | P0 |
| 3 | Step 3: Customize settings | Unit | P1 |
| 4 | Step 4: Review and confirm | Unit | P1 |
| 5 | Back navigation between steps | Unit | P1 |
| 6 | Validation errors displayed per step | Unit | P1 |
| 7 | Success state with "Chat Now" CTA | Unit | P1 |
| 8 | Wizard progress indicator | Unit | P2 |

### 2.10 Chat Context KB Integration (#4096)

**File**: `components/agent/chat/__tests__/ChatKBContext.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Chat shows KB source citations | Unit | P0 |
| 2 | Citation links to document section | Unit | P1 |
| 3 | "Sources" panel toggleable | Unit | P1 |
| 4 | KB not ready shows warning banner | Unit | P1 |
| 5 | Fallback to general knowledge when KB empty | Unit | P2 |

### 2.11 Tier Limit Enforcement (#4095)

**File**: `components/agent/__tests__/TierLimitEnforcement.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows remaining tokens/messages count | Unit | P0 |
| 2 | Warning at 80% usage | Unit | P0 |
| 3 | Blocked state at 100% with upgrade CTA | Unit | P0 |
| 4 | Rate limit countdown timer | Unit | P1 |
| 5 | Graceful degradation when tier API unavailable | Unit | P2 |

**Totale Epic #2**: ~95 test cases

---

## Epic #3: Navbar Restructuring (#4070)

### 3.1 Dynamic Route / (#4099)

**File**: `components/layout/__tests__/DynamicRoute.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Anonymous user sees Welcome page at / | Unit | P0 |
| 2 | Authenticated user sees Dashboard at / | Unit | P0 |
| 3 | Route transition on login | Unit | P0 |
| 4 | Route transition on logout | Unit | P1 |
| 5 | SSR renders correct page per auth state | Unit | P1 |
| 6 | No flash of wrong page content | Unit | P1 |

### 3.2 Anonymous Catalog Restrictions (#4100)

**File**: `components/layout/__tests__/AnonymousRestrictions.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Anonymous can browse catalog | Unit | P0 |
| 2 | Anonymous cannot access library | Unit | P0 |
| 3 | Anonymous cannot access agents | Unit | P0 |
| 4 | "Login to access" overlay on restricted features | Unit | P1 |
| 5 | Restricted action redirects to login | Unit | P1 |

### 3.3 Dropdown Grouping Structure (#4097)

**File**: `components/layout/navbar/__tests__/DropdownGrouping.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Main nav groups: Catalogo, La Mia Libreria, AI, Admin | Unit | P0 |
| 2 | Dropdown opens on click | Unit | P0 |
| 3 | Dropdown closes on outside click | Unit | P0 |
| 4 | Dropdown closes on Escape key | Unit | P1 |
| 5 | Nested items render with correct indentation | Unit | P1 |
| 6 | Active route highlighted in dropdown | Unit | P1 |
| 7 | Admin group hidden for non-admin users | Unit | P1 |
| 8 | Keyboard navigation: Arrow keys move focus | A11y | P1 |
| 9 | aria-expanded toggles on dropdown state | A11y | P1 |
| 10 | No axe violations with all dropdowns open | A11y | P1 |

### 3.4 Mobile Hamburger Menu (#4098)

**File**: `components/layout/navbar/__tests__/MobileHamburger.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Hamburger icon visible below 768px | Unit | P0 |
| 2 | Click opens full-screen menu | Unit | P0 |
| 3 | Menu shows all navigation groups | Unit | P0 |
| 4 | Close button dismisses menu | Unit | P1 |
| 5 | Swipe gesture closes menu | Unit | P2 |
| 6 | Body scroll locked when menu open | Unit | P1 |
| 7 | Focus trap inside open menu | A11y | P1 |
| 8 | aria-label on hamburger button | A11y | P1 |

### 3.5 Notifications Dropdown (#4103)

**File**: `components/layout/navbar/__tests__/NotificationsDropdown.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Bell icon shows unread count badge | Unit | P0 |
| 2 | Dropdown shows recent 5 notifications | Unit | P0 |
| 3 | Mark single notification as read | Unit | P1 |
| 4 | "Mark all as read" button | Unit | P1 |
| 5 | "View All" link to /notifications | Unit | P1 |
| 6 | Empty state: "No new notifications" | Unit | P2 |
| 7 | Real-time update via SSE/polling | Unit | P1 |
| 8 | No axe violations | A11y | P1 |

### 3.6 Notifications Page (#4104)

**File**: `components/notifications/__tests__/NotificationsPage.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Lists all 10 notification types | Unit | P0 |
| 2 | Filter by type (game, agent, system, etc.) | Unit | P0 |
| 3 | Filter by read/unread status | Unit | P1 |
| 4 | Pagination/infinite scroll | Unit | P1 |
| 5 | Notification click navigates to source | Unit | P1 |
| 6 | Bulk "Mark as Read" selection | Unit | P1 |
| 7 | Delete notification with confirmation | Unit | P2 |
| 8 | Relative timestamps ("2 ore fa") | Unit | P2 |
| 9 | No axe violations | A11y | P1 |

### 3.7 Notifications Configuration (#4105)

**File**: `components/notifications/__tests__/NotificationsConfig.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Toggle email notifications on/off | Unit | P0 |
| 2 | Toggle push notifications on/off | Unit | P0 |
| 3 | Per-type notification preferences | Unit | P1 |
| 4 | Save preferences calls API | Unit | P1 |
| 5 | Unsaved changes warning on navigate away | Unit | P2 |

### 3.8 Settings Dropdown (#4102)

**File**: `components/layout/navbar/__tests__/SettingsDropdown.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows 8 setting sections | Unit | P0 |
| 2 | Profile section with avatar | Unit | P1 |
| 3 | Theme toggle (light/dark/system) | Unit | P1 |
| 4 | Language selector | Unit | P1 |
| 5 | Logout button with confirmation | Unit | P1 |
| 6 | Keyboard navigation through sections | A11y | P1 |
| 7 | No axe violations | A11y | P1 |

### 3.9 Dual CTA (#4101)

**File**: `components/layout/navbar/__tests__/DualCTA.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows "Accedi" + "Registrati" for anonymous | Unit | P0 |
| 2 | "Accedi" navigates to /login | Unit | P0 |
| 3 | "Registrati" navigates to /register | Unit | P0 |
| 4 | Hidden when authenticated | Unit | P0 |
| 5 | Responsive: stacks vertically on mobile | Unit | P2 |

**Totale Epic #3**: ~70 test cases

---

## Epic #4: PDF Status Tracking (#4071)

### 4.1 7-State Embedding Pipeline UI (#4106)

**File**: `components/pdf/__tests__/EmbeddingPipeline.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Renders all 7 states: Pending, Uploading, Extracting, Chunking, Embedding, Completed, Failed | Unit | P0 |
| 2 | Current state highlighted with animation | Unit | P0 |
| 3 | Completed states show green checkmark | Unit | P0 |
| 4 | Failed state shows red X with error | Unit | P0 |
| 5 | Transition between states animates smoothly | Unit | P1 |
| 6 | Progress percentage per state | Unit | P1 |
| 7 | aria-live announces state changes | A11y | P1 |
| 8 | role="progressbar" with value attributes | A11y | P1 |
| 9 | No axe violations in each state | A11y | P1 |

### 4.2 Multi-Location Status UI (#4108)

**File**: `components/pdf/__tests__/MultiLocationStatus.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows status per service (API, Embedding, Qdrant) | Unit | P0 |
| 2 | Service health indicators (green/yellow/red) | Unit | P0 |
| 3 | Expandable detail per service | Unit | P1 |
| 4 | Service unavailable shows degraded mode | Unit | P1 |
| 5 | Refresh button updates all statuses | Unit | P1 |
| 6 | No axe violations | A11y | P1 |

### 4.3 Real-time Updates SSE + Polling (#4109)

**File**: `hooks/__tests__/usePdfStatusStream.test.ts`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Connects to SSE endpoint on mount | Unit | P0 |
| 2 | Updates state on SSE message | Unit | P0 |
| 3 | Falls back to polling when SSE unavailable | Unit | P0 |
| 4 | Polling interval configurable | Unit | P1 |
| 5 | Reconnects SSE on connection drop | Unit | P1 |
| 6 | Stops polling on terminal state | Unit | P1 |
| 7 | Cleanup on unmount (no memory leaks) | Unit | P1 |

### 4.4 Manual Retry + Error Handling (#4107)

**File**: `components/pdf/__tests__/PdfRetryError.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows "Retry" button on failed state | Unit | P0 |
| 2 | Retry triggers re-upload/re-process | Unit | P0 |
| 3 | Error message displayed with details | Unit | P0 |
| 4 | Retry count shown (attempt 2/3) | Unit | P1 |
| 5 | Max retries reached shows contact support | Unit | P1 |
| 6 | Partial failure shows which step failed | Unit | P1 |
| 7 | role="alert" on error state | A11y | P1 |

### 4.5 Duration Metrics & ETA (#4110)

**File**: `components/pdf/__tests__/PdfDurationMetrics.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows elapsed time since processing started | Unit | P0 |
| 2 | Shows estimated time remaining (ETA) | Unit | P1 |
| 3 | ETA updates as processing progresses | Unit | P1 |
| 4 | Shows "Processing faster than expected" message | Unit | P2 |
| 5 | Handles unknown ETA gracefully | Unit | P2 |

### 4.6 Notification Channel Config (#4111)

**File**: `components/pdf/__tests__/PdfNotificationChannel.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Toggle browser notification on complete | Unit | P0 |
| 2 | Toggle email notification on complete | Unit | P1 |
| 3 | Notification permission request flow | Unit | P1 |
| 4 | Settings persist across sessions | Unit | P2 |

**Totale Epic #4**: ~48 test cases

---

## New UI Features (#4113-#4118)

### 5.1 User Bulk Operations UI (#4118)

**File**: `components/admin/__tests__/BulkOperations.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Checkbox selects individual users | Unit | P0 |
| 2 | "Select All" checkbox | Unit | P0 |
| 3 | Bulk action dropdown (Enable, Disable, Delete, Change Role) | Unit | P0 |
| 4 | Confirmation modal before execution | Unit | P0 |
| 5 | Progress indicator during bulk action | Unit | P1 |
| 6 | Success/failure summary after execution | Unit | P1 |
| 7 | Partial failure shows which users failed | Unit | P1 |
| 8 | Undo option within 10 seconds | Unit | P2 |
| 9 | No axe violations | A11y | P1 |

### 5.2 Achievement System Display UI (#4117)

**File**: `components/gamification/__tests__/AchievementDisplay.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Renders earned badge with icon and name | Unit | P0 |
| 2 | Unearned badges shown as locked/grayed | Unit | P0 |
| 3 | Progress bar for partial achievements | Unit | P1 |
| 4 | Badge detail modal on click | Unit | P1 |
| 5 | "Recently Earned" section highlighted | Unit | P1 |
| 6 | Share badge button | Unit | P2 |
| 7 | Streak counter for streak achievements | Unit | P1 |
| 8 | No axe violations | A11y | P1 |

### 5.3 2FA Self-Service UI (#4116)

**File**: `components/auth/__tests__/TwoFactorSetup.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows QR code for TOTP setup | Unit | P0 |
| 2 | Manual entry code displayed | Unit | P0 |
| 3 | 6-digit verification code input | Unit | P0 |
| 4 | Success state with backup codes | Unit | P0 |
| 5 | Disable 2FA with password confirmation | Unit | P1 |
| 6 | Recovery codes download option | Unit | P1 |
| 7 | Error on invalid verification code | Unit | P1 |
| 8 | No axe violations | A11y | P1 |

### 5.4 Play Records Actions UI (#4115)

**File**: `components/library/__tests__/PlayRecordActions.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | "Log Play" button opens form | Unit | P0 |
| 2 | Date picker for play date | Unit | P0 |
| 3 | Player count selector | Unit | P1 |
| 4 | Duration input | Unit | P1 |
| 5 | Notes/comments field | Unit | P1 |
| 6 | Edit existing play record | Unit | P1 |
| 7 | Delete play record with confirmation | Unit | P1 |
| 8 | Play history list with sort/filter | Unit | P1 |

### 5.5 Wishlist Management System UI (#4114)

**File**: `components/library/__tests__/WishlistManagement.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Add game to wishlist from catalog | Unit | P0 |
| 2 | Remove game from wishlist | Unit | P0 |
| 3 | Wishlist page shows all wishlisted games | Unit | P0 |
| 4 | Priority ordering (drag & drop or arrows) | Unit | P1 |
| 5 | Price alert toggle per game | Unit | P2 |
| 6 | Share wishlist link | Unit | P2 |
| 7 | Empty state with catalog CTA | Unit | P2 |

### 5.6 Notification System UI (#4113)

**File**: `components/notifications/__tests__/NotificationSystem.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Toast notification appears on event | Unit | P0 |
| 2 | Toast auto-dismisses after timeout | Unit | P0 |
| 3 | Toast closable via X button | Unit | P1 |
| 4 | Multiple toasts stack correctly | Unit | P1 |
| 5 | Notification sound toggle | Unit | P2 |
| 6 | No axe violations on toast | A11y | P1 |

**Totale New UI Features**: ~55 test cases

---

## AI Platform UI (#3708-3717)

### 6.1 Agent Builder UI (#3709)

**File**: `components/ai-platform/__tests__/AgentBuilder.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Form renders all fields (name, description, model, strategy) | Unit | P0 |
| 2 | Validation errors on empty required fields | Unit | P0 |
| 3 | Model dropdown populated from API | Unit | P1 |
| 4 | Strategy template selection | Unit | P1 |
| 5 | Submit creates agent via API | Unit | P1 |
| 6 | Edit mode loads existing agent | Unit | P1 |
| 7 | Delete with confirmation | Unit | P1 |

### 6.2 Agent Playground (#3710)

**File**: `components/ai-platform/__tests__/AgentPlayground.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Side-by-side: config panel + chat preview | Unit | P0 |
| 2 | Real-time strategy parameter adjustment | Unit | P1 |
| 3 | Test message sends to preview chat | Unit | P1 |
| 4 | Response quality metrics displayed | Unit | P2 |
| 5 | Save current config as strategy | Unit | P1 |

### 6.3 Strategy Editor (#3711)

**File**: `components/ai-platform/__tests__/StrategyEditor.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | JSON/YAML editor for strategy config | Unit | P0 |
| 2 | Schema validation with error highlighting | Unit | P0 |
| 3 | Visual form mode toggle | Unit | P1 |
| 4 | Diff view for changes | Unit | P2 |
| 5 | Version history dropdown | Unit | P2 |

### 6.4 Visual Pipeline Builder (#3712)

**File**: `components/ai-platform/__tests__/PipelineBuilder.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Canvas renders pipeline nodes | Unit | P0 |
| 2 | Drag node from palette to canvas | Unit | P1 |
| 3 | Connect nodes with edges | Unit | P1 |
| 4 | Delete node/edge | Unit | P1 |
| 5 | Node configuration panel on click | Unit | P1 |
| 6 | Save pipeline to API | Unit | P1 |
| 7 | Load existing pipeline | Unit | P1 |
| 8 | Validation: no orphan nodes | Unit | P2 |

### 6.5 Agent Catalog & Usage Stats (#3713)

**File**: `components/ai-platform/__tests__/AgentCatalog.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Lists all available agents with stats | Unit | P0 |
| 2 | Usage chart (messages/day) | Unit | P1 |
| 3 | Filter by category/game | Unit | P1 |
| 4 | Sort by popularity/rating | Unit | P1 |
| 5 | Agent detail card with metrics | Unit | P1 |

### 6.6 Chat Analytics (#3714)

**File**: `components/ai-platform/__tests__/ChatAnalytics.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Messages per day chart | Unit | P0 |
| 2 | Average response time metric | Unit | P1 |
| 3 | User satisfaction rating | Unit | P1 |
| 4 | Top topics/questions word cloud | Unit | P2 |
| 5 | Date range selector | Unit | P1 |

### 6.7 PDF Analytics (#3715)

**File**: `components/ai-platform/__tests__/PdfAnalytics.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Documents processed count | Unit | P0 |
| 2 | Average processing time chart | Unit | P1 |
| 3 | Success/failure rate pie chart | Unit | P1 |
| 4 | Top error types breakdown | Unit | P1 |
| 5 | Storage usage metric | Unit | P2 |

### 6.8 Model Performance Tracking (#3716)

**File**: `components/ai-platform/__tests__/ModelPerformance.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Model comparison table | Unit | P0 |
| 2 | Latency vs quality scatter plot | Unit | P1 |
| 3 | Cost per 1K tokens metric | Unit | P1 |
| 4 | Token usage trends | Unit | P1 |
| 5 | Model selection recommendation | Unit | P2 |

### 6.9 A/B Testing Framework (#3717)

**File**: `components/ai-platform/__tests__/ABTesting.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Create A/B test experiment | Unit | P0 |
| 2 | Configure variant percentages | Unit | P1 |
| 3 | Results dashboard with statistical significance | Unit | P1 |
| 4 | End experiment and select winner | Unit | P1 |
| 5 | Active experiments list | Unit | P1 |

**Totale AI Platform**: ~55 test cases

---

## Multi-Agent AI Tests

### 7.1 Unified Multi-Agent Dashboard (#3778)

**File**: `components/ai-platform/__tests__/MultiAgentDashboard.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Shows all agent types (Tutor, Arbitro, Decisore) | Unit | P0 |
| 2 | Per-agent status indicators | Unit | P0 |
| 3 | Agent switching controls | Unit | P1 |
| 4 | Orchestration status panel | Unit | P1 |
| 5 | Performance comparison view | Unit | P2 |

### 7.2 Agent Builder Form (#3809)

**File**: `components/ai-platform/__tests__/AgentBuilderForm.test.tsx`

| # | Test Case | Tipo | Priorita |
|---|-----------|------|----------|
| 1 | Form fields for all agent properties | Unit | P0 |
| 2 | Agent type selection (Tutor/Arbitro/Decisore) | Unit | P0 |
| 3 | Model configuration per agent type | Unit | P1 |
| 4 | Validation rules per agent type | Unit | P1 |
| 5 | CRUD operations (Create, Read, Update, Delete) | Unit | P1 |

**Totale Multi-Agent**: ~10 test cases

---

## Riepilogo Test

| Epic / Area | Test Cases | File Test | P0 | P1 | P2 |
|-------------|-----------|-----------|-----|-----|-----|
| #1 MeepleCard Enhancements | 55 | 9 | 18 | 28 | 9 |
| #2 Agent System | 95 | 11 | 28 | 50 | 17 |
| #3 Navbar Restructuring | 70 | 9 | 22 | 38 | 10 |
| #4 PDF Status Tracking | 48 | 6 | 14 | 25 | 9 |
| #5 New UI Features | 55 | 6 | 16 | 27 | 12 |
| #6 AI Platform | 55 | 9 | 10 | 33 | 12 |
| #7 Multi-Agent | 10 | 2 | 4 | 4 | 2 |
| **TOTALE** | **388** | **52** | **112** | **205** | **71** |

### Coverage Targets

| Priorita | Target | Quando |
|----------|--------|--------|
| P0 (112 test) | 100% | Ogni issue prima del merge |
| P1 (205 test) | 95%+ | Checkpoint sync |
| P2 (71 test) | 80%+ | Fine fase |
| A11y (axe) | 100% ogni componente | Ogni issue |

### Esecuzione

```bash
# Run all UI tests
pwsh -c "cd apps/web; pnpm test"

# Run by epic
pwsh -c "cd apps/web; pnpm test -- --grep 'MeepleCard'"
pwsh -c "cd apps/web; pnpm test -- --grep 'Agent'"
pwsh -c "cd apps/web; pnpm test -- --grep 'Navbar'"
pwsh -c "cd apps/web; pnpm test -- --grep 'PDF'"

# Run accessibility only
pwsh -c "cd apps/web; pnpm test -- --grep 'axe|a11y|Accessibility'"

# Run with coverage
pwsh -c "cd apps/web; pnpm test:coverage"
```

---

*Generato: 2026-02-11 | 388 test cases across 52 test files*
