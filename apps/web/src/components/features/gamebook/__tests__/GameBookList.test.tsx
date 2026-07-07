/**
 * GameBookList — tests for issue #2637 (SI-6): the 1..N book-manager surface.
 *
 * Renders the shipped `GameBook` aggregate (community `ownerUserId=null` +
 * personal) with role/origin/status badges, replacing the hardcoded
 * "Press Start + Rules" 2-PDF model. Covers loading / empty / list states.
 */

import { render, screen, within, type RenderResult } from '@testing-library/react';
import { axe } from 'jest-axe';
import type { ReactElement, ReactNode } from 'react';
import { IntlProvider } from 'react-intl';
import { describe, expect, it } from 'vitest';

import enMessages from '@/locales/en.json';
import itMessages from '@/locales/it.json';
import { GameBookRole, type GameBookDto } from '@/lib/api/gamebook';

import { GameBookList } from '../GameBookList';

// react-intl wants dot-notation flat keys; the locale catalogue is nested.
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
const FLAT_EN = flatten(enMessages as Record<string, unknown>);

function renderList(ui: ReactElement, locale: 'it' | 'en' = 'it'): RenderResult {
  const messages = locale === 'it' ? FLAT_IT : FLAT_EN;
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <IntlProvider locale={locale} messages={messages} onError={() => {}}>
        {children}
      </IntlProvider>
    );
  }
  return render(ui, { wrapper: Wrapper });
}

let seq = 0;
function makeBook(overrides: Partial<GameBookDto> = {}): GameBookDto {
  seq += 1;
  return {
    id: overrides.id ?? `book-${seq}`,
    gameRefId: 'game-1',
    gameRefKind: 0,
    ownerUserId: null,
    displayName: `Book ${seq}`,
    roles: GameBookRole.RulesReference,
    paragraphScheme: 0,
    language: 'it',
    sequentialRead: false,
    kbSourceDocId: 'kb-1',
    physicalOnly: false,
    createdAt: '2026-07-01T00:00:00Z',
    ...overrides,
  };
}

describe('GameBookList', () => {
  it('renders the loading skeleton while the caller query is in flight', () => {
    renderList(<GameBookList books={[]} isLoading />);
    expect(screen.getByTestId('game-book-list-loading')).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByTestId('game-book-list')).not.toBeInTheDocument();
  });

  it('renders the empty state when the game has no books', () => {
    renderList(<GameBookList books={[]} />);
    expect(screen.getByTestId('game-book-list-empty')).toBeInTheDocument();
    expect(screen.queryByTestId('game-book-list')).not.toBeInTheDocument();
  });

  it('renders one row per book with its displayName (1..N, not the fixed 2-PDF model)', () => {
    const books = [
      makeBook({ id: 'a', displayName: 'Manuale Base' }),
      makeBook({ id: 'b', displayName: 'Storybook' }),
      makeBook({ id: 'c', displayName: 'Encounter Book' }),
    ];
    renderList(<GameBookList books={books} />);
    const list = screen.getByTestId('game-book-list');
    expect(within(list).getAllByRole('listitem')).toHaveLength(3);
    expect(screen.getByText('Manuale Base')).toBeInTheDocument();
    expect(screen.getByText('Storybook')).toBeInTheDocument();
    expect(screen.getByText('Encounter Book')).toBeInTheDocument();
  });

  it('decodes role badges from the roles bitflag using the backend-parity enum', () => {
    // RulesReference is BE value 2; the pre-fix FE enum mislabeled 2 as "Setup".
    const book = makeBook({ id: 'r', roles: GameBookRole.RulesReference });
    renderList(<GameBookList books={[book]} />);
    const row = screen.getByTestId('game-book-list-row-r');
    expect(within(row).getByText('Regole')).toBeInTheDocument();
    expect(within(row).queryByText('Setup')).not.toBeInTheDocument();
  });

  it('renders multiple role badges for a composite bitflag', () => {
    const book = makeBook({
      id: 'multi',
      roles: GameBookRole.Tutorial | GameBookRole.Setup,
    });
    renderList(<GameBookList books={[book]} />);
    const row = screen.getByTestId('game-book-list-row-multi');
    expect(within(row).getByText('Tutorial')).toBeInTheDocument();
    expect(within(row).getByText('Setup')).toBeInTheDocument();
  });

  it('distinguishes community (ownerUserId=null) from personal books', () => {
    const books = [
      makeBook({ id: 'community', ownerUserId: null }),
      makeBook({ id: 'personal', ownerUserId: 'user-9' }),
    ];
    renderList(<GameBookList books={books} />);
    expect(
      within(screen.getByTestId('game-book-list-row-community')).getByText('Community')
    ).toBeInTheDocument();
    expect(
      within(screen.getByTestId('game-book-list-row-personal')).getByText('Personale')
    ).toBeInTheDocument();
  });

  it('surfaces physical-only, indexed and not-yet-indexed status per book', () => {
    const books = [
      makeBook({ id: 'indexed', physicalOnly: false, kbSourceDocId: 'kb-9' }),
      makeBook({ id: 'physical', physicalOnly: true, kbSourceDocId: null }),
      // digital book whose PDF has not been indexed yet (or failed): the
      // reachable third status branch — reflects an aggregate state, not a
      // hardcoded page count.
      makeBook({ id: 'notidx', physicalOnly: false, kbSourceDocId: null }),
    ];
    renderList(<GameBookList books={books} />);
    expect(
      within(screen.getByTestId('game-book-list-row-indexed')).getByText('Indicizzato')
    ).toBeInTheDocument();
    expect(
      within(screen.getByTestId('game-book-list-row-physical')).getByText('Solo fisico')
    ).toBeInTheDocument();
    expect(
      within(screen.getByTestId('game-book-list-row-notidx')).getByText('Non indicizzato')
    ).toBeInTheDocument();
  });

  it('renders a generic count summary (no hardcoded page count)', () => {
    const books = [
      makeBook({ id: 'x1', kbSourceDocId: 'kb-1' }),
      makeBook({ id: 'x2', kbSourceDocId: null, physicalOnly: true }),
    ];
    renderList(<GameBookList books={books} />);
    const summary = screen.getByTestId('game-book-list-summary');
    expect(summary).toHaveTextContent('2 libri');
    expect(summary).toHaveTextContent('1 indicizzato');
    expect(summary.textContent).not.toMatch(/24 pag/i);
  });

  it('has no axe violations in the list state', async () => {
    const { container } = renderList(
      <GameBookList books={[makeBook({ id: 'ax', displayName: 'Ax Book' })]} />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
