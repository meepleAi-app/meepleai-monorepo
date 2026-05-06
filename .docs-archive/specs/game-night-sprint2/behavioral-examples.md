# Sprint 2 — Behavioral Examples (#212)

> Given/When/Then scenarios for all Sprint 2 user flows.
> Each scenario maps to a testable acceptance criterion.

---

## Table of Contents

1. [QuickView Dual-Context Resolution (#214)](#quickview-dual-context-resolution-214)
2. [Server-Side Timer (#216)](#server-side-timer-216)
3. [Responsive Layout (#215)](#responsive-layout-215)
4. [Cross-Feature Integration](#cross-feature-integration)

---

## QuickView Dual-Context Resolution (#214)

### Feature: Shared catalog game with library entry

```gherkin
Feature: QuickView dual-context resolution (#214)

  Background:
    Given user "Alice" is authenticated
    And "Catan" exists in the shared catalog with:
      | field           | value           |
      | averageRating   | 8.2             |
      | publisher       | Kosmos          |
      | yearPublished   | 1995            |
      | categories      | Strategy, Trade |
      | weight          | 2.3             |

  Scenario: View shared game with library entry
    Given Alice has "Catan" in her library with:
      | field          | value       |
      | playCount      | 5           |
      | personalRating | 9           |
      | lastPlayedAt   | 2026-03-01  |
    When Alice opens QuickView for "Catan" via GET /api/v1/games/{catanId}/quick-view
    Then the response status is 200
    And response.game.name is "Catan"
    And response.sharedContext.averageRating is 8.2
    And response.sharedContext.publisher is "Kosmos"
    And response.userContext is not null
    And response.userContext.playCount is 5
    And response.userContext.personalRating is 9

  Scenario: View shared game without library entry
    Given Alice does NOT have "Catan" in her library
    When Alice opens QuickView for "Catan" via GET /api/v1/games/{catanId}/quick-view
    Then the response status is 200
    And response.game.name is "Catan"
    And response.sharedContext.averageRating is 8.2
    And response.userContext is null

  Scenario: View game from library perspective
    Given Alice has "Catan" in her library with playCount 5
    When Alice opens QuickView via GET /api/v1/library/games/{catanId}/quick-view
    Then the response status is 200
    And response.userContext is not null
    And response.userContext.playCount is 5
    And response.sharedContext is not null
    And response.recentPlays is an array

  Scenario: View private game (no shared catalog entry)
    Given Alice has a private game "Homebrew RPG" in her library
    And "Homebrew RPG" has NO shared catalog entry
    When Alice opens QuickView via GET /api/v1/library/games/{homebrewId}/quick-view
    Then the response status is 200
    And response.isPrivateGame is true
    And response.userContext is not null
    And response.sharedContext is null

  Scenario: QuickView for nonexistent shared game
    When Alice opens QuickView via GET /api/v1/games/{nonExistentId}/quick-view
    Then the response status is 404
    And response.error is "Game not found"

  Scenario: Library QuickView for game not in library
    Given "Catan" exists in the shared catalog
    But Alice does NOT have "Catan" in her library
    When Alice opens QuickView via GET /api/v1/library/games/{catanId}/quick-view
    Then the response status is 404
    And response.error contains "not in library"

  Scenario: QuickView without authentication
    Given user is not authenticated
    When user opens QuickView for "Catan"
    Then the response status is 401
```

---

## Server-Side Timer (#216)

### Feature: Timer lifecycle

```gherkin
Feature: Server-side timer (#216)

  Background:
    Given user "Bob" is the host of session "game-night-42"
    And session "game-night-42" is in "InProgress" state
    And user "Charlie" is a player in session "game-night-42"
    And both Bob and Charlie have active SSE connections to the session

  Scenario: Start timer for session
    When Bob sends POST /api/v1/sessions/{sessionId}/timer/start
    Then the response status is 204
    And all connected clients receive a "timer_state" SSE event with:
      | field   | value |
      | paused  | false |
      | elapsed | 0     |
    And all connected clients begin receiving "timer_tick" SSE events every 1 second

  Scenario: Timer tick events are synchronized
    Given the timer has been running for 10 seconds
    When Charlie's client receives the next "timer_tick" SSE event
    Then the event data contains:
      | field      | type   | description                    |
      | elapsed    | number | Elapsed milliseconds (~ 10000) |
      | paused     | boolean| false                          |
      | serverTime | string | ISO 8601 server timestamp      |
    And the elapsed value is within 500ms of Bob's client elapsed value

  Scenario: Pause timer
    Given the timer is running at elapsed time 3:15 (195000ms)
    When Bob sends POST /api/v1/sessions/{sessionId}/timer/pause
    Then the response status is 204
    And all clients receive a "timer_state" SSE event with:
      | field   | value  |
      | paused  | true   |
      | elapsed | 195000 |
    And "timer_tick" SSE events stop

  Scenario: Resume timer after pause
    Given the timer is paused at elapsed time 3:15 (195000ms)
    When Bob sends POST /api/v1/sessions/{sessionId}/timer/start
    Then the response status is 204
    And "timer_tick" SSE events resume
    And the first tick's elapsed value is approximately 195000ms (not 0)

  Scenario: Reset timer while running
    Given the timer is running at elapsed time 5:32
    When Bob sends POST /api/v1/sessions/{sessionId}/timer/reset
    Then the response status is 204
    And all clients receive a "timer_state" SSE event with:
      | field   | value |
      | elapsed | 0     |
      | paused  | false |
    And "timer_tick" events continue with elapsed counting from 0

  Scenario: Reset timer while paused
    Given the timer is paused at elapsed time 2:00
    When Bob sends POST /api/v1/sessions/{sessionId}/timer/reset
    Then the response status is 204
    And all clients receive a "timer_state" SSE event with:
      | field   | value |
      | elapsed | 0     |
      | paused  | true  |

  Scenario: Timer survives page refresh
    Given the timer is running at elapsed time 5:32
    When Charlie closes and reopens the page
    And Charlie sends GET /api/v1/sessions/{sessionId}/timer
    Then the response contains the current server-side elapsed time (approximately 5:32 + reconnect delay)
    And Charlie reconnects to SSE and receives "timer_state" with the current elapsed time
    And no time is lost

  Scenario: Non-host cannot start timer
    When Charlie (non-host) sends POST /api/v1/sessions/{sessionId}/timer/start
    Then the response status is 403
    And response.error is "NOT_SESSION_HOST"

  Scenario: Cannot start timer on non-InProgress session
    Given session "game-night-42" is in "Created" state
    When Bob sends POST /api/v1/sessions/{sessionId}/timer/start
    Then the response status is 409
    And response.error is "SESSION_NOT_IN_PROGRESS"

  Scenario: Cannot pause already-paused timer
    Given the timer is paused
    When Bob sends POST /api/v1/sessions/{sessionId}/timer/pause
    Then the response status is 409
    And response.error is "TIMER_NOT_RUNNING"

  Scenario: Cannot start already-running timer
    Given the timer is running
    When Bob sends POST /api/v1/sessions/{sessionId}/timer/start
    Then the response status is 409
    And response.error is "TIMER_ALREADY_RUNNING"

  Scenario: Get timer state for nonexistent session
    When Bob sends GET /api/v1/sessions/{nonExistentId}/timer
    Then the response status is 404

  Scenario: Non-participant cannot view timer
    Given user "Eve" is NOT a participant in session "game-night-42"
    When Eve sends GET /api/v1/sessions/{sessionId}/timer
    Then the response status is 403
```

---

## Responsive Layout (#215)

### Feature: Desktop layout

```gherkin
Feature: Responsive layout (#215)

  Background:
    Given user "Alice" is authenticated
    And a game night "Friday Board Games" exists with 4 RSVPs

  Scenario: Desktop (>= 1024px) — full layout
    Given viewport width is 1280px
    When Alice navigates to the game night page
    Then a 3-column layout is displayed
    And the game list shows a 3-column grid
    And all game details are visible without scrolling

  Scenario: Desktop QuickView opens as side panel
    Given viewport width is 1280px
    When Alice clicks on a game card
    Then QuickView opens as a side panel on the right
    And the game list remains visible behind the panel
    And the side panel width does not exceed 400px

  Scenario: Tablet (768px - 1023px) — adapted layout
    Given viewport width is 800px
    When Alice navigates to the game night page
    Then a 2-column layout is displayed
    And the game list shows a 2-column grid
    And secondary details (mechanics, weight) are collapsed into expandable sections

  Scenario: Tablet QuickView opens as bottom sheet
    Given viewport width is 800px
    When Alice clicks on a game card
    Then QuickView opens as a bottom sheet (50% viewport height)
    And the bottom sheet can be dragged to expand to full height
    And the bottom sheet can be dismissed by swiping down

  Scenario: Mobile (< 768px) — stacked layout
    Given viewport width is 375px
    When Alice navigates to the game night page
    Then a single-column stacked layout is displayed
    And the game list shows a 1-column list
    And only essential info is shown (name, image, rating)

  Scenario: Mobile QuickView opens as full-screen modal
    Given viewport width is 375px
    When Alice clicks on a game card
    Then QuickView opens as a full-screen modal
    And a back/close button is prominently visible in the top-left
    And the modal scrolls vertically for long content

  Scenario: Score table adapts to viewport
    Given an active session with 4 players and 3 rounds of scores
    When viewport width is >= 1024px
    Then scores display as a full table with columns per round
    When viewport width is 768px - 1023px
    Then scores display as a horizontally scrollable table
    When viewport width is < 768px
    Then scores display as one card per player with stacked rounds

  Scenario: Timer display adapts to viewport
    Given an active session with a running timer
    When viewport width is >= 768px
    Then the timer displays in the persistent header bar
    When viewport width is < 768px
    Then the timer displays as a floating pill overlay
    And the pill does not obscure action buttons

  Scenario: RSVP list adapts to viewport
    Given a game night with 6 RSVPs
    When viewport width is >= 1024px
    Then RSVPs display as an inline list below the event details
    When viewport width is 768px - 1023px
    Then RSVPs display in a collapsible accordion section
    When viewport width is < 768px
    Then RSVPs are accessible via a separate view/modal
    And a summary badge shows "6 responses" on the main page
```

---

## Cross-Feature Integration

### Feature: QuickView within a game night context

```gherkin
Feature: QuickView in game night context

  Background:
    Given user "Alice" is viewing game night "Friday Board Games"
    And the game night has games: "Catan", "Wingspan", "Azul"
    And Alice has "Catan" and "Wingspan" in her library

  Scenario: QuickView shows library status for owned game
    When Alice opens QuickView for "Catan" from the game night page
    Then QuickView shows the shared catalog data
    And QuickView shows Alice's library context (play count, personal rating)
    And no "Add to Library" CTA is shown

  Scenario: QuickView shows add-to-library CTA for unowned game
    When Alice opens QuickView for "Azul" from the game night page
    Then QuickView shows the shared catalog data
    And an "Add to Library" CTA is prominently displayed
    And the user context section is empty

  Scenario: Timer and QuickView coexist during active session
    Given a live session is active for game night "Friday Board Games"
    And the timer is running at 12:30
    When Alice opens QuickView for a game
    Then the timer remains visible in its designated position
    And QuickView does not cover the timer display
    And both can be interacted with independently
```

### Feature: Timer and score interaction

```gherkin
Feature: Timer and score recording interaction

  Background:
    Given an active session for "Catan" with host "Bob" and players "Alice", "Charlie"
    And the timer is running

  Scenario: Score recorded while timer is running
    When Alice records a score of 10 for round 1
    Then a "score_update" SSE event is broadcast to all clients
    And the timer continues running uninterrupted
    And the score table updates for all connected clients

  Scenario: Session completed stops the timer
    Given the timer is running at 45:00
    When Bob completes the session
    Then the timer stops automatically
    And a final "timer_state" SSE event is emitted with paused: true
    And no further "timer_tick" events are emitted
```
