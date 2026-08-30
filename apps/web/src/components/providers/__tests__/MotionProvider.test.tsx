/**
 * MotionProvider.test.tsx — gate comportamentale dell'issue #3898.
 *
 * framer-motion 13 ha rimosso l'integrazione implicita con
 * `@emotion/is-prop-valid`: senza un `<MotionConfig isValidProp>` esplicito, le
 * prop non-DOM passate a un `motion.*` vengono inoltrate all'elemento.
 *
 * Questo file e' l'UNICO posto in cui la libreria vera viene caricata: il resto
 * della suite usa il mock globale di `vitest.setup.tsx`, che rimuove da se' le
 * prop di animazione e quindi non potrebbe vedere la regressione.
 *
 * Il primo test documenta il comportamento senza provider. Serve a rendere il
 * secondo non vacuo: se un giorno framer-motion tornasse a filtrare da solo,
 * il primo test diventa rosso e ci dice che il provider non serve piu'.
 *
 * Refs: https://github.com/meepleAi-app/meepleai-monorepo/issues/3898
 */

import { describe, it, expect, vi } from 'vitest';

vi.unmock('framer-motion');

import { render, screen } from '@testing-library/react';
import { motion } from 'framer-motion';

import { MotionProvider } from '../MotionProvider';

// Una prop che non esiste nel DOM e che framer-motion non consuma.
const propNonDom = { customProp: 'boom' } as unknown as Record<string, unknown>;

describe('MotionProvider (#3898)', () => {
  it('senza provider, framer-motion 13 inoltra la prop non-DOM all elemento', () => {
    render(<motion.div data-testid="senza" {...propNonDom} />);

    expect(screen.getByTestId('senza')).toHaveAttribute('customprop');
  });

  it('dentro il provider, la prop non-DOM non arriva al DOM', () => {
    render(
      <MotionProvider>
        <motion.div data-testid="con" {...propNonDom} />
      </MotionProvider>
    );

    expect(screen.getByTestId('con')).not.toHaveAttribute('customprop');
  });

  it('le prop DOM legittime continuano a passare', () => {
    render(
      <MotionProvider>
        <motion.div data-testid="con" id="reale" role="status" />
      </MotionProvider>
    );

    const el = screen.getByTestId('con');
    expect(el).toHaveAttribute('id', 'reale');
    expect(el).toHaveAttribute('role', 'status');
  });
});
