/**
 * Button component tests - coverage increment (frontend)
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { Button, buttonVariants } from '../primitives/button';

describe('Button', () => {
  it('renders a native button by default with default sizing', () => {
    render(<Button>Click me</Button>);

    const btn = screen.getByRole('button', { name: 'Click me' });
    expect(btn).toBeInTheDocument();
    expect(btn).toHaveClass('h-11', 'px-6');
    expect(btn).toHaveClass('bg-primary');
  });

  it('applies variant and size classes', () => {
    render(
      <Button variant="destructive" size="sm">
        Delete
      </Button>
    );

    const btn = screen.getByRole('button', { name: 'Delete' });
    expect(btn).toHaveClass('bg-destructive');
    expect(btn).toHaveClass('h-9');
  });

  it('renders as child element when asChild is true', () => {
    render(
      <Button asChild>
        <a href="/docs">Docs</a>
      </Button>
    );

    const link = screen.getByRole('link', { name: 'Docs' });
    expect(link).toHaveAttribute('href', '/docs');
    expect(link.tagName.toLowerCase()).toBe('a');
  });

  it('merges custom className with variant styles', () => {
    const className = 'custom-shadow';
    render(
      <Button className={className} variant="secondary">
        Custom
      </Button>
    );

    const btn = screen.getByRole('button', { name: 'Custom' });
    expect(btn).toHaveClass('custom-shadow');
    expect(btn).toHaveClass('bg-secondary');
  });

  it('buttonVariants produces expected classes', () => {
    const classes = buttonVariants({ variant: 'link', size: 'icon' });
    expect(classes).toContain('underline-offset-4');
    expect(classes).toContain('h-11');
    expect(classes).toContain('w-11');
  });
});
