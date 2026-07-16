/**
 * @vitest-environment jsdom
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi } from 'vitest';

import { flattenMessages } from '@/locales';
import itMessages from '@/locales/it.json';

import { MeepleWishlistCard } from '../MeepleWishlistCard';

import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';
import type { ReactElement } from 'react';

const MESSAGES = flattenMessages(itMessages as unknown as Record<string, unknown>);
const CARD_I18N = itMessages.pages.library.wishlist.card;
const PRIORITY_I18N = itMessages.pages.library.wishlist.priority;

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

function buildItem(overrides: Partial<WishlistItemDto> = {}): WishlistItemDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    userId: '00000000-0000-0000-0000-000000000002',
    gameId: '00000000-0000-0000-0000-000000000003',
    gameName: 'Catan',
    priority: 'high',
    targetPrice: null,
    notes: null,
    addedAt: '2026-01-01',
    updatedAt: null,
    visibility: 'private',
    ...overrides,
  };
}

describe('MeepleWishlistCard', () => {
  it('renders with correct entity type', () => {
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem()}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    const card = screen.getByTestId('wishlist-card');
    expect(card).toHaveAttribute('data-entity', 'game');
  });

  it('displays game name as title', () => {
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem()}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('falls back to the localized "unknown game" label when no game name is available', () => {
    const itemNoName = buildItem({ gameName: undefined });
    renderWithIntl(
      <MeepleWishlistCard
        item={itemNoName}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    expect(screen.getByText(CARD_I18N.unknownGame)).toBeInTheDocument();
  });

  it('shows the localized priority badge label', () => {
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem({ priority: 'high' })}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    expect(
      screen.getByRole('button', { name: new RegExp(PRIORITY_I18N.high) })
    ).toBeInTheDocument();
  });

  it('calls onFilterPriority with the normalized priority when the badge is clicked', async () => {
    const user = userEvent.setup();
    const onFilterPriority = vi.fn();
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem({ priority: 'high' })}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={onFilterPriority}
      />
    );

    await user.click(screen.getByRole('button', { name: new RegExp(PRIORITY_I18N.high) }));
    expect(onFilterPriority).toHaveBeenCalledWith('high');
  });

  it('renders the priority badge as a non-interactive element when onFilterPriority is omitted', () => {
    renderWithIntl(
      <MeepleWishlistCard item={buildItem({ priority: 'high' })} onRemove={vi.fn()} />
    );

    expect(
      screen.queryByRole('button', { name: new RegExp(PRIORITY_I18N.high) })
    ).not.toBeInTheDocument();
    const badge = screen.getByTestId('wishlist-priority-badge-static');
    expect(badge.tagName).toBe('SPAN');
    expect(badge).toHaveTextContent(PRIORITY_I18N.high);
  });

  it('calls onEdit with the item when the edit action is clicked', async () => {
    const user = userEvent.setup();
    const onEdit = vi.fn();
    const item = buildItem();
    renderWithIntl(
      <MeepleWishlistCard
        item={item}
        onRemove={vi.fn()}
        onEdit={onEdit}
        onFilterPriority={vi.fn()}
      />
    );

    await user.click(screen.getByRole('button', { name: new RegExp(CARD_I18N.edit) }));
    expect(onEdit).toHaveBeenCalledWith(item);
  });

  it('calls onRemove with the item id when the remove action is clicked', async () => {
    const user = userEvent.setup();
    const onRemove = vi.fn();
    const item = buildItem();
    renderWithIntl(
      <MeepleWishlistCard
        item={item}
        onRemove={onRemove}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );

    await user.click(screen.getByRole('button', { name: new RegExp(CARD_I18N.remove) }));
    expect(onRemove).toHaveBeenCalledWith(item.id);
  });

  it('renders a left-border accent for a high-priority item', () => {
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem({ priority: 'high' })}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    const shell = screen.getByTestId('wishlist-card-shell');
    expect(shell.className).toMatch(/border-l-destructive/);
  });

  it('does not render the left-border accent for a non-high-priority item', () => {
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem({ priority: 'medium' })}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    const shell = screen.getByTestId('wishlist-card-shell');
    expect(shell.className).not.toMatch(/border-l-destructive/);
  });

  it('shows the target price with the € currency symbol when present', () => {
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem({ targetPrice: 65 })}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    expect(screen.getByText(/€/)).toBeInTheDocument();
  });

  it('shows the item notes when present', () => {
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem({ notes: 'Great with kids' })}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    expect(screen.getByText('Great with kids')).toBeInTheDocument();
  });

  it('shows the localized "no notes" placeholder when notes is absent', () => {
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem({ notes: null })}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    expect(screen.getByText(CARD_I18N.noNote)).toBeInTheDocument();
  });

  it('exposes accessible labels for the edit and remove actions including the game name', () => {
    renderWithIntl(
      <MeepleWishlistCard
        item={buildItem({ gameName: 'Catan' })}
        onRemove={vi.fn()}
        onEdit={vi.fn()}
        onFilterPriority={vi.fn()}
      />
    );
    const shell = screen.getByTestId('wishlist-card-shell');
    expect(
      within(shell).getByRole('button', { name: CARD_I18N.editAria.replace('{name}', 'Catan') })
    ).toBeInTheDocument();
    expect(
      within(shell).getByRole('button', { name: CARD_I18N.removeAria.replace('{name}', 'Catan') })
    ).toBeInTheDocument();
  });
});
