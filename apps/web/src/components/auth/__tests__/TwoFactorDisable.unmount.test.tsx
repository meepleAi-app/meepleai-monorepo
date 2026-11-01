/**
 * TwoFactorDisable — the submit that outlives the component (#4796).
 *
 * `onFormSubmit` awaits `onDisable(data)` and then sets `isSubmitting` back to false. When the component is
 * unmounted while `onDisable` is still in flight, that update is dispatched on a dead tree. React 19 no
 * longer warns about it, so it is invisible while `window` exists; once jsdom is torn down,
 * `resolveUpdatePriority` reads `window.event` and throws `ReferenceError: window is not defined` outside
 * any test, which turned a frontend shard red with zero failing tests (the third class in CLAUDE.md,
 * «Known Flaky Tests»; the same shape as #4132).
 *
 * The contract is checked from the symptom's own entry point: every update React dispatches reads
 * `window.event`, so a reader installed AFTER the unmount sees whether one was dispatched. It is checked in
 * both directions — a guard that also swallowed the update on a mounted component would leave the button
 * stuck disabled.
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
import { TwoFactorDisable } from '../TwoFactorDisable';

const ORIGINAL_EVENT = Object.getOwnPropertyDescriptor(window, 'event');

/** Counts every read of `window.event` from now on: one per update React dispatches. */
function watchDispatchedUpdates(): () => number {
  let reads = 0;
  Object.defineProperty(window, 'event', {
    configurable: true,
    get() {
      reads += 1;
      return undefined;
    },
  });
  return () => reads;
}

afterEach(() => {
  if (ORIGINAL_EVENT) {
    Object.defineProperty(window, 'event', ORIGINAL_EVENT);
  } else {
    delete (window as unknown as Record<string, unknown>).event;
  }
});

/** Renders the form and submits it with an `onDisable` that stays pending until `finish` is called. */
async function submitWithPendingDisable() {
  let finish: () => void = () => {};
  const onDisable = vi.fn(
    () =>
      new Promise<void>(resolve => {
        finish = resolve;
      })
  );
  const user = userEvent.setup();
  const view = renderWithIntl(<TwoFactorDisable onDisable={onDisable} />);

  await user.type(screen.getByTestId('disable-password-input'), 'correct horse');
  await user.type(screen.getByTestId('disable-code-input'), '123456');
  await user.click(screen.getByTestId('confirm-disable-button'));
  await waitFor(() => expect(onDisable).toHaveBeenCalledTimes(1));

  return { ...view, finish: () => finish() };
}

describe('TwoFactorDisable — a submit still pending when the component goes away (#4796)', () => {
  it('does not dispatch an update after it was unmounted', async () => {
    const { unmount, finish } = await submitWithPendingDisable();

    unmount();
    const updatesAfterUnmount = watchDispatchedUpdates();
    finish();
    // Let the continuation of `onFormSubmit` run.
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(updatesAfterUnmount()).toBe(0);
  });

  it('while it is still mounted, the button comes back once onDisable resolves', async () => {
    const { finish } = await submitWithPendingDisable();
    expect(screen.getByTestId('confirm-disable-button')).toBeDisabled();

    finish();

    await waitFor(() => expect(screen.getByTestId('confirm-disable-button')).toBeEnabled());
  });
});
