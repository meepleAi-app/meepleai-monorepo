# Game Table Behavioral Examples (Given/When/Then)

**Issue**: #212
**Status**: Draft
**Date**: 2026-03-13

## Overview

BDD-style behavioral specifications for Game Table interactions. Each scenario is testable via Playwright E2E or Vitest unit tests.

---

## CardRack Navigation

### Scenario: Hover-expand on desktop

```gherkin
Given the user is on a desktop viewport (≥768px)
  And the CardRack is in collapsed state (64px)
When the user hovers over the CardRack
Then the CardRack expands to 240px with a 200ms ease-in-out transition
  And navigation labels become visible alongside icons
```

### Scenario: Click navigation item

```gherkin
Given the CardRack is visible
When the user clicks a navigation card (e.g. "Dashboard")
Then the app navigates to the corresponding route (/dashboard)
  And the active card receives a highlighted border
```

### Scenario: CardRack hidden on mobile

```gherkin
Given the user is on a mobile viewport (<768px)
Then the CardRack is not rendered (hidden class applied)
  And navigation is available via MobileTabBar at the bottom
```

---

## QuickView Panel

### Scenario: Open QuickView for a game

```gherkin
Given the user is on a page with game context
When the user clicks the QuickView trigger for "Catan"
Then the QuickView panel opens on the right (300px, xl+ only)
  And the "Regole" tab is active by default
  And the game name "Catan" appears in the panel header
```

### Scenario: Switch tabs in QuickView

```gherkin
Given the QuickView is open with "Regole" tab active
When the user clicks the "FAQ" tab
Then the FAQ content is displayed
  And the "FAQ" tab is visually selected
  And the store's activeTab is updated to "faq"
```

### Scenario: Close QuickView

```gherkin
Given the QuickView panel is open
When the user clicks the close button (X)
Then the QuickView panel is removed from the DOM
  And the content area expands to fill the freed space
```

### Scenario: Collapse QuickView to icon strip

```gherkin
Given the QuickView panel is expanded (300px)
When the user clicks the collapse button (PanelRightClose icon)
Then the panel narrows to 44px showing only tab icons
  And clicking an icon re-expands with that tab active
```

---

## MobileTabBar

### Scenario: Primary navigation on mobile

```gherkin
Given the user is on a mobile viewport (<768px)
Then a fixed bottom tab bar is visible with navigation icons
  And the current route's tab is highlighted
When the user taps a tab
Then the app navigates to the corresponding route
```

### Scenario: Hidden on desktop

```gherkin
Given the user is on a desktop viewport (≥768px)
Then the MobileTabBar is not rendered (md:hidden class applied)
```

---

## Live Session Feed

### Scenario: Receive real-time event

```gherkin
Given the user is in an active game session
  And the SSE connection is established
When another player rolls dice
Then a "dice_roll" event appears in the activity feed within 500ms
  And the event shows the player name, dice values, and total
```

### Scenario: SSE reconnection

```gherkin
Given the SSE connection drops unexpectedly
Then the UI shows a "Reconnecting..." indicator
  And the client retries with exponential backoff (1s, 2s, 4s...)
When the connection is restored
Then missed events are replayed via Last-Event-ID
  And the "Reconnecting..." indicator disappears
```

### Scenario: Session stale after 5 minutes offline

```gherkin
Given the SSE connection has been down for 5 minutes
Then the UI shows a "Session stale" prompt
  And the user can click "Reconnect" to force a fresh connection
  And stale events are fetched from the paginated API
```

---

## FloatingActionBar

### Scenario: Context-aware actions

```gherkin
Given the user is viewing a game night detail page
Then the FloatingActionBar shows relevant actions (e.g. "Start Session", "Invite Player")
When no contextual actions are available
Then the FloatingActionBar auto-hides
```

### Scenario: Responsive positioning

```gherkin
Given the user is on mobile (<768px)
Then the FloatingActionBar is positioned above the MobileTabBar (bottom: calc(72px + 1.5rem))
Given the user is on desktop (≥768px)
Then the FloatingActionBar is positioned at bottom: 24px
```

---

## Keyboard Navigation

### Scenario: Tab through layout

```gherkin
Given the user presses Tab from the top of the page
Then focus moves through: skip-link → TopBar items → CardRack items → main content
  And each focused element has a visible focus indicator
```

### Scenario: Skip to content

```gherkin
Given the user presses Tab once from page load
Then a "Skip to content" link becomes visible
When the user presses Enter
Then focus moves to main content area (id="main-content")
  And CardRack and TopBar are skipped
```

---

## Game Night CRUD

### Scenario: Create a game night

```gherkin
Given the user is on the "New Game Night" page
When the user fills in title, date, max players, and selects a game
  And clicks "Create"
Then a POST request is sent to /api/v1/game-nights
  And the user is redirected to the new game night detail page
  And a success toast is shown
```

### Scenario: Validation error on create

```gherkin
Given the user submits the game night form with an empty title
Then a 422 validation error is returned
  And the title field shows "Title is required" error message
  And focus moves to the first invalid field
```

### Scenario: Delete a game night

```gherkin
Given the user is the host of a planned game night
When the user clicks "Delete" and confirms the dialog
Then a DELETE request soft-deletes the game night
  And the user is redirected to the game nights list
  And the deleted night no longer appears in the list
```
