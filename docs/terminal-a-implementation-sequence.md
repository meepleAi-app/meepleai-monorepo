# Terminal A - Implementation Sequence

**Focus**: Frontend-Heavy Track
**Total Issues**: 21
**Duration**: 4 settimane

---

## 📋 Week 1: Epic #1 (MeepleCard) + Wishlist

### Issue #4073 - MeepleCard WCAG 2.1 AA Accessibility
**Priority**: 🟡 MEDIUM | **Effort**: 1 giorno

```bash
/implementa #4073

Scope: Implementa accessibility completa per MeepleCard
- Keyboard navigation (Tab, Enter, Escape)
- ARIA labels (role, aria-label, aria-describedby)
- Color contrast ratios WCAG AA compliance
- Screen reader support
- Focus indicators visibili

Files:
- src/components/ui/data-display/meeple-card/meeple-card.tsx
- src/components/ui/data-display/meeple-card/__tests__/accessibility.test.tsx

Tests:
- apps/web/e2e/epic-1-meeple-card.spec.ts (accessibility test)
- Unit tests per keyboard handlers
- axe-core accessibility audit

Acceptance:
- [ ] Tab navigation works through all interactive elements
- [ ] Enter key activates card
- [ ] Escape closes popover/tooltip
- [ ] All elements have proper ARIA labels
- [ ] Color contrast >4.5:1 for text
- [ ] Focus indicators clearly visible
- [ ] Screen reader announces card content correctly
```

---

### Issue #4076 - Mobile Tag Optimization
**Priority**: 🟡 MEDIUM | **Effort**: 1 giorno

```bash
/implementa #4076

Scope: Ottimizza tag system per mobile devices
- Responsive tag layout (wrap, truncate, max count)
- Touch targets >44px (iOS/Android standard)
- Tag truncation intelligente su small screens
- Max 2-3 tags visibili su mobile

Files:
- src/components/ui/data-display/meeple-card/tag-list.tsx
- src/components/ui/data-display/meeple-card/tag.tsx
- src/components/ui/data-display/meeple-card/__tests__/mobile.test.tsx

Tests:
- apps/web/e2e/epic-1-meeple-card.spec.ts (mobile test)
- Viewport testing (375px, 768px, 1024px)

Acceptance:
- [ ] Tags wrap correctly on mobile (no overflow)
- [ ] Touch targets minimum 44x44px
- [ ] Max 2 tags visible on 375px width (+ counter)
- [ ] Truncation works: "Strategic..." instead of full text
- [ ] Tap interaction smooth (no double-tap delay)
```

---

### Issue #4075 - Tag System Vertical Layout
**Priority**: 🟡 MEDIUM | **Effort**: 1 giorno

```bash
/implementa #4075

Scope: Redesign tag positioning con layout verticale
- Stack tags vertically invece di horizontal
- Migliore utilizzo spazio card
- Alignment con card content

Files:
- src/components/ui/data-display/meeple-card/tag-container.tsx
- src/components/ui/data-display/meeple-card/meeple-card.tsx (layout)

Tests:
- Visual regression test per layout changes
- Unit test layout calculations

Acceptance:
- [ ] Tags posizionati verticalmente (stack)
- [ ] Spacing consistente (gap: 0.5rem)
- [ ] Alignment con bordo card
- [ ] Non sovrappone altri elementi card
```

---

### Issue #4072 - Smart Tooltip Positioning
**Priority**: 🟡 MEDIUM | **Effort**: 0.5 giorni

```bash
/implementa #4072

Scope: Sistema auto-positioning tooltip per evitare overflow viewport
- Detect viewport boundaries
- Auto-flip tooltip direction (top/bottom/left/right)
- Smart offset calculation

Files:
- src/components/ui/data-display/meeple-card/tooltip-provider.tsx
- src/lib/utils/tooltip-positioning.ts

Tests:
- apps/web/e2e/epic-1-meeple-card.spec.ts (tooltip test)
- Unit test positioning algorithm

Acceptance:
- [ ] Tooltip never overflows viewport
- [ ] Auto-flip when near edges (all 4 edges)
- [ ] Smooth transition when flipping
- [ ] Arrow pointer adjusts to position
```

---

### Issue #4080 - Context-Aware Tests
**Priority**: 🟡 MEDIUM | **Effort**: 1 giorno

