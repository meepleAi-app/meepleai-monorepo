# Game Session Toolkit - User Flows

**Feature**: Collaborative Board Game Scorekeeper
**Version**: 1.0.0
**Last Updated**: 2026-01-30

---

## Flow 1: Create Generic Session

**Entry Point**: `/toolkit`

```
┌─────────────────────────────────────┐
│ /toolkit Landing Page               │
│                                     │
│ ┌─────────────────────────────┐   │
│ │ Create New Session          │   │
│ │                             │   │
│ │ Participants:               │   │
│ │ ┌─────────────────────┐    │   │
│ │ │ Player 1: Alice      │    │   │
│ │ └─────────────────────┘    │   │
│ │ ┌─────────────────────┐    │   │
│ │ │ Player 2: Bob        │    │   │
│ │ └─────────────────────┘    │   │
│ │                             │   │
│ │ [+ Add Player]              │   │
│ │                             │   │
│ │ [Create Session]            │   │
│ └─────────────────────────────┘   │
└─────────────────────────────────────┘
            ↓
┌─────────────────────────────────────┐
│ /toolkit/{sessionId}                │
│                                     │
│ ┌─────────────────────────────────┐│
│ │ 7 WONDERS • ABC123              ││
│ │ Active • 🟢 Connected            ││
│ └─────────────────────────────────┘│
│                                     │
│ Participants      │ Scoreboard      │
│ ┌──────────┐     │ ┌────────────┐ │
│ │ Alice    │     │ │ Round │ Sc │ │
│ │ 87 pts   │     │ ├────────────┤ │
│ │ Rank #1  │     │ │  1  │ 25  │ │
│ └──────────┘     │ │  2  │ 30  │ │
│                  │ │  3  │ 32  │ │
│ ┌──────────┐     │ └────────────┘ │
│ │ Bob      │     │                 │
│ │ 73 pts   │     │                 │
│ │ Rank #2  │     │                 │
│ └──────────┘     │                 │
│                                     │
│ ┌─────────────────────────────────┐│
│ │ Add Score                       ││
│ │ Player: [Alice ▼]               ││
│ │ Round: [3 ▼] Category: [Wond ▼]││
│ │ Score: [___] [Submit] 💾        ││
│ └─────────────────────────────────┘│
└─────────────────────────────────────┘
            ↓ (Finalize)
┌─────────────────────────────────────┐
│ Final Results                       │
│                                     │
│ 🥇 Alice - 87 points                │
│ 🥈 Bob - 73 points                  │
│                                     │
│ Redirecting to /toolkit...          │
└─────────────────────────────────────┘
```

**Steps**:
1. User navigates to `/toolkit`
2. Fills participant names (min 1)
3. Clicks "Create Session"
4. Backend creates session with 6-char code
5. Redirects to `/toolkit/{sessionId}`
6. SSE connection established
7. User adds scores → optimistic UI update
8. Backend broadcasts ScoreUpdatedEvent via SSE
9. Other participants see score in real-time
10. Owner finalizes session
11. Redirect to `/toolkit` with success toast

---

## Flow 2: Join Existing Session

**Entry Point**: `/toolkit`

```
┌─────────────────────────────────────┐
│ /toolkit Landing Page               │
│                                     │
│ ┌─────────────────────────────┐   │
│ │ Join Existing Session       │   │
│ │                             │   │
│ │ Session Code:               │   │
│ │ ┌─────────────────────┐    │   │
│ │ │ A B C 1 2 3          │    │   │
│ │ └─────────────────────┘    │   │
│ │ (6 characters)              │   │
│ │                             │   │
│ │ [Join Session]              │   │
│ └─────────────────────────────┘   │
└─────────────────────────────────────┘
            ↓
┌─────────────────────────────────────┐
│ Validation                          │
│                                     │
│ ✅ Code format valid                │
│ ✅ Session exists                   │
│ ✅ User added as participant        │
└─────────────────────────────────────┘
            ↓
     /toolkit/{sessionId}
     (Same active session view)
```

**Steps**:
1. Owner shares session code (displayed in SessionHeader)
2. Player navigates to `/toolkit`
3. Enters 6-character code (auto-uppercase)
4. Clicks "Join Session"
5. Backend validates code and adds player
6. Redirects to `/toolkit/{sessionId}`
7. SSE connection established
8. Player sees real-time scores from all participants

