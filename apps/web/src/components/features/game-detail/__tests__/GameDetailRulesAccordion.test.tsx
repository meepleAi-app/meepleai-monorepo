/**
 * Wave C.1 (Issue #581) — GameDetailRulesAccordion unit tests.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import {
  GameDetailRulesAccordion,
  type GameDetailRuleSection,
  type GameDetailRulesAccordionLabels,
} from '../GameDetailRulesAccordion';

const labels: GameDetailRulesAccordionLabels = {
  title: 'Regole',
  subtitle: 'Sezioni principali.',
  viewAll: 'Vedi tutto',
  viewAllAriaLabel: 'Apri pagina regolamento',
  empty: 'Nessuna sezione',
};

const sections: ReadonlyArray<GameDetailRuleSection> = [
  { id: 's1', title: 'Setup iniziale', summary: 'Mescola il mazzo, distribuisci le tessere.' },
  { id: 's2', title: 'Turno di gioco', summary: 'Pesca, gioca, attiva poteri.' },
  { id: 's3', title: 'Fine partita', summary: 'Conta i punti, vince chi ha più carte.' },
];

describe('GameDetailRulesAccordion (Wave C.1)', () => {
  it('renders empty state when no sections are provided', () => {
    render(<GameDetailRulesAccordion sections={[]} viewAllHref="/games/g/rules" labels={labels} />);
    expect(screen.getByText('Nessuna sezione')).toBeInTheDocument();
  });

  it('renders one details/summary per section', () => {
    const { container } = render(
      <GameDetailRulesAccordion sections={sections} viewAllHref="/games/g/rules" labels={labels} />
    );
    const detailsList = container.querySelectorAll(
      'details[data-slot="game-detail-rules-section"]'
    );
    expect(detailsList).toHaveLength(3);
  });

  it('opens sections listed in defaultOpen', () => {
    const { container } = render(
      <GameDetailRulesAccordion
        sections={sections}
        viewAllHref="/games/g/rules"
        labels={labels}
        defaultOpen={['s1']}
      />
    );
    const s1 = container.querySelector(
      'details[data-section-id="s1"]'
    ) as HTMLDetailsElement | null;
    const s2 = container.querySelector(
      'details[data-section-id="s2"]'
    ) as HTMLDetailsElement | null;
    expect(s1?.open).toBe(true);
    expect(s2?.open).toBe(false);
  });

  it('toggles a section open on summary click', () => {
    const { container } = render(
      <GameDetailRulesAccordion sections={sections} viewAllHref="/games/g/rules" labels={labels} />
    );
    const details = container.querySelector('details[data-section-id="s1"]') as HTMLDetailsElement;
    const summary = details.querySelector('summary')!;
    expect(details.open).toBe(false);
    fireEvent.click(summary);
    expect(details.open).toBe(true);
  });

  it('exposes view-all link with href', () => {
    render(
      <GameDetailRulesAccordion
        sections={sections}
        viewAllHref="/games/abc/rules"
        labels={labels}
      />
    );
    expect(screen.getByRole('link', { name: 'Apri pagina regolamento' })).toHaveAttribute(
      'href',
      '/games/abc/rules'
    );
  });

  it('exposes data-slot="game-detail-rules-accordion" for E2E selector', () => {
    const { container } = render(
      <GameDetailRulesAccordion sections={sections} viewAllHref="/games/g/rules" labels={labels} />
    );
    expect(
      container.querySelector('[data-slot="game-detail-rules-accordion"]')
    ).toBeInTheDocument();
  });
});
