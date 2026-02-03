/**
 * Accessibility Tests for Input (UI-02)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { Input } from '../input';

describe('Input - Accessibility', () => {
  it('should have no accessibility violations (default state)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-input-default">Test Input</label>
        <Input id="test-input-default" type="text" placeholder="Enter text" />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with aria-label)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-input-aria">Input Label</label>
        <Input id="test-input-aria" type="text" aria-label="Search input" placeholder="Search..." />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (disabled state)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-input-disabled">Input Label</label>
        <Input id="test-input-disabled" type="text" disabled placeholder="Disabled input" />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (error state with aria-invalid)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-input-error">Input Label</label>
        <Input id="test-input-error" type="email" aria-invalid="true" aria-describedby="error-msg" />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (password input)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-input-password">Input Label</label>
        <Input id="test-input-password" type="password" aria-label="Password" placeholder="Enter password" />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper focus ring for keyboard navigation', () => {
    const { getByPlaceholderText } = render(
      <div>
        <label htmlFor="test-input-focus">Input Label</label>
        <Input id="test-input-focus" type="text" placeholder="Focus test" />
      </div>
    );

    const input = getByPlaceholderText('Focus test');
    expect(input).toHaveClass('focus-visible:ring-2');
  });

  it('should be disabled when disabled prop is true', () => {
    const { getByPlaceholderText } = render(
      <div>
        <label htmlFor="test-input-disabled-check">Input Label</label>
        <Input id="test-input-disabled-check" type="text" disabled placeholder="Disabled field" />
      </div>
    );

    const input = getByPlaceholderText('Disabled field');
    expect(input).toBeDisabled();
  });

  it('should have aria-invalid when specified', () => {
    const { getByPlaceholderText } = render(
      <div>
        <label htmlFor="test-input-aria-invalid">Input Label</label>
        <Input id="test-input-aria-invalid" type="email" aria-invalid="true" placeholder="Email error" />
      </div>
    );

    const input = getByPlaceholderText('Email error');
    expect(input).toHaveAttribute('aria-invalid', 'true');
  });
});