**Error Handling**:
- Invalid code format → Toast: "Session code must be 6 characters"
- Session not found (404) → Toast: "Session not found. Please check the code."
- Already joined → Redirects silently (idempotent)

---

## Flow 3: Game-Specific Session

**Entry Point**: `/library` (My Games)

```
┌─────────────────────────────────────┐
│ My Games Library                    │
│                                     │
│ ┌────────────────┐ ┌──────────────┐│
│ │ 🏛️              │ │ 💎           ││
│ │ 7 Wonders      │ │ Splendor     ││
│ │ [Toolkit] 🎲   │ │ [Toolkit] 🎲 ││
│ │ [Ask Agent]    │ │ [Ask Agent]  ││
│ └────────────────┘ └──────────────┘│
└─────────────────────────────────────┘
            ↓ (Click Toolkit)
┌─────────────────────────────────────┐
│ /library/games/{gameId}/toolkit     │
│                                     │
│ 🏛️ 7 Wonders Toolkit                │
│                                     │
│ ┌─────────────────────────────┐   │
│ │ Game Template               │   │
│ │                             │   │
│ │ Scoring Categories:         │   │
│ │ • Military  • Science       │   │
│ │ • Commerce  • Wonders       │   │
│ │ • Coins     • Guilds        │   │
│ │                             │   │
│ │ Rounds: 1, 2, 3             │   │
│ │                             │   │
│ │ Rules: Sum all categories   │   │
│ │ Players: 3-7                │   │
│ └─────────────────────────────┘   │
│                                     │
│ ┌─────────────────────────────┐   │
│ │ Start 7 Wonders Session     │   │
│ │                             │   │
│ │ [Player 1] [Player 2] [+]   │   │
│ │                             │   │
│ │ [Start Session] 🎮          │   │
│ └─────────────────────────────┘   │
└─────────────────────────────────────┘
            ↓
┌─────────────────────────────────────┐
│ /library/games/{gameId}/toolkit/... │
│                                     │
│ Score Input Pre-Filled:             │
│ ┌─────────────────────────────┐   │
│ │ Category: [Military ▼]      │   │
│ │           [Science  ]       │   │
│ │           [Commerce ]       │   │
│ │ Round: [1 ▼] [2] [3]        │   │
│ └─────────────────────────────┘   │
│                                     │
│ Sidebar: 📜 Scoring Rules           │
│ "Final score = sum of all           │
│  categories. Most points wins."     │
└─────────────────────────────────────┘
```

**Steps**:
1. User browses library (`/library`)
2. Clicks "Toolkit" button on game card
3. System fetches game details
4. Template lookup: `getGameTemplateByName(game.name)`
5. Displays template preview (categories, rounds, rules)
6. User adds participants (validated against template.playerCount)
7. Clicks "Start {GameName} Session"
8. Backend creates session with gameId + sessionType='GameSpecific'
9. Redirects to `/library/games/{gameId}/toolkit/{sessionId}`
10. ScoreInput auto-populated with template categories/rounds
11. Scoring rules displayed in sidebar
12. On finalize: Redirects to `/library/games/{gameId}` (game detail page)

---

## Flow 4: Real-Time Multi-User Session

**Scenario**: 2 users tracking scores simultaneously

```
User 1 (Owner)                    User 2 (Participant)
     │                                  │
     │ Creates session                  │
     │ Code: ABC123                     │
     │────────────────────────────────▶ │
     │                                  │ Joins via code
     │                                  │ /toolkit → Enter ABC123
     │                                  │
     ├─ SSE: /sessions/{id}/stream ────┤
     │                                  │
     │ Adds score: Alice +10            │
     │ Optimistic UI: Shows immediately │
     │                                  │
     ├─ POST /sessions/{id}/scores ────▶ Backend
     │                                  │
     │◀─────── ScoreUpdatedEvent ───────┤
     │ (via SSE)                        │ (via SSE)
     │                                  │
     │ UI confirms score                │ Toast: "Alice: +10"
     │                                  │ Scoreboard updates
     │                                  │
     │                                  │ Adds score: Bob +15
     │                                  │ Optimistic UI
     │                                  │
     │◀─────── ScoreUpdatedEvent ───────┤
     │ Toast: "Bob: +15"                │ UI confirms
     │ Scoreboard updates               │
     │                                  │
     │ Finalizes session                │
     ├─ PUT /sessions/{id}/finalize ───▶ Backend
     │                                  │
     │◀──── SessionFinalizedEvent ──────┤
     │ Toast: "Session finalized"       │ Toast: "Session finalized"
     │ Redirect → /toolkit              │ Redirect → /toolkit
```

