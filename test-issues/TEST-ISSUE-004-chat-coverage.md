# TEST-ISSUE-004: Increase Chat Component Coverage to 85%+

**Priority**: 🟡 HIGH
**Labels**: `high-priority`, `testing`, `chat`, `enhancement`
**Estimated Effort**: 10-12 hours
**Current Coverage**: 73.89% statements, 75% functions
**Target Coverage**: 85%+ across all metrics

---

## Problem Statement

Chat components are core application features but have lower coverage (73.89%) than the target (85%+). Missing tests for edge cases, error scenarios, and complex user interactions reduce confidence in chat functionality.

### Current State

```
components/chat Coverage:
- Statements: 73.89% (283/383)
- Branches:   80.6% (212/263)
- Functions:  75% (75/100)
- Lines:      76.31% (261/342)
```

### Gaps

- **100 uncovered statements** out of 383
- **51 untested branches** out of 263
- **25 untested functions** out of 100

---

## Affected Components

| Component | Current | Target | Priority | Effort |
|-----------|---------|--------|----------|--------|
| ChatHistory | Low (~60%) | 85%+ | HIGH | 3h |
| MessageActions | Medium (~70%) | 85%+ | HIGH | 3h |
| Message rendering | Medium (~75%) | 85%+ | MEDIUM | 2-3h |
| Chat state mgmt | Unknown | 85%+ | MEDIUM | 2-3h |

---

## Implementation Tasks

### Task 1: ChatHistory Component (3 hours)

**File**: `src/__tests__/components/chat/ChatHistory.test.tsx` (enhance existing or create)

**Current Issues**:
- Test suite failing (see TEST-ISSUE-002)
- Missing tests for loading states
- Missing tests for pagination
- Missing tests for infinite scroll

**Test Scenarios**:

```typescript
describe('ChatHistory', () => {
  describe('Loading States', () => {
    it('should show loading spinner when fetching history', () => {
      const { getByRole } = render(<ChatHistory loading={true} />);
      expect(getByRole('progressbar')).toBeInTheDocument();
    });

    it('should hide loading spinner when history loaded', async () => {
      const { queryByRole } = render(<ChatHistory />);
      await waitFor(() => {
        expect(queryByRole('progressbar')).not.toBeInTheDocument();
      });
    });
  });

  describe('Empty State', () => {
    it('should show empty message when no history', () => {
      const { getByText } = render(<ChatHistory messages={[]} />);
      expect(getByText(/no messages/i)).toBeInTheDocument();
    });

    it('should show call-to-action in empty state', () => {
      const { getByRole } = render(<ChatHistory messages={[]} />);
      expect(getByRole('button', { name: /start chat/i })).toBeInTheDocument();
    });
  });

  describe('Pagination', () => {
    it('should load more messages when scrolling to top', async () => {
      const onLoadMore = jest.fn();
      const { container } = render(
        <ChatHistory onLoadMore={onLoadMore} hasMore={true} />
      );

      // Simulate scroll to top
      fireEvent.scroll(container.firstChild, { target: { scrollTop: 0 } });

      await waitFor(() => {
        expect(onLoadMore).toHaveBeenCalled();
      });
    });

    it('should not load more when already at start', () => {
      const onLoadMore = jest.fn();
      render(<ChatHistory onLoadMore={onLoadMore} hasMore={false} />);

      // Should not call onLoadMore
      expect(onLoadMore).not.toHaveBeenCalled();
    });
  });

  describe('Infinite Scroll', () => {
    it('should support infinite scroll behavior', async () => {
      const { container } = render(
        <ChatHistory messages={mockMessages} infiniteScroll={true} />
      );

      // Verify IntersectionObserver setup
      expect(global.IntersectionObserver).toHaveBeenCalled();
    });
  });

  describe('Error Handling', () => {
    it('should show error message when history fetch fails', () => {
      const { getByText } = render(
        <ChatHistory error="Failed to load history" />
      );
      expect(getByText(/failed to load/i)).toBeInTheDocument();
    });

    it('should allow retry after error', async () => {
      const onRetry = jest.fn();
      const { getByRole } = render(
        <ChatHistory error="Error" onRetry={onRetry} />
      );

      const retryButton = getByRole('button', { name: /retry/i });
      await userEvent.click(retryButton);

      expect(onRetry).toHaveBeenCalled();
    });
  });

  describe('Message Grouping', () => {
    it('should group messages by date', () => {
      const messages = [
        { ...mockMessage, timestamp: '2025-11-01T10:00:00Z' },
        { ...mockMessage, timestamp: '2025-11-01T11:00:00Z' },
        { ...mockMessage, timestamp: '2025-11-02T10:00:00Z' },
      ];

      const { getAllByRole } = render(<ChatHistory messages={messages} />);
      const dateHeaders = getAllByRole('heading', { level: 3 });

      expect(dateHeaders).toHaveLength(2); // Two different dates
    });
  });

  describe('Performance', () => {
    it('should virtualize long message lists', () => {
      const manyMessages = Array.from({ length: 1000 }, (_, i) => ({
        ...mockMessage,
        id: `msg-${i}`,
      }));

      const { container } = render(
        <ChatHistory messages={manyMessages} virtualized={true} />
      );

      // Only visible messages should be rendered
      const renderedMessages = container.querySelectorAll('[data-message-id]');
      expect(renderedMessages.length).toBeLessThan(50);
    });
  });
});
```

