# Live Session Feed — Performance Budget

**Issue**: #218
**Status**: Draft
**Date**: 2026-03-13

## Overview

Performance requirements for the live game session activity feed. A session with 6 players generating dice rolls, notes, and photos can produce significant event throughput.

## Performance Budget

| Metric | Target | Measurement Method | Alert Threshold |
|--------|--------|-------------------|-----------------|
| Feed render latency | <100ms per event batch | `performance.mark()` around render | >200ms |
| Max client throughput | 20 events/sec | RAF batching, drop counter | >30 events/sec unbatched |
| Client memory (events) | Last 500 events (~2MB) | Ring buffer, `performance.memory` | >5MB event data |
| Initial feed load | <1s for 50 events | Navigation timing API | >2s |
| SSE reconnect to streaming | <3s | Custom timing from drop to first event | >5s |
| Feed scroll FPS | 60fps sustained | `requestAnimationFrame` timing | <30fps |
| Event list DOM nodes | ≤100 visible | Virtual list windowing | >200 DOM nodes |
| Bundle size (feed module) | <15KB gzipped | Build analyzer | >25KB gzipped |

## Implementation Requirements

### 1. RAF Event Batching

Batch incoming SSE events per animation frame to prevent render thrashing:

```
SSE events → Buffer[] → requestAnimationFrame → Batch render
```

- Buffer all events received between frames
- Render one batch per frame (max 20 events)
- If buffer exceeds 20, render first 20 and carry remainder

### 2. Virtualized Feed List

Use `react-window` (or `@tanstack/virtual`) when event count exceeds 100:

| Events | Strategy |
|--------|----------|
| 0-100 | Standard DOM rendering |
| 100-500 | Virtualized list (only visible + overscan rendered) |
| 500+ | Oldest events evicted from client, paginate from server |

**Overscan**: 5 items above/below viewport for smooth scrolling.

### 3. Client-Side Event Ring Buffer

```
Capacity: 500 events
Eviction: FIFO (oldest removed when full)
Memory estimate: ~4KB per event × 500 = ~2MB max
Persistence: None (events are server-authoritative)
```

### 4. Skeleton Loading

Initial feed load shows skeleton placeholders:

| State | Duration | Display |
|-------|----------|---------|
| Loading | 0-1s | 5 skeleton event cards |
| Loaded | — | Real events replace skeletons |
| Empty | — | "No activity yet" empty state |
| Error | — | Retry button with error message |

## Load Test Scenarios

### Scenario 1: Normal game (baseline)

```
Players: 4
Event rate: 2-3 events/min
Duration: 90 min
Total events: ~200
Expected: No performance issues
```

### Scenario 2: Active game (target)

```
Players: 6
Event rate: 10-15 events/min (dice + notes + photos)
Duration: 120 min
Total events: ~1500
Expected: Virtualization active, older events paginated
```

### Scenario 3: Stress test (burst)

```
Simulated: 100 events in 5 seconds (20 events/sec)
Expected:
  - RAF batching throttles renders to ≤60/sec
  - No frame drops (FPS stays ≥55)
  - All events eventually rendered (no data loss)
  - Memory stays under 5MB
```

## Testing

### Vitest Performance Test

```typescript
it('renders 100 events in 5 seconds without frame drops', async () => {
  const events = generateMockEvents(100);
  const { rerender } = render(<SessionFeed events={[]} />);

  const start = performance.now();
  for (let i = 0; i < 100; i++) {
    rerender(<SessionFeed events={events.slice(0, i + 1)} />);
  }
  const elapsed = performance.now() - start;

  expect(elapsed).toBeLessThan(5000);
});
```

### Playwright Performance Test

```typescript
test('feed maintains 60fps during rapid updates', async ({ page }) => {
  await page.goto('/game-night/test-session');

  // Inject rapid events via SSE mock
  const metrics = await page.evaluate(() => {
    const frames: number[] = [];
    let last = performance.now();

    return new Promise(resolve => {
      const measure = () => {
        const now = performance.now();
        frames.push(now - last);
        last = now;
        if (frames.length < 300) requestAnimationFrame(measure);
        else resolve({ avgFrameTime: frames.reduce((a, b) => a + b) / frames.length });
      };
      requestAnimationFrame(measure);
    });
  });

  expect(metrics.avgFrameTime).toBeLessThan(20); // ~50fps minimum
});
```

## Monitoring Dashboard

Track in production:

| Metric | Collection | Dashboard |
|--------|-----------|-----------|
| Feed render p95 | `performance.measure()` → analytics | Grafana |
| Event delivery latency | Server timestamp vs client receive | Grafana |
| Client event buffer size | Periodic sampling | HyperDX |
| SSE reconnection count | Client counter | HyperDX |
| Virtual list scroll FPS | `requestAnimationFrame` timing | Dev-only |
