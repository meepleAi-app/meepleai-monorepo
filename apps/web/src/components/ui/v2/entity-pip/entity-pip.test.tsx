import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { EntityPip } from './entity-pip';

expect.extend(toHaveNoViolations);

describe('EntityPip', () => {
  it('renders empty dot (no text) when count is not provided', () => {
    const { container } = render(<EntityPip entity="game" />);
    const pip = container.querySelector('[data-entity="game"]');
    expect(pip).toBeInTheDocument();
    expect(pip?.textContent).toBe('');
  });

  it('renders count when count > 0', () => {
    render(<EntityPip entity="session" count={3} />);
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('applies entity color class via background token', () => {
    const { container } = render(<EntityPip entity="session" count={2} />);
    // bg-entity-<key> class from getEntityToken
    expect(container.querySelector('.bg-entity-session')).toBeInTheDocument();
  });

  it('sets data-entity attribute', () => {
    const { container } = render(<EntityPip entity="agent" count={1} />);
    expect(container.querySelector('[data-entity="agent"]')).toBeInTheDocument();
  });

  it('renders as a button when onClick is provided', async () => {
    const onClick = vi.fn();
    render(<EntityPip entity="player" count={2} onClick={onClick} ariaLabel="2 players" />);
    const btn = screen.getByRole('button', { name: '2 players' });
    expect(btn).toBeInTheDocument();
    expect(btn.tagName).toBe('BUTTON');
    expect(btn).toHaveAttribute('type', 'button');
    await userEvent.click(btn);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('renders as a span with role="presentation" when no onClick', () => {
    const { container } = render(<EntityPip entity="event" count={1} />);
    const el = container.querySelector('[data-entity="event"]');
    expect(el?.tagName).toBe('SPAN');
    expect(el).toHaveAttribute('role', 'presentation');
  });

  describe('ariaLabel enforcement when clickable', () => {
    let errSpy: ReturnType<typeof vi.spyOn>;

    beforeEach(() => {
      errSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    });

    afterEach(() => {
      errSpy.mockRestore();
    });

    it('throws when onClick provided without ariaLabel', () => {
      expect(() => render(<EntityPip entity="player" count={2} onClick={() => {}} />)).toThrow(
        /ariaLabel/i
      );
    });
  });

  it('shows ring when active', () => {
    const { container } = render(<EntityPip entity="kb" count={5} active />);
    const el = container.querySelector('[data-entity="kb"]');
    expect(el?.className).toMatch(/ring-2/);
  });

  it('applies opacity-40 when count === 0', () => {
    const { container } = render(<EntityPip entity="tool" count={0} />);
    const el = container.querySelector('[data-entity="tool"]');
    expect(el?.className).toMatch(/opacity-40/);
  });

  it('applies cursor-default when count === 0', () => {
    const { container } = render(<EntityPip entity="tool" count={0} />);
    const el = container.querySelector('[data-entity="tool"]');
    expect(el?.className).toMatch(/cursor-default/);
  });

  it('forwards className', () => {
    const { container } = render(<EntityPip entity="chat" count={1} className="custom-xyz" />);
    const el = container.querySelector('[data-entity="chat"]');
    expect(el?.className).toMatch(/custom-xyz/);
  });

  it('applies tabular-nums on count text', () => {
    const { container } = render(<EntityPip entity="session" count={12} />);
    const el = container.querySelector('[data-entity="session"]');
    expect(el?.className).toMatch(/tabular-nums/);
  });

  it('size sm produces correct dimension classes for empty dot', () => {
    const { container } = render(<EntityPip entity="game" size="sm" />);
    const el = container.querySelector('[data-entity="game"]');
    expect(el?.className).toMatch(/h-2\b/);
    expect(el?.className).toMatch(/w-2\b/);
  });

  it('size md produces correct dimension classes for empty dot', () => {
    const { container } = render(<EntityPip entity="game" size="md" />);
    const el = container.querySelector('[data-entity="game"]');
    expect(el?.className).toMatch(/h-2\.5/);
    expect(el?.className).toMatch(/w-2\.5/);
  });

  it('size sm produces correct dimension classes with count', () => {
    const { container } = render(<EntityPip entity="game" size="sm" count={2} />);
    const el = container.querySelector('[data-entity="game"]');
    expect(el?.className).toMatch(/h-4\b/);
    expect(el?.className).toMatch(/min-w-4/);
  });

  it('size md produces correct dimension classes with count', () => {
    const { container } = render(<EntityPip entity="game" size="md" count={3} />);
    const el = container.querySelector('[data-entity="game"]');
    expect(el?.className).toMatch(/h-5\b/);
    expect(el?.className).toMatch(/min-w-5/);
  });

  it('has no a11y violations as presentation', async () => {
    const { container } = render(<EntityPip entity="player" count={2} />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it('has no a11y violations as button', async () => {
    const { container } = render(
      <EntityPip entity="player" count={2} onClick={() => {}} ariaLabel="2 players" />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
