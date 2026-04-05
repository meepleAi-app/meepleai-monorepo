/**
 * PrivateGamePdfSection — Client component for the private game detail page.
 *
 * Issue #3664: Private game PDF support — detail page PDF/chat flow.
 *
 * Manages local upload state:
 * - No PDF yet  → renders PdfUploadForm
 * - PDF uploaded → renders PdfProcessingStatus
 * - Status Completed → also renders "Chatta con l'agente" button
 */

'use client';

import { useState } from 'react';

import { MessageCircle } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { usePrivateGameKbStatus } from '@/hooks/queries/usePrivateGameKbStatus';

import { PdfProcessingStatus } from './PdfProcessingStatus';
import { PdfUploadForm } from './PdfUploadForm';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface PrivateGamePdfSectionProps {
  /** Private-game UUID */
  privateGameId: string;
  /**
   * Whether the game already has a PDF/KB entry.
   * Pass true when the server-side fetch already knows a PDF exists.
   */
  hasPdf?: boolean;
  /** Extra CSS classes for the wrapper */
  className?: string;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function PrivateGamePdfSection({
  privateGameId,
  hasPdf: initialHasPdf = false,
  className,
}: PrivateGamePdfSectionProps) {
  // Once the user uploads a PDF in this session, flip to true
  const [pdfUploaded, setPdfUploaded] = useState(false);

  const showStatus = initialHasPdf || pdfUploaded;

  // Only poll when we know there's a PDF to track
  const { data: kbStatus } = usePrivateGameKbStatus(showStatus ? privateGameId : null);

  const isCompleted = kbStatus?.status === 'Completed';

  const handleUploadComplete = () => {
    setPdfUploaded(true);
  };

  return (
    <section
      className={className}
      data-testid="private-game-pdf-section"
      aria-label="Sezione PDF e Knowledge Base"
    >
      <h2 className="text-lg font-semibold mb-4">Manuale di gioco</h2>

      {!showStatus ? (
        /* No PDF yet: show upload form */
        <PdfUploadForm
          privateGameId={privateGameId}
          onUploadComplete={handleUploadComplete}
          data-testid="pdf-upload-form-wrapper"
        />
      ) : (
        /* PDF exists: show processing status */
        <div className="space-y-4">
          <PdfProcessingStatus privateGameId={privateGameId} />

          {/* Chat button: enabled only when Completed */}
          {isCompleted ? (
            <Button asChild variant="default" data-testid="chat-button-enabled">
              <Link href={`/chat?gameId=${privateGameId}`}>
                <MessageCircle className="h-4 w-4 mr-2" aria-hidden="true" />
                Chatta con l&apos;agente
              </Link>
            </Button>
          ) : (
            <Button variant="default" disabled data-testid="chat-button-disabled">
              <MessageCircle className="h-4 w-4 mr-2" aria-hidden="true" />
              Chatta con l&apos;agente
            </Button>
          )}
        </div>
      )}
    </section>
  );
}
