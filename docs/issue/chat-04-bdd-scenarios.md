# CHAT-04: BDD Scenarios - Polish Loading States and Animations

**Epic**: EPIC-03 (Chat Interface)
**Issue**: #400
**Priority**: Medium
**Effort**: M (1 week)

## Architecture Decision

**Approach**: Hybrid CSS + Framer Motion
- **CSS**: Simple transitions (skeleton loaders, button states, hover effects)
- **Framer Motion**: Complex orchestration (message stagger, typing indicator, exit animations)

**Rationale**: Balances performance (CSS on compositor thread) with orchestration power (Framer Motion AnimatePresence) while leveraging existing dependencies.

---

## Feature 1: Skeleton Loading States

### Scenario 1.1: Games List Skeleton Loader
```gherkin
Feature: Skeleton Loading States
  As a user
  I want to see skeleton screens while content loads
  So that I perceive the app as faster and more responsive

Scenario: Display skeleton while games list loads
  Given I navigate to the chat page
  When the page starts loading
  Then I should see 3 skeleton game cards with shimmer animation
  And the skeleton cards should match the layout of real game cards
  And a screen reader should announce "Loading games"

  When the games data finishes loading
  Then the skeleton cards should disappear
  And the real game cards should fade in smoothly
  And a screen reader should announce "Games loaded"
```

**Acceptance Criteria**:
- ✅ Skeleton cards shown during `isLoadingGames === true`
- ✅ Shimmer animation (CSS `animate-pulse`)
- ✅ ARIA live region: `role="status" aria-live="polite"`
- ✅ Respects `prefers-reduced-motion` (no animation if set)
- ✅ Smooth transition to real content (fade-in 300ms)

**Implementation**:
- Component: `<SkeletonLoader variant="games" count={3} />`
- CSS: Tailwind `animate-pulse` class
- Accessibility: `useReducedMotion()` hook disables animation

---

### Scenario 1.2: Agents List Skeleton Loader
```gherkin
Scenario: Display skeleton while agents list loads
  Given I am on the chat page
  And games have finished loading
  When I select a game from the dropdown
  Then I should see a skeleton loader for the agents list
  And the skeleton should have a single item placeholder
  And a screen reader should announce "Loading agents"

  When the agents data finishes loading
  Then the skeleton should disappear
  And the agents dropdown should populate with options
  And a screen reader should announce "Agents loaded"
```

**Acceptance Criteria**:
- ✅ Skeleton shown during `isLoadingAgents === true`
- ✅ Single item skeleton (agents typically 1-3 items)
- ✅ ARIA announcements for state changes
- ✅ Smooth transition to real dropdown content

**Implementation**:
- Component: `<SkeletonLoader variant="agents" count={1} />`
- Integration: Triggered by game selection

---

### Scenario 1.3: Chat History Skeleton Loader
```gherkin
Scenario: Display skeleton while chat history loads
  Given I am on the chat page
  And I have selected a game and agent
  When the chat history starts loading
  Then I should see 5 skeleton chat items in the sidebar
  And each skeleton should resemble a chat list item
  And a screen reader should announce "Loading chat history"

  When the chat history finishes loading
  Then the skeletons should disappear
  And the real chat list items should fade in with a slight stagger (50ms delay each)
  And a screen reader should announce "Chat history loaded"
```

**Acceptance Criteria**:
- ✅ 5 skeleton items during `isLoadingChats === true`
- ✅ Skeleton mimics chat item layout (title + date)
- ✅ Stagger animation on real content (Framer Motion `staggerChildren: 0.05`)
- ✅ ARIA live announcements

**Implementation**:
- Component: `<SkeletonLoader variant="chatHistory" count={5} />`
- Framer Motion: `staggerChildren` for chat list items

---

