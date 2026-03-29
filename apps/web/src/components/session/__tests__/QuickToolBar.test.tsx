import React from 'react';

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { QuickToolBar } from '../QuickToolBar';
import type { ToolId } from '../QuickToolBar';

describe('QuickToolBar', () => {
  it('renders all 5 tool buttons with correct aria-labels', () => {
    render(<QuickToolBar activeTool={null} onSelectTool={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Dadi' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Moneta' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Timer' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Contatore' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Carte' })).toBeInTheDocument();
  });

  it('highlights the active tool with amber styling', () => {
    render(<QuickToolBar activeTool="dadi" onSelectTool={vi.fn()} />);

    const dadiBtn = screen.getByRole('button', { name: 'Dadi' });
    expect(dadiBtn).toHaveAttribute('aria-pressed', 'true');
    expect(dadiBtn.className).toContain('bg-amber-500');

    const monetaBtn = screen.getByRole('button', { name: 'Moneta' });
    expect(monetaBtn).toHaveAttribute('aria-pressed', 'false');
    expect(monetaBtn.className).not.toContain('bg-amber-500');
  });

  it('calls onSelectTool with the correct ToolId when a button is clicked', async () => {
    const user = userEvent.setup();
    const onSelectTool = vi.fn();
    render(<QuickToolBar activeTool={null} onSelectTool={onSelectTool} />);

    await user.click(screen.getByRole('button', { name: 'Timer' }));
    expect(onSelectTool).toHaveBeenCalledOnce();
    expect(onSelectTool).toHaveBeenCalledWith('timer' satisfies ToolId);
  });
});
