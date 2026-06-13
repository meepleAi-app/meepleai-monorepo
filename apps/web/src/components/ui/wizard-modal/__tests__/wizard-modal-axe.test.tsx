/**
 * WizardModal — axe AA accessibility regression guard.
 *
 * Closes #2274. Restores the WizardModal axe scan that was previously the
 * 3rd `it()` block of the deleted `apps/web/__tests__/asse-b-axe.test.tsx`
 * (dropped in PR #2270 as orphan post-#2161 layout-shell dedupe). Without
 * this file the codebase has zero jest-axe coverage on WizardModal — the
 * e2e a11y suite skips authenticated routes including /onboarding where
 * WizardModal is mounted.
 *
 * Sibling to `wizard-modal.test.tsx`; standalone so the axe scan stays
 * isolated from the integration suite.
 */

import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { WizardModal } from '../wizard-modal';

expect.extend(toHaveNoViolations);

describe('WizardModal — axe AA gate', () => {
  it('open state — no axe violations on step 1', async () => {
    const { container } = render(
      <WizardModal
        steps={[
          { title: 'Step 1', content: <p>Content 1</p> },
          { title: 'Step 2', content: <p>Content 2</p> },
        ]}
        onComplete={async () => undefined}
        onCancel={() => undefined}
        open={true}
        onOpenChange={() => undefined}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
