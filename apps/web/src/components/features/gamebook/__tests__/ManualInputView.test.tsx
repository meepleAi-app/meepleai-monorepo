import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as analytics from '@/lib/analytics/track-event';
import * as sseHook from '@/lib/gamebook/hooks/useTranslateTextSSE';
import * as gameBooksHook from '@/hooks/useGameBooks';
import { ManualInputView } from '../ManualInputView';
import { GameBookRole, type GameRef } from '@/lib/api/gamebook';

const CAMPAIGN_ID = 'c-1';
const BOOK_ID = 'b-1';
const GAME_REF: GameRef = { id: 'g-1', kind: 0 };

const mockBooks = [{ id: BOOK_ID, displayName: 'Test Book', roles: GameBookRole.Narrative }];

beforeEach(() => {
  vi.spyOn(gameBooksHook, 'useGameBooks').mockReturnValue({ data: mockBooks } as never);
  vi.spyOn(sseHook, 'useTranslateTextSSE').mockReturnValue({
    partialText: '',
    isComplete: false,
    appliedTerms: [],
    error: undefined,
    start: vi.fn(),
    stop: vi.fn(),
  } as never);
  localStorage.clear();
});
afterEach(() => {
  vi.restoreAllMocks();
});

describe('ManualInputView', () => {
  it('S1: renders head with "Inserimento manuale" + MANUAL badge + textarea + counter 0/2000 + CTA disabled', () => {
    render(<ManualInputView campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);
    expect(screen.getByText(/inserimento manuale/i)).toBeInTheDocument();
    expect(screen.getByText(/^MANUAL$/i)).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /inserisci il testo/i })).toBeInTheDocument();
    expect(screen.getByText('0/2000')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /traduci/i })).toBeDisabled();
  });

  it('S2 (a): typing 12 chars → counter "12/2000" normal + CTA enabled', async () => {
    const user = userEvent.setup();
    render(<ManualInputView campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);
    await user.type(screen.getByRole('textbox', { name: /inserisci il testo/i }), 'Hello world.');
    expect(screen.getByText('12/2000')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /traduci/i })).not.toBeDisabled();
  });

  it('S2 (b): 1850 chars → warning counter', async () => {
    const user = userEvent.setup();
    render(<ManualInputView campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);
    const textarea = screen.getByRole('textbox', { name: /inserisci il testo/i });
    await user.click(textarea);
    fireEvent.change(textarea, { target: { value: 'a'.repeat(1850) } });
    const counter = screen.getByText('1850/2000');
    expect(counter.className).toMatch(/warn/i);
  });

  it('S2 (c): 2001 chars → over + CTA disabled', () => {
    render(<ManualInputView campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);
    const textarea = screen.getByRole('textbox', { name: /inserisci il testo/i });
    fireEvent.change(textarea, { target: { value: 'a'.repeat(2001) } });
    const counter = screen.getByText('2001/2000');
    expect(counter.className).toMatch(/over/i);
    expect(screen.getByRole('button', { name: /traduci/i })).toBeDisabled();
  });

  it('S3: submit fires analytics + sse.start with correct args + persists lang to localStorage', async () => {
    const startMock = vi.fn();
    vi.spyOn(sseHook, 'useTranslateTextSSE').mockReturnValue({
      partialText: '',
      isComplete: false,
      appliedTerms: [],
      error: undefined,
      start: startMock,
      stop: vi.fn(),
    } as never);
    const trackSpy = vi.spyOn(analytics, 'trackEvent');
    const user = userEvent.setup();
    render(<ManualInputView campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await user.type(screen.getByRole('textbox', { name: /inserisci il testo/i }), 'Hello.');
    await user.click(screen.getByRole('button', { name: /traduci/i }));

    expect(trackSpy).toHaveBeenCalledWith(
      'translate.manual_submit',
      expect.objectContaining({
        campaignId: CAMPAIGN_ID,
        textLength: 6,
        sourceLang: 'IT',
        gameBookId: BOOK_ID,
      })
    );
    expect(startMock).toHaveBeenCalledWith(CAMPAIGN_ID, 'Hello.', 'IT', BOOK_ID);
  });

  it('S7: lang dropdown change updates next submit body', async () => {
    const startMock = vi.fn();
    vi.spyOn(sseHook, 'useTranslateTextSSE').mockReturnValue({
      partialText: '',
      isComplete: false,
      appliedTerms: [],
      error: undefined,
      start: startMock,
      stop: vi.fn(),
    } as never);
    const user = userEvent.setup();
    render(<ManualInputView campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await user.click(screen.getByRole('button', { name: /lingua sorgente/i }));
    await user.click(screen.getByRole('option', { name: /francese/i }));

    await user.type(screen.getByRole('textbox', { name: /inserisci il testo/i }), 'Bonjour.');
    await user.click(screen.getByRole('button', { name: /traduci/i }));

    expect(startMock).toHaveBeenCalledWith(CAMPAIGN_ID, 'Bonjour.', 'FR', BOOK_ID);
  });

  it('S8: jest-axe 0 violations in idle state', async () => {
    const { container } = render(<ManualInputView campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
