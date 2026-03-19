/**
 * PublicLibraryClient — Placeholder for Task 10
 *
 * This component will display the public shared catalog browse experience.
 * Full implementation is in Task 10.
 */

'use client';

import { BookOpen } from 'lucide-react';

import { EmptyState } from '@/components/empty-state/EmptyState';

export default function PublicLibraryClient() {
  return (
    <div className="py-8" data-testid="public-library-placeholder">
      <EmptyState
        title="Catalogo Condiviso"
        description="La navigazione del catalogo condiviso è in arrivo. Torna presto!"
        icon={BookOpen}
        variant="noData"
      />
    </div>
  );
}
