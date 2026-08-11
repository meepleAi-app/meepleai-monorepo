/**
 * #2375 G3 — axe AA test for ChatAgentPanel + LiveAgentChat (expanded + collapsed).
 *
 * Renders the panel in isolation with IntlProvider and IntersectionObserver
 * mock, then runs axe-core on both states.
 *
 * Spec: docs/superpowers/specs/2026-06-16-issue-2375-g3-chatagent-always-visible-design.md §7.4
 */

import { describe, it, expect, vi, beforeAll, afterAll } from 'vitest';
import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { IntlProvider } from 'react-intl';

import { ChatAgentPanel } from '@/components/features/session-live/ChatAgentPanel';

// toHaveNoViolations is extended globally in vitest.setup.tsx

const messages = {
  'pages.sessionLive.chat.newMessagesToast':
    '{count, plural, one {# new message} other {# new messages}}',
};

const labels = {
  title: 'ChatAgent',
  agentNameAriaLabel: 'Agent name MeepleAI',
  chatPanelLabels: {
    title: 'Chat',
    inputAriaLabel: 'Write a message',
    sendAriaLabel: 'Send message',
    visibilityPrivate: 'Private',
    visibilityShared: 'Shared',
    emptyMessage: 'No messages yet.',
    newMessagesToastAriaLabel: 'New messages — click to scroll',
    attachAriaLabel: 'Attach image',
  },
};

beforeAll(() => {
  vi.stubGlobal(
    'IntersectionObserver',
    vi.fn(function (this: unknown, cb: IntersectionObserverCallback) {
      return {
        callback: cb,
        observe: vi.fn(),
        disconnect: vi.fn(),
      } as unknown as IntersectionObserver;
    })
  );
});

afterAll(() => {
  vi.unstubAllGlobals();
});

function renderPanel(collapsed: boolean) {
  return render(
    <IntlProvider locale="en" messages={messages}>
      <ChatAgentPanel
        sessionId="sess-1"
        messages={[]}
        viewerRole="Player"
        viewerId="me"
        onSendMessage={() => {}}
        agentName="MeepleAI"
        agentEmoji="🤖"
        collapsed={collapsed}
        onHeaderClick={() => {}}
        labels={labels}
      />
    </IntlProvider>
  );
}

describe('#2375 G3 — ChatAgentPanel axe AA', () => {
  it('expanded state: 0 violations', async () => {
    const { container } = renderPanel(false);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('collapsed state: 0 violations', async () => {
    const { container } = renderPanel(true);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('aria-expanded reflects !collapsed on header button when expanded', () => {
    const { container } = renderPanel(false);
    const button = container.querySelector('button[aria-expanded]');
    expect(button?.getAttribute('aria-expanded')).toBe('true');
  });

  it('aria-expanded reflects !collapsed on header button when collapsed', () => {
    const { container } = renderPanel(true);
    const button = container.querySelector('button[aria-expanded]');
    expect(button?.getAttribute('aria-expanded')).toBe('false');
  });
});
