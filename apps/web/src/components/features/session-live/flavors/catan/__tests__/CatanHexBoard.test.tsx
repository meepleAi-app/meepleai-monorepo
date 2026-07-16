import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CatanHexBoard } from '../CatanHexBoard';
import { generateStandardBoard } from '../catan-board-preset';

const board = generateStandardBoard();
const labels = { hexAriaTemplate: '{terrain} {number}', robberLabel: 'Ladro' };

describe('CatanHexBoard', () => {
  it('renders all 19 hex tiles', () => {
    const { container } = render(<CatanHexBoard board={board} editable={false} {...labels} />);
    expect(container.querySelectorAll('[data-slot="catan-hex"]')).toHaveLength(19);
  });

  it('marks the robber on the robber hex', () => {
    const { container } = render(<CatanHexBoard board={board} editable={false} {...labels} />);
    const robber = container.querySelector('[data-slot="catan-robber"]');
    expect(robber).not.toBeNull();
    expect(robber?.getAttribute('data-hex')).toBe(board.robberHexId);
  });

  it('read-only mode exposes no hex buttons', () => {
    render(<CatanHexBoard board={board} editable={false} {...labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host mode: clicking a hex fires onMoveRobber with its id', async () => {
    const onMoveRobber = vi.fn();
    const { container } = render(
      <CatanHexBoard board={board} editable onMoveRobber={onMoveRobber} {...labels} />
    );
    const firstHexButton = container.querySelector('[data-slot="catan-hex"]') as HTMLElement;
    await userEvent.click(firstHexButton);
    expect(onMoveRobber).toHaveBeenCalledOnce();
    expect(onMoveRobber.mock.calls[0][0]).toMatch(/^h\d+$/);
  });
});
