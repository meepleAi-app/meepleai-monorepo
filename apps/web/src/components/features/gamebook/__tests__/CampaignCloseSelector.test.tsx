/**
 * CampaignCloseSelector — tests for issue #2639 (SI-8): the play-evening-end
 * 3-way close selector. Completa/Abbandona close the campaign (POST close);
 * Archivia leaves it resumable (callback only, no close call).
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, type RenderResult } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement, ReactNode } from 'react';
import { IntlProvider } from 'react-intl';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import itMessages from '@/locales/it.json';
import * as client from '@/lib/api/gamebook-campaigns';
import { GamebookCampaignOutcome } from '@/lib/api/gamebook-campaigns';

import { CampaignCloseSelector } from '../CampaignCloseSelector';

vi.mock('@/lib/api/gamebook-campaigns', async importOriginal => {
  const actual = await importOriginal<typeof import('@/lib/api/gamebook-campaigns')>();
  return { ...actual, closeCampaign: vi.fn() };
});

function flatten(obj: Record<string, unknown>, prefix = ''): Record<string, string> {
  return Object.keys(obj).reduce(
    (acc, key) => {
      const full = prefix ? `${prefix}.${key}` : key;
      const value = obj[key];
      if (value && typeof value === 'object') {
        Object.assign(acc, flatten(value as Record<string, unknown>, full));
      } else {
        acc[full] = String(value);
      }
      return acc;
    },
    {} as Record<string, string>
  );
}
const FLAT_IT = flatten(itMessages as Record<string, unknown>);

const CAMPAIGN_ID = '11111111-1111-4111-8111-111111111111';
const closedCampaign = { id: CAMPAIGN_ID, title: 'Eldoria', outcome: 1 } as never;

function renderSelector(ui: ReactElement): RenderResult {
  const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={qc}>
        <IntlProvider locale="it" messages={FLAT_IT} onError={() => {}}>
          {children}
        </IntlProvider>
      </QueryClientProvider>
    );
  }
  return render(ui, { wrapper: Wrapper });
}

beforeEach(() => {
  vi.mocked(client.closeCampaign).mockReset();
});
afterEach(() => {
  vi.clearAllMocks();
});

describe('CampaignCloseSelector', () => {
  it('renders the 3-way selector (Completa / Archivia / Abbandona)', () => {
    renderSelector(
      <CampaignCloseSelector campaignId={CAMPAIGN_ID} campaignName="Eldoria" onArchive={() => {}} />
    );
    expect(screen.getByTestId('campaign-close-selector')).toBeInTheDocument();
    expect(screen.getByTestId('campaign-close-complete')).toHaveTextContent('Completa');
    expect(screen.getByTestId('campaign-close-archive')).toHaveTextContent('Archivia');
    expect(screen.getByTestId('campaign-close-abandon')).toHaveTextContent('Abbandona');
  });

  it('Completa closes the campaign as Completed and fires onClosed', async () => {
    vi.mocked(client.closeCampaign).mockResolvedValueOnce(closedCampaign);
    const onClosed = vi.fn();
    const user = userEvent.setup();
    renderSelector(
      <CampaignCloseSelector
        campaignId={CAMPAIGN_ID}
        campaignName="Eldoria"
        onArchive={() => {}}
        onClosed={onClosed}
      />
    );

    await user.click(screen.getByTestId('campaign-close-complete'));

    await waitFor(() =>
      expect(client.closeCampaign).toHaveBeenCalledWith(
        CAMPAIGN_ID,
        GamebookCampaignOutcome.Completed
      )
    );
    await waitFor(() => expect(onClosed).toHaveBeenCalledTimes(1));
  });

  it('Abbandona closes the campaign as Abandoned', async () => {
    vi.mocked(client.closeCampaign).mockResolvedValueOnce(closedCampaign);
    const user = userEvent.setup();
    renderSelector(
      <CampaignCloseSelector campaignId={CAMPAIGN_ID} campaignName="Eldoria" onArchive={() => {}} />
    );

    await user.click(screen.getByTestId('campaign-close-abandon'));

    await waitFor(() =>
      expect(client.closeCampaign).toHaveBeenCalledWith(
        CAMPAIGN_ID,
        GamebookCampaignOutcome.Abandoned
      )
    );
  });

  it('Archivia fires onArchive and does NOT close the campaign (resumable)', async () => {
    const onArchive = vi.fn();
    const user = userEvent.setup();
    renderSelector(
      <CampaignCloseSelector
        campaignId={CAMPAIGN_ID}
        campaignName="Eldoria"
        onArchive={onArchive}
      />
    );

    await user.click(screen.getByTestId('campaign-close-archive'));

    expect(onArchive).toHaveBeenCalledTimes(1);
    expect(client.closeCampaign).not.toHaveBeenCalled();
  });

  it('surfaces an error with a retry when the close fails', async () => {
    vi.mocked(client.closeCampaign).mockRejectedValueOnce(new Error('boom'));
    const user = userEvent.setup();
    renderSelector(
      <CampaignCloseSelector campaignId={CAMPAIGN_ID} campaignName="Eldoria" onArchive={() => {}} />
    );

    await user.click(screen.getByTestId('campaign-close-complete'));

    await waitFor(() => expect(screen.getByTestId('campaign-close-error')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
  });
});
