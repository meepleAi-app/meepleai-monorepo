import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { VoiceTranscriptOverlay } from '@/components/chat-unified/VoiceTranscriptOverlay';
import type { VoiceRecognitionState } from '@/lib/voice/types';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

interface OverlayProps {
  interimText: string;
  finalText: string;
  state: VoiceRecognitionState;
  onEdit: (text: string) => void;
  onSend: () => void;
  onCancel: () => void;
  autoSend: boolean;
}

function renderOverlay(overrides: Partial<OverlayProps> = {}) {
  const props: OverlayProps = {
    interimText: '',
    finalText: '',
    state: 'idle',
    onEdit: vi.fn(),
    onSend: vi.fn(),
    onCancel: vi.fn(),
    autoSend: false,
    ...overrides,
  };
  const result = render(<VoiceTranscriptOverlay {...props} />);
  return { ...result, props };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('VoiceTranscriptOverlay', () => {
  // ------------------------------------------------------------------
  // Null rendering when idle with no text
  // ------------------------------------------------------------------
  describe('visibility', () => {
    it('renders nothing when idle with no finalText', () => {
      renderOverlay({ state: 'idle', finalText: '' });
      expect(screen.queryByTestId('voice-transcript-overlay')).not.toBeInTheDocument();
    });

    it('renders the overlay when listening', () => {
      renderOverlay({ state: 'listening', interimText: 'hello' });
      expect(screen.getByTestId('voice-transcript-overlay')).toBeInTheDocument();
    });
  });

  // ------------------------------------------------------------------
  // Listening state — shows interim text
  // ------------------------------------------------------------------
  describe('listening state', () => {
    it('shows interim text while listening', () => {
      renderOverlay({ state: 'listening', interimText: 'how do I set' });
      expect(screen.getByText('how do I set')).toBeInTheDocument();
    });

    it('shows "Listening" when interimText is empty', () => {
      renderOverlay({ state: 'listening', interimText: '' });
      expect(screen.getByText('Listening')).toBeInTheDocument();
    });
  });

  // ------------------------------------------------------------------
  // Processing state
  // ------------------------------------------------------------------
  describe('processing state', () => {
    it('shows "Processing..." indicator', () => {
      renderOverlay({ state: 'processing' });
      expect(screen.getByText('Processing...')).toBeInTheDocument();
    });
  });

  // ------------------------------------------------------------------
  // Final text — manual mode (autoSend=false)
  // ------------------------------------------------------------------
  describe('manual mode (autoSend=false)', () => {
    it('shows textarea with final text', () => {
      renderOverlay({
        state: 'idle',
        finalText: 'Hello world',
        autoSend: false,
      });
      const textarea = screen.getByTestId('voice-transcript-textarea');
      expect(textarea).toBeInTheDocument();
      expect(textarea).toHaveValue('Hello world');
    });

    it('shows send and cancel buttons', () => {
      renderOverlay({
        state: 'idle',
        finalText: 'Hello world',
        autoSend: false,
      });
      expect(screen.getByTestId('voice-transcript-send')).toBeInTheDocument();
      expect(screen.getByTestId('voice-transcript-cancel')).toBeInTheDocument();
    });

    it('calls onEdit when textarea changes', () => {
      const onEdit = vi.fn();
      renderOverlay({
        state: 'idle',
        finalText: 'Hello',
        autoSend: false,
        onEdit,
      });

      const textarea = screen.getByTestId('voice-transcript-textarea');
      fireEvent.change(textarea, { target: { value: 'Hello world' } });

      expect(onEdit).toHaveBeenCalledWith('Hello world');
    });

    it('calls onSend when send button is clicked', () => {
      const onSend = vi.fn();
      renderOverlay({
        state: 'idle',
        finalText: 'Hello',
        autoSend: false,
        onSend,
      });

      fireEvent.click(screen.getByTestId('voice-transcript-send'));
      expect(onSend).toHaveBeenCalledOnce();
    });

    it('calls onCancel when cancel button is clicked', () => {
      const onCancel = vi.fn();
      renderOverlay({
        state: 'idle',
        finalText: 'Hello',
        autoSend: false,
        onCancel,
      });

      fireEvent.click(screen.getByTestId('voice-transcript-cancel'));
      expect(onCancel).toHaveBeenCalledOnce();
    });
  });

  // ------------------------------------------------------------------
  // Auto-send mode
  // ------------------------------------------------------------------
  describe('autoSend mode', () => {
    it('triggers auto-send flow when autoSend is true with final text', () => {
      vi.useFakeTimers();

      const onSend = vi.fn();
      renderOverlay({
        state: 'idle',
        finalText: 'Hello world',
        autoSend: true,
        onSend,
      });

      // Before timer fires, onSend has not been called
      expect(onSend).not.toHaveBeenCalled();

      // After 600ms the auto-send fires
      act(() => {
        vi.advanceTimersByTime(600);
      });
      expect(onSend).toHaveBeenCalledOnce();

      vi.useRealTimers();
    });

    it('calls onSend after delay in autoSend mode', () => {
      vi.useFakeTimers();
      const onSend = vi.fn();

      renderOverlay({
        state: 'idle',
        finalText: 'Hello world',
        autoSend: true,
        onSend,
      });

      // Before the 600ms delay
      expect(onSend).not.toHaveBeenCalled();

      // After the delay
      vi.advanceTimersByTime(600);
      expect(onSend).toHaveBeenCalledOnce();

      vi.useRealTimers();
    });

    it('does not show textarea in autoSend mode', () => {
      vi.useFakeTimers();

      renderOverlay({
        state: 'idle',
        finalText: 'Hello world',
        autoSend: true,
      });

      expect(screen.queryByTestId('voice-transcript-textarea')).not.toBeInTheDocument();

      vi.useRealTimers();
    });
  });

  // ------------------------------------------------------------------
  // Keyboard shortcuts
  // ------------------------------------------------------------------
  describe('keyboard shortcuts', () => {
    it('sends on Ctrl+Enter in textarea', () => {
      const onSend = vi.fn();
      renderOverlay({
        state: 'idle',
        finalText: 'Hello',
        autoSend: false,
        onSend,
      });

      const textarea = screen.getByTestId('voice-transcript-textarea');
      fireEvent.keyDown(textarea, { key: 'Enter', ctrlKey: true });

      expect(onSend).toHaveBeenCalledOnce();
    });

    it('cancels on Escape in textarea', () => {
      const onCancel = vi.fn();
      renderOverlay({
        state: 'idle',
        finalText: 'Hello',
        autoSend: false,
        onCancel,
      });

      const textarea = screen.getByTestId('voice-transcript-textarea');
      fireEvent.keyDown(textarea, { key: 'Escape' });

      expect(onCancel).toHaveBeenCalledOnce();
    });
  });

  // ------------------------------------------------------------------
  // Accessibility
  // ------------------------------------------------------------------
  describe('accessibility', () => {
    it('has role="status" on the overlay container', () => {
      renderOverlay({ state: 'listening', interimText: 'hello' });
      const overlay = screen.getByTestId('voice-transcript-overlay');
      expect(overlay).toHaveAttribute('role', 'status');
    });

    it('has aria-live="polite"', () => {
      renderOverlay({ state: 'listening', interimText: 'hello' });
      const overlay = screen.getByTestId('voice-transcript-overlay');
      expect(overlay).toHaveAttribute('aria-live', 'polite');
    });

    it('textarea has aria-label "Edit transcribed text"', () => {
      renderOverlay({
        state: 'idle',
        finalText: 'text',
        autoSend: false,
      });
      const textarea = screen.getByTestId('voice-transcript-textarea');
      expect(textarea).toHaveAttribute('aria-label', 'Edit transcribed text');
    });
  });
});