**Key Interactions**:
- All participants maintain SSE connections
- Optimistic UI for score submitter (instant)
- SSE broadcast for other participants (10-100ms latency)
- Connection status indicator (🟢/🔴)
- Auto-reconnect on network interruption
- Toast notifications for all state changes

---

## Flow 5: Session History Review

**Entry Point**: `/toolkit/history`

```
┌─────────────────────────────────────┐
│ 📅 Session History                  │
│                                     │
│ Filters: [Game ▼] [Start] [End] [Reset] │
│                                     │
│ ┌────────────┐ ┌────────────┐     │
│ │ 🏛️          │ │ 💎          │     │
│ │ 7 Wonders  │ │ Splendor   │     │
│ │ ABC123     │ │ XYZ789     │     │
│ │ Jan 28     │ │ Jan 29     │     │
│ │ 4 players  │ │ 3 players  │     │
│ │            │ │            │     │
│ │ [Details]  │ │ [Details]  │     │
│ └────────────┘ └────────────┘     │
└─────────────────────────────────────┘
            ↓ (Click Details)
┌─────────────────────────────────────┐
│ Session Detail Modal                │
│                                     │
│ 🏛️ 7 Wonders - ABC123               │
│                                     │
│ Date: Jan 28, 2026 15:30           │
│ Participants: 4 players             │
│ Status: Finalized                   │
│                                     │
│ ┌─────────────────────────────┐   │
│ │ Final Scores (Scoreboard)   │   │
│ │                             │   │
│ │ Player │ R1 │ R2 │ R3 │ Tot │   │
│ │ Alice  │ 25 │ 30 │ 32 │ 87  │   │
│ │ Bob    │ 20 │ 28 │ 25 │ 73  │   │
│ └─────────────────────────────┘   │
│                                     │
│ Participants:                       │
│ 🥇 #1 Alice - 87 points             │
│ 🥈 #2 Bob - 73 points               │
│                                     │
│ [Close]                             │
└─────────────────────────────────────┘
```

**Steps**:
1. User navigates to `/toolkit/history`
2. Views list of finalized sessions
3. Applies filters (optional):
   - Game dropdown
   - Start date
   - End date
4. Clicks "View Details" on session card
5. Modal opens with:
   - Full scoreboard
   - Ranked participants
   - Session metadata
6. User closes modal or browses other sessions

**Filters Behavior** (Phase 2):
- Game filter: Shows only sessions for selected game
- Date range: Filters by session date
- Reset: Clears all filters
- Empty state: "No sessions found" with CTA to create new session

---

## Flow 6: Error Recovery

### Scenario A: Network Disconnection During Session

```
Active Session (/toolkit/{sessionId})
  │
  │ User adds score
  │ Optimistic UI: ✅ Score visible immediately
  │
  ├─ API Request fails (network error)
  │
  │ Optimistic update REVERTED
  │ Toast: ❌ "Failed to update score"
  │ syncStatus: 'error'
  │
  │ User retries
  │ Score resubmitted
  │
  ├─ API Success
  │ Toast: ✅ "Score synced"
  │ syncStatus: 'synced' → 'idle'
```

### Scenario B: SSE Connection Lost

```
SSE Stream Active
  │
  ├─ Connection interrupted (network/server restart)
  │
  │ isConnected: false
  │ Connection indicator: 🔴 Disconnected
  │
  │ Auto-reconnect attempt 1 (1s delay)
  ├─ Failed
  │
  │ Auto-reconnect attempt 2 (2s delay)
  ├─ Failed
  │
  │ Auto-reconnect attempt 3 (4s delay)
  ├─ Success ✅
  │
  │ isConnected: true
  │ Connection indicator: 🟢 Connected
  │ Missed events fetched (backend sends snapshot)
```

### Scenario C: Invalid Session Code

