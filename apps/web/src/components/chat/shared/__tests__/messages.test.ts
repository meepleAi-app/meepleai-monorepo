import { describe, it, expect } from 'vitest';

import { collectCitations, getSuggestedQuestions } from '../messages';
import type { ChatMessageItem, Citation } from '../types';

// ──────────────────────────────────────────────────────────────────
// Test fixtures — minimal valid Citation / ChatMessageItem shapes
// ──────────────────────────────────────────────────────────────────

const cite = (documentId: string, pageNumber: number): Citation => ({
  documentId,
  pageNumber,
  snippet: `snippet-${documentId}-${pageNumber}`,
  relevanceScore: 0.9,
  copyrightTier: 'full',
});

const userMsg = (id: string, content = 'u'): ChatMessageItem => ({
  id,
  role: 'user',
  content,
});

const assistantMsg = (
  id: string,
  opts: Partial<Pick<ChatMessageItem, 'citations' | 'followUpQuestions' | 'content'>> = {}
): ChatMessageItem => ({
  id,
  role: 'assistant',
  content: opts.content ?? 'a',
  citations: opts.citations,
  followUpQuestions: opts.followUpQuestions,
});

// ──────────────────────────────────────────────────────────────────
// collectCitations
// ──────────────────────────────────────────────────────────────────

describe('collectCitations', () => {
  it('returns empty array for empty messages', () => {
    expect(collectCitations([])).toEqual([]);
  });

  it('returns empty array when no message has citations', () => {
    const messages: ChatMessageItem[] = [userMsg('u1'), assistantMsg('a1'), userMsg('u2')];
    expect(collectCitations(messages)).toEqual([]);
  });

  it('flattens citations across messages preserving message order and intra-message order', () => {
    const c1 = cite('doc-a', 1);
    const c2 = cite('doc-a', 2);
    const c3 = cite('doc-b', 7);
    const messages: ChatMessageItem[] = [
      userMsg('u1', 'q'),
      assistantMsg('a1', { citations: [c1, c2] }),
      userMsg('u2', 'follow-up'),
      assistantMsg('a2', { citations: [c3] }),
    ];
    expect(collectCitations(messages)).toEqual([c1, c2, c3]);
  });

  it('treats undefined citations as empty (does not throw)', () => {
    const c1 = cite('doc', 1);
    const messages: ChatMessageItem[] = [
      assistantMsg('a1', { citations: undefined }),
      assistantMsg('a2', { citations: [c1] }),
    ];
    expect(collectCitations(messages)).toEqual([c1]);
  });

  it('treats explicitly empty citations array as empty', () => {
    const messages: ChatMessageItem[] = [
      assistantMsg('a1', { citations: [] }),
      assistantMsg('a2', { citations: [] }),
    ];
    expect(collectCitations(messages)).toEqual([]);
  });
});

// ──────────────────────────────────────────────────────────────────
// getSuggestedQuestions
// ──────────────────────────────────────────────────────────────────

describe('getSuggestedQuestions', () => {
  it('returns empty array for empty messages', () => {
    expect(getSuggestedQuestions([])).toEqual([]);
  });

  it('returns empty array when only user messages exist', () => {
    expect(getSuggestedQuestions([userMsg('u1'), userMsg('u2')])).toEqual([]);
  });

  it('returns followUpQuestions of the most recent assistant message', () => {
    const messages: ChatMessageItem[] = [
      userMsg('u1'),
      assistantMsg('a1', { followUpQuestions: ['old-q1', 'old-q2'] }),
      userMsg('u2'),
      assistantMsg('a2', { followUpQuestions: ['new-q1', 'new-q2', 'new-q3'] }),
    ];
    expect(getSuggestedQuestions(messages)).toEqual(['new-q1', 'new-q2', 'new-q3']);
  });

  it('ignores later user messages and still reads the most recent assistant', () => {
    const messages: ChatMessageItem[] = [
      assistantMsg('a1', { followUpQuestions: ['q1'] }),
      userMsg('u1'),
      userMsg('u2'),
    ];
    expect(getSuggestedQuestions(messages)).toEqual(['q1']);
  });

  it('returns empty array when the last assistant message has undefined followUpQuestions', () => {
    const messages: ChatMessageItem[] = [
      assistantMsg('a1', { followUpQuestions: ['q1'] }),
      assistantMsg('a2'),
    ];
    expect(getSuggestedQuestions(messages)).toEqual([]);
  });

  it('returns empty array when the last assistant message has explicit empty followUpQuestions', () => {
    const messages: ChatMessageItem[] = [
      assistantMsg('a1', { followUpQuestions: ['q1'] }),
      assistantMsg('a2', { followUpQuestions: [] }),
    ];
    expect(getSuggestedQuestions(messages)).toEqual([]);
  });

  it('does not mutate the input array (reverse() on copy)', () => {
    const messages: ChatMessageItem[] = [
      userMsg('u1'),
      assistantMsg('a1', { followUpQuestions: ['q1'] }),
    ];
    const snapshot = [...messages];
    getSuggestedQuestions(messages);
    expect(messages).toEqual(snapshot);
  });
});
