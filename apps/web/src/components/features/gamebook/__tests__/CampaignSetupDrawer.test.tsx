import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { axe, toHaveNoViolations } from 'jest-axe';
import type { ReactNode } from 'react';

import * as client from '@/lib/api/gamebook-campaigns';

import { CampaignSetupDrawer } from '../CampaignSetupDrawer';

expect.extend(toHaveNoViolations);

vi.mock('@/lib/api/gamebook-campaigns');
const pushSpy = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushSpy }),
}));

/**
 * Force desktop mode so Radix Dialog is used instead of Vaul.
 * Vaul requires PointerEvent capture, scrollIntoView, and transform parsing
 * APIs that jsdom does not implement. Radix Dialog renders cleanly in jsdom.
 */
function installDesktopMatchMedia(): void {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn().mockReturnValue({
      matches: true, // (min-width: 768px) → true → desktop mode
      media: '(min-width: 768px)',
      addEventListener: () => {},
      removeEventListener: () => {},
      onchange: null,
    }),
  });
}

function wrap(ui: ReactNode) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  installDesktopMatchMedia();
  pushSpy.mockReset();
  vi.clearAllMocks();
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ─── Step 1: Name + Preset ───────────────────────────────────────────────────

describe('CampaignSetupDrawer — Step 1 (Name + Preset)', () => {
  it('renders title input + 3 preset radios when opened', () => {
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    expect(screen.getByTestId('campaign-setup-title')).toBeVisible();

    const radios = screen.getAllByRole('radio');
    expect(radios).toHaveLength(3);

    // Default preset "Gruppo A · I ragazzi" should be checked
    const groupARadio = screen.getByText('Gruppo A · I ragazzi').closest('[role="radio"]');
    expect(groupARadio).toHaveAttribute('aria-checked', 'true');
  });

  it('disables Next button when title is 2 chars or shorter', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'ab');

    expect(screen.getByTestId('campaign-setup-next')).toBeDisabled();
    expect(screen.getByText(/almeno 3 caratteri/i)).toBeInTheDocument();
  });

  it('enables Next button when title is 3+ chars', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'abc');

    expect(screen.getByTestId('campaign-setup-next')).not.toBeDisabled();
  });

  it('switches preset and updates aria-checked', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    // Click on "Gruppo B · Coppia" radio
    const gruppoBText = screen.getByText('Gruppo B · Coppia');
    const gruppoBRadio = gruppoBText.closest('[role="radio"]');
    await user.click(gruppoBRadio!);

    expect(gruppoBRadio).toHaveAttribute('aria-checked', 'true');

    // Gruppo A should now be unchecked
    const gruppoAText = screen.getByText('Gruppo A · I ragazzi');
    const gruppoARadio = gruppoAText.closest('[role="radio"]');
    expect(gruppoARadio).toHaveAttribute('aria-checked', 'false');
  });

  it('jest-axe smoke on step 1 default render', async () => {
    const { container } = wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    await waitFor(() => screen.getByTestId('campaign-setup-title'));

    // aria-hidden-focus may fire for Radix portal elements mounted outside test container
    const results = await axe(container, {
      rules: { 'aria-hidden-focus': { enabled: false } },
    });
    expect(results).toHaveNoViolations();
  });
});

// ─── Step 2: Players ─────────────────────────────────────────────────────────

describe('CampaignSetupDrawer — Step 2 (Players)', () => {
  async function advanceToStep2(user: ReturnType<typeof userEvent.setup>): Promise<void> {
    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');
    await user.click(screen.getByTestId('campaign-setup-next'));
  }

  it('shows the host and an enabled add-guest input (#2917)', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );
    await advanceToStep2(user);

    // Host (the authenticated owner, seeded server-side)
    expect(screen.getByText('Tu')).toBeInTheDocument();
    expect(screen.getByText(/Host · tu/)).toBeInTheDocument();

    // The roster is now editable: a real add-guest input (no disabled placeholder).
    expect(screen.getByTestId('campaign-add-guest-input')).toBeInTheDocument();
  });

  it('adds a guest to the roster and removes it (#2917)', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );
    await advanceToStep2(user);

    await user.type(screen.getByTestId('campaign-add-guest-input'), 'Marco');
    await user.click(screen.getByTestId('campaign-add-guest-button'));
    expect(screen.getByText('Marco')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Rimuovi Marco/ }));
    expect(screen.queryByText('Marco')).not.toBeInTheDocument();
  });
});