### Scenario 1.4: Messages Skeleton Loader
```gherkin
Scenario: Display skeleton while messages load
  Given I am on the chat page
  And I have selected a chat from the history
  When the messages for that chat start loading
  Then I should see 3 skeleton message bubbles
  And the skeletons should alternate between left (AI) and right (user) alignment
  And a screen reader should announce "Loading messages"

  When the messages finish loading
  Then the skeletons should disappear
  And the real messages should slide in from their respective sides (AI: left, User: right)
  And the animation should take 300ms with ease-out timing
  And a screen reader should announce "Messages loaded"
```

**Acceptance Criteria**:
- ✅ 3 skeleton bubbles during `isLoadingMessages === true`
- ✅ Alternating alignment (AI left, User right)
- ✅ Slide-in animation (Framer Motion `initial={{ x: direction === 'left' ? -20 : 20 }}`)
- ✅ 300ms duration with ease-out

**Implementation**:
- Component: `<SkeletonLoader variant="message" count={3} />`
- Animation: `<MessageAnimator direction={msg.role === 'user' ? 'right' : 'left'}>`

---

## Feature 2: Typing Indicator

### Scenario 2.1: AI Typing Indicator During Streaming
```gherkin
Feature: Typing Indicator
  As a user
  I want to see a visual indicator when the AI is generating a response
  So that I know the system is working

Scenario: Display typing indicator when AI is generating
  Given I am in an active chat
  When I send a message by clicking the send button
  Then I should see a typing indicator with 3 bouncing dots
  And the indicator should display "MeepleAI is typing"
  And the dots should animate with a spring physics effect
  And each dot should stagger by 100ms
  And a screen reader should announce "MeepleAI is typing"

  When the AI starts streaming tokens
  Then the typing indicator should remain visible

  When the streaming completes
  Then the typing indicator should fade out smoothly (200ms)
  And the full AI response should appear as a message bubble
```

**Acceptance Criteria**:
- ✅ Typing indicator visible when `streamingState.isStreaming === true`
- ✅ 3 dots with spring animation (Framer Motion physics)
- ✅ Stagger: 100ms delay between dots
- ✅ Fade in/out with AnimatePresence (200ms transition)
- ✅ ARIA live region announces typing state

**Implementation**:
- Component: `<TypingIndicator visible={streamingState.isStreaming} agentName="MeepleAI" />`
- Framer Motion: Spring physics (`type: 'spring', stiffness: 500, damping: 30`)
- Animation: `variants.dotBounce` with `staggerChildren: 0.1`

---

### Scenario 2.2: Typing Indicator with Reduced Motion
```gherkin
Scenario: Typing indicator respects reduced motion preference
  Given I have enabled "Reduce motion" in my system settings
  And I am in an active chat
  When I send a message
  Then I should see the typing indicator text "MeepleAI is typing"
  But the dots should not animate (static)
  And a screen reader should still announce "MeepleAI is typing"

  When the streaming completes
  Then the typing indicator should disappear instantly (no fade animation)
```

**Acceptance Criteria**:
- ✅ `useReducedMotion()` hook detects preference
- ✅ No dot animation when `prefers-reduced-motion: reduce`
- ✅ Instant appearance/disappearance (no transitions)
- ✅ ARIA announcements still functional

**Implementation**:
- Hook: `const shouldReduceMotion = useReducedMotion();`
- Conditional: `variants={shouldReduceMotion ? {} : dotVariants}`

---

## Feature 3: Message Animations

### Scenario 3.1: New Message Fade-In Animation
```gherkin
Feature: Message Animations
  As a user
  I want smooth animations when messages appear
  So that the chat feels polished and professional

Scenario: User message slides in from the right
  Given I am in an active chat
  When I send a new message
  Then the message bubble should appear from the right side
  And it should fade in from 0 to 100% opacity over 300ms
  And it should slide horizontally from +20px to 0px
  And it should scale from 95% to 100%
  And the animation should use ease-out timing

Scenario: AI message slides in from the left
  Given I am in an active chat
  When the AI sends a response
  Then the message bubble should appear from the left side
  And it should fade in from 0 to 100% opacity over 300ms
  And it should slide horizontally from -20px to 0px
  And it should scale from 95% to 100%
  And the animation should use ease-out timing
```

