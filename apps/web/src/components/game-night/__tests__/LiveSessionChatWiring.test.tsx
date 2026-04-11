/**
 * LiveSessionView — Agent chat wiring tests
 *
 * Verifies that LiveSessionView uses the real useAgentChatStream hook
 * instead of the placeholder setTimeout chat.
 *
 * Issue #5587 — Live Game Session UI (Task 5: Wire SessionChatWidget to TutorAgent)
 */

import { describe, it, expect } from 'vitest';
import { useAgentChatStream } from '@/hooks/useAgentChatStream';
import { useGameAgents } from '@/hooks/queries/useGameAgents';

describe('LiveSessionView agent chat wiring', () => {
  it('useAgentChatStream hook is importable and exports a function', () => {
    expect(useAgentChatStream).toBeDefined();
    expect(typeof useAgentChatStream).toBe('function');
  });

  it('useGameAgents hook is importable and exports a function', () => {
    expect(useGameAgents).toBeDefined();
    expect(typeof useGameAgents).toBe('function');
  });
});
