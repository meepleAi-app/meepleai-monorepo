/**
 * Accessibility Tests for AccessibleFormInput (UI-05)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { AccessibleFormInput } from '../AccessibleFormInput';

describe('AccessibleFormInput - Accessibility', () => {
  it('should have no accessibility violations (basic input)', async () => {
    const { container } = render(
      <AccessibleFormInput label="Email" type="email" value="" onChange={() => {}} />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with hint)', async () => {
    const { container } = render(
      <AccessibleFormInput
        label="Password"
        type="password"
        value=""
        onChange={() => {}}
        hint="Must be at least 8 characters"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with error)', async () => {
    const { container} = render(
      <AccessibleFormInput
        label="Username"
        value=""
        onChange={() => {}}
        error="Username is already taken"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (required field)', async () => {
    const { container } = render(
      <AccessibleFormInput label="Full Name" value="" onChange={() => {}} required />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (hidden label)', async () => {
    const { container } = render(
      <AccessibleFormInput
        label="Search"
        value=""
        onChange={() => {}}
        hideLabel
        placeholder="Search..."
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper label association (htmlFor/id)', () => {
    render(<AccessibleFormInput label="Email Address" value="" onChange={() => {}} />);

    const input = screen.getByLabelText(/email address/i);
    expect(input).toBeInTheDocument();

    const label = screen.getByText(/email address/i);
    expect(label).toHaveAttribute('for', input.id);
  });

  it('should show required indicator (*)', () => {
    render(<AccessibleFormInput label="Email" value="" onChange={() => {}} required />);

    const asterisk = screen.getByText('*');
    expect(asterisk).toBeInTheDocument();
    expect(asterisk).toHaveAttribute('aria-label', 'required');
  });

  it('should have aria-required when required', () => {
    render(<AccessibleFormInput label="Email" value="" onChange={() => {}} required />);

    const input = screen.getByLabelText(/email/i);
    expect(input).toHaveAttribute('aria-required', 'true');
  });

  it('should have aria-describedby for hint', () => {
    render(
      <AccessibleFormInput
        label="Password"
        value=""
        onChange={() => {}}
        hint="At least 8 characters"
      />
    );

    const input = screen.getByLabelText(/password/i);
    const describedBy = input.getAttribute('aria-describedby');
    expect(describedBy).toBeTruthy();

    const hintElement = document.getElementById(describedBy!);
    expect(hintElement).toHaveTextContent('At least 8 characters');
  });

  it('should have aria-describedby for error', () => {
    render(
      <AccessibleFormInput
        label="Username"
        value=""
        onChange={() => {}}
        error="Username already exists"
      />
    );

    const input = screen.getByLabelText(/username/i);
    const describedBy = input.getAttribute('aria-describedby');
    expect(describedBy).toBeTruthy();

    const errorElement = document.getElementById(describedBy!);
    expect(errorElement).toHaveTextContent('Username already exists');
  });

  it('should have aria-invalid when error is present', () => {
    render(
      <AccessibleFormInput label="Email" value="" onChange={() => {}} error="Invalid email" />
    );

    const input = screen.getByLabelText(/email/i);
    expect(input).toHaveAttribute('aria-invalid', 'true');
  });

  it('should have role="alert" for error message', () => {
    render(
      <AccessibleFormInput label="Email" value="" onChange={() => {}} error="Invalid email" />
    );

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Invalid email');
  });

  it('should have aria-live="polite" for error announcement', () => {
    render(
      <AccessibleFormInput label="Email" value="" onChange={() => {}} error="Invalid email" />
    );

    const errorElement = screen.getByText(/invalid email/i).closest('div');
    expect(errorElement).toHaveAttribute('aria-live', 'polite');
  });

  it('should hide label visually when hideLabel is true', () => {
    render(
      <AccessibleFormInput
        label="Search"
        value=""
        onChange={() => {}}
        hideLabel
        placeholder="Search..."
      />
    );

    const label = screen.getByText(/search/i);
    expect(label).toHaveClass('sr-only');
  });

  it('should show hint but not error when both are provided', () => {
    render(
      <AccessibleFormInput
        label="Email"
        value=""
        onChange={() => {}}
        hint="Enter your email"
        error="Invalid email"
      />
    );

    // Error should be visible
    expect(screen.getByText(/invalid email/i)).toBeInTheDocument();

    // Hint should NOT be visible when error is present
    expect(screen.queryByText(/enter your email/i)).not.toBeInTheDocument();
  });

  it('should apply custom className to input', () => {
    render(
      <AccessibleFormInput
        label="Email"
        value=""
        onChange={() => {}}
        inputClassName="custom-class"
      />
    );

    const input = screen.getByLabelText(/email/i);
    expect(input).toHaveClass('custom-class');
  });

  it('should support different input types', () => {
    const { rerender } = render(
      <AccessibleFormInput label="Email" type="email" value="" onChange={() => {}} />
    );

    let input = screen.getByLabelText(/email/i);
    expect(input).toHaveAttribute('type', 'email');

    rerender(<AccessibleFormInput label="Password" type="password" value="" onChange={() => {}} />);

    input = screen.getByLabelText(/password/i);
    expect(input).toHaveAttribute('type', 'password');

    rerender(<AccessibleFormInput label="Age" type="number" value="" onChange={() => {}} />);

    input = screen.getByLabelText(/age/i);
    expect(input).toHaveAttribute('type', 'number');
  });
});
