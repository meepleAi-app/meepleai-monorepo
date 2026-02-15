// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  parsePlaygroundSSELine,
  parsePlaygroundSSEChunk,
  StreamingEventType,
  type PlaygroundSSEHandlers,
  type Snippet,
} from '@/lib/agent/playground-sse-parser';

function createMockHandlers(): PlaygroundSSEHandlers {
  return {
    onStateUpdate: vi.fn(),
    onCitations: vi.fn(),
    onToken: vi.fn(),
    onComplete: vi.fn(),
    onFollowUpQuestions: vi.fn(),
    onError: vi.fn(),
    onHeartbeat: vi.fn(),
  };
}

describe('parsePlaygroundSSELine', () => {
  let handlers: PlaygroundSSEHandlers;

  beforeEach(() => {
    handlers = createMockHandlers();
  });

  it('parses StateUpdate event (type 0)', () => {
    const line = 'data: {"type":0,"data":{"message":"Loading agent..."},"timestamp":"2026-02-14T10:00:00Z"}';
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onStateUpdate).toHaveBeenCalledWith('Loading agent...');
    expect(handlers.onToken).not.toHaveBeenCalled();
  });

  it('parses Citations event (type 1)', () => {
    const citations: Snippet[] = [
      { text: 'Setup the board', source: 'Catan_Rules.pdf', page: 3, line: 12, score: 0.92 },
      { text: 'Each player gets', source: 'Catan_Rules.pdf', page: 5, line: 1, score: 0.85 },
    ];
    const line = `data: {"type":1,"data":{"citations":${JSON.stringify(citations)}},"timestamp":"2026-02-14T10:00:01Z"}`;
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onCitations).toHaveBeenCalledWith(citations);
  });

  it('parses Token event (type 7)', () => {
    const line = 'data: {"type":7,"data":{"token":"Hello"},"timestamp":"2026-02-14T10:00:02Z"}';
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onToken).toHaveBeenCalledWith('Hello');
  });

  it('parses Complete event (type 4)', () => {
    const metadata = {
      estimatedReadingTimeMinutes: 2,
      promptTokens: 512,
      completionTokens: 234,
      totalTokens: 746,
      confidence: 0.92,
    };
    const line = `data: {"type":4,"data":${JSON.stringify(metadata)},"timestamp":"2026-02-14T10:00:03Z"}`;
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onComplete).toHaveBeenCalledWith(metadata);
  });

  it('parses Error event (type 5)', () => {
    const line = 'data: {"type":5,"data":{"errorMessage":"Agent not found","errorCode":"AGENT_NOT_FOUND"},"timestamp":"2026-02-14T10:00:04Z"}';
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onError).toHaveBeenCalledWith({
      errorMessage: 'Agent not found',
      errorCode: 'AGENT_NOT_FOUND',
    });
  });

  it('parses Heartbeat event (type 6)', () => {
    const line = 'data: {"type":6,"data":{"message":"keep-alive"},"timestamp":"2026-02-14T10:00:05Z"}';
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onHeartbeat).toHaveBeenCalled();
  });

  it('parses FollowUpQuestions event (type 8)', () => {
    const questions = ['How do you win?', 'What about longest road?'];
    const line = `data: {"type":8,"data":{"questions":${JSON.stringify(questions)}},"timestamp":"2026-02-14T10:00:06Z"}`;
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onFollowUpQuestions).toHaveBeenCalledWith(questions);
  });

  it('ignores empty lines', () => {
    parsePlaygroundSSELine('', handlers);
    parsePlaygroundSSELine('  ', handlers);
    expect(handlers.onToken).not.toHaveBeenCalled();
    expect(handlers.onError).not.toHaveBeenCalled();
  });

  it('ignores comment lines', () => {
    parsePlaygroundSSELine(': comment', handlers);
    expect(handlers.onToken).not.toHaveBeenCalled();
  });

  it('ignores event: prefix lines', () => {
    parsePlaygroundSSELine('event: chunk', handlers);
    expect(handlers.onToken).not.toHaveBeenCalled();
  });

  it('ignores [DONE] signal', () => {
    parsePlaygroundSSELine('data: [DONE]', handlers);
    expect(handlers.onToken).not.toHaveBeenCalled();
    expect(handlers.onError).not.toHaveBeenCalled();
  });

  it('handles malformed JSON gracefully', () => {
    parsePlaygroundSSELine('data: {invalid json}', handlers);
    expect(handlers.onError).toHaveBeenCalledWith(
      expect.objectContaining({
        errorCode: 'PARSE_ERROR',
      }),
    );
  });

  it('ignores unknown event types gracefully', () => {
    const line = 'data: {"type":99,"data":{},"timestamp":"2026-02-14T10:00:07Z"}';
    parsePlaygroundSSELine(line, handlers);
    // No handler should be called for unknown type
    expect(handlers.onToken).not.toHaveBeenCalled();
    expect(handlers.onError).not.toHaveBeenCalled();
    expect(handlers.onStateUpdate).not.toHaveBeenCalled();
  });

  it('silently ignores Outline events (type 2)', () => {
    const line = 'data: {"type":2,"data":{"outline":{}},"timestamp":"2026-02-14T10:00:08Z"}';
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onToken).not.toHaveBeenCalled();
    expect(handlers.onError).not.toHaveBeenCalled();
  });

  it('handles Citations with null citations array', () => {
    const line = 'data: {"type":1,"data":{"citations":null},"timestamp":"2026-02-14T10:00:09Z"}';
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onCitations).toHaveBeenCalledWith([]);
  });

  it('handles FollowUpQuestions with null questions', () => {
    const line = 'data: {"type":8,"data":{"questions":null},"timestamp":"2026-02-14T10:00:10Z"}';
    parsePlaygroundSSELine(line, handlers);
    expect(handlers.onFollowUpQuestions).toHaveBeenCalledWith([]);
  });
});

