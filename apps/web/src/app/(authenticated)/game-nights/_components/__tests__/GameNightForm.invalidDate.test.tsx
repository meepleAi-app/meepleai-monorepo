/**
 * GameNightForm — a date that is not one (#4840).
 *
 * The field is a `datetime-local`, `required`, and a year of five digits passes the native check.
 * `handleSubmit` then converted the raw string with `new Date(scheduledAt).toISOString()` and threw inside the
 * React event handler: nothing was sent and the organizer saw nothing.
 *
 * React reports an error from a handler on `window` and does not rethrow it from `fireEvent`, so the throw is
 * looked for there; an `expect(...).not.toThrow()` would pass with or without the fix.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { GameNightForm } from '../GameNightForm';

// `delete process.env.TZ` does not make Node re-read the zone: the one the process started in is assigned back.
const ORIGINAL_ZONE = Intl.DateTimeFormat().resolvedOptions().timeZone;

afterEach(() => {
  process.env.TZ = ORIGINAL_ZONE;
});

/** Hands the input to React with the string a browser would, which jsdom would otherwise blank. */
const typeRaw = (input: HTMLInputElement, value: string): void => {
  Object.defineProperty(input, 'value', { configurable: true, get: () => value });
  fireEvent.change(input);
};

const submit = (container: HTMLElement): unknown[] => {
  const reported: unknown[] = [];
  const onError = (event: ErrorEvent): void => {
    reported.push(event.error);
    event.preventDefault();
  };
  window.addEventListener('error', onError);
  try {
    fireEvent.submit(container.querySelector('form') as HTMLFormElement);
  } finally {
    window.removeEventListener('error', onError);
  }
  return reported;
};

describe('GameNightForm — a date that is not one (#4840)', () => {
  it('does not submit, throws nothing, and says why', () => {
    const onSubmit = vi.fn();
    const { container } = render(<GameNightForm onSubmit={onSubmit} isSubmitting={false} />);
    typeRaw(container.querySelector('#scheduledAt') as HTMLInputElement, '12345-10-30T20:00');

    const reported = submit(container);

    expect(reported).toEqual([]);
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toHaveTextContent('La data non è valida.');
  });

  it('takes the message away as soon as the date is changed', () => {
    const { container } = render(<GameNightForm onSubmit={vi.fn()} isSubmitting={false} />);
    const field = container.querySelector('#scheduledAt') as HTMLInputElement;
    typeRaw(field, '12345-10-30T20:00');
    submit(container);
    expect(screen.getByRole('alert')).toBeInTheDocument();

    // The input still answers with the string put on it above, so the new value is put the same way.
    typeRaw(field, '2026-10-30T20:00');

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('still submits a real date, as the instant it names', () => {
    // The other direction: refusing what is not a date must not refuse what is. In a zone that is not UTC (the CI
    // runner is UTC, where reading the value as UTC would pass for nothing), with the zone checked first.
    process.env.TZ = 'Europe/Rome';
    expect(new Date(2026, 9, 30, 20, 0).getTimezoneOffset()).toBe(-60);
    const onSubmit = vi.fn();
    const { container } = render(<GameNightForm onSubmit={onSubmit} isSubmitting={false} />);
    fireEvent.change(container.querySelector('#scheduledAt') as HTMLInputElement, {
      target: { value: '2026-10-30T20:00' },
    });

    const reported = submit(container);

    expect(reported).toEqual([]);
    expect(onSubmit).toHaveBeenCalledTimes(1);
    expect(onSubmit.mock.calls[0][0].scheduledAt).toBe('2026-10-30T19:00:00.000Z');
  });
});
