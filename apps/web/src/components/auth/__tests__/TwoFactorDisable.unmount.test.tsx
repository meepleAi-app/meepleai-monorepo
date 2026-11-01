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
 * It cannot be reproduced in isolation: the teardown has to fall between the unmount and the resolution of
 * the promise. What can be asserted is the contract: once unmounted, the app asks for no update. It is
 * checked in both directions — a guard that also swallowed the update on a mounted component would leave the
 * button stuck disabled.
 *
 * ⚠️ «No read of `window.event`» is NOT that contract. React reads `window.event` on its own account — for
 * example in the task that flushes passive effects, which a commit made while the component was still
 * MOUNTED queues in the scheduler; if the scheduler yields, the task runs after the unmount and the count
 * goes up although nobody updated anything. The login page test hit exactly this (#4134, PR #4304: a shard
 * red on a diff that did not touch the login), so, as there, only the reads with app code in the stack are
 * counted, and the first two tests keep the detector honest in both directions.
 */

import { useEffect, useState } from 'react';

import { configure, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
import { TwoFactorDisable } from '../TwoFactorDisable';

let originalWindowEvent: PropertyDescriptor | undefined;

/** A stack frame from code under `src/`, excluding the getter below. */
const isAppFrame = (frame: string) =>
  /[\\/]src[\\/]/.test(frame) &&
  !frame.includes('node_modules') &&
  !frame.includes('readWindowEvent');

/**
 * Replaces `window.event` with a getter that records the stack of whoever reads it (the same detector as
 * `app/(auth)/login/__tests__/_content.test.tsx`, #4134).
 *
 * A `setState` called by app code has the calling file in its stack; the reads React makes on its own have
 * only React and the scheduler. Limit, measured there: it does not see a setter handed straight to something
 * no app function awaits (`void promise.then(setX)`); `onFormSubmit` calls its setter from its own body, after
 * an `await`.
 */
function watchReadsOfWindowEvent() {
  const stacks: string[] = [];
  originalWindowEvent = Object.getOwnPropertyDescriptor(window, 'event');
  Object.defineProperty(window, 'event', {
    configurable: true,
    get: function readWindowEvent() {
      const limit = Error.stackTraceLimit;
      Error.stackTraceLimit = 50;
      try {
        stacks.push(new Error('window.event').stack ?? '');
      } finally {
        Error.stackTraceLimit = limit;
      }
      return undefined;
    },
  });
  return {
    /** Every read, by anyone. */
    all: () => stacks,
    /** The reads made for an update that the app code asked for. */
    requestedByTheApp: () => stacks.filter(stack => stack.split('\n').some(isAppFrame)),
  };
}

const settle = () => new Promise(resolve => setTimeout(resolve, 0));

afterEach(() => {
  // `window.event` goes back as it was: an own property if it was one, otherwise the inherited one.
  delete (window as unknown as { event?: unknown }).event;
  if (originalWindowEvent) {
    Object.defineProperty(window, 'event', originalWindowEvent);
    originalWindowEvent = undefined;
  }
});

/** Renders the form and submits it with an `onDisable` that stays pending until `finish` is called. */
async function submitWithPendingDisable({ strict = false }: { strict?: boolean } = {}) {
  let finish: () => void = () => {};
  const onDisable = vi.fn(
    () =>
      new Promise<void>(resolve => {
        finish = resolve;
      })
  );
  const user = userEvent.setup();
  const form = <TwoFactorDisable onDisable={onDisable} />;
  // StrictMode at the root, through Testing Library: a `<StrictMode>` put around the component inside
  // `renderWithIntl` left its effect running once, so it never exercised the double mount (measured).
  if (strict) configure({ reactStrictMode: true });
  let view: ReturnType<typeof renderWithIntl>;
  try {
    view = renderWithIntl(form);
  } finally {
    configure({ reactStrictMode: false });
  }

  await user.type(screen.getByTestId('disable-password-input'), 'correct horse');
  await user.type(screen.getByTestId('disable-code-input'), '123456');
  await user.click(screen.getByTestId('confirm-disable-button'));
  await waitFor(() => expect(onDisable).toHaveBeenCalledTimes(1));

  return { ...view, finish: () => finish() };
}

describe('the detector of updates requested after an unmount', () => {
  /**
   * First direction: an update asked for after the unmount MUST show up. The day React stops reading
   * `window.event` for a `setState`, or the stack changes shape, this test says so; without it the tests
   * below would pass for nothing.
   */
  it('sees an update the app requests after unmount', () => {
    const late = { requestUpdate: () => {} };
    function UpdatesLate() {
      const [, setValue] = useState(0);
      useEffect(() => {
        late.requestUpdate = () => setValue(1);
      }, []);
      return null;
    }
    const { unmount } = render(<UpdatesLate />);
    unmount();
    const reads = watchReadsOfWindowEvent();

    late.requestUpdate();

    expect(reads.requestedByTheApp().length).toBeGreaterThanOrEqual(1);
  });

  /**
   * Second direction: a read nobody in the app asked for does NOT count. Here it is a timer calling the
   * native `Reflect.get`: no app file in the stack, as when the reader is the scheduler.
   */
  it('does not count a read nobody in the app asked for', async () => {
    const reads = watchReadsOfWindowEvent();

    setTimeout(Reflect.get, 0, window, 'event');
    await settle();

    expect(reads.all().length).toBeGreaterThanOrEqual(1);
    expect(reads.requestedByTheApp()).toEqual([]);
  });
});

describe('TwoFactorDisable — a submit still pending when the component goes away (#4796)', () => {
  it('requests no update after it was unmounted', async () => {
    const { unmount, finish } = await submitWithPendingDisable();

    unmount();
    const reads = watchReadsOfWindowEvent();
    finish();
    // Let the continuation of `onFormSubmit` run.
    await settle();

    expect(reads.requestedByTheApp()).toEqual([]);
  });

  it('while it is still mounted, the button comes back once onDisable resolves', async () => {
    const { finish } = await submitWithPendingDisable();
    expect(screen.getByTestId('confirm-disable-button')).toBeDisabled();

    finish();

    await waitFor(() => expect(screen.getByTestId('confirm-disable-button')).toBeEnabled());
  });

  it('is the same under StrictMode, which mounts, unmounts and mounts again in development', async () => {
    // The guard's flag must come back to true on the second mount: left false, the button would stay disabled
    // after a submit that did succeed.
    const { finish } = await submitWithPendingDisable({ strict: true });
    expect(screen.getByTestId('confirm-disable-button')).toBeDisabled();

    finish();

    await waitFor(() => expect(screen.getByTestId('confirm-disable-button')).toBeEnabled());
  });
});
