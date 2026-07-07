import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, type RenderResult } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement, ReactNode } from 'react';
import { IntlProvider } from 'react-intl';

import itMessages from '@/locales/it.json';
import type { GamebookGlossaryEntry } from '@/lib/api/gamebook-glossary';
import { GlossaryLookupModal } from '../GlossaryLookupModal';

// #2750 C11: the modal is a read-only look-up over useGamebookGlossary. Mock the
// hook so no QueryClientProvider is needed and we control the list.
const glossaryState = vi.hoisted(() => ({
  data: undefined as GamebookGlossaryEntry[] | undefined,
  isLoading: false,
  isError: false,
}));
vi.mock('@/lib/gamebook/hooks/useGamebookGlossary', () => ({
  useGamebookGlossary: () => glossaryState,
}));

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

function renderModal(ui: ReactElement): RenderResult {
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <IntlProvider locale="it" messages={FLAT_IT} onError={() => {}}>
        {children}
      </IntlProvider>
    );
  }
  return render(ui, { wrapper: Wrapper });
}

function makeEntry(overrides: Partial<GamebookGlossaryEntry> = {}): GamebookGlossaryEntry {
  return {
    id: 'e1',
    termEn: 'Hive',
    termIt: 'Alveare',
    source: 'AutoBootstrap',
    updatedAt: '2026-07-01T00:00:00Z',
    contexts: [],
    ...overrides,
  };
}

describe('GlossaryLookupModal', () => {
  beforeEach(() => {
    glossaryState.data = undefined;
    glossaryState.isLoading = false;
    glossaryState.isError = false;
  });

  it('renders nothing when closed', () => {
    const { container } = renderModal(
      <GlossaryLookupModal campaignId="c1" isOpen={false} onClose={vi.fn()} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('lists glossary terms EN→IT when open', () => {
    glossaryState.data = [
      makeEntry({ id: '1', termEn: 'Hive', termIt: 'Alveare' }),
      makeEntry({ id: '2', termEn: 'Sword', termIt: 'Spada', source: 'Manual' }),
    ];
    renderModal(<GlossaryLookupModal campaignId="c1" isOpen onClose={vi.fn()} />);

    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText('Hive')).toBeVisible();
    expect(within(dialog).getByText('Alveare')).toBeVisible();
    expect(within(dialog).getByText('Sword')).toBeVisible();
    expect(within(dialog).getByText('Spada')).toBeVisible();
  });

  it('shows the empty state when the campaign has no glossary terms', () => {
    glossaryState.data = [];
    renderModal(<GlossaryLookupModal campaignId="c1" isOpen onClose={vi.fn()} />);
    expect(screen.getByTestId('glossary-lookup-empty')).toBeVisible();
  });

  it('shows an error state when the glossary fetch fails', () => {
    glossaryState.isError = true;
    renderModal(<GlossaryLookupModal campaignId="c1" isOpen onClose={vi.fn()} />);
    expect(screen.getByTestId('glossary-lookup-error')).toBeVisible();
  });

  it('calls onClose when the close button is clicked', async () => {
    glossaryState.data = [];
    const onClose = vi.fn();
    renderModal(<GlossaryLookupModal campaignId="c1" isOpen onClose={onClose} />);
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