**Acceptance Criteria**:
- ✅ User messages: `initial={{ opacity: 0, x: 20, scale: 0.95 }}`
- ✅ AI messages: `initial={{ opacity: 0, x: -20, scale: 0.95 }}`
- ✅ Animation: `animate={{ opacity: 1, x: 0, scale: 1 }}`
- ✅ Duration: 300ms with ease-out easing
- ✅ Framer Motion spring transition (`stiffness: 500, damping: 30`)

**Implementation**:
- Component: `<MessageAnimator direction={msg.role === 'user' ? 'right' : 'left'}>`
- Variants: `VARIANTS.slideIn` from `@/lib/animations/variants.ts`

---

### Scenario 3.2: Multiple Messages Stagger Animation
```gherkin
Scenario: Chat history messages stagger in
  Given I navigate to the chat page
  And I select a chat with 10 messages
  When the messages load
  Then the first message should animate in immediately
  And each subsequent message should animate in with a 50ms delay
  And the stagger effect should create a wave from top to bottom
  And all messages should complete animation within 1 second total
```

**Acceptance Criteria**:
- ✅ Stagger delay: 50ms per message
- ✅ Framer Motion `staggerChildren: 0.05` on parent container
- ✅ Total animation time: ~500ms (10 messages * 50ms + 300ms animation)
- ✅ AnimatePresence with `mode="popLayout"`

**Implementation**:
```tsx
<AnimatePresence mode="popLayout">
  <motion.div variants={VARIANTS.staggerContainer}>
    {messages.map((msg, i) => (
      <MessageAnimator key={msg.id} delay={i * 0.05}>
        <MessageBubble {...msg} />
      </MessageAnimator>
    ))}
  </motion.div>
</AnimatePresence>
```

---

### Scenario 3.3: Smooth Scroll to Latest Message
```gherkin
Scenario: Auto-scroll to new message
  Given I am in an active chat with many messages
  And the chat container is scrollable
  When a new AI response arrives
  Then the chat should smoothly scroll to the bottom
  And the scroll animation should take 400ms
  And the scroll should use smooth easing (ease-in-out)
  And the new message should be fully visible after scrolling
```

**Acceptance Criteria**:
- ✅ Native `scrollIntoView({ behavior: 'smooth', block: 'end' })`
- ✅ Triggered on new message addition
- ✅ Scroll duration: ~400ms (browser default for smooth scroll)
- ✅ Message fully visible in viewport

**Implementation**:
```typescript
useEffect(() => {
  if (messages.length > 0) {
    const lastMessage = messagesEndRef.current;
    lastMessage?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }
}, [messages]);
```

---

## Feature 4: Interactive Element Animations

### Scenario 4.1: Send Button Pulse on Hover
```gherkin
Feature: Interactive Element Animations
  As a user
  I want visual feedback when I hover over interactive elements
  So that I know they are clickable

Scenario: Send button pulses on hover
  Given I am on the chat page
  When I hover over the send button
  Then the button should scale up to 105% size
  And the transition should take 200ms
  And the button should have a subtle shadow
  And the cursor should change to pointer

  When I move my mouse away
  Then the button should scale back to 100%
  And the transition should take 200ms
```

**Acceptance Criteria**:
- ✅ CSS transition: `transform: scale(1.05)` on hover
- ✅ Duration: 200ms with ease-out
- ✅ Shadow: `hover:shadow-lg`
- ✅ Cursor: `cursor-pointer`
- ✅ Respects `prefers-reduced-motion` (no scale if reduced)

