/**
 * Accessibility Tests for Textarea (UI-03)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { Textarea } from '../textarea';

describe('Textarea - Accessibility', () => {
  it('should have no accessibility violations (default state)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-textarea-default">Textarea Label</label>
        <Textarea id="test-textarea-default" placeholder="Enter your message" />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with aria-label)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-textarea-aria">Textarea Label</label>
        <Textarea id="test-textarea-aria" aria-label="Comment field" placeholder="Leave a comment" />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (disabled state)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-textarea-disabled">Textarea Label</label>
        <Textarea id="test-textarea-disabled" disabled placeholder="Disabled textarea" />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with aria-invalid)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-textarea-error">Textarea Label</label>
        <Textarea
          id="test-textarea-error"
          aria-invalid="true"
          aria-describedby="error-msg"
          placeholder="Error state"
        />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (readonly state)', async () => {
    const { container } = render(
      <div>
        <label htmlFor="test-textarea-readonly">Textarea Label</label>
        <Textarea id="test-textarea-readonly" readOnly value="Read-only content" />
      </div>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper focus ring for keyboard navigation', () => {
    const { getByPlaceholderText } = render(
      <div>
        <label htmlFor="test-textarea-focus">Textarea Label</label>
        <Textarea id="test-textarea-focus" placeholder="Focus test" />
      </div>
    );

    const textarea = getByPlaceholderText('Focus test');
    expect(textarea).toHaveClass('focus-visible:ring-2');
  });

  it('should be disabled when disabled prop is true', () => {
    const { getByPlaceholderText } = render(
      <div>
        <label htmlFor="test-textarea-disabled-check">Textarea Label</label>
        <Textarea id="test-textarea-disabled-check" disabled placeholder="Disabled area" />
      </div>
    );

    const textarea = getByPlaceholderText('Disabled area');
    expect(textarea).toBeDisabled();
  });

  it('should have aria-invalid when specified', () => {
    const { getByPlaceholderText } = render(
      <div>
        <label htmlFor="test-textarea-aria-invalid">Textarea Label</label>
        <Textarea id="test-textarea-aria-invalid" aria-invalid="true" placeholder="Error textarea" />
      </div>
    );

    const textarea = getByPlaceholderText('Error textarea');
    expect(textarea).toHaveAttribute('aria-invalid', 'true');
  });
});
