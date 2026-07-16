import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { AiConsentSection } from '../sections/AiConsentSection';

expect.extend(toHaveNoViolations);

// useToast mock
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

const CONSENT = {
  userId: 'u1',
  consentedToAiProcessing: true,
  consentedToExternalProviders: false,
  consentedAt: '2026-01-01T00:00:00Z',
  consentVersion: '1.0.0',
};

beforeEach(() => {
  global.fetch = vi.fn((url: string, init?: RequestInit) => {
    if (!init || init.method === undefined) {
      // GET
      return Promise.resolve({ ok: true, json: () => Promise.resolve(CONSENT) } as Response);
    }
    // PUT
    return Promise.resolve({ ok: true, json: () => Promise.resolve({}) } as Response);
  }) as unknown as typeof fetch;
});

describe('AiConsentSection', () => {
  it('loads + renders consent toggles', async () => {
    render(<AiConsentSection />);
    await waitFor(() => expect(screen.getByTestId('ai-processing-toggle')).toBeInTheDocument());
    expect(screen.getByTestId('external-providers-toggle')).toBeInTheDocument();
  });

  // #2955 Fase 3: two entity="agent" ToggleSwitch primitives render together
  // here; guard the blocking axe AA gate on this consumer surface.
  it('has no axe AA violations with the entity="agent" consent toggles', async () => {
    const { container } = render(<AiConsentSection />);
    await waitFor(() => expect(screen.getByTestId('ai-processing-toggle')).toBeInTheDocument());
    expect(await axe(container)).toHaveNoViolations();
  });

  it('saves via PUT to /api/v1/users/me/ai-consent', async () => {
    render(<AiConsentSection />);
    await waitFor(() => screen.getByTestId('ai-processing-toggle'));
    // toggle external providers to enable Save
    const extToggle = screen
      .getByTestId('external-providers-toggle')
      .querySelector('button, input');
    if (extToggle) fireEvent.click(extToggle);
    const saveBtn = screen.getByTestId('save-ai-consent');
    fireEvent.click(saveBtn);
    await waitFor(() => {
      const calls = (global.fetch as any).mock.calls;
      const putCall = calls.find((c: any[]) => c[1]?.method === 'PUT');
      expect(putCall).toBeTruthy();
      expect(putCall[0]).toBe('/api/v1/users/me/ai-consent');
    });
  });
});
