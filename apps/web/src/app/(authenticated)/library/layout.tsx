/**
 * Library Section Layout
 * Carte in Mano — NavConfig removed, navigation handled by UserShell card system.
 */

import { type ReactNode } from 'react';

import { ContextBarRegistrar } from '@/components/layout/ContextBar';
import { LibraryContextBar } from '@/components/library/LibraryContextBar';

export default function LibraryLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <ContextBarRegistrar>
        <LibraryContextBar />
      </ContextBarRegistrar>
      {children}
    </>
  );
}
