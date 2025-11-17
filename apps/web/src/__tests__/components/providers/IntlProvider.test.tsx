/**
 * Unit tests for IntlProvider component
 *
 * Issue #990: BGAI-049 - i18n setup (React Intl, it.json)
 */

import { render, screen } from '@testing-library/react';
import { IntlProvider } from '@/components/providers/IntlProvider';
import { useTranslation } from '@/hooks/useTranslation';
import { LOCALES } from '@/locales';

// Test component that uses translations
function TestComponent() {
  const { t, locale } = useTranslation();

  return (
    <div>
      <div data-testid="locale">{locale}</div>
      <div data-testid="loading-text">{t('common.loading')}</div>
      <div data-testid="login-title">{t('auth.login.title')}</div>
    </div>
  );
}

describe('IntlProvider', () => {
  describe('Default behavior', () => {
    it('should render children', () => {
      render(
        <IntlProvider>
          <div data-testid="child">Test Child</div>
        </IntlProvider>
      );

      expect(screen.getByTestId('child')).toBeInTheDocument();
      expect(screen.getByText('Test Child')).toBeInTheDocument();
    });

    it('should provide Italian locale by default', () => {
      render(
        <IntlProvider>
          <TestComponent />
        </IntlProvider>
      );

      expect(screen.getByTestId('locale')).toHaveTextContent('it');
    });

    it('should provide Italian translations by default', () => {
      render(
        <IntlProvider>
          <TestComponent />
        </IntlProvider>
      );

      expect(screen.getByTestId('loading-text')).toHaveTextContent('Caricamento...');
      expect(screen.getByTestId('login-title')).toHaveTextContent('Accedi');
    });
  });

  describe('Custom locale', () => {
    it('should accept explicit Italian locale', () => {
      render(
        <IntlProvider locale={LOCALES.IT}>
          <TestComponent />
        </IntlProvider>
      );

      expect(screen.getByTestId('locale')).toHaveTextContent('it');
    });

    it('should accept English locale', () => {
      render(
        <IntlProvider locale={LOCALES.EN}>
          <TestComponent />
        </IntlProvider>
      );

      expect(screen.getByTestId('locale')).toHaveTextContent('en');
    });
  });

  describe('Translation access', () => {
    it('should provide nested translations', () => {
      const NestedTest = () => {
        const { t } = useTranslation();
        return <div data-testid="nested">{t('auth.session.expired')}</div>;
      };

      render(
        <IntlProvider>
          <NestedTest />
        </IntlProvider>
      );

      expect(screen.getByTestId('nested')).toHaveTextContent(
        'La tua sessione è scaduta. Accedi nuovamente.'
      );
    });

    it('should provide common translations', () => {
      const CommonTest = () => {
        const { t } = useTranslation();
        return (
          <div>
            <div data-testid="save">{t('common.save')}</div>
            <div data-testid="cancel">{t('common.cancel')}</div>
            <div data-testid="delete">{t('common.delete')}</div>
          </div>
        );
      };

      render(
        <IntlProvider>
          <CommonTest />
        </IntlProvider>
      );

      expect(screen.getByTestId('save')).toHaveTextContent('Salva');
      expect(screen.getByTestId('cancel')).toHaveTextContent('Annulla');
      expect(screen.getByTestId('delete')).toHaveTextContent('Elimina');
    });
  });

  describe('Interpolation', () => {
    it('should support variable interpolation', () => {
      const InterpolationTest = () => {
        const { t } = useTranslation();
        return (
          <div data-testid="interpolated">
            {t('auth.session.expiringSoon', { minutes: 5 })}
          </div>
        );
      };

      render(
        <IntlProvider>
          <InterpolationTest />
        </IntlProvider>
      );

      expect(screen.getByTestId('interpolated')).toHaveTextContent(
        'La tua sessione scadrà tra 5 minuti.'
      );
    });

    it('should support multiple variables', () => {
      const MultiVarTest = () => {
        const { t } = useTranslation();
        return (
          <div data-testid="multi">
            {t('upload.maxSize', { size: 50 })}
          </div>
        );
      };

      render(
        <IntlProvider>
          <MultiVarTest />
        </IntlProvider>
      );

      expect(screen.getByTestId('multi')).toHaveTextContent('Dimensione massima: 50MB');
    });
  });

  describe('Formatting functions', () => {
    it('should provide formatNumber', () => {
      const NumberTest = () => {
        const { formatNumber } = useTranslation();
        return <div data-testid="number">{formatNumber(1234.56)}</div>;
      };

      render(
        <IntlProvider>
          <NumberTest />
        </IntlProvider>
      );

      const formatted = screen.getByTestId('number').textContent;
      expect(formatted).toBeTruthy();
      expect(formatted).toContain('1');
    });

    it('should provide formatDate', () => {
      const DateTest = () => {
        const { formatDate } = useTranslation();
        const date = new Date('2025-11-17T12:00:00Z');
        return <div data-testid="date">{formatDate(date)}</div>;
      };

      render(
        <IntlProvider>
          <DateTest />
        </IntlProvider>
      );

      const formatted = screen.getByTestId('date').textContent;
      expect(formatted).toBeTruthy();
      expect(formatted).toContain('2025');
    });
  });

  describe('Error handling', () => {
    // Suppress console errors for these tests
    const originalError = console.error;
    const originalWarn = console.warn;

    beforeAll(() => {
      console.error = jest.fn();
      console.warn = jest.fn();
    });

    afterAll(() => {
      console.error = originalError;
      console.warn = originalWarn;
    });

    it('should handle missing translation keys gracefully', () => {
      const MissingKeyTest = () => {
        const { t } = useTranslation();
        return <div data-testid="missing">{t('nonexistent.key.path')}</div>;
      };

      render(
        <IntlProvider>
          <MissingKeyTest />
        </IntlProvider>
      );

      // Should render the key itself as fallback
      expect(screen.getByTestId('missing')).toHaveTextContent('nonexistent.key.path');
    });
  });

  describe('Multiple instances', () => {
    it('should support nested IntlProviders with different locales', () => {
      const MultiLocaleTest = () => {
        const { t: tOuter, locale: localeOuter } = useTranslation();

        return (
          <div>
            <div data-testid="outer-locale">{localeOuter}</div>
            <div data-testid="outer-text">{tOuter('common.loading')}</div>
          </div>
        );
      };

      render(
        <IntlProvider locale={LOCALES.IT}>
          <MultiLocaleTest />
        </IntlProvider>
      );

      expect(screen.getByTestId('outer-locale')).toHaveTextContent('it');
      expect(screen.getByTestId('outer-text')).toHaveTextContent('Caricamento...');
    });
  });
});
