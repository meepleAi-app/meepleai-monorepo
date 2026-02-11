#!/bin/bash

# Epic #1: MeepleCard Enhancements (10 issue)

gh issue create --title "MeepleCard - Smart Tooltip Positioning System" --label "P1-high,size:M" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P1-High | **Estimate**: M | **Dependencies**: None

## Description
As a user, I want tooltips on MeepleCard to automatically adjust their position based on card location, so they are never cut off by screen edges or container boundaries.

## Acceptance Criteria
- [ ] Tooltip detects card position relative to viewport
- [ ] Tooltip flips automatically when near edges (top/bottom/left/right)
- [ ] Works in all contexts: Grid, Carousel, List, Modal, Sidebar, Mobile drawer
- [ ] Smooth transition animations when position changes
- [ ] No flickering or layout shift during flip
- [ ] Portal-based rendering to avoid overflow issues

## Technical Notes
- **Frontend**: Use Radix UI Tooltip with custom position logic
- **Implementation**: Portal rendering + boundary detection
- **Context handling**: Detect parent container boundaries
- **Performance**: Debounce position calculation on scroll"

gh issue create --title "MeepleCard - WCAG 2.1 AA Accessibility Compliance" --label "P1-high,size:M" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P1-High | **Estimate**: M | **Dependencies**: Previous issue

## Description
As a user with accessibility needs, I want full keyboard navigation and screen reader support for MeepleCard tooltips.