**Implementation**:
```tsx
<button
  className="px-4 py-2 bg-blue-600 text-white rounded
             transition-all duration-200 ease-out
             hover:bg-blue-700 hover:shadow-lg
             motion-safe:hover:scale-105
             focus:ring-2 focus:ring-blue-500"
>
  Send
</button>
```

---

### Scenario 4.2: Input Field Focus Animation
```gherkin
Scenario: Input field border color on focus
  Given I am on the chat page
  When I click on the message input field
  Then the border color should transition from gray to blue
  And the transition should take 150ms
  And a 2px focus ring should appear
  And the focus ring should be blue (#3b82f6)

  When I click outside the input field
  Then the border should transition back to gray
  And the focus ring should disappear
  And both transitions should take 150ms
```

**Acceptance Criteria**:
- ✅ CSS transition: `border-color`, `box-shadow` (150ms)
- ✅ Focus state: `focus:border-blue-500 focus:ring-2 focus:ring-blue-500`
- ✅ Respects `prefers-reduced-motion`

**Implementation**:
```tsx
<input
  className="w-full px-4 py-2 border border-gray-300 rounded
             transition-colors duration-150 ease-out
             focus:border-blue-500 focus:ring-2 focus:ring-blue-500 focus:outline-none"
/>
```

---

### Scenario 4.3: Loading Button State
```gherkin
Scenario: Send button loading state
  Given I am on the chat page
  And I have typed a message
  When I click the send button
  Then the button should show a loading spinner
  And the button text should change to "Sending..."
  And the button should be disabled
  And the button opacity should reduce to 70%
  And the cursor should change to "not-allowed"

  When the message is successfully sent
  Then the spinner should disappear
  And the button text should change back to "Send"
  And the button should be enabled
  And the opacity should return to 100%
  And the cursor should change back to "pointer"
```

**Acceptance Criteria**:
- ✅ `<LoadingButton isLoading={isSending}>`
- ✅ Spinner component visible when loading
- ✅ Disabled state: `disabled={isLoading}`
- ✅ Opacity: `className={isLoading && 'opacity-70 cursor-wait'}`
- ✅ ARIA: `aria-busy={isLoading}`

**Implementation**:
- Component: `<LoadingButton isLoading={isSendingMessage} loadingText="Sending...">`
- CSS: Tailwind classes for opacity/cursor transitions

---

## Feature 5: Performance & Accessibility

### Scenario 5.1: 60fps Animation Performance
```gherkin
Feature: Performance & Accessibility
  As a user
  I want animations to run smoothly at 60fps
  So that the interface feels responsive

Scenario: Animations maintain 60fps during message streaming
  Given I am on the chat page
  And I send a message that triggers a long AI response
  When the AI is streaming tokens (50+ tokens)
  And multiple animations are active (typing indicator, message fade-in, scroll)
  Then the frame rate should stay at or above 55 fps (p95)
  And there should be no visible jank or stuttering
  And the Chrome DevTools Performance tab should show <16.67ms frame times
```

**Acceptance Criteria**:
- ✅ p95 FPS ≥ 55 (measured with `requestAnimationFrame`)
- ✅ Frame budget: <16.67ms per frame
- ✅ CSS animations use `transform` and `opacity` only (GPU-accelerated)
- ✅ Framer Motion optimized for performance (minimal re-renders)

**Testing**:
- E2E test: Performance profiling during streaming
- Playwright: Record FPS data via `page.evaluate()`

---

### Scenario 5.2: Reduced Motion Accessibility
```gherkin
Scenario: All animations disabled with reduced motion
  Given I have enabled "Reduce motion" in my system settings
  And I navigate to the chat page
  When I interact with the interface
  Then all CSS animations should be disabled
  And all Framer Motion animations should be disabled
  And content should appear/disappear instantly (no transitions)
  And skeleton loaders should not pulse
  And the typing indicator dots should not bounce
  And messages should not slide in
  And buttons should not scale on hover
  But all functionality should still work correctly
  And screen reader announcements should still occur
```

