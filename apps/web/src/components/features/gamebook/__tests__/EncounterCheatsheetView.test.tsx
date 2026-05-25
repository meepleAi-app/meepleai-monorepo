import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import type { EncounterCheatsheet } from '@/lib/api/gamebook-encounter';

import {
  EncounterCheatsheetView,
  type EncounterCheatsheetLabels,
} from '../EncounterCheatsheetView';

const labels: EncounterCheatsheetLabels = {
  entryStoryMeta: 'Storybook · ultimo paragrafo letto',
  entryRefTitle: '→ Encounter §218',
  entryRefHint: 'Fotografa l’Encounter Book',
  entryCta: 'Fotografa Encounter §218',
  entryCtaHint: 'card pronta in ~6s · nessun salvataggio',
  loadingTitle: 'Analizzo l’Encounter',
  loadingHint: 'Estraggo stats e opzioni',
  cancel: 'Annulla',
  optionsTitle: 'Le tue opzioni',
  conditionsWin: '🏆 Vittoria',
  conditionsLoss: '💀 Sconfitta',
  parseConfidence: 'Affidabilità parse',
  lowConfidenceHint: 'Bassa affidabilità — verifica manualmente',
  ephemeralNote: 'Card ephemeral — non salvata',
  retake: 'Rifotografa',
  glossary: 'Glossario',
  resolve: 'Risolvi',
  errorParseFailed: 'Non sono riuscito a leggere l’encounter',
  errorNotFound: 'Foto o paragrafo non trovati',
  errorGeneric: 'Qualcosa è andato storto',
  retry: 'Riprova',
  confidence: {
    high: 'Alta affidabilità',
    medium: 'Media affidabilità',
    low: 'Bassa affidabilità',
  },
};

const cheatsheet: EncounterCheatsheet = {
  enemies: [
    {
      name: 'Goblin Scout (×3)',
      icon: '⚔️',
      paragraphMarker: '§218',
      hp: '8',
      atk: '+3',
      def: '12',
      mov: '5',
    },
  ],
  options: [
    {
      label: 'Attacca subito',
      diceRoll: { sides: 6, count: 1, modifier: 0, threshold: 4 },
      outcome: 'Successo → §223',
    },
    { label: 'Tenta di parlare', diceRoll: null, outcome: null },
  ],
  conditions: { win: 'KO tutti i Goblin', loss: 'Party HP ≤ 0 → §247' },
  confidence: { enemies: 0.94, options: 0.94, conditions: 0.88 },
};

function noop() {
  /* no-op */
}

