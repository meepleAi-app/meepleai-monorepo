/**
 * Gamebook upload page — client shell (Sprint 1, Task 1.8)
 *
 * Reads `?gameId=<uuid>` from the query string and renders the upload UI.
 * Phase 2 will replace the query-param stub with a proper game picker.
 */

'use client';

import type { JSX } from 'react';

import { useSearchParams } from 'next/navigation';

import { useTranslation } from '@/hooks/useTranslation';

import { PhotoUploader } from './PhotoUploader';

/**
 * Sprint 1 stub: reads gameId from the URL query string.
 * Phase 2 will add a proper game picker component.
 */
export function GamebookUploadClient(): JSX.Element {
  const { t } = useTranslation();
  const searchParams = useSearchParams();
  const gameId = searchParams.get('gameId');

  if (!gameId) {
    return (
      <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-6 text-center">
        <p className="text-sm text-destructive">
          {/* Sprint 1 stub — Phase 2 replaces with game picker */}
          Missing <code className="font-mono">?gameId=</code> query parameter.
          <br />
          <span className="text-muted-foreground text-xs mt-1 block">
            Phase 2 will add a game picker.
          </span>
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">
          {t('gamebook.upload.title', 'Upload Game Manual Photos')}
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          {t(
            'gamebook.upload.subtitle',
            'Upload photos of your board game manual for AI-powered indexing'
          )}
        </p>
        <p className="mt-2 text-xs text-muted-foreground">
          {t('gamebook.upload.gameIdLabel', 'Game ID')}: <code className="font-mono">{gameId}</code>
        </p>
      </div>

      <PhotoUploader gameId={gameId} />
    </div>
  );
}
