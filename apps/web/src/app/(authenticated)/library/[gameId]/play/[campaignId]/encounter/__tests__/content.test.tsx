import { describe, it, expect, vi } from 'vitest';
import { render, screen, type RenderResult } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement, ReactNode } from 'react';
import { IntlProvider } from 'react-intl';

import itMessages from '@/locales/it.json';
import { Content } from '../_content';

// #2750 C11: verify the encounter orchestrator wires the cheatsheet's "Glossario"
// affordance to the read-only GlossaryLookupModal. EncounterCheatsheetView and
// GlossaryLookupModal are tested independently; here we test the _content wiring.

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/lib/gamebook/hooks/useEncounterParse', () => ({
  useEncounterParse: () => ({
    status: 'idle',
    data: null,
    error: undefined,
    mutate: vi.fn(),
    reset: vi.fn(),
  }),
}));

// The real GlossaryLookupModal (imported directly, not via the barrel) reads this.
vi.mock('@/lib/gamebook/hooks/useGamebookGlossary', () => ({
  useGamebookGlossary: () => ({ data: [], isLoading: false, isError: false }),
}));

// Replace EncounterCheatsheetView with a stub that surfaces the onOpenGlossary prop
// as a clickable button, so the wiring (not the FSM) is under test.
vi.mock('@/components/features/gamebook', async importOriginal => {
  const actual = (await importOriginal()) as Record<string, unknown>;
  return {
    ...actual,
    EncounterCheatsheetView: ({ onOpenGlossary }: { onOpenGlossary?: () => void }) => (
      <button type="button" onClick={onOpenGlossary}>
        stub-open-glossary
      </button>
    ),
  };
});

function flatten(obj: Record<string, unknown>, prefix = ''): Record<string, string> {
  return Object.keys(obj).reduce(
    (acc, key) => {
      const full = prefix ? `${prefix}.${key}` : key;
      const value = obj[key];
      if (value && typeof value === 'object') {
        Object.assign(acc, flatten(value as Record<string, unknown>, full));
      } else {
        acc[full] = String(value);
      }
      return acc;
    },
    {} as Record<string, string>
  );
}
const FLAT_IT = flatten(itMessages as Record<string, unknown>);

function renderContent(ui: ReactElement): RenderResult {
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <IntlProvider locale="it" messages={FLAT_IT} onError={() => {}}>
        {children}
      </IntlProvider>
    );
  }
  return render(ui, { wrapper: Wrapper });
}

const PROPS = {
  gameId: 'g1',
  campaignId: 'c1',
  photoId: 'p1',
  paragraphNumber: 218,
  gameBookId: 'b1',
  fromLabel: '147',
  excerpt: 'You face a sentinel.',
};

describe('encounter/_content — GlossaryLookupModal wiring (#2750 C11)', () => {
  it('does not show the glossary modal initially', () => {
    renderContent(<Content {...PROPS} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('opens the read-only glossary look-up when the cheatsheet glossary action fires', async () => {
    renderContent(<Content {...PROPS} />);
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: 'stub-open-glossary' }));

    const dialog = screen.getByRole('dialog', { name: 'Glossario' });
    expect(dialog).toBeVisible();
  });
});
