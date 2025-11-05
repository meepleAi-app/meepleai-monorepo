# TEST-ISSUE-006: Close Component Coverage Gaps

**Priority**: 🟢 MEDIUM
**Labels**: `medium-priority`, `testing`, `coverage`, `components`
**Estimated Effort**: 8-12 hours
**Components**: diff (84.25%), admin (84.61%), editor (various)
**Target**: 85%+ for all components

---

## Problem Statement

Several component groups are close to but below the 85% coverage target. These gaps represent untested code paths that could harbor bugs or break during refactoring.

---

## Affected Components

### 1. Diff Components (84.25% coverage)

**Gap**: Need +0.75% to reach 85%
**Effort**: 3-4 hours

**Uncovered**:
- 20 statements
- 11 branches
- 7 functions

**Files**:
- `components/diff/DiffToolbar.tsx`
- `components/diff/DiffSearchInput.tsx`
- `components/diff/DiffNavigationControls.tsx`
- `components/diff/DiffCodePanel.tsx`

---

### 2. Admin Components (84.61% coverage)

**Gap**: Need +0.39% to reach 85%
**Effort**: 3-4 hours

**Uncovered**:
- 14 statements
- 15 branches
- 3 functions

**Files**:
- `components/admin/CategoryConfigTab.tsx`
- `components/admin/FeatureFlagsTab.tsx`
- Admin form validations
- Admin permission checks

---

### 3. Editor Components (various coverage)

**Effort**: 2-4 hours

**Files**:
- `components/editor/EditorToolbar.tsx` (failing tests - see TEST-ISSUE-002)
- `components/editor/RichTextEditor.tsx` (good coverage)
- Editor utilities and commands

---

## Implementation Tasks

### Task 1: Diff Component Tests (3-4 hours)

#### Subtask 1.1: Diff Algorithm Edge Cases (1.5 hours)

```typescript
describe('DiffProcessor', () => {
  describe('Edge Cases', () => {
    it('should handle empty files', () => {
      const diff = processDiff('', '');
      expect(diff.changes).toEqual([]);
    });

    it('should handle identical content', () => {
      const content = 'same content';
      const diff = processDiff(content, content);
      expect(diff.changes).toEqual([]);
    });

    it('should handle very long lines', () => {
      const longLine = 'x'.repeat(10000);
      const diff = processDiff(longLine, longLine + 'y');
      expect(diff.changes).toHaveLength(1);
    });

    it('should handle special characters', () => {
      const text = 'line with\ttabs\nand\nnewlines';
      const diff = processDiff(text, text);
      expect(diff.changes).toEqual([]);
    });
  });

  describe('Performance', () => {
    it('should handle large diffs efficiently', () => {
      const start = Date.now();
      const largeText = 'line\n'.repeat(10000);
      processDiff(largeText, largeText + 'change');
      const elapsed = Date.now() - start;

      expect(elapsed).toBeLessThan(1000); // < 1 second
    });
  });
});
```

#### Subtask 1.2: Diff Navigation Edge Cases (1 hour)

```typescript
describe('DiffNavigationControls', () => {
  describe('Boundary Conditions', () => {
    it('should disable prev button at first change', () => {
      const { getByLabelText } = render(
        <DiffNavigationControls currentChange={0} totalChanges={5} />
      );

      expect(getByLabelText('Previous change')).toBeDisabled();
    });

    it('should disable next button at last change', () => {
      const { getByLabelText } = render(
        <DiffNavigationControls currentChange={4} totalChanges={5} />
      );

      expect(getByLabelText('Next change')).toBeDisabled();
    });

    it('should handle single change', () => {
      const { getByLabelText } = render(
        <DiffNavigationControls currentChange={0} totalChanges={1} />
      );

      expect(getByLabelText('Previous change')).toBeDisabled();
      expect(getByLabelText('Next change')).toBeDisabled();
    });

    it('should handle no changes', () => {
      const { getByText } = render(
        <DiffNavigationControls currentChange={0} totalChanges={0} />
      );

      expect(getByText(/no changes/i)).toBeInTheDocument();
    });
  });
});
```

#### Subtask 1.3: Diff Search Edge Cases (0.5 hour)

```typescript
describe('DiffSearchInput', () => {
  describe('Search Edge Cases', () => {
    it('should handle empty search', () => {
      const onSearch = jest.fn();
      const { getByRole } = render(
        <DiffSearchInput onSearch={onSearch} />
      );

      const input = getByRole('textbox');
      fireEvent.change(input, { target: { value: '' } });

      expect(onSearch).toHaveBeenCalledWith('');
    });

    it('should handle special regex characters', () => {
      const onSearch = jest.fn();
      const { getByRole } = render(
        <DiffSearchInput onSearch={onSearch} />
      );

      const input = getByRole('textbox');
      fireEvent.change(input, { target: { value: '.*+?[]{}()' } });

      // Should not crash
      expect(onSearch).toHaveBeenCalled();
    });
  });
});
```

---

### Task 2: Admin Component Tests (3-4 hours)

#### Subtask 2.1: Form Validations (1.5 hours)

