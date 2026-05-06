/**
 * TranslateParagraphDemo — DEMO-ONLY component tests (Path 5a / Nanolith demo).
 *
 * Tests cover the UI contract:
 *   - Form rendering (both inputs present)
 *   - Submit button disabled/enabled behavior
 *   - Paragraph ref parsed as number (numeric string) or string (non-numeric)
 *   - Translation output renders when translation is present
 *   - Streaming "In corso..." indicator on submit button
 *   - Citations panel rendered when citations array is non-empty
 *   - Error state rendered with retry button
 *   - A11y attributes: aria-live, aria-busy, role="alert"
 *
 * No real API calls. useTranslateParagraph is fully mocked.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

// ---------------------------------------------------------------------------
// Mock useTranslateParagraph
// ---------------------------------------------------------------------------

import type { UseTranslateParagraphResult } from '@/lib/gamebook/hooks/useTranslateParagraph';

const DEFAULT_HOOK_RESULT: UseTranslateParagraphResult = {
  translation: '',
  citations: [],
  isStreaming: false,
  isError: false,
  error: null,
  translate: vi.fn(),
  reset: vi.fn(),
  lastInput: null,
};

let mockHookResult: UseTranslateParagraphResult = { ...DEFAULT_HOOK_RESULT };

vi.mock('@/lib/gamebook/hooks/useTranslateParagraph', () => ({
  useTranslateParagraph: () => mockHookResult,
  composeTranslationPrompt: vi.fn(),
}));

// Import after mock is registered
import { TranslateParagraphDemo } from '../TranslateParagraphDemo';

// ---------------------------------------------------------------------------
// Shared test helpers
// ---------------------------------------------------------------------------

const GAME_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
const AGENT_ID = 'ffffffff-0000-1111-2222-333333333333';

function renderDemo(overrides?: Partial<UseTranslateParagraphResult>) {
  mockHookResult = { ...DEFAULT_HOOK_RESULT, ...overrides };
  return render(<TranslateParagraphDemo gameId={GAME_ID} agentId={AGENT_ID} />);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('TranslateParagraphDemo (DEMO-ONLY)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockHookResult = { ...DEFAULT_HOOK_RESULT };
  });

  // --- Form rendering ---

  it('renders the paragraph ref input', () => {
    renderDemo();
    expect(screen.getByRole('textbox', { name: 'Numero paragrafo' })).toBeInTheDocument();
  });

  it('renders the chapter context input', () => {
    renderDemo();
    expect(screen.getByRole('textbox', { name: 'Capitolo (opzionale)' })).toBeInTheDocument();
  });

  it('renders the submit button with label "Traduci"', () => {
    renderDemo();
    expect(screen.getByRole('button', { name: 'Traduci' })).toBeInTheDocument();
  });

  // --- Submit button disabled/enabled ---

  it('submit button is enabled when paragraph input has a value', async () => {
    renderDemo();
    const input = screen.getByRole('textbox', { name: 'Numero paragrafo' });
    fireEvent.change(input, { target: { value: '42' } });
    const btn = screen.getByRole('button', { name: 'Traduci' });
    expect(btn).not.toBeDisabled();
  });

  it('submit button shows "In corso..." when isStreaming=true', () => {
    renderDemo({ isStreaming: true });
    expect(screen.getByRole('button', { name: 'In corso...' })).toBeInTheDocument();
  });

  it('submit button is disabled when isStreaming=true', () => {
    renderDemo({ isStreaming: true });
    const btn = screen.getByRole('button', { name: 'In corso...' });
    expect(btn).toBeDisabled();
  });

  // --- Paragraph ref parsing: numeric vs string ---

  it('calls translate() with numeric paragraphRef when input is all-digits', async () => {
    const translateMock = vi.fn();
    mockHookResult = { ...DEFAULT_HOOK_RESULT, translate: translateMock };
    render(<TranslateParagraphDemo gameId={GAME_ID} agentId={AGENT_ID} />);

    const input = screen.getByRole('textbox', { name: 'Numero paragrafo' });
    fireEvent.change(input, { target: { value: '42' } });
    const form = screen.getByRole('form', { hidden: true }) ?? input.closest('form');
    fireEvent.submit(form!);

    await waitFor(() => {
      expect(translateMock).toHaveBeenCalledWith(expect.objectContaining({ paragraphRef: 42 }));
    });
  });

  it('calls translate() with string paragraphRef when input is non-numeric', async () => {
    const translateMock = vi.fn();
    mockHookResult = { ...DEFAULT_HOOK_RESULT, translate: translateMock };
    render(<TranslateParagraphDemo gameId={GAME_ID} agentId={AGENT_ID} />);

    const input = screen.getByRole('textbox', { name: 'Numero paragrafo' });
    fireEvent.change(input, { target: { value: '14a' } });
    const form = input.closest('form')!;
    fireEvent.submit(form);

    await waitFor(() => {
      expect(translateMock).toHaveBeenCalledWith(expect.objectContaining({ paragraphRef: '14a' }));
    });
  });

  // --- Translation output ---

  it('renders translation output area when translation is non-empty', () => {
    renderDemo({ translation: 'Testo tradotto di esempio.' });
    const el = document.querySelector('[data-slot="translation-output"]');
    expect(el).not.toBeNull();
    expect(el?.textContent).toContain('Testo tradotto di esempio.');
  });

  it('does not render translation output area when translation is empty and not streaming', () => {
    renderDemo({ translation: '', isStreaming: false });
    expect(document.querySelector('[data-slot="translation-output"]')).toBeNull();
  });

  it('renders translation output during streaming with aria-busy=true', () => {
    renderDemo({ translation: 'Parziale...', isStreaming: true });
    const el = document.querySelector('[data-slot="translation-output"]');
    expect(el).not.toBeNull();
    expect(el?.getAttribute('aria-busy')).toBe('true');
  });

  it('translation output has aria-live="polite"', () => {
    renderDemo({ translation: 'Testo', isStreaming: false });
    const el = document.querySelector('[data-slot="translation-output"]');
    expect(el?.getAttribute('aria-live')).toBe('polite');
  });

  // --- Citations panel ---

  it('renders citations list when citations are present', () => {
    renderDemo({
      citations: [{ docType: 'storybook', pageNumber: 42, snippet: '' }],
    });
    const el = document.querySelector('[data-slot="translation-citations"]');
    expect(el).not.toBeNull();
    expect(el?.textContent).toContain('storybook');
    expect(el?.textContent).toContain('42');
  });

  it('does not render citations panel when citations array is empty', () => {
    renderDemo({ citations: [] });
    expect(document.querySelector('[data-slot="translation-citations"]')).toBeNull();
  });

  // --- Error state ---

  it('renders error state with role="alert" when isError=true', () => {
    renderDemo({ isError: true, error: 'Si è verificato un errore.' });
    const el = document.querySelector('[data-slot="translation-error"]');
    expect(el).not.toBeNull();
    expect(el?.getAttribute('role')).toBe('alert');
    expect(el?.textContent).toContain('Si è verificato un errore.');
  });

  it('renders retry button in error state that calls reset()', () => {
    const resetMock = vi.fn();
    mockHookResult = { ...DEFAULT_HOOK_RESULT, isError: true, error: 'Err', reset: resetMock };
    render(<TranslateParagraphDemo gameId={GAME_ID} agentId={AGENT_ID} />);

    const retryBtn = screen.getByRole('button', { name: 'Riprova' });
    expect(retryBtn).toBeInTheDocument();
    fireEvent.click(retryBtn);
    expect(resetMock).toHaveBeenCalledTimes(1);
  });

  it('does not render error state when isError=false', () => {
    renderDemo({ isError: false });
    expect(document.querySelector('[data-slot="translation-error"]')).toBeNull();
  });

  // --- Root data-slot ---

  it('exposes data-slot="translate-paragraph-demo" on root', () => {
    renderDemo();
    expect(document.querySelector('[data-slot="translate-paragraph-demo"]')).not.toBeNull();
  });

  it('exposes data-slot="translate-form" on the form', () => {
    renderDemo();
    expect(document.querySelector('[data-slot="translate-form"]')).not.toBeNull();
  });

  it('exposes data-slot="paragraph-input" on the paragraph input', () => {
    renderDemo();
    expect(document.querySelector('[data-slot="paragraph-input"]')).not.toBeNull();
  });

  it('exposes data-slot="chapter-input" on the chapter input', () => {
    renderDemo();
    expect(document.querySelector('[data-slot="chapter-input"]')).not.toBeNull();
  });
});
