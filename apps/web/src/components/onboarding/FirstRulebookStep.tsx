'use client';

/**
 * FirstRulebookStep — onboarding step 5.
 *
 * Issue #4138: replaces `FirstAgentStep`. There is now a single, system-wide
 * agent managed by the admin, so a new user has nothing to create. What a new
 * user *does* need is a rulebook: without an indexed document the agent has no
 * context to answer from, and the first question returns nothing useful.
 *
 * `FirstAgentStep` also never worked — it sent `agentType: 'RuleExpert'`, which
 * `AgentType.Parse` rejects, so every submit answered `400`. See the audit on
 * issue #4138.
 *
 * Conditional step: only shown when the user added a game in step 4.
 *
 * The copyright disclaimer gate mirrors `KnowledgeBaseStep` (the library
 * wizard's upload step): upload is unreachable until the disclaimer is
 * accepted, and acceptance is recorded per document. A new upload surface that
 * skipped it would be a regression of that posture, not just an omission.
 */

import { useCallback, useState } from 'react';

import { BookOpen } from 'lucide-react';
import { toast } from 'sonner';

import { AccessibleButton } from '@/components/accessible';
import { PdfUploadZone } from '@/components/library/add-game-sheet/steps/PdfUploadZone';
import { CopyrightDisclaimerModal } from '@/components/pdf/CopyrightDisclaimerModal';
import { trackPdfUploaded } from '@/lib/analytics/flywheel-events';
import { api } from '@/lib/api';

export interface FirstRulebookStepProps {
  gameId: string;
  gameName: string;
  onComplete: () => void;
  onSkip: () => void;
}

export function FirstRulebookStep({
  gameId,
  gameName,
  onComplete,
  onSkip,
}: FirstRulebookStepProps) {
  const [showDisclaimer, setShowDisclaimer] = useState(false);
  const [disclaimerAccepted, setDisclaimerAccepted] = useState(false);
  const [showUpload, setShowUpload] = useState(false);
  const [uploadedFileName, setUploadedFileName] = useState<string | null>(null);

  const handleUpload = useCallback(
    (file: File, onProgress: (percent: number) => void) =>
      api.pdf.uploadPdf(gameId, file, onProgress),
    [gameId]
  );

  const handleUploadComplete = useCallback(
    (documentId: string, fileName: string, fileSizeBytes: number) => {
      setUploadedFileName(fileName);
      setShowUpload(false);

      // Fire-and-forget, as in KnowledgeBaseStep: neither call gates the step,
      // and a failure here must not strand the user mid-onboarding.
      api.documents.acceptDisclaimer(documentId).catch(() => {});
      try {
        trackPdfUploaded({ gameId, fileSizeKb: Math.round(fileSizeBytes / 1024) });
      } catch {
        // analytics is never load-bearing
      }

      toast.success('Regolamento caricato: lo stiamo indicizzando.');
    },
    [gameId]
  );

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold text-foreground">
          Carica un regolamento
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Carica il PDF del regolamento di <strong>{gameName}</strong>: è ciò su cui l&apos;AI
          risponderà alle tue domande.
        </p>
      </div>

      {uploadedFileName ? (
        <div className="rounded-lg border border-border bg-muted/40 p-4" data-testid="upload-done">
          <div className="flex items-center gap-2">
            <BookOpen className="h-4 w-4 text-muted-foreground" />
            <p className="text-sm font-medium text-foreground">{uploadedFileName}</p>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            L&apos;indicizzazione continua in background: puoi proseguire.
          </p>
        </div>
      ) : showUpload ? (
        <PdfUploadZone onUpload={handleUpload} onUploadComplete={handleUploadComplete} />
      ) : (
        <AccessibleButton
          type="button"
          variant="primary"
          onClick={() => (disclaimerAccepted ? setShowUpload(true) : setShowDisclaimer(true))}
          data-testid="rulebook-upload-start"
        >
          Scegli un PDF
        </AccessibleButton>
      )}

      <div className="flex items-center gap-3 pt-2">
        {uploadedFileName && (
          <AccessibleButton
            type="button"
            variant="primary"
            className="flex-1"
            onClick={onComplete}
            data-testid="rulebook-continue"
          >
            Continua
          </AccessibleButton>
        )}
        <button
          type="button"
          onClick={onSkip}
          className="text-sm text-muted-foreground hover:text-foreground"
          data-testid="rulebook-skip"
        >
          {uploadedFileName ? 'Salta' : 'Lo faccio dopo'}
        </button>
      </div>

      <CopyrightDisclaimerModal
        open={showDisclaimer}
        onAccept={() => {
          setDisclaimerAccepted(true);
          setShowDisclaimer(false);
          setShowUpload(true);
        }}
        onCancel={() => setShowDisclaimer(false)}
      />
    </div>
  );
}