---

### Task 2: MessageActions Component (3 hours)

**File**: `src/components/chat/__tests__/MessageActions.test.tsx` (enhance existing)

**Current Issues**:
- Test suite failing (see TEST-ISSUE-002)
- Missing permission tests
- Missing interaction tests

**Test Scenarios**:

```typescript
describe('MessageActions', () => {
  describe('Action Buttons', () => {
    it('should show edit button for user messages', () => {
      const message = { ...mockMessage, role: 'user' };
      const { getByLabelText } = render(<MessageActions message={message} />);

      expect(getByLabelText(/edit/i)).toBeInTheDocument();
    });

    it('should show delete button for user messages', () => {
      const message = { ...mockMessage, role: 'user' };
      const { getByLabelText } = render(<MessageActions message={message} />);

      expect(getByLabelText(/delete/i)).toBeInTheDocument();
    });

    it('should show copy button for all messages', () => {
      const { getByLabelText } = render(<MessageActions message={mockMessage} />);

      expect(getByLabelText(/copy/i)).toBeInTheDocument();
    });
  });

  describe('Edit Action', () => {
    it('should call onEdit when edit button clicked', async () => {
      const onEdit = jest.fn();
      const { getByLabelText } = render(
        <MessageActions message={mockMessage} onEdit={onEdit} />
      );

      await userEvent.click(getByLabelText(/edit/i));

      expect(onEdit).toHaveBeenCalledWith(mockMessage.id);
    });

    it('should enter edit mode when editing', async () => {
      const { getByLabelText, getByRole } = render(
        <MessageActions message={mockMessage} />
      );

      await userEvent.click(getByLabelText(/edit/i));

      expect(getByRole('textbox')).toBeInTheDocument();
    });
  });

  describe('Delete Action', () => {
    it('should show confirmation dialog before delete', async () => {
      const { getByLabelText, getByRole } = render(
        <MessageActions message={mockMessage} />
      );

      await userEvent.click(getByLabelText(/delete/i));

      expect(getByRole('dialog')).toBeInTheDocument();
      expect(getByRole('button', { name: /confirm/i })).toBeInTheDocument();
    });

    it('should call onDelete when confirmed', async () => {
      const onDelete = jest.fn();
      const { getByLabelText, getByRole } = render(
        <MessageActions message={mockMessage} onDelete={onDelete} />
      );

      await userEvent.click(getByLabelText(/delete/i));
      await userEvent.click(getByRole('button', { name: /confirm/i }));

      expect(onDelete).toHaveBeenCalledWith(mockMessage.id);
    });
  });

  describe('Copy Action', () => {
    it('should copy message content to clipboard', async () => {
      const { getByLabelText } = render(
        <MessageActions message={mockMessage} />
      );

      await userEvent.click(getByLabelText(/copy/i));

      expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
        mockMessage.content
      );
    });

    it('should show success toast after copy', async () => {
      const { getByLabelText, getByText } = render(
        <MessageActions message={mockMessage} />
      );

      await userEvent.click(getByLabelText(/copy/i));

      await waitFor(() => {
        expect(getByText(/copied/i)).toBeInTheDocument();
      });
    });
  });

  describe('Permissions', () => {
    it('should only show edit for own messages', () => {
      const ownMessage = { ...mockMessage, userId: 'current-user' };
      const { getByLabelText } = render(
        <MessageActions message={ownMessage} currentUserId="current-user" />
      );

      expect(getByLabelText(/edit/i)).toBeInTheDocument();
    });

    it('should not show edit for other user messages', () => {
      const otherMessage = { ...mockMessage, userId: 'other-user' };
      const { queryByLabelText } = render(
        <MessageActions message={otherMessage} currentUserId="current-user" />
      );

      expect(queryByLabelText(/edit/i)).not.toBeInTheDocument();
    });

    it('should allow admin to delete any message', () => {
      const { getByLabelText } = render(
        <MessageActions
          message={mockMessage}
          currentUserId="admin"
          currentUserRole="Admin"
        />
      );

      expect(getByLabelText(/delete/i)).toBeInTheDocument();
    });
  });
});
```

