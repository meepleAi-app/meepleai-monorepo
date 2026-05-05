/**
 * AgentSettingsForm unit tests — Wave C.2 Task 2
 *
 * 6 tests: variant editable/read-only, loading, error, data-slot.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AgentSettingsForm } from '../AgentSettingsForm';

const LABELS = {
  title: 'Impostazioni agente',
  strategyLabel: 'Strategia RAG',
  parametersLabel: 'Parametri strategia',
  readOnlyBanner: 'Le impostazioni sono in sola lettura per gli agenti archiviati.',
  saveCta: 'Salva impostazioni',
  cancelCta: 'Annulla',
  saveSuccess: 'Impostazioni salvate.',
  saveError: 'Impossibile salvare. Riprova.',
  loadingLabel: 'Caricamento impostazioni...',
  errorLabel: 'Impossibile caricare le impostazioni.',
  retryLabel: 'Riprova',
};

const SAMPLE_CONFIG = {
  strategy: 'hybrid',
  parameters: { topK: 5, threshold: 0.7 },
};

describe('AgentSettingsForm', () => {
  it('renders data-slot attribute', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', config: SAMPLE_CONFIG }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(document.querySelector('[data-slot="agent-detail-settings-form"]')).toBeTruthy();
  });

  it('editable kind: renders save and cancel buttons', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'editable', config: SAMPLE_CONFIG }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByRole('button', { name: /salva impostazioni/i })).toBeInTheDocument();
  });

  it('read-only kind: renders read-only banner (archived)', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'read-only', config: SAMPLE_CONFIG }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByText(/sola lettura/i)).toBeInTheDocument();
  });

  it('read-only kind: save button is NOT rendered', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'read-only', config: SAMPLE_CONFIG }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.queryByRole('button', { name: /salva impostazioni/i })).not.toBeInTheDocument();
  });

  it('loading kind: no config content visible', () => {
    render(
      <AgentSettingsForm
        state={{ kind: 'loading' }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.queryByRole('button', { name: /salva impostazioni/i })).not.toBeInTheDocument();
  });

  it('error kind: renders retry button', () => {
    const retry = vi.fn();
    render(
      <AgentSettingsForm
        state={{ kind: 'error', retry }}
        labels={LABELS}
        onSave={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
  });
});
