/**
 * Wave A.4 (Issue #603) — AgentListItem rendering tests.
 *
 * Verifies the published agent row contract from spec §3.4:
 *  - Title + invocations meta + last-updated date
 *  - tryHref present → enabled <a> with aria-label
 *  - tryHref absent → disabled <span aria-disabled="true"> fallback
 *  - data-slot + data-agent-id attributes
 *  - Optional description rendering
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AgentListItem } from './agent-list-item';

const labels = {
  updatedPrefix: 'Updated',
  invocationsLabel: (n: number) => `${n} convs`,
  tryLabel: 'Prova →',
  tryAriaLabel: (name: string) => `Try agent ${name}`,
};

describe('AgentListItem (Wave A.4)', () => {
  it('renders name and invocation meta', () => {
    render(
      <AgentListItem
        id="a1"
        name="RuleBot"
        invocationCount={42}
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    expect(screen.getByRole('heading', { level: 3, name: 'RuleBot' })).toBeInTheDocument();
    expect(screen.getByText('42 convs')).toBeInTheDocument();
    expect(screen.getByText('Updated', { exact: false })).toBeInTheDocument();
  });

  it('renders <time> element with dateTime attribute', () => {
    const { container } = render(
      <AgentListItem
        id="a1"
        name="RuleBot"
        invocationCount={0}
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    const time = container.querySelector('time');
    expect(time).not.toBeNull();
    expect(time).toHaveAttribute('datetime', '2026-04-15T00:00:00Z');
  });

  it('renders enabled link when tryHref is provided', () => {
    render(
      <AgentListItem
        id="a1"
        name="RuleBot"
        invocationCount={0}
        lastUpdatedAt="2026-04-15T00:00:00Z"
        tryHref="/agents/a1"
        labels={labels}
      />
    );
    const link = screen.getByRole('link', { name: 'Try agent RuleBot' });
    expect(link).toHaveAttribute('href', '/agents/a1');
    expect(link.tagName).toBe('A');
  });

  it('renders disabled span fallback when tryHref is omitted', () => {
    const { container } = render(
      <AgentListItem
        id="a1"
        name="RuleBot"
        invocationCount={0}
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    expect(container.querySelector('a')).toBeNull();
    const disabled = container.querySelector('[aria-disabled="true"]');
    expect(disabled).not.toBeNull();
    expect(disabled?.textContent).toBe('Prova →');
  });

  it('renders optional description', () => {
    render(
      <AgentListItem
        id="a1"
        name="RuleBot"
        invocationCount={0}
        lastUpdatedAt="2026-04-15T00:00:00Z"
        description="Helpful rules assistant"
        labels={labels}
      />
    );
    expect(screen.getByText('Helpful rules assistant')).toBeInTheDocument();
  });

  it('does not render description paragraph when omitted', () => {
    const { container } = render(
      <AgentListItem
        id="a1"
        name="RuleBot"
        invocationCount={0}
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    // Only the meta line <p> should exist
    expect(container.querySelectorAll('p')).toHaveLength(1);
  });

  it('exposes data-slot and data-agent-id attributes', () => {
    const { container } = render(
      <AgentListItem
        id="agent-xyz"
        name="RuleBot"
        invocationCount={0}
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
      />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-agent-item"]');
    expect(root).not.toBeNull();
    expect(root).toHaveAttribute('data-agent-id', 'agent-xyz');
  });

  it('passes through optional className', () => {
    const { container } = render(
      <AgentListItem
        id="a1"
        name="RuleBot"
        invocationCount={0}
        lastUpdatedAt="2026-04-15T00:00:00Z"
        labels={labels}
        className="custom-cls"
      />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-agent-item"]');
    expect(root?.className).toContain('custom-cls');
  });
});
