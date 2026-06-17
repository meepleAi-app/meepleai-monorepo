/**
 * Regression test for #2421 — vitest test runner crashes with
 * `RangeError: Invalid string length` when a component triggers
 * `react-intl` MissingTranslationError during render.
 *
 * Root cause: native Node `console.error(<Error>)` explodes inside the
 * vitest stderr-intercept code path when the Error's `.stack` is deep
 * enough that vitest's source-map resolver enters an exponential string
 * concat. The trigger only manifests when an Error originates from
 * inside React's render tree (the stack includes react-dom, react-intl
 * and Vite source-map frames), which is why a standalone
 * `console.error(new Error('x'))` does not reproduce it.
 *
 * The fix lives in `apps/web/vitest.setup.tsx`: stringify args via
 * `util.format` before forwarding to the original `console.error`,
 * bypassing the broken Error-handling path.
 *
 * Without the fix, this test fails with
 *   `RangeError: Invalid string length`
 * thrown from the vitest console.error wrapper at the
 * `originalError.call(console, ...args)` line. Symptom on the
 * feature/issue-2373 branch: 67 cases failed across
 * `SessionLiveView.test.tsx` because LiveTopBar requested i18n keys
 * missing from that file's test fixture.
 */

import { render } from '@testing-library/react';
import { IntlProvider, useIntl } from 'react-intl';
import { describe, expect, it } from 'vitest';

function TriggerMissingTranslation(): null {
  const intl = useIntl();
  // Forces react-intl to log a MissingTranslationError via console.error.
  intl.formatMessage({ id: 'pages.test.nonexistent.key.for.regression.2421' });
  return null;
}

describe('vitest setup — console.error robustness (#2421)', () => {
  it('rendering a component that emits a react-intl MissingTranslationError does not crash the runner', () => {
    expect(() => {
      render(
        <IntlProvider locale="it" messages={{}}>
          <TriggerMissingTranslation />
        </IntlProvider>
      );
    }).not.toThrow();
  });
});