```bash
/implementa #4080

Scope: Comprehensive unit tests per tutti i variant contexts
- Test all entity types: game, player, collection, event, agent
- Test all variants: grid, list, compact, featured, hero
- Test all states: owned, wishlist, not-owned
- Context-specific features testing

Files:
- src/components/ui/data-display/meeple-card/__tests__/meeple-card.test.tsx
- src/components/ui/data-display/meeple-card/__tests__/variants.test.tsx
- src/components/ui/data-display/meeple-card/__tests__/contexts.test.tsx

Tests:
- 25+ unit tests covering all combinations
- Mock data per ogni entity type
- Snapshot tests per variants

Acceptance:
- [ ] All 5 entity types tested
- [ ] All 5 variants tested
- [ ] All state combinations tested
- [ ] Coverage >95% for MeepleCard component
```

---

### Issue #4081 - Performance Optimization
**Priority**: 🟠 HIGH | **Effort**: 0.5 giorni

```bash
/implementa #4081

Scope: Ottimizzazioni React performance per rendering 100+ cards
- React.memo per MeepleCard
- useMemo per expensive calculations
- Lazy loading per heavy components (images)
- Virtual scrolling investigation

Files:
- src/components/ui/data-display/meeple-card/index.tsx
- src/components/ui/data-display/meeple-card/meeple-card.tsx

Tests:
- apps/web/e2e/epic-1-meeple-card.spec.ts (performance test)
- Performance profiling test

Acceptance:
- [ ] MeepleCard wrapped in React.memo
- [ ] Expensive calculations in useMemo
- [ ] Image lazy loading implemented
- [ ] Render time <1s for 100 cards
- [ ] Scroll performance smooth (60fps)
```

---

### Issue #4114 - Wishlist Management System UI
**Priority**: 🔴 CRITICAL | **Effort**: 3 giorni

```bash
/implementa #4114

Scope: Implementa sistema wishlist completo
- Page /library/wishlist (table + grid views)
- WishlistButton component (heart icon toggle)
- WishlistHighlightsWidget (dashboard top 3)
- Sorting & filtering
- Bulk operations

Files:
- src/app/(protected)/library/wishlist/page.tsx
- src/components/wishlist/wishlist-button.tsx
- src/components/wishlist/wishlist-table.tsx
- src/components/wishlist/wishlist-grid.tsx
- src/components/dashboard/wishlist-highlights-widget.tsx
- src/lib/api/wishlist-client.ts
- src/stores/use-wishlist-store.ts

API Endpoints:
- GET /wishlist
- POST /wishlist
- PUT /wishlist/{id}
- DELETE /wishlist/{id}
- GET /wishlist/highlights

Tests:
- apps/web/e2e/gap-analysis-critical.spec.ts (wishlist tests)
- Unit tests per componenti
- Integration tests API client

Acceptance:
- [ ] Page /library/wishlist funzionante
- [ ] Heart icon toggle su MeepleCard
- [ ] Dashboard widget mostra top 3
- [ ] Sorting: priority, date, name, rating
- [ ] Filtering: complexity, players, playtime
- [ ] Bulk remove con checkbox selection
- [ ] Optimistic UI updates
- [ ] Empty states handled
- [ ] Mobile responsive
```

---

## 📋 Week 2: Epic #3 (Navbar) + Notifications UI

### Issue #4097 - Dropdown Grouping Structure
**Priority**: 🟡 MEDIUM | **Effort**: 0.5 giorni

```bash
/implementa #4097

Scope: Redesign navbar menu con grouped dropdowns
- Logical grouping: Profile, Library, Settings, Admin
- Separators tra groups
- Clear visual hierarchy

Files:
- src/components/layout/navbar/nav-dropdown.tsx
- src/components/layout/navbar/nav-group.tsx

Acceptance:
- [ ] Dropdown menu shows logical groups
- [ ] Separators between groups
- [ ] Hover states correct
```

---

### Issue #4098 - Mobile Hamburger Menu
**Priority**: 🟠 HIGH | **Effort**: 1 giorno

```bash
/implementa #4098

Scope: Mobile navigation completa con hamburger menu
- Sheet/Drawer component
- Full navigation tree
- Swipe gestures
- Overlay backdrop

Files:
- src/components/layout/navbar/mobile-nav.tsx
- src/components/layout/navbar/mobile-sheet.tsx

Tests:
- apps/web/e2e/epic-3-navbar.spec.ts (mobile menu test)

Acceptance:
- [ ] Hamburger icon su mobile (<768px)
- [ ] Sheet opens from left/right
- [ ] All navigation items present
- [ ] Swipe to close works
- [ ] Closes after navigation
```

---

### Issue #4099 - Dynamic Route /
**Priority**: 🟡 MEDIUM | **Effort**: 0.5 giorni