// ─── Step 3: Confirm ─────────────────────────────────────────────────────────

describe('CampaignSetupDrawer — Step 3 (Confirm)', () => {
  it('renders review card with campaign metadata', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');

    // Step 1 → 2
    await user.click(screen.getByTestId('campaign-setup-next'));
    // Step 2 → 3
    await user.click(screen.getByTestId('campaign-setup-next'));

    // Review card
    expect(screen.getByTestId('campaign-setup-review')).toBeInTheDocument();

    // Campaign title heading
    expect(screen.getByText('Test campaign')).toBeInTheDocument();

    // Preset row
    expect(screen.getByText('Preset')).toBeInTheDocument();
    expect(screen.getByText('Gruppo A · I ragazzi')).toBeInTheDocument();

    // Submit button
    expect(screen.getByTestId('campaign-setup-submit')).toBeInTheDocument();
  });
});

// ─── Footer + Submit ─────────────────────────────────────────────────────────

describe('CampaignSetupDrawer — Footer + Submit', () => {
  it('Back button text changes from "Annulla" on step 1 to "← Indietro" on step 2+', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    // Step 1: Back button says "Annulla"
    expect(screen.getByTestId('campaign-setup-back')).toHaveTextContent(/Annulla/);

    // Advance to step 2
    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');
    await user.click(screen.getByTestId('campaign-setup-next'));

    // Step 2: Back button says "← Indietro"
    expect(screen.getByTestId('campaign-setup-back')).toHaveTextContent(/Indietro/);
  });

  it('clicking Back on step 2 returns to step 1', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    // Advance to step 2
    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');
    await user.click(screen.getByTestId('campaign-setup-next'));

    // Confirm we are on step 2 (title input not visible)
    expect(screen.queryByTestId('campaign-setup-title')).not.toBeInTheDocument();

    // Go back
    await user.click(screen.getByTestId('campaign-setup-back'));

    // We should be back on step 1
    expect(screen.getByTestId('campaign-setup-title')).toBeInTheDocument();
  });

  it('submit success: calls createCampaign and navigates', async () => {
    vi.mocked(client.createCampaign).mockResolvedValueOnce({
      id: 'c1',
      gameRefId: 'g1',
      gameRefKind: 0,
      ownerUserId: 'u',
      title: 'Test campaign',
      currentParagraph: 0,
      history: [],
      lastReadAt: '2026-06-02T00:00:00Z',
      createdAt: '2026-06-02T00:00:00Z',
      updatedAt: '2026-06-02T00:00:00Z',
    } as never);

    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');

    // Step 1 → 2 → 3
    await user.click(screen.getByTestId('campaign-setup-next'));
    await user.click(screen.getByTestId('campaign-setup-next'));

    await user.click(screen.getByTestId('campaign-setup-submit'));

    await waitFor(() =>
      expect(client.createCampaign).toHaveBeenCalledWith({
        gameId: 'g1',
        title: 'Test campaign',
        guestNames: [],
      })
    );
    await waitFor(() => expect(pushSpy).toHaveBeenCalledWith('/library/g1/play/c1'));
  });

  it('submit sends the guest roster in the payload (#2917)', async () => {
    vi.mocked(client.createCampaign).mockResolvedValueOnce({ id: 'c1' } as never);

    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');
    await user.click(screen.getByTestId('campaign-setup-next'));

    // Add a guest in Step 2
    await user.type(screen.getByTestId('campaign-add-guest-input'), 'Marco');
    await user.click(screen.getByTestId('campaign-add-guest-button'));

    await user.click(screen.getByTestId('campaign-setup-next'));
    await user.click(screen.getByTestId('campaign-setup-submit'));

    await waitFor(() =>
      expect(client.createCampaign).toHaveBeenCalledWith({
        gameId: 'g1',
        title: 'Test campaign',
        guestNames: ['Marco'],
      })
    );
  });

  it('submit error: shows error message', async () => {
    vi.mocked(client.createCampaign).mockRejectedValueOnce(new Error('Boom failure'));

    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');

    await user.click(screen.getByTestId('campaign-setup-next'));
    await user.click(screen.getByTestId('campaign-setup-next'));
    await user.click(screen.getByTestId('campaign-setup-submit'));

    await waitFor(() => expect(screen.getByText(/Boom failure/)).toBeInTheDocument());
  });

  it('submit pending: button text changes to "Creazione…" and disables', async () => {
    // Never-resolving promise — keeps mutation in pending state
    vi.mocked(client.createCampaign).mockImplementationOnce(() => new Promise(() => {}));

    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');

    await user.click(screen.getByTestId('campaign-setup-next'));
    await user.click(screen.getByTestId('campaign-setup-next'));
    await user.click(screen.getByTestId('campaign-setup-submit'));

    await waitFor(() =>
      expect(screen.getByTestId('campaign-setup-submit')).toHaveTextContent(/Creazione/)
    );
    expect(screen.getByTestId('campaign-setup-submit')).toBeDisabled();
  });
});