```typescript
describe('Admin Form Validations', () => {
  describe('User Form', () => {
    it('should validate email format', async () => {
      const { getByLabelText, getByText } = render(<UserForm />);

      const emailInput = getByLabelText(/email/i);
      await userEvent.type(emailInput, 'invalid-email');

      await waitFor(() => {
        expect(getByText(/valid email/i)).toBeInTheDocument();
      });
    });

    it('should validate required fields', async () => {
      const { getByRole, getAllByText } = render(<UserForm />);

      const submitButton = getByRole('button', { name: /save/i });
      await userEvent.click(submitButton);

      await waitFor(() => {
        expect(getAllByText(/required/i)).toHaveLength(3); // 3 required fields
      });
    });

    it('should validate password strength', async () => {
      const { getByLabelText, getByText } = render(<UserForm />);

      const passwordInput = getByLabelText(/password/i);
      await userEvent.type(passwordInput, 'weak');

      await waitFor(() => {
        expect(getByText(/password must/i)).toBeInTheDocument();
      });
    });
  });
});
```

#### Subtask 2.2: Permission Checks (1 hour)

```typescript
describe('Admin Permissions', () => {
  it('should show admin-only actions for admin users', () => {
    const { getByRole } = render(
      <AdminDashboard currentUser={{ role: 'Admin' }} />
    );

    expect(getByRole('button', { name: /delete user/i })).toBeInTheDocument();
  });

  it('should hide admin-only actions for non-admin users', () => {
    const { queryByRole } = render(
      <AdminDashboard currentUser={{ role: 'Editor' }} />
    );

    expect(queryByRole('button', { name: /delete user/i })).not.toBeInTheDocument();
  });

  it('should show editor actions for editors', () => {
    const { getByRole } = render(
      <AdminDashboard currentUser={{ role: 'Editor' }} />
    );

    expect(getByRole('button', { name: /edit/i })).toBeInTheDocument();
  });
});
```

#### Subtask 2.3: Data Mutations (0.5 hour)

```typescript
describe('Admin Data Mutations', () => {
  it('should handle successful user update', async () => {
    mockAPI.updateUser.mockResolvedValue({ success: true });

    const { getByRole, getByText } = render(<UserForm user={mockUser} />);

    const saveButton = getByRole('button', { name: /save/i });
    await userEvent.click(saveButton);

    await waitFor(() => {
      expect(getByText(/saved successfully/i)).toBeInTheDocument();
    });
  });

  it('should handle update errors', async () => {
    mockAPI.updateUser.mockRejectedValue(new Error('Network error'));

    const { getByRole, getByText } = render(<UserForm user={mockUser} />);

    const saveButton = getByRole('button', { name: /save/i });
    await userEvent.click(saveButton);

    await waitFor(() => {
      expect(getByText(/error/i)).toBeInTheDocument();
    });
  });
});
```

---

### Task 3: Editor Component Tests (2-4 hours)

#### Subtask 3.1: Fix EditorToolbar Tests (1 hour)

**See TEST-ISSUE-002** for EditorToolbar test fixes.

#### Subtask 3.2: Editor Commands (1-2 hours)

```typescript
describe('Editor Commands', () => {
  it('should apply bold formatting', async () => {
    const { container } = render(<RichTextEditor />);

    const editor = container.querySelector('.ProseMirror');
    const boldButton = screen.getByLabelText(/bold/i);

    // Select text
    await userEvent.click(editor);
    await userEvent.keyboard('{Control>}a{/Control}');

    // Apply bold
    await userEvent.click(boldButton);

    expect(editor.querySelector('strong')).toBeInTheDocument();
  });

  it('should insert code block', async () => {
    const { container } = render(<RichTextEditor />);

    const editor = container.querySelector('.ProseMirror');
    const codeButton = screen.getByLabelText(/code block/i);

    await userEvent.click(editor);
    await userEvent.click(codeButton);

    expect(editor.querySelector('pre code')).toBeInTheDocument();
  });
});
```

#### Subtask 3.3: Editor State Management (1 hour)

```typescript
describe('Editor State', () => {
  it('should track content changes', async () => {
    const onChange = jest.fn();
    const { container } = render(
      <RichTextEditor onChange={onChange} />
    );

    const editor = container.querySelector('.ProseMirror');
    await userEvent.click(editor);
    await userEvent.type(editor, 'Hello World');

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ text: expect.stringContaining('Hello World') })
    );
  });

  it('should handle undo/redo', async () => {
    const { container } = render(<RichTextEditor />);

    const editor = container.querySelector('.ProseMirror');
    await userEvent.click(editor);
    await userEvent.type(editor, 'Test');

    // Undo
    await userEvent.keyboard('{Control>}z{/Control}');
    expect(editor).toHaveTextContent('');

    // Redo
    await userEvent.keyboard('{Control>}{Shift>}z{/Shift}{/Control}');
    expect(editor).toHaveTextContent('Test');
  });
});
```

---

## Acceptance Criteria

- [ ] Diff components: 84.25% → 85%+
- [ ] Admin components: 84.61% → 85%+
- [ ] Editor components: 85%+ (after fixes)
- [ ] All edge cases covered
- [ ] All error scenarios tested
- [ ] All permission checks validated

---

## Success Metrics

**Before**:
- Diff: 84.25%
- Admin: 84.61%
- Editor: Various

**After**:
- Diff: 85%+ ✅
- Admin: 85%+ ✅
- Editor: 85%+ ✅

---

**Created**: 2025-11-05
**Estimated Effort**: 8-12 hours
