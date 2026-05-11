import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { Btn } from './btn';

expect.extend(toHaveNoViolations);

describe('Btn', () => {
  it('renders children', () => {
    render(<Btn>Click me</Btn>);
    expect(screen.getByRole('button', { name: 'Click me' })).toBeInTheDocument();
  });

  it('default variant is primary (bg-primary)', () => {
    render(<Btn>x</Btn>);
    expect(screen.getByRole('button')).toHaveClass('bg-primary');
  });

  it('default size is md (h-10)', () => {
    render(<Btn>x</Btn>);
    expect(screen.getByRole('button')).toHaveClass('h-10');
  });

  it('size sm produces h-8', () => {
    render(<Btn size="sm">x</Btn>);
    expect(screen.getByRole('button')).toHaveClass('h-8');
  });

  it('size lg produces h-12', () => {
    render(<Btn size="lg">x</Btn>);
    expect(screen.getByRole('button')).toHaveClass('h-12');
  });

  it('variant outline has border class', () => {
    render(<Btn variant="outline">x</Btn>);
    const btn = screen.getByRole('button');
    expect(btn.className).toMatch(/\bborder\b/);
  });

  it('variant destructive has bg-destructive', () => {
    render(<Btn variant="destructive">x</Btn>);
    expect(screen.getByRole('button')).toHaveClass('bg-destructive');
  });

  it('variant secondary has bg-secondary', () => {
    render(<Btn variant="secondary">x</Btn>);
    expect(screen.getByRole('button')).toHaveClass('bg-secondary');
  });

  it('variant ghost has bg-transparent', () => {
    render(<Btn variant="ghost">x</Btn>);
    expect(screen.getByRole('button')).toHaveClass('bg-transparent');
  });

  it('entity prop applies entity background on primary variant', () => {
    render(
      <Btn variant="primary" entity="game">
        x
      </Btn>
    );
    const btn = screen.getByRole('button');
    expect(btn.style.backgroundColor).toBe('hsl(var(--e-game))');
  });

  it('entity=kb maps to --e-document css variable', () => {
    render(
      <Btn variant="primary" entity="kb">
        x
      </Btn>
    );
    const btn = screen.getByRole('button');
    expect(btn.style.backgroundColor).toBe('hsl(var(--e-document))');
  });

  it('entity prop applies entity border + color on outline variant', () => {
    render(
      <Btn variant="outline" entity="agent">
        x
      </Btn>
    );
    const btn = screen.getByRole('button');
    expect(btn.style.borderColor).toBe('hsl(var(--e-agent))');
    expect(btn.style.color).toBe('hsl(var(--e-agent))');
  });

  it('entity prop has no inline style effect on ghost/secondary/destructive', () => {
    const { rerender } = render(
      <Btn variant="ghost" entity="game">
        x
      </Btn>
    );
    expect(screen.getByRole('button').style.backgroundColor).toBe('');
    rerender(
      <Btn variant="secondary" entity="game">
        x
      </Btn>
    );
    expect(screen.getByRole('button').style.backgroundColor).toBe('');
    rerender(
      <Btn variant="destructive" entity="game">
        x
      </Btn>
    );
    expect(screen.getByRole('button').style.backgroundColor).toBe('');
  });

  it('leftIcon renders left of children', () => {
    render(<Btn leftIcon={<span data-testid="left">L</span>}>label</Btn>);
    const btn = screen.getByRole('button');
    const left = screen.getByTestId('left');
    expect(btn.firstChild).toBe(left);
  });

  it('rightIcon renders right of children', () => {
    render(<Btn rightIcon={<span data-testid="right">R</span>}>label</Btn>);
    const btn = screen.getByRole('button');
    const right = screen.getByTestId('right');
    expect(btn.lastChild).toBe(right);
  });

  it('loading shows spinner svg with animate-spin and hides leftIcon', () => {
    render(
      <Btn loading leftIcon={<span data-testid="left">L</span>}>
        Submitting
      </Btn>
    );
    const btn = screen.getByRole('button');
    const spinner = btn.querySelector('svg.animate-spin');
    expect(spinner).not.toBeNull();
    expect(spinner).toHaveAttribute('aria-hidden', 'true');
    expect(screen.queryByTestId('left')).toBeNull();
  });

  it('loading sets aria-busy=true and disables button', () => {
    render(<Btn loading>x</Btn>);
    const btn = screen.getByRole('button');
    expect(btn).toHaveAttribute('aria-busy', 'true');
    expect(btn).toBeDisabled();
  });

  it('disabled prop sets disabled attr', () => {
    render(<Btn disabled>x</Btn>);
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('fullWidth adds w-full', () => {
    render(<Btn fullWidth>x</Btn>);
    expect(screen.getByRole('button')).toHaveClass('w-full');
  });

  it('onClick fires when not disabled or loading', async () => {
    const onClick = vi.fn();
    render(<Btn onClick={onClick}>go</Btn>);
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('onClick does not fire when disabled', async () => {
    const onClick = vi.fn();
    render(
      <Btn onClick={onClick} disabled>
        go
      </Btn>
    );
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).not.toHaveBeenCalled();
  });

  it('onClick does not fire when loading', async () => {
    const onClick = vi.fn();
    render(
      <Btn onClick={onClick} loading>
        go
      </Btn>
    );
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).not.toHaveBeenCalled();
  });

  it('forwards className', () => {
    render(<Btn className="custom-xyz">x</Btn>);
    expect(screen.getByRole('button')).toHaveClass('custom-xyz');
  });

  it('default type is button', () => {
    render(<Btn>x</Btn>);
    expect(screen.getByRole('button')).toHaveAttribute('type', 'button');
  });

  it('asChild renders via Slot, applying classes to child element', () => {
    render(
      <Btn asChild variant="primary">
        <a href="/foo" data-testid="link">
          Go
        </a>
      </Btn>
    );
    const link = screen.getByTestId('link');
    expect(link.tagName).toBe('A');
    expect(link).toHaveClass('bg-primary');
    expect(link).toHaveAttribute('href', '/foo');
  });

  it('forwards data-testid to the button element', () => {
    render(<Btn data-testid="my-btn">x</Btn>);
    const btn = screen.getByTestId('my-btn');
    expect(btn.tagName).toBe('BUTTON');
  });

  it('forwards id to the button element', () => {
    render(<Btn id="my-btn-id">x</Btn>);
    expect(screen.getByRole('button')).toHaveAttribute('id', 'my-btn-id');
  });

  it('forwards data-testid and id through asChild Slot', () => {
    render(
      <Btn asChild data-testid="slot-btn" id="slot-id">
        <a href="/bar">Go</a>
      </Btn>
    );
    const link = screen.getByTestId('slot-btn');
    expect(link.tagName).toBe('A');
    expect(link).toHaveAttribute('id', 'slot-id');
    expect(link).toHaveAttribute('href', '/bar');
  });

  it('has no a11y violations', async () => {
    const { container } = render(<Btn>Accessible</Btn>);
    expect(await axe(container)).toHaveNoViolations();
  });

  it('has no a11y violations when loading', async () => {
    const { container } = render(<Btn loading>Loading</Btn>);
    expect(await axe(container)).toHaveNoViolations();
  });
});
