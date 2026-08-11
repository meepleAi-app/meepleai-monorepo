'use client';

/**
 * CoverPagePicker — proposes a PDF page as a SharedGame cover (Task 8, Game
 * Cover-da-PDF plan).
 *
 * Lets the user pick a page number from an already-uploaded PDF, preview it,
 * and submit it as a cover proposal via `POST
 * /api/v1/games/{gameId}/cover/propose-from-pdf`. The endpoint materializes a
 * pending cover image and creates a Pending CoverChange share request for
 * admin approval — this component does not apply the cover directly.
 *
 * If the backend render step (SmolDocling) fails, it responds 503 with
 * `{ error: "cover_render_unavailable" }`. That failure is surfaced as a
 * distinct, non-blocking message (retryable) rather than a generic error.
 */

import { useState } from 'react';
import type { JSX } from 'react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';

export interface CoverPagePickerProps {
  gameId: string;
  pdfDocumentId: string;
  onProposed: (shareRequestId: string) => void;
}

const COVER_RENDER_UNAVAILABLE_MESSAGE =
  'Anteprima cover non disponibile al momento, riprova più tardi.';
const GENERIC_ERROR_MESSAGE = 'Impossibile proporre la cover in questo momento.';

/** Reads the machine-readable error code off an ApiError-shaped error, if present. */
function getErrorCode(error: unknown): string | undefined {
  if (error && typeof error === 'object' && 'code' in error) {
    const code = (error as { code?: unknown }).code;
    return typeof code === 'string' ? code : undefined;
  }
  return undefined;
}

export function CoverPagePicker({
  gameId,
  pdfDocumentId,
  onProposed,
}: CoverPagePickerProps): JSX.Element {
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function propose(): Promise<void> {
    setBusy(true);
    setError(null);
    try {
      const { shareRequestId } = await api.sharedGames.proposeCoverFromPdf(
        gameId,
        pdfDocumentId,
        page
      );
      onProposed(shareRequestId);
    } catch (err) {
      // 503 cover_render_unavailable is a non-blocking, retryable failure
      // (SmolDocling render hiccup) — distinct from a generic server error.
      if (getErrorCode(err) === 'cover_render_unavailable') {
        setError(COVER_RENDER_UNAVAILABLE_MESSAGE);
      } else {
        setError(GENERIC_ERROR_MESSAGE);
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-3">
      {pdfDocumentId && (
        // Dynamic API-served page preview (not a static/optimizable asset);
        // matches CoverImagePicker.tsx's PDF-page-preview pattern.
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={api.sharedGames.getPdfPageImageUrl(pdfDocumentId, page)}
          alt={`Anteprima pagina ${page}`}
          className="max-h-48 rounded border border-border object-contain"
        />
      )}

      <div className="flex items-end gap-2">
        <div className="flex-1 space-y-1">
          <Label htmlFor="cover-page-picker-page">Pagina</Label>
          <Input
            id="cover-page-picker-page"
            type="number"
            min={1}
            value={page}
            onChange={e => setPage(Number(e.target.value))}
            className="w-24"
          />
        </div>
        <Button type="button" size="sm" disabled={busy} onClick={propose}>
          {busy ? 'Invio...' : 'Proponi cover'}
        </Button>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription className="text-sm">{error}</AlertDescription>
        </Alert>
      )}
    </div>
  );
}