---

### Task 3: Message Rendering (2-3 hours)

**Files**: Various message rendering components

**Test Scenarios**:

```typescript
describe('Message Rendering', () => {
  describe('Message Types', () => {
    it('should render user message', () => {
      const userMsg = { ...mockMessage, role: 'user' };
      const { container } = render(<Message message={userMsg} />);

      expect(container.firstChild).toHaveClass('message-user');
    });

    it('should render assistant message', () => {
      const assistantMsg = { ...mockMessage, role: 'assistant' };
      const { container } = render(<Message message={assistantMsg} />);

      expect(container.firstChild).toHaveClass('message-assistant');
    });

    it('should render system message', () => {
      const systemMsg = { ...mockMessage, role: 'system' };
      const { container } = render(<Message message={systemMsg} />);

      expect(container.firstChild).toHaveClass('message-system');
    });
  });

  describe('Content Formatting', () => {
    it('should render markdown content', () => {
      const message = {
        ...mockMessage,
        content: '**bold** and *italic*',
      };

      const { container } = render(<Message message={message} />);

      expect(container.querySelector('strong')).toHaveTextContent('bold');
      expect(container.querySelector('em')).toHaveTextContent('italic');
    });

    it('should render code blocks with syntax highlighting', () => {
      const message = {
        ...mockMessage,
        content: '```js\nconst x = 1;\n```',
      };

      const { container } = render(<Message message={message} />);

      expect(container.querySelector('pre code')).toBeInTheDocument();
    });

    it('should render inline code', () => {
      const message = {
        ...mockMessage,
        content: 'Use `console.log()` for debugging',
      };

      const { getByText } = render(<Message message={message} />);

      expect(getByText('console.log()')).toHaveClass('inline-code');
    });

    it('should auto-link URLs', () => {
      const message = {
        ...mockMessage,
        content: 'Visit https://example.com',
      };

      const { getByRole } = render(<Message message={message} />);

      const link = getByRole('link');
      expect(link).toHaveAttribute('href', 'https://example.com');
    });
  });

  describe('Citations', () => {
    it('should render citations when present', () => {
      const message = {
        ...mockMessage,
        citations: [{ page: 5, text: 'Rule text' }],
      };

      const { getByText } = render(<Message message={message} />);

      expect(getByText(/page 5/i)).toBeInTheDocument();
    });

    it('should open PDF to citation page when clicked', async () => {
      const message = {
        ...mockMessage,
        citations: [{ page: 5, pdfId: 'pdf-1' }],
      };

      const { getByText } = render(<Message message={message} />);

      await userEvent.click(getByText(/page 5/i));

      // Verify PDF opened to page 5
    });
  });

  describe('Streaming', () => {
    it('should show typing indicator for streaming message', () => {
      const message = { ...mockMessage, streaming: true };
      const { getByLabelText } = render(<Message message={message} />);

      expect(getByLabelText(/typing/i)).toBeInTheDocument();
    });

    it('should update content as stream arrives', async () => {
      const { rerender, getByText } = render(
        <Message message={{ ...mockMessage, content: 'Hello' }} />
      );

      rerender(<Message message={{ ...mockMessage, content: 'Hello World' }} />);

      await waitFor(() => {
        expect(getByText('Hello World')).toBeInTheDocument();
      });
    });
  });
});
```