```
/toolkit (Join Session)
  │
  │ User enters: "ABC12" (5 chars)
  │ Join button: DISABLED (validation)
  │
  │ User enters: "ABC123" (6 chars)
  │ Join button: ENABLED
  │
  │ Clicks "Join Session"
  ├─ POST /sessions/join/ABC123
  │
  ├─ 404 Not Found
  │
  │ Toast: ❌ "Session not found. Please check the code."
  │ User remains on /toolkit landing
```

---

## Flow 7: Mobile Responsive Behavior

### Desktop (≥1024px)

```
┌────────────────────────────────────────┐
│ SessionHeader (full width)            │
├────────────────┬───────────────────────┤
│ Participants   │ Scoreboard            │
│ (1 column)     │ (2 columns)           │
│                │                       │
│ ParticipantCard│ ┌──────────────────┐ │
│ ParticipantCard│ │ Score Table      │ │
│ ParticipantCard│ │                  │ │
│                │ └──────────────────┘ │
├────────────────┴───────────────────────┤
│ ScoreInput (sticky bottom, full width) │
└────────────────────────────────────────┘
```

### Mobile (≤768px)

```
┌──────────────────┐
│ SessionHeader    │
│ (compact)        │
├──────────────────┤
│ Scoreboard       │
│ (single column)  │
│                  │
│ ┌──────────────┐│
│ │ Alice - 87   ││
│ │ Bob - 73     ││
│ └──────────────┘│
│                  │
│ Participants     │
│ (cards stacked)  │
│                  │
│ ParticipantCard  │
│ ParticipantCard  │
├──────────────────┤
│ ScoreInput       │
│ (sticky bottom)  │
│ [Add Score]      │
└──────────────────┘
```

**Responsive Breakpoints**:
- Mobile: < 640px (single column)
- Tablet: 640-1024px (2 columns)
- Desktop: ≥1024px (3 columns)

---

## Flow 8: Dark Mode

**Trigger**: System preference or manual toggle

**Visual Changes**:
- Background: Gradient blue/purple → Dark gray gradients
- Cards: White → Dark gray (#1f2937)
- Text: Dark gray → Light gray/white
- Borders: Light gray → Dark borders with opacity
- Badges: Adjusted color schemes for contrast
- Connection indicator: Green/Red maintains visibility

**Accessibility**: All color combinations maintain WCAG 2.1 AA contrast ratios

---

## Edge Cases & Error States

### Empty States

1. **No Participants**: "Please add at least one participant"
2. **No Sessions**: "No sessions found. Start your first session above!"
3. **No Template**: Falls back to generic session
4. **No Scoreboard Data**: Shows empty table with headers

### Validation Errors

1. **Player Count**: "{Game} requires at least {min} players"
2. **Max Players**: "{Game} supports maximum {max} players"
3. **Invalid Score**: "Score must be a valid number"
4. **Session Not Found**: Redirect to `/toolkit` with error toast

### Network Failures

1. **API Timeout**: Optimistic update reverts, retry button shown
2. **SSE Disconnect**: Auto-reconnect (max 5 attempts)
3. **Connection Lost**: Offline banner, scores queue for sync

---

## Performance Characteristics

### Perceived Latency

- **Score Submission**: 0ms (optimistic UI)
- **Real-Time Update**: 10-100ms (SSE latency)
- **Page Load**: < 1s (Next.js 14 optimizations)
- **Modal Open**: < 50ms (Radix UI animations)

### Resource Usage

- **SSE Connection**: 1 per active session (auto-closed on unmount)
- **Memory**: ~2MB per session (50 participants, 200 scores)
- **Network**: 1-5 KB/score update event

---

## User Guidance

### Best Practices

1. **Share Code Early**: Display session code prominently for participants to join
2. **Finalize Sessions**: Always finalize to save to history
3. **Use Templates**: Leverage pre-configured templates for faster setup
4. **Check Connection**: Green indicator = real-time sync active

### Common Questions

**Q**: Why didn't my score sync?
**A**: Check connection indicator (🟢). If red, scores queue and sync on reconnection.

**Q**: Can I edit past scores?
**A**: Not yet - scores are immutable once submitted (planned Phase 2).

**Q**: How do I find my session code?
**A**: Displayed at top of active session page in SessionHeader.

**Q**: Can players join mid-session?
**A**: Yes - enter session code on `/toolkit` landing page.

---

**Epic**: EPIC-GST-001
**Issues**: #3163, #3164, #3165
**Status**: Production Ready
