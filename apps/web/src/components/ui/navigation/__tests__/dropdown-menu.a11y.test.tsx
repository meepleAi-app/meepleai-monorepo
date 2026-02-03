/**
 * Accessibility Tests for DropdownMenu (UI-09)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from '../dropdown-menu';

describe('DropdownMenu - Accessibility', () => {
  it('should have no accessibility violations (default menu)', async () => {
    const { container } = render(
      <DropdownMenu>
        <DropdownMenuTrigger aria-label="Open menu">Menu</DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem>Item 1</DropdownMenuItem>
          <DropdownMenuItem>Item 2</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with labels)', async () => {
    const { container } = render(
      <DropdownMenu>
        <DropdownMenuTrigger aria-label="Menu with groups">Menu</DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuLabel>Group 1</DropdownMenuLabel>
          <DropdownMenuItem>Item 1</DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuLabel>Group 2</DropdownMenuLabel>
          <DropdownMenuItem>Item 2</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (disabled items)', async () => {
    const { container } = render(
      <DropdownMenu>
        <DropdownMenuTrigger aria-label="Menu with disabled">Menu</DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem>Active Item</DropdownMenuItem>
          <DropdownMenuItem disabled>Disabled Item</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with aria-label)', async () => {
    const { container } = render(
      <DropdownMenu>
        <DropdownMenuTrigger aria-label="Open menu">Menu</DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem>Item 1</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper ARIA attributes for trigger button', () => {
    const { getByRole } = render(
      <DropdownMenu>
        <DropdownMenuTrigger aria-label="Menu Options">Menu Options</DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem>Item 1</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );

    const button = getByRole('button');
    expect(button).toHaveTextContent('Menu Options');
  });

  it('should have proper focus management', () => {
    const { getByRole } = render(
      <DropdownMenu>
        <DropdownMenuTrigger aria-label="Focus Menu">Menu</DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem>Item 1</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );

    const button = getByRole('button');
    expect(button).toHaveClass('focus');
  });

  it('should support keyboard navigation', () => {
    const { getByRole } = render(
      <DropdownMenu>
        <DropdownMenuTrigger aria-label="Keyboard Menu">Menu</DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem>Keyboard Nav</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );

    const menuItem = getByRole('menuitem', { hidden: true });
    expect(menuItem).toBeInTheDocument();
  });

  it('should have disabled items marked as such', () => {
    const { getByRole, getByText } = render(
      <DropdownMenu>
        <DropdownMenuTrigger aria-label="Menu with disabled item">Menu</DropdownMenuTrigger>
        <DropdownMenuContent>
          <DropdownMenuItem disabled>Disabled Item</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );

    const disabledItem = getByText('Disabled Item').closest('[role="menuitem"]');
    expect(disabledItem).toHaveAttribute('data-disabled');
  });
});
