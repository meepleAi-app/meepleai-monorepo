# Frontend Testing Strategy

**Coverage Target**: 90%+ (enforced in CI)
**Status**: Production
**Last Updated**: 2025-01-15

---

## Test Pyramid

```
     /\        E2E (Playwright) - 5%
    /  \       User journeys, visual regression
   /----\
  /------\     Integration (Jest + RTL) - 15%
 /--------\    Page-level flows, component interactions
/----------\
------------   Unit (Jest + RTL) - 80%
              Component behavior, utilities
```

---

## Testing Tools

| Layer | Tool | Purpose |
|-------|------|---------|
| **Unit** | Jest + React Testing Library | Component behavior |
| **Integration** | Jest + RTL | Page-level flows |
| **E2E** | Playwright | User journeys |
| **Visual** | Playwright | Screenshot regression |
| **Accessibility** | jest-axe + Playwright | WCAG validation |
| **Performance** | Lighthouse CI | Web Vitals |

---

## Test Categories

### 1. Unit Tests (80%)

**Location**: `__tests__/` next to components

```typescript
// components/__tests__/ChatInput.test.tsx
describe('ChatInput', () => {
  it('renders with placeholder', () => {
    render(<ChatInput />);
    expect(screen.getByPlaceholderText(/ask a question/i)).toBeInTheDocument();
  });

  it('calls onSend when submitted', () => {
    const onSend = jest.fn();
    render(<ChatInput onSend={onSend} />);

    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'test' } });
    fireEvent.click(screen.getByRole('button', { name: /send/i }));

    expect(onSend).toHaveBeenCalledWith('test');
  });
});
```

### 2. Integration Tests (15%)

**Location**: `__tests__/pages/`

```typescript
// __tests__/pages/chat.test.tsx
describe('Chat Page', () => {
  it('displays messages and allows sending new message', async () => {
    render(<ChatPage />);

    // Wait for messages to load
    await waitFor(() => {
      expect(screen.getByText('Previous message')).toBeInTheDocument();
    });

    // Send new message
    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: 'New question' } });
    fireEvent.click(screen.getByRole('button', { name: /send/i }));

    // Verify message sent
    await waitFor(() => {
      expect(screen.getByText('New question')).toBeInTheDocument();
    });
  });
});
```

### 3. E2E Tests (5%)

**Location**: `e2e/`

```typescript
// e2e/chat-flow.spec.ts
test('user can ask question and receive answer', async ({ page }) => {
  await page.goto('/chat');

  // Select game
  await page.click('[data-testid="game-selector"]');
  await page.click('text=Catan');

  // Ask question
  await page.fill('[data-testid="chat-input"]', 'Come si costruisce una strada?');
  await page.click('[data-testid="send-button"]');

  // Verify answer
  await expect(page.locator('[data-testid="chat-message"]').last()).toContainText('strada');
  await expect(page.locator('[data-testid="citation"]')).toBeVisible();
});
```

### 4. Visual Regression Tests

```typescript
// e2e/visual/components.spec.ts
test('Chat interface visual regression', async ({ page }) => {
  await page.goto('/chat');
  await expect(page).toHaveScreenshot('chat-interface.png');
});
```

### 5. Accessibility Tests

```typescript
// Automated
test('page has no accessibility violations', async () => {
  const { container } = render(<ChatPage />);
  const results = await axe(container);
  expect(results).toHaveNoViolations();
});

// E2E
test('keyboard navigation works', async ({ page }) => {
  await page.goto('/chat');
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab');
  await expect(page.locator('[data-testid="chat-input"]')).toBeFocused();
});
```

---

## Test Scripts

```json
{
  "scripts": {
    "test": "jest --passWithNoTests",
    "test:watch": "jest --watch",
    "test:coverage": "jest --coverage",
    "test:e2e": "playwright test",
    "test:e2e:ui": "playwright test --ui",
    "test:a11y": "jest --testPathPatterns=\"(a11y|accessibility)\"",
    "audit:a11y": "tsx scripts/run-accessibility-audit.ts"
  }
}
```

---

## CI/CD Quality Gates

```yaml
quality_gates:
  - name: "Unit Test Coverage"
    threshold: 90%
    blocker: true

  - name: "E2E Tests Passing"
    threshold: 100%
    blocker: true

  - name: "Accessibility Violations"
    max_critical: 0
    blocker: true

  - name: "Visual Regression"
    threshold: 99%  # Allow 1% pixel difference
    blocker: false
```

---

## Element Identification Strategy

### Query Priority (from Testing Library)

1. **Accessible Queries** (Preferred)
   - `getByRole()` - Best for semantic elements
   - `getByLabelText()` - Best for form inputs
   - `getByPlaceholderText()` - Good for inputs
   - `getByText()` - Use with i18n helpers

2. **Test IDs** (When Necessary)
   - `getByTestId()` - Explicit identifiers
   - Use when accessible queries insufficient

3. **Avoid** (Fragile)
   - CSS selectors (`.className`)
   - Index-based selectors (`.nth()`, `.first()`)
   - DOM traversal (`closest()`, `querySelector()`)

### Test ID Naming Convention

```typescript
[component]-[id?]-[element?]-[action?]

Examples:
- message-123                  (message with ID 123)
- message-123-delete           (delete button for message 123)
- chat-message-list            (message list container)
- user-row-456                 (user table row with ID 456)
```

**See**: [UI Element Identification Guide](../../02-development/testing/ui-element-identification-guide.md) for comprehensive guidelines and migration roadmap.

---

## Testing Best Practices

1. **AAA Pattern**: Arrange, Act, Assert
2. **User-Centric**: Test behavior, not implementation
3. **Isolation**: Each test independent
4. **Fast**: Unit tests <1s, integration <5s
5. **Readable**: Clear test names and assertions
6. **Accessible Queries**: Prefer `getByRole()` over test IDs
7. **Unique Identifiers**: Never use index-based selectors in lists

---

**See Also**:
- [UI Element Identification Guide](../../02-development/testing/ui-element-identification-guide.md) ⭐ **NEW**
- [Test Writing Guide](../../02-development/testing/test-writing-guide.md)
- [Accessibility Standards](./accessibility-standards.md)
- [E2E Patterns](../../02-development/testing/e2e-patterns.md)

---

**Maintained by**: Frontend Team
**Review Frequency**: Monthly
