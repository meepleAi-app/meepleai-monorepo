/**
 * Tests for RateLimitBanner component
 *
 * Tests rendering, accessibility, and user interactions
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RateLimitBanner } from '../RateLimitBanner';

describe('RateLimitBanner', () => {
  it('should render with message and countdown', () => {
    render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
      />
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Rate Limit Exceeded')).toBeInTheDocument();
    expect(screen.getByText('Too many requests. Please wait 45 seconds.')).toBeInTheDocument();
    expect(screen.getByText(/Retry available in:/)).toBeInTheDocument();
    expect(screen.getByText('45s')).toBeInTheDocument();
  });

  it('should not render when message is null', () => {
    const { container } = render(
      <RateLimitBanner message={null} remainingSeconds={45} />
    );

    expect(container.firstChild).toBeNull();
  });

  it('should not render when remainingSeconds is 0', () => {
    const { container } = render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={0}
      />
    );

    expect(container.firstChild).toBeNull();
  });

  it('should hide countdown when showCountdown is false', () => {
    render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
        showCountdown={false}
      />
    );

    expect(screen.getByText('Too many requests. Please wait 45 seconds.')).toBeInTheDocument();
    expect(screen.queryByText(/Retry available in:/)).not.toBeInTheDocument();
  });

  it('should render dismiss button when dismissible', () => {
    const onDismiss = jest.fn();

    render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
        dismissible={true}
        onDismiss={onDismiss}
      />
    );

    const dismissButton = screen.getByRole('button', {
      name: 'Dismiss rate limit notification',
    });
    expect(dismissButton).toBeInTheDocument();
  });

  it('should call onDismiss when dismiss button is clicked', async () => {
    const user = userEvent.setup();
    const onDismiss = jest.fn();

    render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
        dismissible={true}
        onDismiss={onDismiss}
      />
    );

    const dismissButton = screen.getByRole('button', {
      name: 'Dismiss rate limit notification',
    });

    await user.click(dismissButton);

    expect(onDismiss).toHaveBeenCalledTimes(1);
  });

  it('should not render dismiss button when not dismissible', () => {
    render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
        dismissible={false}
      />
    );

    const dismissButton = screen.queryByRole('button', {
      name: 'Dismiss rate limit notification',
    });
    expect(dismissButton).not.toBeInTheDocument();
  });

  it('should apply custom className', () => {
    const { container } = render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
        className="custom-class"
      />
    );

    const alert = container.querySelector('.custom-class');
    expect(alert).toBeInTheDocument();
  });

  it('should have accessible ARIA attributes', () => {
    render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
      />
    );

    const alert = screen.getByRole('alert');
    expect(alert).toHaveAttribute('aria-live', 'polite');
    expect(alert).toHaveAttribute('aria-atomic', 'true');
  });

  it('should have accessible countdown with aria-live', () => {
    render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
      />
    );

    const countdown = screen.getByText(/Retry available in:/).parentElement;
    expect(countdown).toHaveAttribute('aria-live', 'polite');
    expect(countdown).toHaveAttribute('aria-atomic', 'true');
  });

  it('should render with destructive variant (red/error styling)', () => {
    const { container } = render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
      />
    );

    // Alert should be rendered (component uses destructive variant internally)
    const alert = container.querySelector('[role="alert"]');
    expect(alert).toBeInTheDocument();
  });

  it('should format countdown with seconds suffix', () => {
    render(
      <RateLimitBanner
        message="Too many requests. Please wait 45 seconds."
        remainingSeconds={45}
      />
    );

    // Check for the formatted countdown with "s" suffix
    expect(screen.getByText('45s')).toBeInTheDocument();
  });

  it('should render with 1 second remaining', () => {
    render(
      <RateLimitBanner
        message="Too many requests. Please wait 1 second."
        remainingSeconds={1}
      />
    );

    expect(screen.getByText('1s')).toBeInTheDocument();
  });

  it('should handle large countdown values', () => {
    render(
      <RateLimitBanner
        message="Too many requests. Please wait 5 minutes."
        remainingSeconds={300}
      />
    );

    expect(screen.getByText('300s')).toBeInTheDocument();
  });
});
