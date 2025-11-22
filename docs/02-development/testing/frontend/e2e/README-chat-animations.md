# Chat Animations E2E Tests (CHAT-04)

Comprehensive end-to-end tests for chat loading states and animations.

## Test File

**Location**: `apps/web/e2e/chat-animations.spec.ts`

## Test Coverage

### 16 Comprehensive Tests

1. **Skeleton Loaders on Initial Page Load** - Verifies skeleton loaders appear during data fetching and disappear when content loads
2. **Games Skeleton Loader Variant** - Tests games skeleton structure, accessibility attributes, and timing
3. **Agents Skeleton Loader** - Verifies agents skeleton appears when game is selected
4. **Chat History Skeleton Loader** - Tests chat list skeleton during chat loading
5. **Messages Skeleton Loader** - Verifies message skeleton when loading chat history
6. **Typing Indicator During Streaming** - Tests typing indicator with 3 animated dots and agent name
7. **User Message Animation (Right)** - Verifies user messages slide in from right
8. **AI Message Animation (Left)** - Verifies AI messages slide in from left
9. **Stagger Animation** - Tests sequential message appearance with stagger delay
10. **Send Button Loading State** - Verifies send button shows spinner and "Invio..." text
11. **New Chat Button Loading State** - Tests new chat button shows "Creazione..." with spinner
12. **Smooth Scroll to Latest** - Verifies smooth scroll behavior to new messages
13. **Reduced Motion Accessibility** - Tests animations respect `prefers-reduced-motion`
14. **Animation Performance (60fps)** - Measures FPS during animations (>45fps threshold)
15. **Multiple Skeleton Items** - Verifies skeleton count rendering works correctly
16. **Stop Button During Streaming** - Tests stop button appearance and functionality

## Running Tests

### Run All Animation Tests

```bash
cd apps/web
pnpm test:e2e e2e/chat-animations.spec.ts
```

### Run Specific Test

```bash
# By test name
pnpm test:e2e -g "typing indicator"

# By line number
pnpm test:e2e e2e/chat-animations.spec.ts:265
```

### Run with UI Mode

```bash
pnpm test:e2e:ui e2e/chat-animations.spec.ts
```

### Run in Headed Mode (Watch Animations)

```bash
npx playwright test e2e/chat-animations.spec.ts --headed --workers=1
```

## Test Patterns

### Authentication

All tests use the helper function:

```typescript
async function loginAsTestUser(page: Page): Promise<void> {
  await page.goto('/');
  await page.fill('input[type="email"]', 'user@meepleai.dev');
  await page.fill('input[type="password"]', 'Demo123!');
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL('/');
}
```

### Skeleton Loader Testing

Tests verify:
- `role="status"` and `aria-live="polite"` attributes
- `aria-label` with descriptive text
- Skeleton disappears when real content loads
- Correct variant structure (games, agents, chatHistory, message)

### Animation Testing

Tests use:
- `data-message-id` attribute for message identification
- `data-animation-complete="true"` for animation completion tracking
- `aria-busy="true"` for loading button states
- `getBoundingClientRect()` for scroll verification

### Performance Testing

FPS measurement using `requestAnimationFrame`:

```typescript
async function measureFPS(page: Page, durationMs: number): Promise<number> {
  return await page.evaluate((duration) => {
    return new Promise<number>((resolve) => {
      const frameTimestamps: number[] = [];
      const startTime = performance.now();

      function recordFrame(timestamp: number) {
        frameTimestamps.push(timestamp);
        if (timestamp - startTime < duration) {
          requestAnimationFrame(recordFrame);
        } else {
          const totalDuration = frameTimestamps[frameTimestamps.length - 1] - frameTimestamps[0];
          const fps = (frameTimestamps.length / totalDuration) * 1000;
          resolve(fps);
        }
      }

      requestAnimationFrame(recordFrame);
    });
  }, durationMs);
}
```

## Selectors Reference

### Skeleton Loaders