---

### Task 4: Chat State Management (2-3 hours)

**Test chat state, optimistic updates, error recovery**

```typescript
describe('Chat State Management', () => {
  describe('Message Sending', () => {
    it('should add message to state when sent', async () => {
      const { result } = renderHook(() => useChatState());

      await act(async () => {
        await result.current.sendMessage('Hello');
      });

      expect(result.current.messages).toHaveLength(1);
      expect(result.current.messages[0].content).toBe('Hello');
    });
  });

  describe('Optimistic Updates', () => {
    it('should show message immediately before server response', async () => {
      const { result } = renderHook(() => useChatState());

      act(() => {
        result.current.sendMessage('Hello');
      });

      // Message should appear immediately (optimistic)
      expect(result.current.messages).toHaveLength(1);
      expect(result.current.messages[0].status).toBe('sending');
    });

    it('should update message status after server response', async () => {
      const { result } = renderHook(() => useChatState());

      await act(async () => {
        await result.current.sendMessage('Hello');
      });

      await waitFor(() => {
        expect(result.current.messages[0].status).toBe('sent');
      });
    });
  });

  describe('Error Recovery', () => {
    it('should mark message as failed on error', async () => {
      mockAPICall.mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useChatState());

      await act(async () => {
        await result.current.sendMessage('Hello');
      });

      await waitFor(() => {
        expect(result.current.messages[0].status).toBe('failed');
      });
    });

    it('should allow retry of failed message', async () => {
      const { result } = renderHook(() => useChatState());

      // First attempt fails
      mockAPICall.mockRejectedValueOnce(new Error('Error'));
      await act(async () => {
        await result.current.sendMessage('Hello');
      });

      // Retry succeeds
      mockAPICall.mockResolvedValueOnce({ success: true });
      await act(async () => {
        await result.current.retryMessage(result.current.messages[0].id);
      });

      await waitFor(() => {
        expect(result.current.messages[0].status).toBe('sent');
      });
    });
  });
});
```

---

## Acceptance Criteria

- [ ] Chat component coverage: 73.89% → 85%+
- [ ] ChatHistory tests complete
- [ ] MessageActions tests complete
- [ ] Message rendering tests complete
- [ ] State management tests complete
- [ ] All edge cases covered
- [ ] All error scenarios tested
- [ ] All user interactions tested

---

## Success Metrics

**Before**: 73.89% statements, 75% functions
**After**: 85%+ statements, 85%+ functions ✅

---

## Related Issues

- TEST-ISSUE-002: Fix ChatHistory.test.tsx failures
- TEST-ISSUE-003: Chat shared utilities

---

**Created**: 2025-11-05
**Estimated Effort**: 10-12 hours
