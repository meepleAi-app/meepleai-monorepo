/**
 * Settings → Security (2FA + active sessions) in the user's language (#4813).
 *
 * The area wrote its strings in the components, in English — with a few sentences in Italian among them —
 * so a user with the interface in Italian read the rest of the page in Italian and this section in English.
 * These tests render each screen of the area with the real catalogue of each language.
 *
 * "In Italian" is checked two ways, because each alone passes a half-translated screen: the exact
 * sentences, and the absence of English words in everything a user can read or hear (the text, and the
 * `aria-label`, `placeholder` and `title` attributes). The English assertions are the ones the e2e specs
 * (`settings-tab-2fa.spec.ts`) select by, so they must not move.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';
import { EnglishIntl, ItalianIntl } from '@/test-utils/intl-wrapper';

import { SecuritySection } from '../sections/SecuritySection';
import { ActiveSessionsCard } from '../two-factor/ActiveSessionsCard';
import { BackupCodesView } from '../two-factor/BackupCodesView';
import { OTPInput6Slot } from '../two-factor/OTPInput6Slot';
import { TwoFactorDisableDialog } from '../two-factor/TwoFactorDisableDialog';
import { TwoFactorBottomSheet } from '../two-factor/TwoFactorBottomSheet';
import { TwoFactorSetupModal } from '../two-factor/TwoFactorSetupModal';
import { TwoFactorStatusCard } from '../two-factor/TwoFactorStatusCard';

vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getTwoFactorStatus: vi.fn(),
      setup2FA: vi.fn(),
      enable2FA: vi.fn(),
      disable2FA: vi.fn(),
      getUserSessions: vi.fn(),
      revokeSession: vi.fn(),
      revokeAllSessions: vi.fn(),
    },
  },
}));

// Words that exist in English and not in Italian. Not a dictionary: a tripwire for a string left behind.
// Known gap, outside this area: the dialog primitive (`components/ui/overlays/dialog.tsx`) labels its own
// close button "Close dialog" / "Close" in English for every dialog of the app, so the words are not in the
// list below — adding them would fail on a string this change does not own.
const ENGLISH_WORD =
  /\b(the|your|you|please|this|for|and|with|are|is|if|not|can|now|to|of|our|we|enable|enabled|disable|continue|back|done|cancel|copy|download|revoke|active|unknown|device|current|digit|verification|recovery|scan|enter|ago|sign out|all other)\b/i;

const SETUP = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUrl: 'otpauth://totp/MeepleAI:test%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=MeepleAI',
  backupCodes: ['AAAA-1111', 'BBBB-2222'],
};

const MINUTE = 60_000;
// Built per test, not once at import: the relative times are counted from "now", at one-minute resolution.
function sessions() {
  return [
    session('s1', 'Mozilla/5.0 (Macintosh) Chrome/124', '1.2.3.4', Date.now() - MINUTE),
    session('s2', 'iPhone iOS 17', '5.6.7.8', Date.now() - 3 * 60 * MINUTE),
    session('s4', 'Firefox/120.0', '9.9.9.9', Date.now() - 2 * 24 * 60 * MINUTE),
    session('s3', null, null, null),
  ];
}

function session(
  id: string,
  userAgent: string | null,
  ipAddress: string | null,
  seenAt: number | null
) {
  return {
    id,
    userId: 'u1',
    userEmail: 'a@b.it',
    createdAt: new Date().toISOString(),
    expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
    lastSeenAt: seenAt === null ? null : new Date(seenAt).toISOString(),
    revokedAt: null,
    ipAddress,
    userAgent,
  };
}

function inQueryClient(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>;
}

/** Everything a user reads or hears on the screen: the text, and the attributes read out by assistive tech. */
function whatTheUserCanRead(): string {
  const attributes = [
    ...document.body.querySelectorAll('[aria-label],[placeholder],[title]'),
  ].flatMap(el => ['aria-label', 'placeholder', 'title'].map(name => el.getAttribute(name) ?? ''));
  return [document.body.textContent ?? '', ...attributes].join(' ');
}

function expectNoEnglishLeft(): void {
  const match = ENGLISH_WORD.exec(whatTheUserCanRead());
  expect(match, `English left on an Italian screen: "${match?.[0]}"`).toBeNull();
}