// ─── Focus Management (#1554) ─────────────────────────────────────────────────

describe('CampaignSetupDrawer — Focus management (#1554)', () => {
  it('does NOT move focus on initial mount (preserves autoFocus on title input)', async () => {
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );
    // The title input has autoFocus and should retain focus on first render.
    // If the useEffect([step]) fires on initial mount, focus would jump to the
    // step container (tabIndex=-1) instead — that's the regression we guard against.
    const titleInput = await screen.findByTestId('campaign-setup-title');
    await waitFor(() => {
      expect(document.activeElement).toBe(titleInput);
    });
  });

  it('step 1 → 2: focus moves into step 2 region after Next click', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');

    const nextBtn = screen.getByTestId('campaign-setup-next');
    await user.click(nextBtn);

    // Focus must have moved away from the Next button, into the step content
    // container — and not vacuously passed by landing on document.body.
    const stepContent = await screen.findByTestId('campaign-setup-step-content');
    await waitFor(() => {
      expect(document.activeElement).not.toBe(nextBtn);
      expect(document.activeElement).not.toBe(document.body);
      expect(
        stepContent.contains(document.activeElement) || document.activeElement === stepContent
      ).toBe(true);
    });
  });

  it('step 2 → 3: focus moves into step 3 region after Next click', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');

    // Advance to step 2
    await user.click(screen.getByTestId('campaign-setup-next'));

    // Advance to step 3
    const nextBtn = screen.getByTestId('campaign-setup-next');
    await user.click(nextBtn);

    // Focus must have moved away from the Next button, into the step content
    // container — and not vacuously passed by landing on document.body.
    const stepContent = await screen.findByTestId('campaign-setup-step-content');
    await waitFor(() => {
      expect(document.activeElement).not.toBe(nextBtn);
      expect(document.activeElement).not.toBe(document.body);
      expect(
        stepContent.contains(document.activeElement) || document.activeElement === stepContent
      ).toBe(true);
    });
  });

  it('step 3 → 2 (back): focus moves into step 2 region after Back click', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');

    // Advance to step 3
    await user.click(screen.getByTestId('campaign-setup-next'));
    await user.click(screen.getByTestId('campaign-setup-next'));

    // Go back to step 2
    const backBtn = screen.getByTestId('campaign-setup-back');
    await user.click(backBtn);

    // Focus must have moved away from the Back button, into the step content
    // container — and not vacuously passed by landing on document.body.
    const stepContent = await screen.findByTestId('campaign-setup-step-content');
    await waitFor(() => {
      expect(document.activeElement).not.toBe(backBtn);
      expect(document.activeElement).not.toBe(document.body);
      expect(
        stepContent.contains(document.activeElement) || document.activeElement === stepContent
      ).toBe(true);
    });
  });

  it('modal-trap regression: focus stays inside the drawer after step change', async () => {
    const user = userEvent.setup();
    wrap(
      <CampaignSetupDrawer gameId="g1" gameTitle="Nanolith" open={true} onOpenChange={() => {}} />
    );

    const titleInput = screen.getByTestId('campaign-setup-title');
    await user.clear(titleInput);
    await user.type(titleInput, 'Test campaign');

    await user.click(screen.getByTestId('campaign-setup-next'));

    // After step change, focus must remain inside the drawer
    await waitFor(() => {
      const drawer = screen.getByTestId('campaign-setup-drawer');
      expect(drawer.contains(document.activeElement)).toBe(true);
    });
  });
});
