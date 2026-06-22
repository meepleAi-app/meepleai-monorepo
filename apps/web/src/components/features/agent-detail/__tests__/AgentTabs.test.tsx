/**
 * AgentTabs unit tests — Wave C.2 Task 2
 *
 * 5 tests: a11y (role=tablist/tab), keyboard nav, tabIdFor/panelIdFor helpers.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AgentTabs, tabIdFor, panelIdFor } from '../AgentTabs';

const LABELS = {
  ariaLabel: "Sezioni dell'agente",
  identity: 'Identità',
  knowledge: 'Conoscenza',
  performance: 'Prestazioni',
  history: 'Storico',
  settings: 'Impostazioni',
};

const ALL_TABS = [
  { key: 'identity' as const, label: LABELS.identity },
  { key: 'knowledge' as const, label: LABELS.knowledge },
  { key: 'performance' as const, label: LABELS.performance },
  { key: 'history' as const, label: LABELS.history },
  { key: 'settings' as const, label: LABELS.settings },
];

describe('AgentTabs', () => {
  it('renders data-slot and role=tablist', () => {
    render(
      <AgentTabs
        tabs={ALL_TABS}
        active="identity"
        onChange={vi.fn()}
        ariaLabel={LABELS.ariaLabel}
      />
    );
    const tablist = document.querySelector('[data-slot="agent-detail-tabs"]');
    expect(tablist).toBeTruthy();
    expect(tablist?.getAttribute('role')).toBe('tablist');
  });

  it('each tab has role=tab and aria-selected', () => {
    render(
      <AgentTabs
        tabs={ALL_TABS}
        active="knowledge"
        onChange={vi.fn()}
        ariaLabel={LABELS.ariaLabel}
      />
    );
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(5);
    const knowledgeTab = tabs.find(t => t.textContent?.includes('Conoscenza'));
    expect(knowledgeTab?.getAttribute('aria-selected')).toBe('true');
  });

  it('locked tabs render with disabled state', () => {
    const tabsWithLock = ALL_TABS.map(t => (t.key === 'performance' ? { ...t, locked: true } : t));
    render(
      <AgentTabs
        tabs={tabsWithLock}
        active="identity"
        onChange={vi.fn()}
        ariaLabel={LABELS.ariaLabel}
      />
    );
    const tabs = screen.getAllByRole('tab');
    const perfTab = tabs.find(t => t.textContent?.includes('Prestazioni'));
    expect(perfTab).toBeDisabled();
  });

  it('tabIdFor and panelIdFor return consistent ids', () => {
    expect(tabIdFor('identity')).toBe('agent-detail-tab-identity');
    expect(panelIdFor('knowledge')).toBe('agent-detail-panel-knowledge');
  });

  it('ArrowRight key calls onChange with next tab key', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <AgentTabs
        tabs={ALL_TABS}
        active="identity"
        onChange={onChange}
        ariaLabel={LABELS.ariaLabel}
      />
    );
    const identityTab = screen.getByRole('tab', { name: /identità/i });
    identityTab.focus();
    await user.keyboard('[ArrowRight]');
    expect(onChange).toHaveBeenCalledWith('knowledge');
  });
});
