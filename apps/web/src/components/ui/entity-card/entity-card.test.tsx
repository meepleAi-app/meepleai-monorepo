import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { EntityCard } from './entity-card';

expect.extend(toHaveNoViolations);

describe('EntityCard', () => {
  it('renders children', () => {
    render(
      <EntityCard entity="game">
        <span>hello</span>
      </EntityCard>
    );
    expect(screen.getByText('hello')).toBeInTheDocument();
  });

  it('sets data-entity attribute matching entity prop', () => {
    const { container } = render(
      <EntityCard entity="agent">
        <span>x</span>
      </EntityCard>
    );
    expect(container.querySelector('[data-entity="agent"]')).toBeInTheDocument();
  });

  it('applies entity-colored left border by default', () => {
    const { container } = render(
      <EntityCard entity="game">
        <span>x</span>
      </EntityCard>
    );
    const el = container.querySelector<HTMLElement>('[data-entity="game"]');
    expect(el?.className).toMatch(/border-l-4/);
    expect(el?.style.borderLeftColor).toBe('hsl(var(--e-game))');
  });

  it('maps kb entity to --e-document css variable', () => {
    const { container } = render(
      <EntityCard entity="kb">
        <span>x</span>
      </EntityCard>
    );
    const el = container.querySelector<HTMLElement>('[data-entity="kb"]');
    expect(el?.style.borderLeftColor).toBe('hsl(var(--e-document))');
  });

  it('omits left border when entityBorder=false', () => {
    const { container } = render(
      <EntityCard entity="game" entityBorder={false}>
        <span>x</span>
      </EntityCard>
    );
    const el = container.querySelector<HTMLElement>('[data-entity="game"]');
    expect(el?.className).not.toMatch(/border-l-4/);
    expect(el?.style.borderLeftColor).toBe('');
  });

  it('variant=default applies border border-border classes', () => {
    const { container } = render(
      <EntityCard entity="game" variant="default">
        <span>x</span>
      </EntityCard>
    );
    const el = container.querySelector<HTMLElement>('[data-entity="game"]');
    expect(el?.className).toMatch(/border-border/);
  });

  it('variant=elevated applies shadow-md class', () => {
    const { container } = render(
      <EntityCard entity="game" variant="elevated">
        <span>x</span>
      </EntityCard>
    );
    const el = container.querySelector<HTMLElement>('[data-entity="game"]');
    expect(el?.className).toMatch(/shadow-md/);
  });

  it('variant=flat has no border-border and no shadow-md', () => {
    const { container } = render(
      <EntityCard entity="game" variant="flat" entityBorder={false}>
        <span>x</span>
      </EntityCard>
    );
    const el = container.querySelector<HTMLElement>('[data-entity="game"]');
    expect(el?.className).not.toMatch(/border-border/);
    expect(el?.className).not.toMatch(/shadow-md/);
  });

  it('renders as a button with aria-label when onClick is provided', async () => {
    const onClick = vi.fn();
    render(
      <EntityCard entity="player" onClick={onClick} ariaLabel="Open player card">
        <span>content</span>
      </EntityCard>
    );
    const btn = screen.getByRole('button', { name: 'Open player card' });
    expect(btn).toBeInTheDocument();
    expect(btn.tagName).toBe('BUTTON');
    expect(btn).toHaveAttribute('type', 'button');
    await userEvent.click(btn);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('renders as a div when no onClick', () => {
    const { container } = render(
      <EntityCard entity="event">
        <span>x</span>
      </EntityCard>
    );
    const el = container.querySelector('[data-entity="event"]');
    expect(el?.tagName).toBe('DIV');
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
      expect(() =>
        render(
          <EntityCard entity="game" onClick={() => {}}>
            <span>x</span>
          </EntityCard>
        )
      ).toThrow(/ariaLabel/i);
    });
  });

  it('interactive=true adds cursor-pointer and hover class', () => {
    const { container } = render(
      <EntityCard entity="session" interactive>
        <span>x</span>
      </EntityCard>
    );
    const el = container.querySelector<HTMLElement>('[data-entity="session"]');
    expect(el?.className).toMatch(/cursor-pointer/);
    expect(el?.className).toMatch(/hover:/);
  });

  it('forwards className', () => {
    const { container } = render(
      <EntityCard entity="chat" className="custom-xyz">
        <span>x</span>
      </EntityCard>
    );
    const el = container.querySelector('[data-entity="chat"]');
    expect(el?.className).toMatch(/custom-xyz/);
  });

  it('has no a11y violations as presentation div', async () => {
    const { container } = render(
      <EntityCard entity="game">
        <p>Game summary</p>
      </EntityCard>
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('has no a11y violations as button', async () => {
    const { container } = render(
      <EntityCard entity="game" onClick={() => {}} ariaLabel="Open game">
        <p>Game summary</p>
      </EntityCard>
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