beforeEach(() => {
  vi.clearAllMocks();
  // `userEvent.setup()` installs a read-only clipboard of its own, so a plain assignment would throw.
  Object.defineProperty(navigator, 'clipboard', {
    value: { writeText: vi.fn().mockResolvedValue(undefined) },
    configurable: true,
  });
  vi.mocked(api.auth.getUserSessions).mockResolvedValue(sessions());
  vi.mocked(api.auth.getTwoFactorStatus).mockResolvedValue({
    isEnabled: false,
    enabledAt: null,
    unusedBackupCodesCount: 0,
  });
});

describe('status card', () => {
  const off = { isEnabled: false, enabledAt: null, unusedBackupCodesCount: 0 };
  // Mid-month at midday UTC: month and year are the same in every time zone; the day can shift by one in the
  // extreme zones, so the tests do not pin it.
  const on = { isEnabled: true, enabledAt: '2026-01-15T12:00:00Z', unusedBackupCodesCount: 10 };

  it('speaks Italian when it is off', () => {
    render(<TwoFactorStatusCard status={off} onSetup={() => {}} onDisable={() => {}} />, {
      wrapper: ItalianIntl,
    });

    expect(
      screen.getByRole('heading', { name: 'Autenticazione a due fattori' })
    ).toBeInTheDocument();
    expect(screen.getByTestId('2fa-status')).toHaveTextContent('Non abilitata');
    expect(screen.getByTestId('enable-2fa')).toHaveTextContent(
      "Configura l'autenticazione a due fattori"
    );
    expect(screen.getByText(/Aggiungi un secondo livello di protezione/)).toBeInTheDocument();
    expectNoEnglishLeft();
  });

  it('speaks Italian when it is on, with the date written the Italian way', () => {
    render(<TwoFactorStatusCard status={on} onSetup={() => {}} onDisable={() => {}} />, {
      wrapper: ItalianIntl,
    });

    expect(screen.getByTestId('2fa-status')).toHaveTextContent(/Abilitata · \d{2} gen 2026/);
    expect(screen.getByTestId('disable-2fa')).toHaveTextContent('Disabilita 2FA');
    expect(screen.getByRole('button', { name: 'Rigenera i codici di backup' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Rigenera i codici di backup' })).toHaveAttribute(
      'title',
      'Rigenera i codici di backup (in arrivo)'
    );
    expect(
      screen.getByText(/Disabilitala solo se hai perso l'accesso al dispositivo/)
    ).toBeInTheDocument();
    expectNoEnglishLeft();
  });

  it('keeps its English words when the interface is in English', () => {
    render(<TwoFactorStatusCard status={off} onSetup={() => {}} onDisable={() => {}} />, {
      wrapper: EnglishIntl,
    });

    expect(screen.getByRole('heading', { name: 'Two-factor authentication' })).toBeInTheDocument();
    expect(screen.getByTestId('2fa-status')).toHaveTextContent('Not enabled');
    expect(screen.getByTestId('enable-2fa')).toHaveTextContent('Set up two-factor authentication');
  });

  it('writes the date the English way in English, not always the Italian way', () => {
    render(<TwoFactorStatusCard status={on} onSetup={() => {}} onDisable={() => {}} />, {
      wrapper: EnglishIntl,
    });

    expect(screen.getByTestId('2fa-status')).toHaveTextContent(/Enabled · Jan \d{2}, 2026/);
    expect(screen.getByTestId('disable-2fa')).toHaveTextContent('Disable 2FA');
  });
});

describe('disable dialog', () => {
  it('speaks Italian', async () => {
    render(inQueryClient(<TwoFactorDisableDialog open onClose={() => {}} />), {
      wrapper: ItalianIntl,
    });

    expect(
      screen.getByRole('heading', { name: "Disabilita l'autenticazione a due fattori" })
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Codice dell'app o codice di backup")).toHaveAttribute(
      'placeholder',
      '123456 oppure ABCD-2345'
    );
    expect(screen.getByPlaceholderText('La password del tuo account')).toBeInTheDocument();
    expect(screen.getByText(/ciascuno può essere usato una sola volta/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Annulla' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Disabilita 2FA' })).toBeDisabled();
    expectNoEnglishLeft();
  });

  it('says a wrong password in Italian', async () => {
    vi.mocked(api.auth.disable2FA).mockRejectedValue(
      new ApiError({ message: 'API failed with status 401', statusCode: 401, endpoint: '/x' })
    );
    const user = userEvent.setup();
    render(inQueryClient(<TwoFactorDisableDialog open onClose={() => {}} />), {
      wrapper: ItalianIntl,
    });

    await user.type(screen.getByLabelText('Password'), ['x', 'y', 'z'].join(''));
    await user.type(screen.getByLabelText("Codice dell'app o codice di backup"), '123456');
    await user.click(screen.getByRole('button', { name: 'Disabilita 2FA' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('La password non è corretta.');
  });

  it('keeps its English words when the interface is in English', () => {
    render(inQueryClient(<TwoFactorDisableDialog open onClose={() => {}} />), {
      wrapper: EnglishIntl,
    });

    expect(
      screen.getByRole('heading', { name: 'Disable two-factor authentication' })
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Authenticator or backup code')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Disable 2FA' })).toBeInTheDocument();
  });
});

describe('setup wizard', () => {
  function renderWizard(wrapper: typeof ItalianIntl) {
    return render(
      inQueryClient(
        <TwoFactorSetupModal open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />
      ),
      { wrapper }
    );
  }

  it('speaks Italian from the QR code to the backup codes', async () => {
    vi.mocked(api.auth.enable2FA).mockResolvedValue({
      success: true,
      backupCodes: null,
      errorMessage: null,
    });
    renderWizard(ItalianIntl);

    // Step 1: scan
    expect(
      screen.getByRole('heading', { name: "Configura l'autenticazione a due fattori" })
    ).toBeInTheDocument();
    expect(screen.getByText(/Completa tutti e tre i passaggi/)).toBeInTheDocument();
    expect(
      screen.getByText(/Scansiona questo codice QR con la tua app di autenticazione/)
    ).toBeInTheDocument();
    expect(screen.getByText('Oppure inserisci questo codice a mano:')).toBeInTheDocument();
    expect(screen.getByLabelText(/Codice QR per la 2FA/)).toBeInTheDocument();
    expectNoEnglishLeft();

    // Step 2: verify
    fireEvent.click(screen.getByRole('button', { name: 'Continua' }));
    expect(
      screen.getByText('Inserisci il codice della tua app di autenticazione')
    ).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Codice di verifica a 6 cifre' })).toBeInTheDocument();
    expect(screen.getByLabelText('Cifra 1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Indietro' })).toBeInTheDocument();
    expectNoEnglishLeft();

    // Step 3: backup codes
    fireEvent.paste(screen.getAllByRole('textbox')[0], {
      clipboardData: { getData: () => '123456' },
    });
    await waitFor(() =>
      expect(screen.getByText(/Copia o scarica i codici prima di continuare/)).toBeInTheDocument()
    );
    expect(screen.getByRole('button', { name: 'Copia tutti' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Scarica .txt' })).toBeInTheDocument();
    expect(
      screen.getByLabelText('Ho salvato i codici di backup in un posto sicuro')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Fatto' })).toBeDisabled();
    expectNoEnglishLeft();
  });

  it('speaks Italian in the mobile sheet too', () => {
    render(inQueryClient(<TwoFactorBottomSheet open setupData={SETUP} onClose={() => {}} />), {
      wrapper: ItalianIntl,
    });

    expect(screen.getByText("Configura l'autenticazione a due fattori")).toBeInTheDocument();
    expect(screen.getByText(/Completa tutti e tre i passaggi/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continua' })).toBeInTheDocument();
  });

  it('shows the text the server sends when it refuses a code, as it is: that text is not in the catalogue', async () => {
    // What the backend really answers to a wrong code: HTTP 400 with `{ error: "<text>" }` — the text can
    // also be a lock-out notice — so the interface cannot replace it with a sentence of its own.
    vi.mocked(api.auth.enable2FA).mockRejectedValue(
      new ApiError({ message: 'Invalid verification code', statusCode: 400, endpoint: '/x' })
    );
    renderWizard(ItalianIntl);

    fireEvent.click(screen.getByRole('button', { name: 'Continua' }));
    fireEvent.paste(screen.getAllByRole('textbox')[0], {
      clipboardData: { getData: () => '000000' },
    });

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid verification code');
  });

  it('says a refused code in Italian when the answer carries no text', async () => {
    vi.mocked(api.auth.enable2FA).mockResolvedValue({
      success: false,
      backupCodes: null,
      errorMessage: null,
    });
    renderWizard(ItalianIntl);

    fireEvent.click(screen.getByRole('button', { name: 'Continua' }));
    fireEvent.paste(screen.getAllByRole('textbox')[0], {
      clipboardData: { getData: () => '000000' },
    });

    expect(await screen.findByRole('alert')).toHaveTextContent('Codice non valido. Riprova.');
  });

  it('keeps its English words, the ones the e2e selects by, when the interface is in English', () => {
    renderWizard(EnglishIntl);

    expect(
      screen.getByRole('heading', { name: 'Set up two-factor authentication' })
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        /Scan this QR code with your authenticator app \(Google Authenticator, Authy, etc\.\)/
      )
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    expect(screen.getByLabelText('Digit 1')).toBeInTheDocument();
    expect(screen.getByText(/enter the code/i)).toBeInTheDocument();
  });

  it('ends on the English "Done" the e2e clicks, once the backup codes are shown', async () => {
    vi.mocked(api.auth.enable2FA).mockResolvedValue({
      success: true,
      backupCodes: null,
      errorMessage: null,
    });
    renderWizard(EnglishIntl);

    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    fireEvent.paste(screen.getAllByRole('textbox')[0], {
      clipboardData: { getData: () => '123456' },
    });

    expect(await screen.findByRole('button', { name: 'Done' })).toBeDisabled();
  });

  it.each([
    [ItalianIntl, 'Continua', 'verifica in corso…'],
    [EnglishIntl, 'Continue', 'verifying…'],
  ])('says the code is being checked, while it is', async (wrapper, next, checking) => {
    vi.mocked(api.auth.enable2FA).mockReturnValue(new Promise<never>(() => {}));
    renderWizard(wrapper);

    fireEvent.click(screen.getByRole('button', { name: next }));
    fireEvent.paste(screen.getAllByRole('textbox')[0], {
      clipboardData: { getData: () => '123456' },
    });

    expect(await screen.findByText(checking)).toBeInTheDocument();
  });
});

describe('backup codes', () => {
  it('speaks Italian, and says "copied" in Italian once copied', async () => {
    render(<BackupCodesView codes={['AAAA-1111']} acked={false} onAck={() => {}} />, {
      wrapper: ItalianIntl,
    });

    fireEvent.click(screen.getByRole('button', { name: 'Copia tutti' }));

    expect(await screen.findByRole('button', { name: 'Copiato!' })).toBeInTheDocument();
    expectNoEnglishLeft();
  });

  it('keeps its English words when the interface is in English', () => {
    render(<BackupCodesView codes={['AAAA-1111']} acked={false} onAck={() => {}} />, {
      wrapper: EnglishIntl,
    });

    expect(screen.getByRole('button', { name: 'Copy all' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Download .txt' })).toBeInTheDocument();
    expect(
      screen.getByLabelText('I have saved the recovery codes in a safe place')
    ).toBeInTheDocument();
  });

  it.each([
    [ItalianIntl, 'Scarica .txt', 'MeepleAI · Codici di backup'],
    [EnglishIntl, 'Download .txt', 'MeepleAI · Recovery codes'],
  ])(
    'writes the downloaded file in the language of the interface',
    async (wrapper, button, title) => {
      let written: Blob | undefined;
      Object.assign(URL, {
        createObjectURL: vi.fn((blob: Blob) => {
          written = blob;
          return 'blob:backup-codes';
        }),
        revokeObjectURL: vi.fn(),
      });
      render(<BackupCodesView codes={['AAAA-1111']} acked={false} onAck={() => {}} />, { wrapper });

      fireEvent.click(screen.getByRole('button', { name: button }));

      const text = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = () => reject(reader.error);
        reader.readAsText(written as Blob);
      });
      expect(text.startsWith(title)).toBe(true);
      expect(text).toContain('AAAA-1111');
    }
  );
});

describe('code input', () => {
  it('names the group and each digit in the language of the interface', () => {
    const { unmount } = render(<OTPInput6Slot onComplete={() => {}} />, { wrapper: ItalianIntl });
    expect(screen.getByRole('group', { name: 'Codice di verifica a 6 cifre' })).toBeInTheDocument();
    expect(screen.getByLabelText('Cifra 6')).toBeInTheDocument();
    unmount();

    render(<OTPInput6Slot onComplete={() => {}} />, { wrapper: EnglishIntl });
    expect(screen.getByRole('group', { name: '6-digit verification code' })).toBeInTheDocument();
    expect(screen.getByLabelText('Digit 6')).toBeInTheDocument();
  });
});

describe('active sessions', () => {
  it('speaks Italian: title, current badge, relative times, unknown device and address', async () => {
    render(inQueryClient(<ActiveSessionsCard />), { wrapper: ItalianIntl });

    expect(await screen.findByRole('heading', { name: 'Sessioni attive' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText(/Chrome/)).toBeInTheDocument());
    expect(screen.getByText('ATTUALE')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Questa sessione' })).toBeDisabled();
    expect(screen.getAllByRole('button', { name: 'Revoca' })).toHaveLength(3);
    expect(screen.getByText(/1 min fa · 1\.2\.3\.4/)).toBeInTheDocument();
    expect(screen.getByText(/3 h fa · 5\.6\.7\.8/)).toBeInTheDocument();
    expect(screen.getByText(/2 g fa · 9\.9\.9\.9/)).toBeInTheDocument();
    expect(screen.getByText('Dispositivo sconosciuto')).toBeInTheDocument();
    expect(screen.getByText(/— · IP sconosciuto/)).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Esci da tutte le altre sessioni' })
    ).toBeInTheDocument();
    expectNoEnglishLeft();
  });

  it('says it could not load the sessions in the language of the interface', async () => {
    vi.mocked(api.auth.getUserSessions).mockRejectedValue(new Error('network'));

    const { unmount } = render(inQueryClient(<ActiveSessionsCard />), { wrapper: ItalianIntl });
    expect(await screen.findByText('Impossibile caricare le sessioni.')).toBeInTheDocument();
    unmount();

    render(inQueryClient(<ActiveSessionsCard />), { wrapper: EnglishIntl });
    expect(await screen.findByText('Error loading sessions.')).toBeInTheDocument();
  });

  it.each([
    [ItalianIntl, 'Caricamento'],
    [EnglishIntl, 'Loading'],
  ])('names the spinner, while the sessions load', async (wrapper, label) => {
    vi.mocked(api.auth.getUserSessions).mockReturnValue(new Promise<never>(() => {}));

    render(inQueryClient(<ActiveSessionsCard />), { wrapper });

    expect(await screen.findByLabelText(label)).toBeInTheDocument();
  });

  it('keeps its English words when the interface is in English', async () => {
    render(inQueryClient(<ActiveSessionsCard />), { wrapper: EnglishIntl });

    expect(await screen.findByRole('heading', { name: 'Active sessions' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('CURRENT')).toBeInTheDocument());
    expect(screen.getByText(/1 min ago · 1\.2\.3\.4/)).toBeInTheDocument();
    expect(screen.getByText(/2 d ago · 9\.9\.9\.9/)).toBeInTheDocument();
    expect(screen.getByText('Unknown device')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign out all other sessions' })).toBeInTheDocument();
  });
});

describe('the whole Security section', () => {
  it('has nothing left in English when the interface is in Italian', async () => {
    render(inQueryClient(<SecuritySection />), { wrapper: ItalianIntl });

    await waitFor(() =>
      expect(screen.getByTestId('2fa-status')).toHaveTextContent('Non abilitata')
    );
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Sessioni attive' })).toBeInTheDocument()
    );
    await waitFor(() => expect(screen.getByText(/Chrome/)).toBeInTheDocument());
    expectNoEnglishLeft();
  });
});
