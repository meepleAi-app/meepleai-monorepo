import { describe, it, expect, vi } from 'vitest';

import { toChatMessageProps, type ToChatMessagePropsContext } from '../toChatMessageProps';

import type { ChatMessageItem } from '../../ChatMessageList';

describe('toChatMessageProps', () => {
  const baseCtx: ToChatMessagePropsContext = {
    feedback: null,
    isFeedbackLoading: false,
    showFeedback: false,
    onFeedbackChange: vi.fn().mockResolvedValue(undefined),
  };

  describe('role mapping', () => {
    it('maps assistant role', () => {
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'Hi' };
      expect(toChatMessageProps(item, baseCtx).role).toBe('assistant');
    });

    it('maps user role', () => {
      const item: ChatMessageItem = { id: 'u', role: 'user', content: 'Q?' };
      expect(toChatMessageProps(item, baseCtx).role).toBe('user');
    });
  });

  describe('content + timestamp', () => {
    it('passes content through', () => {
      const item: ChatMessageItem = {
        id: 'a',
        role: 'assistant',
        content: 'Hello\nWorld',
      };
      expect(toChatMessageProps(item, baseCtx).content).toBe('Hello\nWorld');
    });

    it('passes timestamp through', () => {
      const item: ChatMessageItem = {
        id: 'a',
        role: 'assistant',
        content: 'Hi',
        timestamp: '2026-04-09T12:00:00Z',
      };
      expect(toChatMessageProps(item, baseCtx).timestamp).toBe('2026-04-09T12:00:00Z');
    });

    it('passes undefined timestamp as undefined', () => {
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'Hi' };
      expect(toChatMessageProps(item, baseCtx).timestamp).toBeUndefined();
    });
  });

  describe('avatar', () => {
    it('sets user avatar with fallback "U"', () => {
      const item: ChatMessageItem = { id: 'u', role: 'user', content: 'Q' };
      expect(toChatMessageProps(item, baseCtx).avatar).toEqual({ fallback: 'U' });
    });

    it('does NOT set avatar for assistant messages', () => {
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'A' };
      expect(toChatMessageProps(item, baseCtx).avatar).toBeUndefined();
    });
  });

  describe('feedback propagation', () => {
    it('passes feedback value', () => {
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'A' };
      const result = toChatMessageProps(item, { ...baseCtx, feedback: 'helpful' });
      expect(result.feedback).toBe('helpful');
    });

    it('passes isFeedbackLoading', () => {
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'A' };
      const result = toChatMessageProps(item, { ...baseCtx, isFeedbackLoading: true });
      expect(result.isFeedbackLoading).toBe(true);
    });

    it('passes showFeedback', () => {
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'A' };
      const result = toChatMessageProps(item, { ...baseCtx, showFeedback: true });
      expect(result.showFeedback).toBe(true);
    });

    it('passes onFeedbackChange handler through (same reference)', () => {
      const handler = vi.fn().mockResolvedValue(undefined);
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'A' };
      const result = toChatMessageProps(item, { ...baseCtx, onFeedbackChange: handler });
      expect(result.onFeedbackChange).toBe(handler);
    });
  });

  describe('citations and confidence (intentionally not passed)', () => {
    it('does NOT pass citations even when source has them', () => {
      const item: ChatMessageItem = {
        id: 'a',
        role: 'assistant',
        content: 'A',
        citations: [
          {
            documentId: 'doc-1',
            pageNumber: 1,
            snippet: 'rule',
            relevanceScore: 0.9,
            copyrightTier: 'full',
          },
        ],
      };
      const result = toChatMessageProps(item, baseCtx);
      expect(result.citations).toBeUndefined();
    });

    it('does NOT set confidence (not available in ChatMessageItem)', () => {
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'A' };
      expect(toChatMessageProps(item, baseCtx).confidence).toBeUndefined();
    });

    it('does NOT set agentType (not available in ChatMessageItem)', () => {
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'A' };
      expect(toChatMessageProps(item, baseCtx).agentType).toBeUndefined();
    });

    it('does NOT set isTyping (handled by separate streaming bubble)', () => {
      const item: ChatMessageItem = { id: 'a', role: 'assistant', content: 'A' };
      expect(toChatMessageProps(item, baseCtx).isTyping).toBeUndefined();
    });
  });
});
