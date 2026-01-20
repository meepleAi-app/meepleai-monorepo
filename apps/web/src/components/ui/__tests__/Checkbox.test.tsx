/**
 * Tests for Checkbox component
 * Issue #1951: Add coverage for shadcn/ui components
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Checkbox } from '../primitives/checkbox';

describe('Checkbox', () => {
  describe('Rendering', () => {
    it('renders unchecked by default', () => {
      const { container } = render(<Checkbox />);
      const checkbox = container.querySelector('[role="checkbox"]');
      expect(checkbox).toBeInTheDocument();
      expect(checkbox).toHaveAttribute('data-state', 'unchecked');
    });

    it('renders checked when defaultChecked is true', () => {
      const { container } = render(<Checkbox defaultChecked />);
      const checkbox = container.querySelector('[role="checkbox"]');
      expect(checkbox).toHaveAttribute('data-state', 'checked');
    });

    it('renders check icon when checked', () => {
      const { container } = render(<Checkbox defaultChecked />);
      const checkIcon = container.querySelector('svg');
      expect(checkIcon).toBeInTheDocument();
    });
  });

  describe('Interaction', () => {
    it('toggles state when clicked', async () => {
      const user = userEvent.setup();
      const { container } = render(<Checkbox />);
      const checkbox = container.querySelector('[role="checkbox"]') as HTMLElement;

      expect(checkbox).toHaveAttribute('data-state', 'unchecked');

      await user.click(checkbox);
      expect(checkbox).toHaveAttribute('data-state', 'checked');

      await user.click(checkbox);
      expect(checkbox).toHaveAttribute('data-state', 'unchecked');
    });

    it('calls onCheckedChange when toggled', async () => {
      const user = userEvent.setup();
      const onCheckedChange = vi.fn();
      const { container } = render(<Checkbox onCheckedChange={onCheckedChange} />);
      const checkbox = container.querySelector('[role="checkbox"]') as HTMLElement;

      await user.click(checkbox);
      expect(onCheckedChange).toHaveBeenCalledWith(true);

      await user.click(checkbox);
      expect(onCheckedChange).toHaveBeenCalledWith(false);
    });
  });

  describe('Disabled State', () => {
    it('renders disabled checkbox', () => {
      const { container } = render(<Checkbox disabled />);
      const checkbox = container.querySelector('[role="checkbox"]');
      expect(checkbox).toHaveAttribute('data-disabled', '');
      expect(checkbox).toHaveClass('disabled:cursor-not-allowed', 'disabled:opacity-50');
    });

    it('does not toggle when disabled', async () => {
      const user = userEvent.setup();
      const onCheckedChange = vi.fn();
      const { container } = render(<Checkbox disabled onCheckedChange={onCheckedChange} />);
      const checkbox = container.querySelector('[role="checkbox"]') as HTMLElement;

      await user.click(checkbox);
      expect(onCheckedChange).not.toHaveBeenCalled();
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      const { container } = render(<Checkbox className="custom-class" />);
      const checkbox = container.querySelector('[role="checkbox"]');
      expect(checkbox).toHaveClass('custom-class');
    });

    it('applies focus ring on focus', () => {
      const { container } = render(<Checkbox />);
      const checkbox = container.querySelector('[role="checkbox"]');
      expect(checkbox).toHaveClass('focus-visible:ring-2');
    });
  });
});
