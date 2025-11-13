# [TESTING] Comprehensive Component Unit Tests

## 🎯 Objective
Achieve 95%+ test coverage for all components.

## ✅ Acceptance Criteria
- [ ] All custom components have unit tests
- [ ] React Testing Library best practices
- [ ] Accessibility tests with jest-axe
- [ ] Coverage > 95%
- [ ] Fast test execution (< 30s)

## 🏗️ Implementation
Write tests for:
- All wizard components
- Game picker
- PDF upload form
- PDF table
- Chat components (Message, MessageInput, etc.)

Use testing-library patterns:
```tsx
describe('GamePicker', () => {
  it('lists available games', () => {
    render(<GamePicker games={mockGames} ... />);
    expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
  });

  it('creates new game', async () => {
    const onGameCreate = jest.fn();
    render(<GamePicker onGameCreate={onGameCreate} ... />);

    await userEvent.type(screen.getByLabelText('New game'), 'Catan');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    expect(onGameCreate).toHaveBeenCalledWith('Catan');
  });
});
```

## ⏱️ Effort: **1 day** | **Sprint 3** | **Priority**: 🟢 Medium
