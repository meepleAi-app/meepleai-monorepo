import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';

import { FilterAccordion } from '../FilterAccordion';

expect.extend(toHaveNoViolations);

const DEFAULT_LABELS = {
  heading: 'Filtri',
  docTypeLabel: 'Tipo documento',
  gameIdLabel: 'Gioco',
  languageLabel: 'Lingua',
  clearAll: 'Cancella filtri',
  docTypeOptions: {
    Rulebook: 'Regolamento',
    Expansion: 'Espansione',
    Errata: 'Errata',
    QuickStart: 'Quick Start',
    Reference: 'Riferimento',
    PlayerAid: 'Aiuto giocatore',
    Other: 'Altro',
  },
  languageOptions: { en: 'English', it: 'Italiano', de: 'Deutsch', fr: 'Français', es: 'Español' },
};

const GAMES_FIXTURE = [
  { id: '00000000-0000-0000-0000-000000000001', name: 'Azul' },
  { id: '00000000-0000-0000-0000-000000000002', name: 'Wingspan' },
];

describe('FilterAccordion (Phase 3 #1737)', () => {
  it('renders all 3 facet sections', () => {
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{}}
        onChange={vi.fn()}
        labels={DEFAULT_LABELS}
      />
    );
    expect(screen.getByText('Tipo documento')).toBeInTheDocument();
    expect(screen.getByText('Gioco')).toBeInTheDocument();
    expect(screen.getByText('Lingua')).toBeInTheDocument();
  });

  it('renders 7 docType options from allowlist (drops faq per DEC-7)', () => {
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{}}
        onChange={vi.fn()}
        labels={DEFAULT_LABELS}
      />
    );
    // Open the section first (assuming default-collapsed accordion behavior).
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));
    expect(screen.getByLabelText('Regolamento')).toBeInTheDocument();
    expect(screen.getByLabelText('Espansione')).toBeInTheDocument();
    expect(screen.getByLabelText('Errata')).toBeInTheDocument();
    expect(screen.getByLabelText('Altro')).toBeInTheDocument();
    expect(screen.queryByLabelText(/FAQ/i)).not.toBeInTheDocument();
  });

  // S1: facet apply
  it('S1: calls onChange with new docType selection on toggle', () => {
    const onChange = vi.fn();
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{}}
        onChange={onChange}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));
    fireEvent.click(screen.getByLabelText('Regolamento'));
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ docType: ['Rulebook'] }));
  });

  // S2: facet clear
  it('S2: clearAll button resets all facets', () => {
    const onChange = vi.fn();
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{ docType: ['Rulebook'], language: 'it' }}
        onChange={onChange}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /cancella filtri/i }));
    expect(onChange).toHaveBeenCalledWith({});
  });

  // S3: URL SSOT round-trip (controlled component reflects external `selected`)
  it('S3: pre-selects facets from controlled `selected` prop', () => {
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{ docType: ['Rulebook'], language: 'it' }}
        onChange={vi.fn()}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /tipo documento/i }));
    const checkbox = screen.getByLabelText('Regolamento') as HTMLInputElement;
    expect(checkbox.checked).toBe(true);
  });

  it('multi-select GameId: toggle adds a uuid to the array', () => {
    const onChange = vi.fn();
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{ gameId: ['00000000-0000-0000-0000-000000000001'] }}
        onChange={onChange}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /gioco/i }));
    fireEvent.click(screen.getByLabelText('Wingspan'));
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        gameId: expect.arrayContaining([
          '00000000-0000-0000-0000-000000000001',
          '00000000-0000-0000-0000-000000000002',
        ]),
      })
    );
  });

  it('single-select Language: replaces the previous value', () => {
    const onChange = vi.fn();
    render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{ language: 'it' }}
        onChange={onChange}
        labels={DEFAULT_LABELS}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /lingua/i }));
    fireEvent.click(screen.getByLabelText('English'));
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ language: 'en' }));
  });

  it('a11y: no jest-axe violations', async () => {
    const { container } = render(
      <FilterAccordion
        availableGames={GAMES_FIXTURE}
        selected={{}}
        onChange={vi.fn()}
        labels={DEFAULT_LABELS}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
