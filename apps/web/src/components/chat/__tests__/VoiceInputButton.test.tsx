/**
 * VoiceInputButton Tests (Issue #3351)
 *
 * Tests:
 * - Rendering and initial state
 * - Browser support detection
 * - Start/stop listening
 * - Transcript handling
 * - Error handling
 * - Accessibility
 */

import React from 'react';

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach, afterEach, Mock } from 'vitest';

import { VoiceInputButton } from '../VoiceInputButton';

// Mock SpeechRecognition API
const mockRecognition = {
  continuous: false,
  interimResults: false,
  lang: '',
  maxAlternatives: 1,
  start: vi.fn(),
  stop: vi.fn(),
  abort: vi.fn(),
  onresult: null as ((event: unknown) => void) | null,
  onerror: null as ((event: unknown) => void) | null,
  onend: null as (() => void) | null,
  onstart: null as (() => void) | null,
};

const MockSpeechRecognition = vi.fn(() => mockRecognition);

describe('VoiceInputButton', () => {
  const mockOnTranscript = vi.fn();
  const mockOnInterimTranscript = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    // Reset mock recognition
    mockRecognition.start = vi.fn(() => {
      // Simulate onstart callback
      if (mockRecognition.onstart) {
        mockRecognition.onstart();
      }
    });
    mockRecognition.stop = vi.fn(() => {
      // Simulate onend callback
      if (mockRecognition.onend) {
        mockRecognition.onend();
      }
    });
    mockRecognition.abort = vi.fn();
    mockRecognition.onresult = null;
    mockRecognition.onerror = null;
    mockRecognition.onend = null;
    mockRecognition.onstart = null;

    // Set up global SpeechRecognition
    (window as unknown as { SpeechRecognition: unknown }).SpeechRecognition = MockSpeechRecognition;
  });

  afterEach(() => {
    delete (window as unknown as { SpeechRecognition?: unknown }).SpeechRecognition;
  });

  describe('Rendering', () => {
    it('renders microphone button when supported', () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');
      expect(button).toBeInTheDocument();
      expect(button).toHaveAttribute('aria-label', 'Inizia riconoscimento vocale');
    });

    it('renders disabled button when speech recognition is not supported', () => {
      delete (window as unknown as { SpeechRecognition?: unknown }).SpeechRecognition;

      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');
      expect(button).toBeDisabled();
      expect(button).toHaveAttribute('aria-label', 'Riconoscimento vocale non supportato');
    });

    it('renders disabled button when disabled prop is true', () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} disabled />);

      const button = screen.getByRole('button');
      expect(button).toBeDisabled();
    });

    it('applies custom className', () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} className="custom-class" />);

      const button = screen.getByRole('button');
      expect(button).toHaveClass('custom-class');
    });
  });

  describe('Listening state', () => {
    it('starts listening when button is clicked', async () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');
      await userEvent.click(button);

      expect(mockRecognition.start).toHaveBeenCalled();
      expect(button).toHaveAttribute('aria-pressed', 'true');
    });

    it('stops listening when button is clicked while listening', async () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');

      // Start listening
      await userEvent.click(button);
      expect(mockRecognition.start).toHaveBeenCalled();

      // Stop listening
      await userEvent.click(button);
      expect(mockRecognition.stop).toHaveBeenCalled();
    });

    it('updates aria-label when listening', async () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');
      await userEvent.click(button);

      await waitFor(() => {
        expect(button).toHaveAttribute('aria-label', 'Interrompi ascolto');
      });
    });
  });

  describe('Transcript handling', () => {
    it('calls onTranscript with final transcript', async () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');
      await userEvent.click(button);

      // Simulate recognition result
      const mockEvent = {
        resultIndex: 0,
        results: {
          length: 1,
          0: {
            isFinal: true,
            length: 1,
            0: { transcript: 'test message', confidence: 0.9 },
          },
        },
      };

      // Trigger the onresult callback
      if (mockRecognition.onresult) {
        mockRecognition.onresult(mockEvent);
      }

      expect(mockOnTranscript).toHaveBeenCalledWith('test message');
    });

    it('calls onInterimTranscript with interim results', async () => {
      render(
        <VoiceInputButton
          onTranscript={mockOnTranscript}
          onInterimTranscript={mockOnInterimTranscript}
        />
      );

      const button = screen.getByRole('button');
      await userEvent.click(button);

      // Simulate interim result
      const mockEvent = {
        resultIndex: 0,
        results: {
          length: 1,
          0: {
            isFinal: false,
            length: 1,
            0: { transcript: 'interim text', confidence: 0.7 },
          },
        },
      };

      if (mockRecognition.onresult) {
        mockRecognition.onresult(mockEvent);
      }

      expect(mockOnInterimTranscript).toHaveBeenCalledWith('interim text');
    });
  });

  describe('Error handling', () => {
    it('handles no-speech error', async () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');
      await userEvent.click(button);

      // Simulate error
      if (mockRecognition.onerror) {
        mockRecognition.onerror({ error: 'no-speech' });
      }

      await waitFor(() => {
        expect(button).toHaveAttribute('aria-pressed', 'false');
      });
    });

    it('handles not-allowed error', async () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');
      await userEvent.click(button);

      if (mockRecognition.onerror) {
        mockRecognition.onerror({ error: 'not-allowed' });
      }

      await waitFor(() => {
        expect(button).toHaveAttribute('aria-pressed', 'false');
      });
    });
  });

  describe('Language configuration', () => {
    it('uses provided language prop', async () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} language="en-US" />);

      const button = screen.getByRole('button');
      await userEvent.click(button);

      expect(mockRecognition.lang).toBe('en-US');
    });

    it('uses Italian as default when no language provided', async () => {
      // Mock navigator.language
      Object.defineProperty(navigator, 'language', {
        value: 'it-IT',
        configurable: true,
      });

      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');
      await userEvent.click(button);

      expect(mockRecognition.lang).toBe('it-IT');
    });
  });

  describe('Cleanup', () => {
    it('aborts recognition on unmount', () => {
      const { unmount } = render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      // Start listening first
      const button = screen.getByRole('button');
      fireEvent.click(button);

      // Unmount component
      unmount();

      expect(mockRecognition.abort).toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('has correct aria attributes', () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('aria-label');
      expect(button).toHaveAttribute('aria-pressed', 'false');
    });

    it('provides tooltip with instructions', async () => {
      render(<VoiceInputButton onTranscript={mockOnTranscript} />);

      const button = screen.getByRole('button');

      // Hover to show tooltip
      await userEvent.hover(button);

      // Tooltip should be shown (via TooltipProvider)
      await waitFor(() => {
        expect(screen.getByText('Clicca per parlare')).toBeInTheDocument();
      });
    });
  });
});
