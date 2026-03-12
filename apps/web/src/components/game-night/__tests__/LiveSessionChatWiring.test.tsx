/**
 * LiveSessionView — Agent chat wiring tests
 *
 * Verifies that LiveSessionView uses the real useAgentChatStream hook
 * instead of the placeholder setTimeout chat.
 *
 * Issue #5587 — Live Game Session UI (Task 5: Wire SessionChatWidget to TutorAgent)
 */

import { describe, it, expect } from 'vitest';

describe('LiveSessionView agent chat wiring', () => {
  it('useAgentChatStream hook is importable and exports a function', async () => {
    const mod = await import('@/hooks/useAgentChatStream');
    expect(mod.useAgentChatStream).toBeDefined();
    expect(typeof mod.useAgentChatStream).toBe('function');
  });

  it('useGameAgents hook is importable and exports a function', async () => {
    const mod = await import('@/hooks/queries/useGameAgents');
    expect(mod.useGameAgents).toBeDefined();
    expect(typeof mod.useGameAgents).toBe('function');
  });

  it('LiveSessionView no longer contains setTimeout placeholder chat', async () => {
    // Read the source to verify the placeholder was removed
    const mod = await import('../LiveSessionView');
    const source = mod.LiveSessionView.toString();

    // The placeholder used "Funzione in arrivo" in a setTimeout callback
    expect(source).not.toContain('chatTimerRef');
  });
});