```typescript
'[aria-label="Caricamento giochi"]'      // Games skeleton
'[aria-label="Caricamento agenti"]'      // Agents skeleton
'[aria-label="Caricamento cronologia chat"]' // Chat history skeleton
'[aria-label="Caricamento messaggi"]'    // Messages skeleton
```

### Typing Indicator

```typescript
'[aria-label*="is typing"]'              // English
'[aria-label*="sta scrivendo"]'          // Italian
'text=Sto pensando...'                   // Fallback state
```

### Messages

```typescript
'[data-message-id]'                      // Message containers
'[data-animation-complete="true"]'       // Completed animations
```

### Loading Buttons

```typescript
'button[aria-busy="true"]'               // Loading state
'button:has-text("Invio...")'            // Send button loading
'button:has-text("Creazione...")'        // New chat button loading
'svg[aria-hidden="true"]'                // Spinner icon
```

### Other Elements

```typescript
'button[aria-label="Stop streaming"]'    // Stop button
'[role="region"][aria-label="Chat messages"]' // Messages container
```

## Expected Behaviors

### Skeleton Loader Lifecycle

1. **Initial render**: Skeleton appears with `role="status"`
2. **Loading**: Skeleton visible with pulse animation (if not reduced motion)
3. **Content loaded**: Skeleton disappears, real content appears
4. **Duration**: Should complete within 5 seconds

### Message Animation Lifecycle

1. **User message**: Slides from right (x: 20), right-aligned
2. **AI message**: Slides from left (x: -20), left-aligned
3. **Animation duration**: ~300ms spring animation
4. **Stagger delay**: 50ms between messages
5. **Completion**: `data-animation-complete="true"` attribute set

### Loading Button States

1. **Normal**: Enabled, shows button text
2. **Loading**: `aria-busy="true"`, disabled, shows spinner + loading text
3. **Complete**: Returns to normal state

### Typing Indicator

1. **Appears**: When `isStreaming && !currentAnswer`
2. **Shows**: Agent name + 3 bouncing dots
3. **Animation**: Each dot bounces with 0.1s stagger delay
4. **Disappears**: When answer starts appearing

## Accessibility Compliance

All tests verify:

- ✅ ARIA attributes (`role`, `aria-label`, `aria-live`, `aria-busy`)
- ✅ Screen reader compatibility
- ✅ Keyboard navigation support
- ✅ Reduced motion preference respect
- ✅ Color contrast (via visual inspection)

## Performance Thresholds

- **FPS**: >45fps during animations (relaxed for CI, ideal: 60fps)
- **Animation duration**: ≤300ms for message slide-in
- **Stagger delay**: 50ms per message
- **Skeleton timeout**: 5 seconds max

## Troubleshooting

### Flaky Tests

Some tests may be flaky due to timing:

1. **Skeleton loaders**: May load too fast to be visible
   - Solution: Tests handle with conditional checks

2. **Animation measurements**: May vary in CI environments
   - Solution: Relaxed thresholds (45fps vs 60fps)

3. **Network delays**: Tests use route interception with delays
   - Solution: Consistent 2-second delays for visibility

### CI Failures

If tests fail in CI:

1. Check screenshots in artifacts
2. Review test video recordings
3. Verify Playwright version matches local
4. Check for timing issues (increase timeouts if needed)

## Future Enhancements

Potential additions:

- [ ] Visual regression tests with screenshots
- [ ] Lighthouse performance audit integration
- [ ] Cross-browser testing (Firefox, Safari)
- [ ] Mobile viewport testing
- [ ] Network throttling scenarios
- [ ] Animation replay/pause testing

## Related Documentation

- **CHAT-04 Implementation**: `docs/issue/chat-04-loading-states-animations.md`
- **Component Documentation**: `apps/web/src/components/loading/`
- **Animation Library**: `apps/web/src/lib/animations/`
- **Existing Tests**: `apps/web/e2e/chat.spec.ts`, `apps/web/e2e/chat-streaming.spec.ts`
