import { describe, it, expect } from 'vitest';

import { isLastAssistantMessage } from '../isLastAssistantMessage';

import type { ChatMessageItem } from '../../ChatMessageList';

describe('isLastAssistantMessage', () => {
  const u = (id: string): ChatMessageItem => ({ id, role: 'user', content: 'q' });
  const a = (id: string): ChatMessageItem => ({ id, role: 'assistant', content: 'a' });

  it('returns false for user messages', () => {
    const msgs = [u('u1'), a('a1')];
    expect(isLastAssistantMessage(msgs, 0)).toBe(false);
  });

  it('returns true for the last assistant message when followed by user message', () => {
    const msgs = [u('u1'), a('a1'), u('u2'), a('a2'), u('u3')];
    // a2 is at index 3; it IS the last assistant even though u3 comes after
    expect(isLastAssistantMessage(msgs, 3)).toBe(true);
  });

  it('returns true for the last assistant message', () => {
    const msgs = [u('u1'), a('a1'), u('u2'), a('a2')];
    expect(isLastAssistantMessage(msgs, 3)).toBe(true);
  });

  it('returns false for non-last assistant messages', () => {
    const msgs = [u('u1'), a('a1'), u('u2'), a('a2')];
    expect(isLastAssistantMessage(msgs, 1)).toBe(false);
  });

  it('returns true when last message is assistant and index points to it', () => {
    const msgs = [u('u1'), a('a1')];
    expect(isLastAssistantMessage(msgs, 1)).toBe(true);
  });

  it('returns false for out-of-range index', () => {
    const msgs = [u('u1'), a('a1')];
    expect(isLastAssistantMessage(msgs, 5)).toBe(false);
  });

  it('returns false for negative index', () => {
    const msgs = [u('u1'), a('a1')];
    expect(isLastAssistantMessage(msgs, -1)).toBe(false);
  });

  it('returns false for empty list', () => {
    expect(isLastAssistantMessage([], 0)).toBe(false);
  });

  it('returns false when no assistant messages exist', () => {
    const msgs = [u('u1'), u('u2')];
    expect(isLastAssistantMessage(msgs, 1)).toBe(false);
  });

  it('returns true for single assistant message', () => {
    const msgs = [a('a1')];
    expect(isLastAssistantMessage(msgs, 0)).toBe(true);
  });
});
