/**
 * Tests for RateLimitedButton component
 *
 * Tests button behavior during rate limiting periods
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RateLimitedButton } from '../RateLimitedButton';

describe('RateLimitedButton', () => {
  it('should render button with children', () => {
    render(<RateLimitedButton>Submit</RateLimitedButton>);

    expect(screen.getByRole('button', { name: 'Submit' })).toBeInTheDocument();
  });

  it('should enable button when not rate limited', () => {
    render(
      <RateLimitedButton isRateLimited={false}>
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button', { name: 'Submit' });
    expect(button).not.toBeDisabled();
  });

  it('should disable button when rate limited', () => {
    render(
      <RateLimitedButton
        isRateLimited={true}
        remainingSeconds={45}
        message="Too many requests. Please wait 45 seconds."
      >
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });

  it('should show countdown in button text when showCountdownInButton is true', () => {
    render(
      <RateLimitedButton
        isRateLimited={true}
        remainingSeconds={45}
        showCountdownInButton={true}
        originalText="Submit"
      >
        Submit
      </RateLimitedButton>
    );

    expect(screen.getByText('Submit (45s)')).toBeInTheDocument();
  });

  it('should show generic wait message when showCountdownInButton is true but no originalText', () => {
    render(
      <RateLimitedButton
        isRateLimited={true}
        remainingSeconds={45}
        showCountdownInButton={true}
      >
        Submit
      </RateLimitedButton>
    );

    expect(screen.getByText('Wait 45s')).toBeInTheDocument();
  });

  it('should not modify button text when showCountdownInButton is false', () => {
    render(
      <RateLimitedButton
        isRateLimited={true}
        remainingSeconds={45}
        showCountdownInButton={false}
      >
        Submit
      </RateLimitedButton>
    );

    expect(screen.getByText('Submit')).toBeInTheDocument();
  });

  it('should show tooltip with rate limit message', () => {
    render(
      <RateLimitedButton
        isRateLimited={true}
        remainingSeconds={45}
        message="Too many requests. Please wait 45 seconds."
      >
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('title', 'Too many requests. Please wait 45 seconds.');
  });

  it('should not show tooltip when not rate limited', () => {
    render(
      <RateLimitedButton isRateLimited={false}>
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).not.toHaveAttribute('title');
  });

  it('should respect explicit disabled prop', () => {
    render(
      <RateLimitedButton disabled={true} isRateLimited={false}>
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });

  it('should be disabled when both disabled and rate limited', () => {
    render(
      <RateLimitedButton disabled={true} isRateLimited={true} remainingSeconds={45}>
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });

  it('should call onClick when clicked and not rate limited', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();

    render(
      <RateLimitedButton onClick={onClick} isRateLimited={false}>
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    await user.click(button);

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('should not call onClick when rate limited', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();

    render(
      <RateLimitedButton
        onClick={onClick}
        isRateLimited={true}
        remainingSeconds={45}
      >
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    await user.click(button);

    expect(onClick).not.toHaveBeenCalled();
  });

  it('should apply custom className', () => {
    render(
      <RateLimitedButton className="custom-class">
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).toHaveClass('custom-class');
  });

  it('should apply cursor-not-allowed when rate limited', () => {
    render(
      <RateLimitedButton isRateLimited={true} remainingSeconds={45}>
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).toHaveClass('cursor-not-allowed');
  });

  it('should not apply cursor-not-allowed when not rate limited', () => {
    render(
      <RateLimitedButton isRateLimited={false}>
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).not.toHaveClass('cursor-not-allowed');
  });

  it('should have aria-disabled attribute when rate limited', () => {
    render(
      <RateLimitedButton isRateLimited={true} remainingSeconds={45}>
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-disabled', 'true');
  });

  it('should have aria-label with rate limit message', () => {
    render(
      <RateLimitedButton
        isRateLimited={true}
        remainingSeconds={45}
        message="Too many requests. Please wait 45 seconds."
      >
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Too many requests. Please wait 45 seconds.');
  });

  it('should forward ref to button element', () => {
    const ref = React.createRef<HTMLButtonElement>();

    render(
      <RateLimitedButton ref={ref}>
        Submit
      </RateLimitedButton>
    );

    expect(ref.current).toBeInstanceOf(HTMLButtonElement);
  });

  it('should support button variants', () => {
    render(
      <RateLimitedButton variant="destructive">
        Delete
      </RateLimitedButton>
    );

    const button = screen.getByRole('button', { name: 'Delete' });
    expect(button).toBeInTheDocument();
  });

  it('should support button sizes', () => {
    render(
      <RateLimitedButton size="sm">
        Submit
      </RateLimitedButton>
    );

    const button = screen.getByRole('button', { name: 'Submit' });
    expect(button).toBeInTheDocument();
  });

  it('should handle countdown going to 0', () => {
    const { rerender } = render(
      <RateLimitedButton
        isRateLimited={true}
        remainingSeconds={1}
        showCountdownInButton={true}
        originalText="Submit"
      >
        Submit
      </RateLimitedButton>
    );

    expect(screen.getByText('Submit (1s)')).toBeInTheDocument();

    // Countdown expires
    rerender(
      <RateLimitedButton
        isRateLimited={false}
        remainingSeconds={0}
        showCountdownInButton={true}
        originalText="Submit"
      >
        Submit
      </RateLimitedButton>
    );

    expect(screen.getByText('Submit')).toBeInTheDocument();
    expect(screen.getByRole('button')).not.toBeDisabled();
  });
});
