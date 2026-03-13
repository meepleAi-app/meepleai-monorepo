/**
 * LanguageOverrideSelector Component Tests (E5-2)
 * Tests language override UI for PDF documents.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { LanguageOverrideSelector } from '../LanguageOverrideSelector';

// Mock fetch globally
const mockFetch = vi.fn();
global.fetch = mockFetch;

describe('LanguageOverrideSelector', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockResolvedValue({ ok: true });
  });

  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders detected language with confidence percentage', () => {
      render(
        <LanguageOverrideSelector pdfId="test-id" detectedLanguage="de" languageConfidence={0.94} />
      );

      expect(screen.getByTestId('detected-language-label')).toHaveTextContent('Detected: DE (94%)');
    });

    it('renders detected language without confidence when not provided', () => {
      render(<LanguageOverrideSelector pdfId="test-id" detectedLanguage="en" />);

      expect(screen.getByTestId('detected-language-label')).toHaveTextContent('Detected: EN');
    });

    it('shows "No language detected" when detectedLanguage is null', () => {
      render(<LanguageOverrideSelector pdfId="test-id" detectedLanguage={null} />);

      expect(screen.getByTestId('detected-language-label')).toHaveTextContent(
        'No language detected'
      );
    });

    it('shows "No language detected" when detectedLanguage is undefined', () => {
      render(<LanguageOverrideSelector pdfId="test-id" />);

      expect(screen.getByTestId('detected-language-label')).toHaveTextContent(
        'No language detected'
      );
    });

    it('renders the select trigger', () => {
      render(<LanguageOverrideSelector pdfId="test-id" detectedLanguage="en" />);

      expect(screen.getByTestId('language-select-trigger')).toBeInTheDocument();
    });

    it('renders the component container', () => {
      render(<LanguageOverrideSelector pdfId="test-id" detectedLanguage="en" />);

      expect(screen.getByTestId('language-override-selector')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Dropdown Interaction Tests
  // ============================================================================

  describe('Dropdown', () => {
    it('opens dropdown with language options when clicked', async () => {
      const user = userEvent.setup();

      render(<LanguageOverrideSelector pdfId="test-id" detectedLanguage="en" />);

      await user.click(screen.getByTestId('language-select-trigger'));

      // Check some language options are visible
      expect(screen.getByTestId('language-option-en')).toBeInTheDocument();
      expect(screen.getByTestId('language-option-de')).toBeInTheDocument();
      expect(screen.getByTestId('language-option-it')).toBeInTheDocument();
      expect(screen.getByTestId('language-option-fr')).toBeInTheDocument();
      expect(screen.getByTestId('language-option-es')).toBeInTheDocument();
      expect(screen.getByTestId('language-option-ja')).toBeInTheDocument();
    });

    it('shows "Reset to detected" option when override is set', async () => {
      const user = userEvent.setup();

      render(
        <LanguageOverrideSelector pdfId="test-id" detectedLanguage="en" currentOverride="de" />
      );

      await user.click(screen.getByTestId('language-select-trigger'));

      expect(screen.getByTestId('reset-option')).toBeInTheDocument();
      expect(screen.getByText('Reset to detected')).toBeInTheDocument();
    });

    it('does not show "Reset to detected" option when no override is set', async () => {
      const user = userEvent.setup();

      render(
        <LanguageOverrideSelector pdfId="test-id" detectedLanguage="en" currentOverride={null} />
      );

      await user.click(screen.getByTestId('language-select-trigger'));

      expect(screen.queryByTestId('reset-option')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Callback Tests
  // ============================================================================

  describe('Callbacks', () => {
    it('calls onOverrideChange with language code when selecting a language', async () => {
      const user = userEvent.setup();
      const onOverrideChange = vi.fn();

      render(
        <LanguageOverrideSelector
          pdfId="test-pdf-123"
          detectedLanguage="en"
          onOverrideChange={onOverrideChange}
        />
      );

      await user.click(screen.getByTestId('language-select-trigger'));
      await user.click(screen.getByTestId('language-option-de'));

      expect(mockFetch).toHaveBeenCalledWith(
        '/api/v1/pdfs/test-pdf-123/language',
        expect.objectContaining({
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ languageCode: 'de' }),
        })
      );
      expect(onOverrideChange).toHaveBeenCalledWith('de');
    });

    it('calls onOverrideChange with null when resetting override', async () => {
      const user = userEvent.setup();
      const onOverrideChange = vi.fn();

      render(
        <LanguageOverrideSelector
          pdfId="test-pdf-123"
          detectedLanguage="en"
          currentOverride="de"
          onOverrideChange={onOverrideChange}
        />
      );

      await user.click(screen.getByTestId('language-select-trigger'));
      await user.click(screen.getByTestId('reset-option'));

      expect(mockFetch).toHaveBeenCalledWith(
        '/api/v1/pdfs/test-pdf-123/language',
        expect.objectContaining({
          body: JSON.stringify({ languageCode: null }),
        })
      );
      expect(onOverrideChange).toHaveBeenCalledWith(null);
    });

    it('does not call onOverrideChange when API call fails', async () => {
      const user = userEvent.setup();
      const onOverrideChange = vi.fn();
      mockFetch.mockResolvedValueOnce({ ok: false, status: 500 });

      render(
        <LanguageOverrideSelector
          pdfId="test-pdf-123"
          detectedLanguage="en"
          onOverrideChange={onOverrideChange}
        />
      );

      await user.click(screen.getByTestId('language-select-trigger'));
      await user.click(screen.getByTestId('language-option-fr'));

      expect(onOverrideChange).not.toHaveBeenCalled();
    });
  });

  // ============================================================================
  // Confidence Formatting Tests
  // ============================================================================

  describe('Confidence formatting', () => {
    it('rounds confidence to nearest integer', () => {
      render(
        <LanguageOverrideSelector
          pdfId="test-id"
          detectedLanguage="de"
          languageConfidence={0.876}
        />
      );

      expect(screen.getByTestId('detected-language-label')).toHaveTextContent('Detected: DE (88%)');
    });

    it('handles zero confidence', () => {
      render(
        <LanguageOverrideSelector pdfId="test-id" detectedLanguage="en" languageConfidence={0} />
      );

      expect(screen.getByTestId('detected-language-label')).toHaveTextContent('Detected: EN (0%)');
    });

    it('handles full confidence', () => {
      render(
        <LanguageOverrideSelector pdfId="test-id" detectedLanguage="en" languageConfidence={1.0} />
      );

      expect(screen.getByTestId('detected-language-label')).toHaveTextContent(
        'Detected: EN (100%)'
      );
    });
  });
});
