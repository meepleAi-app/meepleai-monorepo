/**
 * Tests for IntlProvider component
 * Issue #1951: Add coverage for i18n provider
 */

import { render, screen } from '@testing-library/react';
import { IntlProvider } from '../IntlProvider';
import { useTranslation } from '@/hooks/useTranslation';

// Test component that uses translations
function TestComponent() {
  const { t, locale } = useTranslation();
  return (
    <div>
      <p data-testid="locale">{locale}</p>
      <p data-testid="translation">{t('common.loading')}</p>
    </div>
  );
}

describe('IntlProvider', () => {
  const originalEnv = process.env.NODE_ENV;

  afterEach(() => {
    process.env.NODE_ENV = originalEnv;
  });

  describe('Rendering', () => {
    it('renders children', () => {
      render(
        <IntlProvider>
          <div data-testid="child">Test Child</div>
        </IntlProvider>
      );

      expect(screen.getByTestId('child')).toBeInTheDocument();
      expect(screen.getByText('Test Child')).toBeInTheDocument();
    });

    it('provides default locale in test environment', () => {
      process.env.NODE_ENV = 'test';

      render(
        <IntlProvider>
          <TestComponent />
        </IntlProvider>
      );

      const locale = screen.getByTestId('locale');
      // DEFAULT_LOCALE is 'it' (Italian-first app)
      expect(locale.textContent).toBe('it');
    });

    it('uses provided locale prop', () => {
      render(
        <IntlProvider locale="it">
          <TestComponent />
        </IntlProvider>
      );

      const locale = screen.getByTestId('locale');
      expect(locale.textContent).toBe('it');
    });
  });

  describe('Translations', () => {
    it('provides translated messages to children', () => {
      render(
        <IntlProvider>
          <TestComponent />
        </IntlProvider>
      );

      const translation = screen.getByTestId('translation');
      expect(translation).toBeInTheDocument();
      expect(translation.textContent).toBeTruthy();
    });

    it('works with English locale', () => {
      render(
        <IntlProvider locale="en">
          <TestComponent />
        </IntlProvider>
      );

      const translation = screen.getByTestId('translation');
      expect(translation.textContent).toBe('Loading...');
    });

    it('works with Italian locale', () => {
      render(
        <IntlProvider locale="it">
          <TestComponent />
        </IntlProvider>
      );

      const locale = screen.getByTestId('locale');
      expect(locale.textContent).toBe('it');
    });
  });

  describe('Error Handling', () => {
    it('handles missing translations gracefully in test mode', () => {
      process.env.NODE_ENV = 'test';
      const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

      render(
        <IntlProvider>
          <TestComponent />
        </IntlProvider>
      );

      // Should render without crashing
      expect(screen.getByTestId('locale')).toBeInTheDocument();

      consoleSpy.mockRestore();
    });
  });
});