```bash
/implementa #4099

Scope: Root route conditional rendering
- Anonymous: / → Welcome page
- Authenticated: / → Dashboard redirect

Files:
- src/app/page.tsx
- src/middleware.ts (se necessario)

Acceptance:
- [ ] Anonymous user sees Welcome
- [ ] Authenticated user redirected to /dashboard
- [ ] No flash of wrong content
```

---

### Issue #4100 - Anonymous Catalog Restrictions
**Priority**: 🟡 MEDIUM | **Effort**: 0.5 giorni

```bash
/implementa #4100

Scope: Limita contenuti per utenti anonymous
- Restricted catalog view
- Login prompt banners
- Feature gates

Files:
- src/app/(public)/games/page.tsx
- src/components/auth/guest-view.tsx
- src/components/auth/auth-guard.tsx

Acceptance:
- [ ] Anonymous vede limited catalog
- [ ] Login prompt banner visibile
- [ ] "Login to see more" CTA
```

---

### Issue #4101 - Dual CTA (Login + Register)
**Priority**: 🟡 MEDIUM | **Effort**: 0.5 giorni

```bash
/implementa #4101

Scope: Login + Register buttons styling navbar
- Primary button: Register
- Secondary button: Login
- Correct routing

Files:
- src/components/layout/navbar/cta-buttons.tsx

Acceptance:
- [ ] Both buttons visible quando anonymous
- [ ] Correct styling (primary/secondary)
- [ ] Navigation works
```

---

### Issue #4102 - Settings Dropdown (8 Sections)
**Priority**: 🟡 MEDIUM | **Effort**: 1 giorno

```bash
/implementa #4102

Scope: Settings dropdown con 8 sezioni
- Profile, Account, Security, Notifications, Privacy, API Keys, Billing, Preferences

Files:
- src/components/layout/navbar/settings-dropdown.tsx
- src/app/(protected)/settings/layout.tsx

Acceptance:
- [ ] Dropdown shows 8 sections
- [ ] Navigation to each section works
- [ ] Active state highlighting
```

---

### Issue #4113 - Notification System UI (Frontend)
**Priority**: 🔴 CRITICAL | **Effort**: 2 giorni

```bash
/implementa #4113 --frontend-only

Scope: UI completa notifiche (backend già pronto da Terminal B Week 1)
- NotificationBell header component
- NotificationDropdown con real-time
- Toast notifications
- /settings/notifications preferences page

Files:
- src/components/layout/navbar/notification-bell.tsx
- src/components/notifications/notification-dropdown.tsx
- src/components/notifications/notification-item.tsx
- src/components/notifications/toast-notification.tsx
- src/app/(protected)/settings/notifications/page.tsx
- src/stores/use-notification-store.ts
- src/hooks/use-notification-events.ts (SSE)

API Integration:
- GET /notifications
- GET /notifications/unread-count
- POST /notifications/{id}/mark-read
- SSE: /api/v1/sse/notifications

Tests:
- apps/web/e2e/gap-analysis-critical.spec.ts (notifications tests)

Acceptance:
- [ ] Bell icon in header con badge
- [ ] Badge shows unread count
- [ ] Click bell → dropdown with list
- [ ] SSE updates real-time (<1s)
- [ ] Click notification → mark read + navigate
- [ ] Mark all as read works
- [ ] Toast for critical events
- [ ] Preferences page configurable
- [ ] Mobile responsive
```

---

## 📋 Week 3: Epic #2 (Agent System UI) + Achievements

### Issue #4085 - Chat UI Base Component
**Priority**: 🟠 HIGH | **Effort**: 1.5 giorni

```bash
/implementa #4085

Scope: Base component per agent chat interface
- Message list con scrolling
- Chat input textarea
- Streaming response display
- Markdown rendering

Files:
- src/components/agent/agent-chat-interface.tsx
- src/components/agent/chat-message.tsx
- src/components/agent/chat-input.tsx
- src/components/agent/streaming-indicator.tsx

Acceptance:
- [ ] Message list scrolls smoothly
- [ ] Auto-scroll to latest message
- [ ] Input textarea auto-resize
- [ ] Streaming dots animation
- [ ] Markdown rendered correctly
- [ ] Code blocks syntax highlighted
```

---

### Issue #4087 - Chat History Page (Timeline + Filters)
**Priority**: 🟡 MEDIUM | **Effort**: 1 giorno

