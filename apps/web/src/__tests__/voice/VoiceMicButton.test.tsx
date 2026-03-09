import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { VoiceMicButton } from '@/components/chat-unified/VoiceMicButton';
import type { VoiceRecognitionState } from '@/lib/voice/types';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function renderButton(
  overrides: Partial<{
    state: VoiceRecognitionState;
    onTap: () => void;
    disabled: boolean;
    size: 'sm' | 'md' | 'lg';
  }> = {}
) {
  const props = {
    state: 'idle' as VoiceRecognitionState,
    onTap: vi.fn(),
    ...overrides,
  };
  const result = render(<VoiceMicButton {...props} />);
  return { ...result, onTap: props.onTap };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('VoiceMicButton', () => {
  // ------------------------------------------------------------------
  // data-testid per state
  // ------------------------------------------------------------------
  describe('data-testid', () => {
    const states: VoiceRecognitionState[] = [
      'idle',
      'requesting',
      'listening',
      'processing',
      'error',
    ];

    it.each(states)('renders data-testid="voice-mic-%s" for state=%s', state => {
      renderButton({ state });
      expect(screen.getByTestId(`voice-mic-${state}`)).toBeInTheDocument();
    });

    it('renders data-testid="voice-mic-disabled" when disabled', () => {
      renderButton({ state: 'idle', disabled: true });
      expect(screen.getByTestId('voice-mic-disabled')).toBeInTheDocument();
    });
  });

  // ------------------------------------------------------------------
  // Click behavior
  // ------------------------------------------------------------------
  describe('click behavior', () => {
    it('calls onTap when clicked', () => {
      const { onTap } = renderButton({ state: 'idle' });
      fireEvent.click(screen.getByRole('button'));
      expect(onTap).toHaveBeenCalledOnce();
    });

    it('does not call onTap when disabled', () => {
      const { onTap } = renderButton({ state: 'idle', disabled: true });
      fireEvent.click(screen.getByRole('button'));
      expect(onTap).not.toHaveBeenCalled();
    });

    it('button element has disabled attribute when disabled', () => {
      renderButton({ state: 'idle', disabled: true });
      expect(screen.getByRole('button')).toBeDisabled();
    });
  });

  // ------------------------------------------------------------------
  // aria-label per state
  // ------------------------------------------------------------------
  describe('aria-label', () => {
    it('has "Start voice input" for idle state', () => {
      renderButton({ state: 'idle' });
      expect(screen.getByRole('button')).toHaveAttribute('aria-label', 'Start voice input');
    });

    it('has "Requesting microphone access" for requesting state', () => {
      renderButton({ state: 'requesting' });
      expect(screen.getByRole('button')).toHaveAttribute(
        'aria-label',
        'Requesting microphone access'
      );
    });

    it('has "Listening - tap to stop" for listening state', () => {
      renderButton({ state: 'listening' });
      expect(screen.getByRole('button')).toHaveAttribute('aria-label', 'Listening - tap to stop');
    });

    it('has "Processing speech" for processing state', () => {
      renderButton({ state: 'processing' });
      expect(screen.getByRole('button')).toHaveAttribute('aria-label', 'Processing speech');
    });

    it('has "Voice input error - tap to retry" for error state', () => {
      renderButton({ state: 'error' });
      expect(screen.getByRole('button')).toHaveAttribute(
        'aria-label',
        'Voice input error - tap to retry'
      );
    });

    it('has "Voice input unavailable" when disabled', () => {
      renderButton({ state: 'idle', disabled: true });
      expect(screen.getByRole('button')).toHaveAttribute('aria-label', 'Voice input unavailable');
    });
  });

  // ------------------------------------------------------------------
  // Listening pulse animation
  // ------------------------------------------------------------------
  describe('listening state', () => {
    it('renders pulse animation overlay when listening', () => {
      renderButton({ state: 'listening' });
      const button = screen.getByTestId('voice-mic-listening');
      const pulseSpan = button.querySelector(
        '[aria-hidden="true"].motion-safe\\:animate-voice-pulse'
      );
      expect(pulseSpan).toBeInTheDocument();
    });

    it('does not render pulse animation when idle', () => {
      renderButton({ state: 'idle' });
      const button = screen.getByTestId('voice-mic-idle');
      const pulseSpan = button.querySelector('.motion-safe\\:animate-voice-pulse');
      expect(pulseSpan).not.toBeInTheDocument();
    });

    it('sets aria-pressed=true when listening', () => {
      renderButton({ state: 'listening' });
      expect(screen.getByRole('button')).toHaveAttribute('aria-pressed', 'true');
    });

    it('sets aria-pressed=false when not listening', () => {
      renderButton({ state: 'idle' });
      expect(screen.getByRole('button')).toHaveAttribute('aria-pressed', 'false');
    });
  });

  // ------------------------------------------------------------------
  // Keyboard interaction
  // ------------------------------------------------------------------
  describe('keyboard interaction', () => {
    it('calls onTap on Enter key', () => {
      const { onTap } = renderButton({ state: 'idle' });
      fireEvent.keyDown(screen.getByRole('button'), { key: 'Enter' });
      expect(onTap).toHaveBeenCalledOnce();
    });

    it('calls onTap on Space key', () => {
      const { onTap } = renderButton({ state: 'idle' });
      fireEvent.keyDown(screen.getByRole('button'), { key: ' ' });
      expect(onTap).toHaveBeenCalledOnce();
    });

    it('does not call onTap on Enter when disabled', () => {
      const { onTap } = renderButton({ state: 'idle', disabled: true });
      fireEvent.keyDown(screen.getByRole('button'), { key: 'Enter' });
      expect(onTap).not.toHaveBeenCalled();
    });
  });
});
