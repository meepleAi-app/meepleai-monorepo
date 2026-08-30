'use client';

import type { ReactNode } from 'react';

import isPropValid from '@emotion/is-prop-valid';
import { MotionConfig } from 'framer-motion';

/**
 * MotionProvider — ripristina il filtro delle prop non-DOM di framer-motion.
 *
 * Fino alla v12, framer-motion rilevava `@emotion/is-prop-valid` se installato e
 * lo usava per non inoltrare al DOM le prop che il DOM non conosce. La v13 ha
 * rimosso quell'integrazione implicita:
 *
 *   > Removed optional `@emotion/is-prop-valid` dependency in favour of explicit
 *   > `<MotionConfig isValidProp={isPropValid}>`.
 *
 * Il bump e' arrivato con #3724 e ha spento il filtro senza che nessuno lo
 * decidesse: `@emotion/is-prop-valid` era — ed e' — una `dependencies` di
 * apps/web che nessun sorgente importa, presente solo perche' framer-motion la
 * pescasse. Con 86 file e 564 occorrenze di `motion.*`, verificare a mano che
 * nessuno passi prop non-DOM costa giorni; questo provider costa una riga e
 * riporta il comportamento a prima della v13.
 *
 * Il comportamento e' coperto da `__tests__/MotionProvider.test.tsx`, l'unico
 * test della suite che carica framer-motion **vera**: ovunque altro il mock
 * globale di `vitest.setup.tsx` rimuove da se' le prop di animazione e non
 * potrebbe vedere la differenza.
 *
 * Refs: https://github.com/meepleAi-app/meepleai-monorepo/issues/3898
 */
export function MotionProvider({ children }: { children: ReactNode }) {
  return <MotionConfig isValidProp={isPropValid}>{children}</MotionConfig>;
}