describe('EncounterCheatsheetView', () => {
  // --- State A: idle / entry ------------------------------------------------
  it('renders the entry CTA and triggers onParse when clicked', async () => {
    const onParse = vi.fn();
    render(
      <EncounterCheatsheetView
        status="idle"
        cheatsheet={null}
        labels={labels}
        onParse={onParse}
        onResolve={noop}
      />
    );

    const cta = screen.getByRole('button', { name: labels.entryCta });
    expect(cta).toBeInTheDocument();
    await userEvent.click(cta);
    expect(onParse).toHaveBeenCalledTimes(1);
  });

  it('renders the optional story context excerpt in the entry state', () => {
    render(
      <EncounterCheatsheetView
        status="idle"
        cheatsheet={null}
        storyContext={{ paragraphLabel: '§147', excerpt: 'Vedi tre figure incappucciate.' }}
        labels={labels}
        onParse={noop}
        onResolve={noop}
      />
    );

    expect(screen.getByText('§147')).toBeInTheDocument();
    expect(screen.getByText(/tre figure incappucciate/)).toBeInTheDocument();
  });

  // --- State B: parsing / loading -------------------------------------------
  it('renders a busy loading state with a cancel control', async () => {
    const onCancel = vi.fn();
    render(
      <EncounterCheatsheetView
        status="parsing"
        cheatsheet={null}
        labels={labels}
        onParse={noop}
        onResolve={noop}
        onCancel={onCancel}
      />
    );

    expect(screen.getByText(labels.loadingTitle)).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true');
    await userEvent.click(screen.getByRole('button', { name: labels.cancel }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  // --- State C: rendered / cheatsheet ---------------------------------------
  it('renders the enemy stat block (HP/ATK/DEF/MOV) verbatim', () => {
    render(
      <EncounterCheatsheetView
        status="rendered"
        cheatsheet={cheatsheet}
        labels={labels}
        onParse={noop}
        onResolve={noop}
      />
    );

    expect(screen.getByText('Goblin Scout (×3)')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
    expect(screen.getByText('+3')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('renders options with inline dice roll and outcome', () => {
    render(
      <EncounterCheatsheetView
        status="rendered"
        cheatsheet={cheatsheet}
        labels={labels}
        onParse={noop}
        onResolve={noop}
      />
    );

    expect(screen.getByText('Attacca subito')).toBeInTheDocument();
    expect(screen.getByText(/1d6/)).toBeInTheDocument();
    expect(screen.getByText(/≥\s*4/)).toBeInTheDocument();
    expect(screen.getByText('Successo → §223')).toBeInTheDocument();
    // narrative option (no diceRoll) still renders its label
    expect(screen.getByText('Tenta di parlare')).toBeInTheDocument();
  });

  it('renders win and loss conditions', () => {
    render(
      <EncounterCheatsheetView
        status="rendered"
        cheatsheet={cheatsheet}
        labels={labels}
        onParse={noop}
        onResolve={noop}
      />
    );

    expect(screen.getByText('KO tutti i Goblin')).toBeInTheDocument();
    expect(screen.getByText('Party HP ≤ 0 → §247')).toBeInTheDocument();
  });

  it('wires resolve, retake and glossary toolbar actions', async () => {
    const onResolve = vi.fn();
    const onParse = vi.fn();
    const onOpenGlossary = vi.fn();
    render(
      <EncounterCheatsheetView
        status="rendered"
        cheatsheet={cheatsheet}
        labels={labels}
        onParse={onParse}
        onResolve={onResolve}
        onOpenGlossary={onOpenGlossary}
      />
    );

    await userEvent.click(screen.getByRole('button', { name: labels.resolve }));
    await userEvent.click(screen.getByRole('button', { name: labels.retake }));
    await userEvent.click(screen.getByRole('button', { name: labels.glossary }));
    expect(onResolve).toHaveBeenCalledTimes(1);
    expect(onParse).toHaveBeenCalledTimes(1);
    expect(onOpenGlossary).toHaveBeenCalledTimes(1);
  });

  it('does NOT show the low-confidence hint when every cluster is ≥ 0.6', () => {
    render(
      <EncounterCheatsheetView
        status="rendered"
        cheatsheet={cheatsheet}
        labels={labels}
        onParse={noop}
        onResolve={noop}
      />
    );
    expect(screen.queryByText(labels.lowConfidenceHint)).not.toBeInTheDocument();
  });

  it('shows the low-confidence hint (§9.1) when any cluster is < 0.6', () => {
    render(
      <EncounterCheatsheetView
        status="rendered"
        cheatsheet={{
          ...cheatsheet,
          confidence: { enemies: 0.94, options: 0.94, conditions: 0.45 },
        }}
        labels={labels}
        onParse={noop}
        onResolve={noop}
      />
    );
    expect(screen.getByText(labels.lowConfidenceHint)).toBeInTheDocument();
  });

  // --- Error paths ----------------------------------------------------------
  it('renders the parse-failed message (409) with a retry that calls onParse', async () => {
    const onParse = vi.fn();
    render(
      <EncounterCheatsheetView
        status="error"
        cheatsheet={null}
        errorKind="parse-failed"
        labels={labels}
        onParse={onParse}
        onResolve={noop}
      />
    );

    expect(screen.getByRole('alert')).toHaveTextContent(labels.errorParseFailed);
    await userEvent.click(screen.getByRole('button', { name: labels.retry }));
    expect(onParse).toHaveBeenCalledTimes(1);
  });

  it('renders the not-found message (404)', () => {
    render(
      <EncounterCheatsheetView
        status="error"
        cheatsheet={null}
        errorKind="not-found"
        labels={labels}
        onParse={noop}
        onResolve={noop}
      />
    );
    expect(screen.getByRole('alert')).toHaveTextContent(labels.errorNotFound);
  });

  // --- Accessibility (axe across the visible states) ------------------------
  it('has no axe violations in the entry state', async () => {
    const { container } = render(
      <EncounterCheatsheetView
        status="idle"
        cheatsheet={null}
        storyContext={{ paragraphLabel: '§147', excerpt: 'Vedi tre figure.' }}
        labels={labels}
        onParse={noop}
        onResolve={noop}
      />
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('has no axe violations in the parsing state', async () => {
    const { container } = render(
      <EncounterCheatsheetView
        status="parsing"
        cheatsheet={null}
        labels={labels}
        onParse={noop}
        onResolve={noop}
        onCancel={noop}
      />
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('has no axe violations in the rendered cheatsheet state', async () => {
    const { container } = render(
      <EncounterCheatsheetView
        status="rendered"
        cheatsheet={cheatsheet}
        labels={labels}
        onParse={noop}
        onResolve={noop}
        onOpenGlossary={noop}
      />
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('has no axe violations in the error state', async () => {
    const { container } = render(
      <EncounterCheatsheetView
        status="error"
        cheatsheet={null}
        errorKind="parse-failed"
        labels={labels}
        onParse={noop}
        onResolve={noop}
      />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
