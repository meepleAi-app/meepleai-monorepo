/**
 * @vitest-environment jsdom
 */
import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { SessionSheet } from '../sheet/SessionSheet';

// ---------------------------------------------------------------------------
// Mock framer-motion — lightweight passthrough to avoid animation issues
// ---------------------------------------------------------------------------

vi.mock('framer-motion', () => ({
  AnimatePresence: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  motion: {
    div: ({
      children,
      drag: _drag,
      dragConstraints: _dc,
      dragElastic: _de,
      onDragEnd: _ode,
      initial: _initial,
      animate: _animate,
      exit: _exit,
      transition: _transition,
      ...props
    }: React.HTMLAttributes<HTMLDivElement> & Record<string, unknown>) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('SessionSheet', () => {
  it('renders children when open', () => {
    render(
      <SessionSheet isOpen onClose={vi.fn()}>
        <p>Sheet content</p>
      </SessionSheet>
    );
    expect(screen.getByText('Sheet content')).toBeInTheDocument();
  });

  it('renders nothing when closed', () => {
    const { container } = render(
      <SessionSheet isOpen={false} onClose={vi.fn()}>
        <p>Sheet content</p>
      </SessionSheet>
    );
    expect(container.innerHTML).toBe('');
  });

  it('renders overlay when open', () => {
    render(
      <SessionSheet isOpen onClose={vi.fn()}>
        <p>content</p>
      </SessionSheet>
    );
    expect(screen.getByTestId('sheet-overlay')).toBeInTheDocument();
  });

  it('calls onClose when overlay is clicked', async () => {
    const onClose = vi.fn();
    render(
      <SessionSheet isOpen onClose={onClose}>
        <p>content</p>
      </SessionSheet>
    );

    await userEvent.click(screen.getByTestId('sheet-overlay'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('renders the drag handle', () => {
    render(
      <SessionSheet isOpen onClose={vi.fn()}>
        <p>content</p>
      </SessionSheet>
    );
    expect(screen.getByTestId('sheet-drag-handle')).toBeInTheDocument();
  });

  it('renders the sheet panel when open', () => {
    render(
      <SessionSheet isOpen onClose={vi.fn()}>
        <p>content</p>
      </SessionSheet>
    );
    expect(screen.getByTestId('sheet-panel')).toBeInTheDocument();
  });
});
