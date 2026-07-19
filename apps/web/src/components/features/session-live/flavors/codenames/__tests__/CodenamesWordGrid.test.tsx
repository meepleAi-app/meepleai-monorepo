import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CodenamesWordGrid } from '../CodenamesWordGrid';
import { generateCodenamesBoard } from '../codenames-board-preset';

const { board } = generateCodenamesBoard('red');
const labels = { revealAriaTemplate: 'Rivela {word}' };

describe('CodenamesWordGrid', () => {
  it('renders 25 word cells', () => {
    const { container } = render(
      <CodenamesWordGrid board={board} editable={false} perspective="operative" {...labels} />
    );
    expect(container.querySelectorAll('[data-slot="codenames-cell"]')).toHaveLength(25);
  });

  it('read-only mode exposes no buttons', () => {
    const { queryByRole } = render(
      <CodenamesWordGrid board={board} editable={false} perspective="operative" {...labels} />
    );
    expect(queryByRole('button')).toBeNull();
  });

  it('host mode: clicking an unrevealed cell fires onRevealCell with its index', async () => {
    const onRevealCell = vi.fn();
    const { container } = render(
      <CodenamesWordGrid
        board={board}
        editable
        perspective="operative"
        onRevealCell={onRevealCell}
        {...labels}
      />
    );
    const firstCell = container.querySelector('[data-slot="codenames-cell"]') as HTMLElement;
    await userEvent.click(firstCell);
    expect(onRevealCell).toHaveBeenCalledWith(0);
  });

  it('spymaster perspective tints covered cells by key (data-key present); operative does not', () => {
    const spy = render(
      <CodenamesWordGrid board={board} editable={false} perspective="spymaster" {...labels} />
    );
    const op = render(
      <CodenamesWordGrid board={board} editable={false} perspective="operative" {...labels} />
    );
    // spymaster exposes each covered cell's key; operative hides it for covered cells
    expect(spy.container.querySelector('[data-slot="codenames-cell"][data-key]')).not.toBeNull();
    expect(op.container.querySelector('[data-slot="codenames-cell"][data-key]')).toBeNull();
  });
});