```bash
/implementa #4087

Scope: Chat history page con timeline e filtri
- /agents/history page
- Timeline view (grouped by date)
- Filters: by game, by date range, by agent type
- Search by content

Files:
- src/app/(protected)/agents/history/page.tsx
- src/components/agent/chat-history-list.tsx
- src/components/agent/chat-history-card.tsx
- src/components/agent/chat-filters.tsx

Acceptance:
- [ ] Timeline grouped by date (Today, Yesterday, This Week)
- [ ] Filter by game works
- [ ] Date range picker works
- [ ] Search by content works
- [ ] Click card → resume chat
- [ ] Pagination for long history
```

---

### Issue #4090 - Agent List Page /agents
**Priority**: 🟡 MEDIUM | **Effort**: 1 giorno

```bash
/implementa #4090

Scope: Agent catalog list page
- Grid view con MeepleCard entity=agent
- Search agents by name/description
- Filter by type (Tutor, Arbitro, Decisore)
- Sort by usage, rating, name

Files:
- src/app/(protected)/agents/page.tsx
- src/components/agent/agent-grid.tsx
- src/components/agent/agent-search.tsx

Acceptance:
- [ ] Grid displays all available agents
- [ ] MeepleCard variant agent works
- [ ] Search filters agents
- [ ] Type filter works
- [ ] Sorting works
- [ ] Click agent → detail or start chat
```

---

### Issue #4091 - Dashboard Widget Your Agents
**Priority**: 🟡 MEDIUM | **Effort**: 0.5 giorni

```bash
/implementa #4091

Scope: Dashboard widget recent agent interactions
- Shows last 3 agent chats
- Quick access to resume
- "View All" link to /agents

Files:
- src/components/dashboard/your-agents-widget.tsx
- src/app/(protected)/dashboard/page.tsx (integrate widget)

Acceptance:
- [ ] Widget shows 3 recent agents
- [ ] Displays: agent name, last interaction time, game
- [ ] Click agent → resume chat
- [ ] "View All" → /agents page
```

---

### Issue #4092 - Game Page Agent Section
**Priority**: 🟡 MEDIUM | **Effort**: 0.5 giorni

```bash
/implementa #4092

Scope: Agent section su game detail page
- Shows available agents per game
- "Start Chat" CTA
- Agent recommendations

Files:
- src/app/(protected)/games/[id]/page.tsx (add section)
- src/components/game/game-agent-section.tsx

Acceptance:
- [ ] Section visibile su game detail
- [ ] Shows available agent types
- [ ] "Start Chat" button works
- [ ] Opens chat with game context loaded
```

---

### Issue #4093 - Strategy Builder UI
**Priority**: 🟡 MEDIUM | **Effort**: 0.5 giorni

```bash
/implementa #4093

Scope: Visual interface per RAG strategy builder
- Strategy selection (FAST, CONSENSUS, PRECISE, Custom)
- Visual pipeline builder (drag-drop)
- Configuration form per strategy

Files:
- src/app/(protected)/admin/strategies/create/page.tsx
- src/components/admin/strategy-builder.tsx
- src/components/admin/pipeline-visual.tsx

Acceptance:
- [ ] Strategy type selector works
- [ ] Visual pipeline displayed
- [ ] Configuration form validates
- [ ] Save strategy works
```

---

### Issue #4117 - Achievement Display UI
**Priority**: 🟡 MEDIUM | **Effort**: 1.5 giorni

```bash
/implementa #4117 --frontend-only

Scope: Achievement system display (backend pronto da Terminal B Week 2)
- /profile/achievements page
- AchievementCard components
- Dashboard widget
- Filtering earned/locked

Files:
- src/app/(protected)/profile/achievements/page.tsx
- src/components/achievements/achievement-card.tsx
- src/components/achievements/achievement-grid.tsx
- src/components/dashboard/recent-achievements-widget.tsx

API Integration:
- GET /achievements
- GET /achievements/recent
- GET /achievements/progress

Acceptance:
- [ ] Page displays all achievements (grid)
- [ ] Earned: full color + date
- [ ] Locked: grayscale + requirements
- [ ] In-progress: progress bar
- [ ] Filtering works (All, Earned, Locked)
- [ ] Dashboard widget shows last 3
- [ ] Click → detail modal
```

---

## 📋 Week 4: PDF Status UI + 2FA UI + Play Records UI

### Issue #4108 - Multi-Location Status UI
**Priority**: 🟡 MEDIUM | **Effort**: 1 giorno