**Acceptance Criteria**:
- ✅ CSS: `@media (prefers-reduced-motion: reduce)` disables all keyframes
- ✅ Framer Motion: `useReducedMotion()` hook returns `true`
- ✅ All components respect reduced motion preference
- ✅ Zero animation duration when reduced motion enabled
- ✅ ARIA live regions unaffected (still announce)

**Implementation**:
```css
@media (prefers-reduced-motion: reduce) {
  .animate-pulse {
    animation: none;
  }

  * {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
```

```typescript
const shouldReduceMotion = useReducedMotion();
const variants = shouldReduceMotion ? noMotionVariants : animationVariants;
```

---

### Scenario 5.3: ARIA Live Regions for Loading States
```gherkin
Scenario: Screen reader announcements for loading states
  Given I am using a screen reader (NVDA, JAWS, or VoiceOver)
  And I navigate to the chat page
  When the games list starts loading
  Then the screen reader should announce "Loading games"

  When the games finish loading
  Then the screen reader should announce "Games loaded"

  When I send a message
  Then the screen reader should announce "Sending message"

  When the AI starts responding
  Then the screen reader should announce "MeepleAI is typing"

  When the AI finishes responding
  Then the screen reader should announce "Message received"
```

**Acceptance Criteria**:
- ✅ ARIA live regions: `role="status" aria-live="polite"`
- ✅ Assertive announcements for errors: `role="alert" aria-live="assertive"`
- ✅ Hidden text for screen readers: `className="sr-only"`
- ✅ All loading states have corresponding announcements

**Implementation**:
```tsx
<div role="status" aria-live="polite" className="sr-only">
  {isLoadingGames && 'Loading games'}
  {games.length > 0 && 'Games loaded'}
</div>

<div role="status" aria-live="polite" aria-label={`${agentName} is typing`}>
  <TypingIndicator visible={isStreaming} />
</div>
```

---

## Feature 6: Error State Animations

### Scenario 6.1: Error Message Shake Animation
```gherkin
Feature: Error State Animations
  As a user
  I want visual feedback when an error occurs
  So that I notice the error immediately

Scenario: Error message shakes on appearance
  Given I am on the chat page
  When a network error occurs (e.g., failed to load games)
  Then an error message should appear
  And the error message should shake horizontally (±10px)
  And the shake should complete in 400ms
  And the error should have a red border and background
  And a screen reader should assertively announce the error

  When I dismiss the error
  Then the error message should fade out over 200ms
```

**Acceptance Criteria**:
- ✅ Shake animation: CSS `@keyframes shake` or Framer Motion
- ✅ Duration: 400ms
- ✅ Visual: Red border (`border-red-500`), light red background (`bg-red-50`)
- ✅ ARIA: `role="alert" aria-live="assertive"`
- ✅ Exit animation: Fade out 200ms

**Implementation**:
```css
@keyframes shake {
  0%, 100% { transform: translateX(0); }
  10%, 30%, 50%, 70%, 90% { transform: translateX(-10px); }
  20%, 40%, 60%, 80% { transform: translateX(10px); }
}

.error-enter {
  animation: shake 0.4s ease-in-out;
}
```

```tsx
<motion.div
  role="alert"
  aria-live="assertive"
  initial={{ opacity: 0, x: 0 }}
  animate={{ opacity: 1, x: [0, -10, 10, -10, 10, 0] }}
  exit={{ opacity: 0 }}
  transition={{ duration: 0.4 }}
  className="border-2 border-red-500 bg-red-50 p-4 rounded"
>
  {errorMessage}
</motion.div>
```

---

## Testing Strategy

