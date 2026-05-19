/**
 * GlossaryEditorModal — tests for issue #952.
 *
 * Covers ACs from `docs/superpowers/specs/2026-05-19-glossary-editor-modal-952.md`
 * §8 (hardened version, post panel critique).
 *
 * NOTE: bypasses `renderWithProviders` because that helper imports a
 * `ChatStoreProvider` that no longer exists in the codebase (pre-existing
 * test infra regression unrelated to #952). Builds a minimal local wrapper.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, type RenderResult } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { HttpResponse } from 'msw';
import type { ReactElement, ReactNode } from 'react';
import { IntlProvider } from 'react-intl';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  getCapturedGlossaryUpserts,
  resetCapturedGlossaryUpserts,
  resetGlossaryResponder,
  setUpsertResponder,
} from '@/__tests__/mocks/handlers/gamebook-glossary.handlers';
import enMessages from '@/locales/en.json';
import itMessages from '@/locales/it.json';
import type { GamebookGlossaryEntry } from '@/lib/api/gamebook-glossary';

import { GlossaryEditorModal } from '../GlossaryEditorModal';

// ---------------------------------------------------------------------------
// Test harness
// ---------------------------------------------------------------------------

// react-intl wants dot-notation flat keys; the locale catalogue is nested.
function flatten(obj: Record<string, unknown>, prefix = ''): Record<string, string> {
  return Object.keys(obj).reduce((acc, key) => {
    const full = prefix ? `${prefix}.${key}` : key;
    const value = obj[key];
    if (value && typeof value === 'object') {
      Object.assign(acc, flatten(value as Record<string, unknown>, full));
    } else {
      acc[full] = String(value);
    }
    return acc;
  }, {} as Record<string, string>);
}

const FLAT_EN = flatten(enMessages as Record<string, unknown>);
const FLAT_IT = flatten(itMessages as Record<string, unknown>);

function makeQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0, staleTime: 0 },
      mutations: { retry: false },
    },
  });
}

function renderModal(
  ui: ReactElement,
  options: { locale?: 'en' | 'it' } = {}
): RenderResult & { queryClient: QueryClient } {
  const queryClient = makeQueryClient();
  const locale = options.locale ?? 'en';
  const messages = locale === 'it' ? FLAT_IT : FLAT_EN;

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <IntlProvider locale={locale} messages={messages}>
          {children}
        </IntlProvider>
      </QueryClientProvider>
    );
  }

  const result = render(ui, { wrapper: Wrapper });
  return { ...result, queryClient };
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const CAMPAIGN_ID = '11111111-2222-4333-8444-555555555555';
const ENTRY: GamebookGlossaryEntry = {
  id: '66666666-7777-4888-8999-aaaaaaaaaaaa',
  termEn: 'Voidstone',
  termIt: 'Pietra del Vuoto',
  source: 'AutoBootstrap',
  updatedAt: '2026-05-01T12:00:00Z',
};

beforeEach(() => {
  resetCapturedGlossaryUpserts();
  resetGlossaryResponder();
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ---------------------------------------------------------------------------
// AC-1 — Pristine mount
// ---------------------------------------------------------------------------

describe('AC-1 — Pristine mount', () => {
  it('mounts with EN locked, IT prefilled, Salva button disabled', () => {
    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onClose={() => {}}
      />
    );

    // EN rendered as locked / read-only text (not in an editable input).
    expect(screen.getByText('Voidstone')).toBeInTheDocument();

    // IT input is prefilled with the current translation.
    const itInput = screen.getByRole('textbox', { name: /italian translation/i });
    expect(itInput).toHaveValue('Pietra del Vuoto');

    // Save button starts disabled because the user hasn't dirtied the input.
    const saveButton = screen.getByRole('button', { name: /^save$/i });
    expect(saveButton).toBeDisabled();
  });
});

// ---------------------------------------------------------------------------
// AC-2 — Dirty edit + diff hint
// ---------------------------------------------------------------------------

describe('AC-2 — Dirty edit + diff hint', () => {
  it('enables Salva and renders the diff hint with the previous value struck through', async () => {
    const user = userEvent.setup();
    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onClose={() => {}}
      />
    );

    const itInput = screen.getByRole('textbox', { name: /italian translation/i });
    await user.clear(itInput);
    await user.type(itInput, 'Pietra del Caos');

    // Save now enabled.
    const saveButton = screen.getByRole('button', { name: /^save$/i });
    expect(saveButton).not.toBeDisabled();

    // Diff hint contains the original value with the Tailwind line-through utility.
    // Tailwind CSS is not loaded in jsdom, so we assert via className rather than
    // computed style (the production CSS resolves to `text-decoration: line-through`).
    const diffHint = screen.getByText('Pietra del Vuoto');
    expect(diffHint).toHaveClass('line-through');
  });
});

// ---------------------------------------------------------------------------
// AC-3 — Submit success
// ---------------------------------------------------------------------------

describe('AC-3 — Submit success', () => {
  it('fires onSaved with the returned entry on 200, does NOT auto-call onClose', async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    const onClose = vi.fn();

    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onSaved={onSaved}
        onClose={onClose}
      />
    );

    const itInput = screen.getByRole('textbox', { name: /italian translation/i });
    await user.clear(itInput);
    await user.type(itInput, 'Pietra del Caos');

    await user.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => {
      expect(onSaved).toHaveBeenCalledTimes(1);
    });
    expect(onSaved).toHaveBeenCalledWith(
      expect.objectContaining({ termEn: 'Voidstone', termIt: 'Pietra del Caos' })
    );
    expect(onClose).not.toHaveBeenCalled();

    // Verify the PUT actually hit the backend with the new termIt.
    const captured = getCapturedGlossaryUpserts();
    expect(captured).toHaveLength(1);
    expect(captured[0]).toMatchObject({
      campaignId: CAMPAIGN_ID,
      entryId: ENTRY.id,
      body: { termEn: 'Voidstone', termIt: 'Pietra del Caos' },
    });
  });
});

// ---------------------------------------------------------------------------
// AC-3b — Dismiss flows (Escape / scrim / X)
// ---------------------------------------------------------------------------

describe('AC-3b — Dismiss flows', () => {
  it('calls onClose when the user presses Escape', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const onSaved = vi.fn();

    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onSaved={onSaved}
        onClose={onClose}
      />
    );

    await user.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onSaved).not.toHaveBeenCalled();
  });

  it('calls onClose when the user clicks the scrim', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const onSaved = vi.fn();

    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onSaved={onSaved}
        onClose={onClose}
      />
    );

    const scrim = screen.getByTestId('glossary-editor-scrim');
    await user.click(scrim);

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onSaved).not.toHaveBeenCalled();
  });

  it('calls onClose when the user clicks the X button', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const onSaved = vi.fn();

    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onSaved={onSaved}
        onClose={onClose}
      />
    );

    await user.click(screen.getByRole('button', { name: /^close$/i }));

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onSaved).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// AC-8 — Accessibility (role/aria + focus + axe across 4 in-scope states)
// ---------------------------------------------------------------------------

describe('AC-8 — Accessibility', () => {
  it('exposes role="dialog" + aria-modal + aria-labelledby pointing to the header', () => {
    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onClose={() => {}}
      />
    );

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');

    const labelledBy = dialog.getAttribute('aria-labelledby');
    expect(labelledBy).toBeTruthy();
    const labelEl = document.getElementById(labelledBy!);
    expect(labelEl).not.toBeNull();
    expect(labelEl?.textContent).toBe('Edit glossary entry');
  });

  it('moves focus to the IT input on mount (not the close button)', async () => {
    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onClose={() => {}}
      />
    );

    const itInput = screen.getByRole('textbox', { name: /italian translation/i });
    await waitFor(() => {
      expect(document.activeElement).toBe(itInput);
    });
  });

  // jest-axe across the 4 in-scope states. State-04 (collision) is OOS.
  const axeStates: Array<{
    name: string;
    render: () => RenderResult;
  }> = [
    {
      name: 's01-m (pristine mobile)',
      render: () =>
        renderModal(
          <GlossaryEditorModal
            campaignId={CAMPAIGN_ID}
            entry={ENTRY}
            onClose={() => {}}
            forceLayout="mobile"
          />
        ),
    },
    {
      name: 's01-d (pristine desktop)',
      render: () =>
        renderModal(
          <GlossaryEditorModal
            campaignId={CAMPAIGN_ID}
            entry={ENTRY}
            onClose={() => {}}
            forceLayout="desktop"
          />
        ),
    },
    // s02-m (edited) and s03-m (error) are driven by user interaction;
    // axe-checked inline below after the interaction completes.
  ];

  for (const { name, render: renderFn } of axeStates) {
    it(`axe finds zero violations on ${name}`, async () => {
      const { container } = renderFn();
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  }

  it('axe finds zero violations on s02-m (edited)', async () => {
    const user = userEvent.setup();
    const { container } = renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onClose={() => {}}
        forceLayout="mobile"
      />
    );
    const itInput = screen.getByRole('textbox', { name: /italian translation/i });
    await user.clear(itInput);
    await user.type(itInput, 'Pietra del Caos');
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('axe finds zero violations on s03-m (save-error)', async () => {
    const user = userEvent.setup();
    setUpsertResponder(() => new HttpResponse(null, { status: 500 }));
    const { container } = renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onClose={() => {}}
        forceLayout="mobile"
      />
    );
    const itInput = screen.getByRole('textbox', { name: /italian translation/i });
    await user.clear(itInput);
    await user.type(itInput, 'Pietra del Caos');
    await user.click(screen.getByRole('button', { name: /^save$/i }));
    await screen.findByTestId('glossary-editor-error');
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// AC-6 — Desktop layout
// ---------------------------------------------------------------------------

describe('AC-6 — Desktop layout', () => {
  it('renders the desktop dialog marker when forceLayout === "desktop"', () => {
    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onClose={() => {}}
        forceLayout="desktop"
      />
    );

    expect(screen.getByTestId('glossary-editor-dialog-desktop')).toBeInTheDocument();
  });

  it('does NOT render the desktop dialog marker when forceLayout === "mobile"', () => {
    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onClose={() => {}}
        forceLayout="mobile"
      />
    );

    expect(screen.queryByTestId('glossary-editor-dialog-desktop')).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// AC-9 — i18n integration (it locale)
// ---------------------------------------------------------------------------

describe('AC-9 — i18n integration', () => {
  it('renders Italian labels when locale is "it" and preserves the entity literals', () => {
    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onClose={() => {}}
      />,
      { locale: 'it' }
    );

    // Italian dialog title from the it.json catalogue (not the en fallback).
    expect(screen.getByText('Modifica voce di glossario')).toBeInTheDocument();

    // Italian save button label.
    expect(screen.getByRole('button', { name: /^salva$/i })).toBeInTheDocument();

    // Entity literals are injected via props, not translated.
    expect(screen.getByText('Voidstone')).toBeInTheDocument();
    expect(
      screen.getByRole('textbox', { name: /traduzione italiana/i })
    ).toHaveValue('Pietra del Vuoto');
  });
});

// ---------------------------------------------------------------------------
// AC-4 — Submit error + retry
// ---------------------------------------------------------------------------

describe('AC-4 — Submit error + retry', () => {
  it('renders error banner on 500 and re-fires the mutation on Riprova click', async () => {
    const user = userEvent.setup();
    // First call: 500. Second call (Retry): success via the default echo responder.
    let callCount = 0;
    setUpsertResponder(({ entryId, body }) => {
      callCount += 1;
      if (callCount === 1) {
        return new HttpResponse(null, { status: 500, statusText: 'Internal Server Error' });
      }
      return HttpResponse.json({
        id: entryId,
        termEn: body.termEn,
        termIt: body.termIt,
        source: 'Manual',
        updatedAt: new Date('2026-05-19T11:00:00Z').toISOString(),
      });
    });

    const onSaved = vi.fn();
    const onClose = vi.fn();

    renderModal(
      <GlossaryEditorModal
        campaignId={CAMPAIGN_ID}
        entry={ENTRY}
        onSaved={onSaved}
        onClose={onClose}
      />
    );

    const itInput = screen.getByRole('textbox', { name: /italian translation/i });
    await user.clear(itInput);
    await user.type(itInput, 'Pietra del Caos');

    await user.click(screen.getByRole('button', { name: /^save$/i }));

    // Error banner appears with the Retry button.
    const errorBanner = await screen.findByTestId('glossary-editor-error');
    expect(errorBanner).toBeInTheDocument();
    expect(onSaved).not.toHaveBeenCalled();

    // Click Retry. Second PUT fires.
    await user.click(screen.getByRole('button', { name: /^retry$/i }));

    await waitFor(() => {
      expect(onSaved).toHaveBeenCalledTimes(1);
    });

    // Both calls captured with the same payload.
    const captured = getCapturedGlossaryUpserts();
    expect(captured).toHaveLength(2);
    expect(captured[0].body).toEqual(captured[1].body);
    expect(captured[0].body.termIt).toBe('Pietra del Caos');
  });
});