```bash
/implementa #4108

Scope: PDF status badges in multiple locations
- Upload page status
- Library PDF list status
- Processing queue admin status

Files:
- src/components/pdf/pdf-status-badge.tsx
- src/components/pdf/processing-progress.tsx

Acceptance:
- [ ] Status badge in upload page
- [ ] Status badge in library
- [ ] Status badge in admin queue
- [ ] Consistent styling across locations
```

---

### Issue #4109 - Real-time Updates UI (SSE)
**Priority**: 🟠 HIGH | **Effort**: 1 giorno

```bash
/implementa #4109 --frontend-only

Scope: SSE connection per live PDF progress
- Hook usePDFStatusEvents
- Real-time progress bar updates
- Status badge live updates

Files:
- src/hooks/use-pdf-status-events.ts
- src/components/pdf/realtime-progress.tsx

Acceptance:
- [ ] SSE connection established on upload
- [ ] Progress bar updates live
- [ ] Status badge updates live
- [ ] Handles disconnect/reconnect
```

---

### Issue #4110 - Duration Metrics Display
**Priority**: 🟡 MEDIUM | **Effort**: 1 giorno

```bash
/implementa #4110

Scope: ETA e duration display per PDF processing
- Processing time estimate
- Progress percentage
- Elapsed time counter

Files:
- src/components/pdf/processing-metrics.tsx
- src/lib/utils/eta-calculator.ts

Acceptance:
- [ ] ETA displayed (~X minutes remaining)
- [ ] Progress percentage (45% complete)
- [ ] Elapsed time updates (00:02:35)
```

---

### Issue #4116 - 2FA Self-Service UI
**Priority**: 🟡 MEDIUM | **Effort**: 2 giorni

```bash
/implementa #4116 --frontend-only

Scope: 2FA setup wizard completo (backend pronto da Terminal B Week 3)
- /settings/security page
- Setup wizard (3 steps)
- QR code display
- Recovery codes download

Files:
- src/app/(protected)/settings/security/page.tsx
- src/components/security/two-factor-setup.tsx
- src/components/security/qr-code-display.tsx
- src/components/security/recovery-codes.tsx

Dependencies:
- pnpm add qrcode.react

API Integration:
- POST /auth/2fa/setup
- POST /auth/2fa/enable
- POST /auth/2fa/disable
- GET /users/me/2fa/status

Tests:
- apps/web/e2e/gap-analysis-critical.spec.ts (2FA tests)

Acceptance:
- [ ] Setup wizard 3 steps complete
- [ ] QR code scannable
- [ ] Manual entry option
- [ ] Verify code before enable
- [ ] Recovery codes downloadable (TXT + PDF)
- [ ] Disable requires password
- [ ] Status indicator works
```

---

### Issue #4115 - Play Records Actions UI
**Priority**: 🟡 MEDIUM | **Effort**: 2 giorni

```bash
/implementa #4115 --frontend-only

Scope: Complete play records actions (backend pronto da Terminal B Week 1)
- Add/Remove players
- Score tracking
- Start/Complete session workflow

Files:
- src/app/(protected)/play-records/[id]/page.tsx (enhance)
- src/components/play-records/add-player-dialog.tsx
- src/components/play-records/score-tracker.tsx
- src/components/play-records/session-controls.tsx
- src/components/play-records/session-timer.tsx

API Integration:
- POST /play-records/{id}/players
- POST /play-records/{id}/scores
- POST /play-records/{id}/start
- POST /play-records/{id}/complete

Acceptance:
- [ ] Add player dialog works
- [ ] Score table inline editing
- [ ] Start button → timer starts
- [ ] Complete button → summary shown
- [ ] Validation: cannot complete without scores
```

---

## 🎯 Execution Summary

### Total Terminal A Issues: 21

**Week 1**: 7 issue (Epic #1 + Wishlist)
**Week 2**: 7 issue (Epic #3 + Notifications UI)
**Week 3**: 5 issue (Epic #2 Agent UI + Achievements UI)
**Week 4**: 2 issue (PDF Status UI + 2FA UI + Play Records UI)

### Command Pattern

```bash
# Per ogni issue, esegui:
/implementa #XXXX [--frontend-only se backend già pronto]

# Dopo ogni issue:
git add .
git commit -m "feat(scope): description (#XXXX)"
git push origin main-dev
pnpm test:e2e -- epic-N-name.spec.ts
```

### Checkpoints

- **End Week 1**: Merge + Test Epic #1 + Wishlist
- **End Week 2**: Merge + Test Epic #3 + Notifications
- **End Week 3**: Merge + Test Epic #2 + Achievements
- **End Week 4**: Final validation + Deploy

---

**Ready to Start**: ✅
**First Command**: `/implementa #4073`