## Acceptance Criteria
- [ ] \`role=\"tooltip\"\`, \`aria-describedby\`, \`aria-label\` implemented
- [ ] Keyboard navigation: Tab to focus, Esc to close
- [ ] Focus trap if tooltip contains clickable actions
- [ ] Screen reader announces tooltip content
- [ ] Visible focus indicators
- [ ] High contrast mode support
- [ ] Tested with NVDA, JAWS, VoiceOver

## Technical Notes
- **Standards**: WCAG 2.1 AA compliance
- **Testing**: Automated (axe-core) + Manual screen reader testing"

gh issue create --title "MeepleCard - Permission System Integration (Tier/Role/State/Resources)" --label "P0-critical,size:L" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P0-Critical | **Estimate**: L | **Dependencies**: Uses existing auth system

## Description
As a user, I want MeepleCard actions to respect my tier, role, account state, and resource availability.

## Acceptance Criteria
- [ ] **Tier limits**: Free (10), Pro (50), Enterprise (500) - fetched from Admin config
- [ ] **Role checks**: User/Editor/Admin/SuperAdmin permissions
- [ ] **State validation**: Verified, Suspended, Trial, Expired, Pending Deletion
- [ ] **Resource availability**: Check library space, token balance
- [ ] Actions dynamically enabled/disabled
- [ ] Clear error messages when blocked
- [ ] Loading states while checking permissions

## Technical Notes
- **Backend**: \`GET /api/v1/users/me/permissions?entityType=game\`
- **Frontend**: Hook \`useEntityActions(entityType, entityId)\`
- **Integration**: Reuse \`hasRole()\` from \`auth.ts\`"

gh issue create --title "MeepleCard - Tag System Redesign (Vertical Layout)" --label "P1-high,size:M" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P1-High | **Estimate**: M | **Dependencies**: None

## Description
As a user, I want tags displayed vertically in top-left corner of MeepleCard.

## Acceptance Criteria
- [ ] Tags positioned vertically in top-left (absolute positioning)
- [ ] **Critical tags** (always visible): Agente, In libreria, Preferito
- [ ] **Secondary tags** (on-hover): Tipo card, Posseduto, Prestato, Wishlist
- [ ] Badge counter: Max 2-3 visible + \"+N\" for overflow
- [ ] Click \"+N\" → show all tags (popover)
- [ ] Smooth fade-in animation on hover
- [ ] Glass morphism: \`bg-white/70 backdrop-blur-md\`

## Technical Notes
- **Component**: Update \`MeepleCard\` tag rendering
- **Animation**: Framer Motion or Tailwind transitions"

gh issue create --title "MeepleCard - Mobile Tag Optimization (Collapse on Scroll)" --label "P2-medium,size:S" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P2-Medium | **Estimate**: S | **Dependencies**: Previous tag issue

## Description
As a mobile user, I want optimized tag behavior on small screens.

## Acceptance Criteria
- [ ] **Badge counter aggressive**: Max 1-2 tags + \"+N\" on mobile (<768px)
- [ ] Tap \"+N\" → bottom sheet with all tags
- [ ] **Collapse on scroll**: Hidden while scrolling, visible when stopped
- [ ] Scroll detection debounced (100ms)
- [ ] Touch-friendly tap targets (min 44x44px)
- [ ] Bottom sheet with slide-up animation

## Technical Notes
- **Scroll detection**: IntersectionObserver or scroll event
- **Breakpoints**: Tailwind \`md:\` for 768px
- **Bottom sheet**: Radix Dialog or Vaul library"

gh issue create --title "MeepleCard - Collection Limits Management System" --label "P1-high,size:L" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P1-High | **Estimate**: L | **Dependencies**: Permission system issue

## Description
As a user, I want clear feedback when collection is full with upgrade/manage options.

## Acceptance Criteria
- [ ] Real-time count: \"8/10 games\" badge
- [ ] Action disabled when limit reached
- [ ] Tooltip: \"Collection full. Upgrade to Pro for 50 games\"
- [ ] **Upgrade CTA**: Link to billing or inline modal
- [ ] **Manage action**: \"Remove games\" → library page
- [ ] Admin override for custom limits
- [ ] Warning at 90% capacity

## Technical Notes
- **Backend**: Admin config table \`user_tier_limits\`
- **Frontend**: Hook \`useCollectionLimits(entityType)\`
- **Admin**: CRUD endpoints for tier limits"

gh issue create --title "MeepleCard - Ownership State Logic (Add vs Remove Actions)" --label "P1-high,size:M" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P1-High | **Estimate**: M | **Dependencies**: Permission + Limits issues

## Description
As a user, I want MeepleCard actions to change based on ownership state.

## Acceptance Criteria
- [ ] **Not in library** → Actions: Info, Add (if space available)
- [ ] **In library** → Actions: Info, Quick Actions, Remove
- [ ] \"Add\" disabled if: full, tier limit, insufficient permissions
- [ ] \"Remove\" always available
- [ ] Confirmation dialog for remove
- [ ] Optimistic UI with rollback on error

## Technical Notes
- **State**: Zustand store for library state
- **API**: POST/DELETE \`/api/v1/library/games/{id}\`
- **Confirmation**: Radix AlertDialog"

gh issue create --title "MeepleCard - Agent Type Support" --label "P1-high,size:M" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P1-High | **Estimate**: M | **Dependencies**: None (parallel with Agent epic)

## Description
As a user, I want MeepleCard for agent entities with agent-specific info.

## Acceptance Criteria
- [ ] New entity type: \`agent\`
- [ ] **Fields**: Nome gioco, Nome agente, Strategia badge, Stato KB, Ultimo utilizzo, Numero messaggi, Statistiche, Tags
- [ ] Purple accent color scheme
- [ ] Quick actions: Start Chat, View Details, Settings
- [ ] Hover preview: Last chat message snippet

## Technical Notes
- **Type**: Extend \`EntityType = 'game' | ... | 'agent'\`
- **Component**: Agent variant in MeepleCard
- **Data model**: \`AgentCardData\` interface"

gh issue create --title "MeepleCard - Context-Aware Rendering Tests" --label "P2-medium,size:M" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P2-Medium | **Estimate**: M | **Dependencies**: Tooltip, Tag, Context issues

## Description
As a developer, I want comprehensive tests for MeepleCard in all contexts.

## Acceptance Criteria
- [ ] **Unit tests** (Vitest): Tooltip positioning, Tag filtering, Permission checks
- [ ] **Integration tests**: All contexts (Grid, Carousel, List, Modal, Sidebar, Drawer)
- [ ] **Visual regression**: Screenshot comparisons for variants
- [ ] **Coverage target**: >85% for MeepleCard components

## Technical Notes
- **Testing**: @testing-library/react + Vitest
- **Visual**: Playwright with screenshot diffs
- **CI/CD**: Block merge if failing"

gh issue create --title "MeepleCard - Performance Optimization (Large Lists)" --label "P2-medium,size:S" --body "**Epic**: #4068 MeepleCard Enhancements
**Priority**: P2-Medium | **Estimate**: S | **Dependencies**: All MeepleCard issues

## Description
As a user, I want smooth performance with 100+ MeepleCards.

## Acceptance Criteria
- [ ] Virtual scrolling for lists >50 cards
- [ ] Lazy loading images
- [ ] Debounced hover events (100ms)
- [ ] Memoized permission calculations
- [ ] Skeleton loading states
- [ ] **Metrics**: FCP <1.5s, TTI <3s, 60fps scrolling

## Technical Notes
- **Virtual scroll**: @tanstack/react-virtual
- **Image**: Next.js Image with blur placeholder
- **Monitoring**: Web Vitals + Lighthouse CI"

echo "Epic #1 (MeepleCard) - 10 issues created"