describe('parsePlaygroundSSEChunk', () => {
  let handlers: PlaygroundSSEHandlers;

  beforeEach(() => {
    handlers = createMockHandlers();
  });

  it('parses multiple lines in a single chunk', () => {
    const chunk = [
      'data: {"type":0,"data":{"message":"Searching..."},"timestamp":"2026-02-14T10:00:00Z"}',
      '',
      'data: {"type":7,"data":{"token":"To"},"timestamp":"2026-02-14T10:00:01Z"}',
      '',
      'data: {"type":7,"data":{"token":" set"},"timestamp":"2026-02-14T10:00:01Z"}',
    ].join('\n');

    parsePlaygroundSSEChunk(chunk, handlers);

    expect(handlers.onStateUpdate).toHaveBeenCalledTimes(1);
    expect(handlers.onStateUpdate).toHaveBeenCalledWith('Searching...');
    expect(handlers.onToken).toHaveBeenCalledTimes(2);
    expect(handlers.onToken).toHaveBeenCalledWith('To');
    expect(handlers.onToken).toHaveBeenCalledWith(' set');
  });

  it('handles chunk with mixed event types', () => {
    const chunk = [
      'data: {"type":1,"data":{"citations":[{"text":"rule","source":"doc.pdf","page":1,"line":1,"score":0.9}]},"timestamp":"t1"}',
      'data: {"type":7,"data":{"token":"Answer"},"timestamp":"t2"}',
      'data: {"type":4,"data":{"promptTokens":100,"completionTokens":50,"totalTokens":150},"timestamp":"t3"}',
    ].join('\n');

    parsePlaygroundSSEChunk(chunk, handlers);

    expect(handlers.onCitations).toHaveBeenCalledTimes(1);
    expect(handlers.onToken).toHaveBeenCalledTimes(1);
    expect(handlers.onComplete).toHaveBeenCalledTimes(1);
  });
});
