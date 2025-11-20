# Improve Streaming Test Stability

**Issue ID**: E2E-009 | **Priorità**: 🟡 MEDIA | **Effort**: 6-8 ore

---

## 📋 Problem

Test streaming SSE (Server-Sent Events) in `chat-streaming.spec.ts` hanno **race conditions** e **timing issues**.

```typescript
// ❌ PROBLEMATIC: Race condition
await page.waitForTimeout(200); // Speriamo streaming inizi
const isStreaming = await stopButton.isVisible().catch(() => false);

// ❌ PROBLEMATIC: Hardcoded timing
await page.waitForTimeout(500); // Aspettiamo risposta completi
```

**Flakiness**: ~10-15% fail rate in CI

---

## 🎯 Impact

- **Reliability**: Test fail randomicamente
- **CI Time**: Retry aumenta tempo CI
- **Confidence**: Team ignora fail (cry wolf syndrome)

---

## ✅ Solution

### Pattern 1: Wait for SSE Events

```typescript
// ✅ BETTER: Wait for specific SSE event
await page.waitForResponse(resp =>
  resp.url().includes('/stream') &&
  resp.status() === 200
);

// Then check UI state
const streamingIndicator = page.getByTestId('streaming-indicator');
await streamingIndicator.waitFor({ state: 'visible', timeout: 5000 });
```

### Pattern 2: Polling with Timeout

```typescript
// ✅ BETTER: Poll for streaming state
await page.waitForFunction(
  () => {
    const indicator = document.querySelector('[data-streaming="true"]');
    return indicator !== null;
  },
  { timeout: 5000 }
);
```

### Pattern 3: Controlled SSE Mock

```typescript
// ✅ BETTER: Deterministic SSE response
await page.route('**/stream', async route => {
  const events = [
    'event: start\ndata: {}\n\n',
    'event: token\ndata: {"token":"Test"}\n\n',
    'event: complete\ndata: {}\n\n'
  ];

  // Send events with small delay
  const stream = events.join('');
  await route.fulfill({
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
    body: stream
  });
});
```

---

## 📝 Checklist

- [ ] Replace waitForTimeout in streaming tests (8 occurrences)
- [ ] Add waitForResponse for SSE requests
- [ ] Add data-streaming attribute to UI
- [ ] Implement deterministic SSE mocks
- [ ] Run tests 20x to verify stability
- [ ] Target: >95% pass rate

---

**Target**: 85% → >95% success rate
