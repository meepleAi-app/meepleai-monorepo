/**
 * Page-level smoke test for `/sessions/[id]` after Wave D.3 BIG-BANG (Issue #756).
 *
 * The legacy live UI render path was migrated to `/sessions/[id]/live` in Wave D.2
 * (PR #749 Foundation + #753 Interactions). This route now renders the
 * post-game session summary via `SessionSummaryView`. Detailed FSM/URL/handler
 * coverage lives in
 * `_components/__tests__/SessionSummaryView.test.tsx`. This file remains a
 * thin smoke test that verifies the page is wired correctly.
 */

import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { describe, it, expect, vi } from 'vitest';

// Mock SessionSummaryView so we don't pull in hook factories etc.
vi.mock('../_components/SessionSummaryView', () => ({
  SessionSummaryView: ({ sessionId }: { sessionId: string }) => (
    <div data-testid="session-summary-view-mock" data-session-id={sessionId} />
  ),
}));

import SessionDetailPage from '../page';

describe('SessionDetailPage', () => {
  it('renders SessionSummaryView with the resolved sessionId', async () => {
    const params = Promise.resolve({ id: '00000000-0000-4000-8000-000000000aaa' });
    const element = await SessionDetailPage({ params });
    const { findByTestId } = render(
      <IntlProvider locale="it" messages={{}} defaultLocale="it">
        {element}
      </IntlProvider>
    );
    const node = await findByTestId('session-summary-view-mock');
    expect(node).toHaveAttribute('data-session-id', '00000000-0000-4000-8000-000000000aaa');
  });
});