### Unit Tests (Jest + React Testing Library)
- `SkeletonLoader.test.tsx`: 8 tests (variants, count, accessibility)
- `TypingIndicator.test.tsx`: 6 tests (visibility, animation, reduced motion)
- `MessageAnimator.test.tsx`: 7 tests (direction, delay, exit animations)
- `LoadingButton.test.tsx`: 5 tests (loading state, spinner, accessibility)
- `useReducedMotion.test.ts`: 4 tests (media query, event listeners)

### Integration Tests (Jest + RTL)
- `chat.test.tsx`: 10+ tests (loading states, transitions, ARIA)

### E2E Tests (Playwright)
- `chat-animations.spec.ts`: 8 scenarios (skeleton loaders, typing indicator, message animations, smooth scroll, reduced motion, performance)

### Visual Regression Tests (Playwright)
- Screenshot comparison for skeleton states, typing indicator, message stagger

### Accessibility Tests
- Axe-core automated scans (100% pass rate target)
- Manual screen reader testing (NVDA, JAWS, VoiceOver)

---

## Definition of Done

### Standard DoD
- [x] Code implemented and functional
- [ ] Unit tests written and passing (90% coverage threshold)
- [ ] Integration tests passing
- [ ] E2E tests passing
- [ ] Code review approved
- [ ] Documentation updated
- [ ] CI/CD pipeline green
- [ ] No regressions identified

### Feature-Specific DoD
- [ ] All BDD scenarios implemented and tested
- [ ] Performance validated (60fps during animations)
- [ ] Accessibility verified (WCAG 2.1 AA compliance)
  - [ ] prefers-reduced-motion support tested
  - [ ] ARIA live regions functional
  - [ ] Screen reader testing completed
- [ ] Responsive design tested (mobile, tablet, desktop)
- [ ] Cross-browser testing (Chrome, Firefox, Safari, Edge)
- [ ] Visual regression tests passing
- [ ] Bundle size increase <5KB
- [ ] Animation guidelines documented

---

## Implementation Order

### Phase 1: Foundation (Week 1, Days 1-2)
1. Animation utilities library (`@/lib/animations/`)
2. CSS animation keyframes in Tailwind config
3. `useReducedMotion` hook
4. Testing infrastructure setup

### Phase 2: Loading Components (Week 1, Days 3-4)
5. `SkeletonLoader` component (CSS-based)
6. `LoadingButton` component (CSS-based)
7. Unit tests for loading components

### Phase 3: Complex Animations (Week 1, Days 5-6 + Week 2, Day 1)
8. `TypingIndicator` component (Framer Motion)
9. `MessageAnimator` component (Framer Motion)
10. Unit tests for animation components

### Phase 4: Integration (Week 2, Days 2-3)
11. Integrate into `chat.tsx`
12. Update `useChatStreaming` hook integration
13. Integration tests

### Phase 5: Validation (Week 2, Days 4-5)
14. E2E tests (Playwright)
15. Performance testing (60fps validation)
16. Accessibility audit (screen readers, axe-core)
17. Cross-browser testing
18. Visual regression tests
19. Documentation

---

## Success Metrics

**Quantitative**:
- Frame rate: p95 ≥ 55fps during animations
- Test coverage: ≥90% (maintained)
- Bundle size: <5KB increase
- WCAG compliance: 100% axe-core pass rate
- E2E stability: <2% flaky test rate

**Qualitative**:
- Perceived performance: Skeleton loaders reduce perceived load time
- Visual polish: Animations feel smooth and professional
- Accessibility: Zero user complaints about motion sickness
- Developer experience: Clear animation guidelines followed

---

## Rollback Plan

If critical issues arise:
1. Feature flag: `NEXT_PUBLIC_ENABLE_ANIMATIONS=false`
2. Disable Framer Motion animations, keep CSS only
3. Fallback to no animations (functional UI with instant transitions)
4. Git revert to pre-implementation commit

---

**Document Version**: 1.0
**Created**: 2025-10-18
**Author**: MeepleAI Development Team
**Related**: Issue #400, EPIC-03
