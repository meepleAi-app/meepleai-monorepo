// @vitest-environment jsdom
import { describe, it, expect, beforeEach } from 'vitest';
import { act } from '@testing-library/react';

import { usePlaygroundStore } from '@/stores/playground-store';

describe('playground-store', () => {
  beforeEach(() => {
    // Reset store to initial state
    act(() => {
      usePlaygroundStore.setState({
        messages: [],
        sessionId: null,
        isStreaming: false,
        currentAgentId: null,
        citations: [],
        stateUpdates: [],
        pipelineSteps: [],
        followUpQuestions: [],
        tokenBreakdown: null,
        confidence: null,
        latencyMs: null,
        systemMessage: '',
      });
    });
    localStorage.clear();
  });

  // ─── Core Actions ────────────────────────────

  it('addMessage creates message with UUID and timestamp', () => {
    act(() => {
      usePlaygroundStore.getState().addMessage({ role: 'user', content: 'Hello' });
    });

    const { messages } = usePlaygroundStore.getState();
    expect(messages).toHaveLength(1);
    expect(messages[0].role).toBe('user');
    expect(messages[0].content).toBe('Hello');
    expect(messages[0].id).toBeDefined();
    expect(messages[0].timestamp).toBeInstanceOf(Date);
  });

  it('appendToLastMessage appends to last assistant message', () => {
    act(() => {
      usePlaygroundStore.getState().addMessage({ role: 'assistant', content: 'Hi' });
      usePlaygroundStore.getState().appendToLastMessage(' there');
    });

    const { messages } = usePlaygroundStore.getState();
    expect(messages[0].content).toBe('Hi there');
  });

  it('appendToLastMessage does nothing if last message is user', () => {
    act(() => {
      usePlaygroundStore.getState().addMessage({ role: 'user', content: 'Hello' });
      usePlaygroundStore.getState().appendToLastMessage(' extra');
    });

    const { messages } = usePlaygroundStore.getState();
    expect(messages[0].content).toBe('Hello');
  });

  it('setMessageFeedback updates feedback on specific message', () => {
    act(() => {
      usePlaygroundStore.getState().addMessage({ role: 'assistant', content: 'Answer' });
    });

    const msgId = usePlaygroundStore.getState().messages[0].id;

    act(() => {
      usePlaygroundStore.getState().setMessageFeedback(msgId, 'up');
    });

    expect(usePlaygroundStore.getState().messages[0].feedback).toBe('up');
  });

  // ─── SSE State Actions ───────────────────────

  it('addCitations merges citations into existing array', () => {
    const batch1 = [{ text: 'a', source: 'doc.pdf', page: 1, line: 1, score: 0.9 }];
    const batch2 = [{ text: 'b', source: 'doc.pdf', page: 2, line: 1, score: 0.7 }];

    act(() => {
      usePlaygroundStore.getState().addCitations(batch1);
      usePlaygroundStore.getState().addCitations(batch2);
    });

    const { citations } = usePlaygroundStore.getState();
    expect(citations).toHaveLength(2);
    expect(citations[0].text).toBe('a');
    expect(citations[1].text).toBe('b');
  });

  it('addStateUpdate appends message and creates pipeline step', () => {
    act(() => {
      usePlaygroundStore.getState().addStateUpdate('Loading agent...');
      usePlaygroundStore.getState().addStateUpdate('Searching KB...');
    });

    const { stateUpdates, pipelineSteps } = usePlaygroundStore.getState();
    expect(stateUpdates).toEqual(['Loading agent...', 'Searching KB...']);
    expect(pipelineSteps).toHaveLength(2);
    expect(pipelineSteps[0].message).toBe('Loading agent...');
    expect(typeof pipelineSteps[0].timestamp).toBe('number');
  });

  it('setFollowUpQuestions replaces existing questions', () => {
    act(() => {
      usePlaygroundStore.getState().setFollowUpQuestions(['Q1', 'Q2']);
    });
    expect(usePlaygroundStore.getState().followUpQuestions).toEqual(['Q1', 'Q2']);

    act(() => {
      usePlaygroundStore.getState().setFollowUpQuestions(['Q3']);
    });
    expect(usePlaygroundStore.getState().followUpQuestions).toEqual(['Q3']);
  });

  it('setCompletionMetadata populates tokenBreakdown and confidence', () => {
    act(() => {
      usePlaygroundStore.getState().setCompletionMetadata({
        promptTokens: 512,
        completionTokens: 234,
        totalTokens: 746,
        confidence: 0.92,
      });
    });

    const { tokenBreakdown, confidence } = usePlaygroundStore.getState();
    expect(tokenBreakdown).toEqual({ prompt: 512, completion: 234, total: 746 });
    expect(confidence).toBe(0.92);
  });

  it('setCompletionMetadata handles missing confidence', () => {
    act(() => {
      usePlaygroundStore.getState().setCompletionMetadata({
        promptTokens: 100,
        completionTokens: 50,
        totalTokens: 150,
      });
    });

    expect(usePlaygroundStore.getState().confidence).toBeNull();
  });

  it('clearResponseState resets per-response fields but keeps messages', () => {
    act(() => {
      usePlaygroundStore.getState().addMessage({ role: 'user', content: 'Hello' });
      usePlaygroundStore.getState().addCitations([{ text: 'a', source: 'doc.pdf', page: 1, line: 1, score: 0.9 }]);
      usePlaygroundStore.getState().addStateUpdate('Loading...');
      usePlaygroundStore.getState().setFollowUpQuestions(['Q1']);
      usePlaygroundStore.getState().setCompletionMetadata({ promptTokens: 100, completionTokens: 50, totalTokens: 150 });
    });

    // Messages should exist
    expect(usePlaygroundStore.getState().messages).toHaveLength(1);

    act(() => {
      usePlaygroundStore.getState().clearResponseState();
    });

    const state = usePlaygroundStore.getState();
    expect(state.messages).toHaveLength(1); // Messages preserved
    expect(state.citations).toEqual([]);
    expect(state.stateUpdates).toEqual([]);
    expect(state.pipelineSteps).toEqual([]);
    expect(state.followUpQuestions).toEqual([]);
    expect(state.tokenBreakdown).toBeNull();
    expect(state.confidence).toBeNull();
  });

  it('clearMessages resets everything including messages', () => {
    act(() => {
      usePlaygroundStore.getState().addMessage({ role: 'user', content: 'Hello' });
      usePlaygroundStore.getState().addCitations([{ text: 'a', source: 'doc.pdf', page: 1, line: 1, score: 0.9 }]);
    });

    act(() => {
      usePlaygroundStore.getState().clearMessages();
    });

    const state = usePlaygroundStore.getState();
    expect(state.messages).toEqual([]);
    expect(state.citations).toEqual([]);
    expect(state.sessionId).toBeNull();
  });

  // ─── System Message ──────────────────────────

  it('setSystemMessage updates system message', () => {
    act(() => {
      usePlaygroundStore.getState().setSystemMessage('You are a board game expert');
    });

    expect(usePlaygroundStore.getState().systemMessage).toBe('You are a board game expert');
  });

  // ─── Session ─────────────────────────────────

  it('startSession creates UUID and resets state', () => {
    act(() => {
      usePlaygroundStore.getState().addMessage({ role: 'user', content: 'Old message' });
      usePlaygroundStore.getState().startSession();
    });

    const state = usePlaygroundStore.getState();
    expect(state.sessionId).toBeDefined();
    expect(state.messages).toEqual([]);
    expect(state.isStreaming).toBe(false);
    expect(state.citations).toEqual([]);
  });
});
