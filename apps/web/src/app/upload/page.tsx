/**
 * Upload Page - Refactored Version
 * Issue #1089: Decompose monolithic upload page into modular components
 *
 * Reduction: 1563 lines → ~450 lines (71% reduction)
 * Components extracted: 7 (WizardSteps, GamePicker, PdfUploadForm, PdfTable, + 3 hooks)
 */

'use client';

import Link from 'next/link';
import { useCallback, useEffect, useState, useMemo } from 'react';
import { api, ApiError } from '@/lib/api';
import { categorizeError, type CategorizedError } from '@/lib/errorUtils';
import { ErrorDisplay } from '@/components/errors';
import { ProcessingProgress } from '@/components/progress';
import { MultiFileUpload } from '@/components/upload';
import { WizardSteps } from '@/components/wizard/WizardSteps';
import { GamePicker } from '@/components/game/GamePicker';
import { PdfUploadForm } from '@/components/pdf/PdfUploadForm';
import { PdfTable } from '@/components/pdf/PdfTable';
import { useWizard, type WizardStep } from '@/hooks/wizard/useWizard';
import { useGames } from '@/hooks/wizard/useGames';
import { usePdfs } from '@/hooks/wizard/usePdfs';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { LoadingButton } from '@/components/loading';

// Types
interface RuleAtom {
  id: string;
  text: string;
  section?: string | null;
  page?: string | null;
  line?: string | null;
}

interface RuleSpec {
  gameId: string;
  version: string;
  createdAt: string;
  rules: RuleAtom[];
}

const AUTHORIZED_ROLES = new Set(['admin', 'editor']);
const enableProcessingProgress = process.env.NEXT_PUBLIC_ENABLE_PROGRESS_UI === 'true';

interface UploadPageProps {
  autoUpload?: boolean;
  onUploadStart?: () => void;
  onUploadError?: () => void;
}

export default function UploadPage({
  autoUpload = true,
  onUploadStart,
  onUploadError
}: UploadPageProps = {}) {
  // Wizard state management
  const { state: wizardState, dispatch: wizardDispatch } = useWizard();

  // Data fetching hooks
  const { games, authUser, loading: loadingGames, createGame } = useGames();
  const [confirmedGameId, setConfirmedGameId] = useState<string | null>(null);
  const { pdfs, loading: loadingPdfs, error: pdfsError, refetch: refetchPdfs } = usePdfs(confirmedGameId);

  // Local state
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [ruleSpec, setRuleSpec] = useState<RuleSpec | null>(null);
  const [uploadError, setUploadError] = useState<CategorizedError | null>(null);
  const [showProcessingProgress, setShowProcessingProgress] = useState(false);
  const [parsing, setParsing] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [retryingPdfId, setRetryingPdfId] = useState<string | null>(null);
  const [autoAdvanceTriggered, setAutoAdvanceTriggered] = useState(false);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  // Authorization check (case-insensitive)
  const isUnauthorizedRole = authUser && !AUTHORIZED_ROLES.has(authUser.role.toLowerCase());

  // Initialize selected game
  useEffect(() => {
    if (games.length > 0 && !selectedGameId) {
      setSelectedGameId(games[0].id);
    }
  }, [games, selectedGameId]);

  // Wizard steps configuration (memoized for performance - PERF #1093)
  const wizardSteps = useMemo(() => [
    { id: 'upload', label: '1. Upload', description: 'Select PDF file' },
    { id: 'parse', label: '2. Parse', description: 'Extract rules' },
    { id: 'review', label: '3. Review', description: 'Edit rules' },
    { id: 'publish', label: '4. Publish', description: 'Finalize' }
  ], []);

  // Handlers
  const handleGameSelect = useCallback((gameId: string) => {
    setSelectedGameId(gameId);
    setConfirmedGameId(null);
  }, []);

  const handleGameCreate = useCallback(async (name: string) => {
    const newGame = await createGame(name);
    if (newGame) {
      setSelectedGameId(newGame.id);
      setConfirmedGameId(null);
    }
  }, [createGame]);

  const confirmSelectedGame = useCallback(() => {
    if (selectedGameId) {
      setConfirmedGameId(selectedGameId);
    }
  }, [selectedGameId]);

  const handleUploadSuccess = useCallback((documentId: string) => {
    wizardDispatch({ type: 'UPLOAD_SUCCESS', documentId });
    if (enableProcessingProgress) {
      setShowProcessingProgress(true);
    }
    setUploadError(null);
    setAutoAdvanceTriggered(false);
    setRuleSpec(null);
  }, [wizardDispatch]);

  const handleUploadError = useCallback((error: CategorizedError) => {
    setUploadError(error);
    onUploadError?.();
  }, [onUploadError]);

  const handleProcessingComplete = useCallback(() => {
    setShowProcessingProgress(false);
    wizardDispatch({ type: 'PROCESSING_UPDATE', status: 'completed' });
  }, [wizardDispatch]);

  const handleProcessingError = useCallback((error: string) => {
    setShowProcessingProgress(false);
    wizardDispatch({ type: 'PROCESSING_ERROR', error });
  }, [wizardDispatch]);

  const handleParse = useCallback(async () => {
    if (!confirmedGameId || !wizardState.documentId) return;

    setParsing(true);
    setAutoAdvanceTriggered(true);

    try {
      const fetchedRuleSpec = await api.get<RuleSpec>(`/api/v1/games/${confirmedGameId}/rulespec`);

      if (!fetchedRuleSpec) {
        wizardDispatch({ type: 'ERROR', error: 'Unable to load RuleSpec' });
        return;
      }

      setRuleSpec(fetchedRuleSpec);
      wizardDispatch({ type: 'PARSING_COMPLETE' });
      await refetchPdfs();
    } catch (error) {
      const errorMessage = error instanceof ApiError
        ? `Parse failed: ${error.message}`
        : `Parse failed: ${error instanceof Error ? error.message : 'Unknown error'}`;
      wizardDispatch({ type: 'ERROR', error: errorMessage });
    } finally {
      setParsing(false);
    }
  }, [confirmedGameId, wizardState.documentId, wizardDispatch, refetchPdfs]);

  // Auto-advance when processing completes
  useEffect(() => {
    if (
      wizardState.currentStep === 'parse' &&
      wizardState.documentId &&
      wizardState.processingStatus === 'completed' &&
      !autoAdvanceTriggered
    ) {
      void handleParse();
    }
  }, [autoAdvanceTriggered, wizardState.currentStep, wizardState.documentId, wizardState.processingStatus, handleParse]);

  const handlePublish = useCallback(async () => {
    if (!ruleSpec || !confirmedGameId) return;

    setPublishing(true);

    try {
      const response = await fetch(`${API_BASE}/api/v1/games/${confirmedGameId}/rulespec`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(ruleSpec)
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        wizardDispatch({ type: 'ERROR', error: `Publish failed: ${error.error ?? response.statusText}` });
        return;
      }

      wizardDispatch({ type: 'PUBLISH_COMPLETE' });
    } catch (error) {
      wizardDispatch({
        type: 'ERROR',
        error: `Publish failed: ${error instanceof Error ? error.message : 'Unknown error'}`
      });
    } finally {
      setPublishing(false);
    }
  }, [ruleSpec, confirmedGameId, API_BASE, wizardDispatch]);

  const updateRuleAtom = useCallback((index: number, field: keyof RuleAtom, value: string) => {
    if (!ruleSpec) return;
    const updatedRules = [...ruleSpec.rules];
    updatedRules[index] = { ...updatedRules[index], [field]: value };
    setRuleSpec({ ...ruleSpec, rules: updatedRules });
  }, [ruleSpec]);

  const deleteRuleAtom = useCallback((index: number) => {
    if (!ruleSpec) return;
    const updatedRules = ruleSpec.rules.filter((_, i) => i !== index);
    setRuleSpec({ ...ruleSpec, rules: updatedRules });
  }, [ruleSpec]);

  const addRuleAtom = useCallback(() => {
    if (!ruleSpec) return;
    const newRule: RuleAtom = {
      id: `new-${Date.now()}`,
      text: '',
      section: null,
      page: null,
      line: null
    };
    setRuleSpec({ ...ruleSpec, rules: [...ruleSpec.rules, newRule] });
  }, [ruleSpec]);

  const resetWizard = useCallback(() => {
    wizardDispatch({ type: 'RESET' });
    setRuleSpec(null);
    setUploadError(null);
    setShowProcessingProgress(false);
    setAutoAdvanceTriggered(false);
  }, [wizardDispatch]);

  const handleRetryParsing = useCallback(async (pdf: any) => {
    setRetryingPdfId(pdf.id);
    try {
      // Retry logic would go here
      await new Promise(resolve => setTimeout(resolve, 1000));
      await refetchPdfs();
    } finally {
      setRetryingPdfId(null);
    }
  }, [refetchPdfs]);

  const handleOpenLog = useCallback((pdf: any) => {
    if (pdf.logUrl) {
      window.open(pdf.logUrl, '_blank');
    }
  }, []);

  const confirmedGame = games.find((g) => g.id === confirmedGameId);

  // Unauthorized state
  if (isUnauthorizedRole) {
    return (
      <div className="p-10 max-w-3xl mx-auto">
        <Link href="/" className="text-primary hover:underline mb-5 inline-block">
          ← Back to Home
        </Link>
        <Card className="p-6">
          <h1 className="text-2xl font-bold mb-4">Unauthorized Access</h1>
          <p className="text-muted-foreground">
            You need admin or editor privileges to access this page.
          </p>
          <p className="mt-2 text-sm text-muted-foreground">
            Current role: <strong>{authUser?.role}</strong>
          </p>
        </Card>
      </div>
    );
  }

  return (
    <div className="p-10 max-w-4xl mx-auto">
      <div className="mb-5">
        <Link href="/" className="text-primary hover:underline">
          ← Back to Home
        </Link>
      </div>

      <main id="main-content">
        <h1 className="text-3xl font-bold mb-2">PDF Import Wizard</h1>
        <p className="text-muted-foreground mb-8">Upload, parse, review, and publish game rules</p>

        <WizardSteps steps={wizardSteps} currentStep={wizardState.currentStep} />

        {uploadError && (
          <ErrorDisplay
            error={uploadError}
            onRetry={uploadError.canRetry ? () => setUploadError(null) : undefined}
            onDismiss={() => setUploadError(null)}
            showTechnicalDetails={true}
          />
        )}

        {wizardState.error && !uploadError && (
          <Card className="p-4 mb-6 bg-destructive/10 border-destructive">
            <p className="text-destructive">{wizardState.error}</p>
          </Card>
        )}

        {/* Step 1: Upload */}
        {wizardState.currentStep === 'upload' && (
          <div className="space-y-6">
            <GamePicker
              games={games}
              selectedGameId={selectedGameId}
              onGameSelect={handleGameSelect}
              onGameCreate={handleGameCreate}
              loading={loadingGames}
            />

            {selectedGameId && !confirmedGameId && (
              <Button onClick={confirmSelectedGame} className="w-full">
                Confirm Game Selection
              </Button>
            )}

            {confirmedGameId && confirmedGame && (
              <>
                <PdfUploadForm
                  gameId={confirmedGameId}
                  gameName={confirmedGame.name}
                  onUploadSuccess={handleUploadSuccess}
                  onUploadError={handleUploadError}
                  onUploadStart={onUploadStart}
                />

                <div className="mt-8">
                  <MultiFileUpload
                    gameId={confirmedGameId}
                    gameName={confirmedGame.name}
                    language="en"
                    autoUpload={autoUpload}
                    onUploadComplete={refetchPdfs}
                    onUploadStart={onUploadStart}
                    onUploadError={onUploadError}
                  />
                </div>

                <PdfTable
                  pdfs={pdfs}
                  loading={loadingPdfs}
                  error={pdfsError}
                  retryingPdfId={retryingPdfId}
                  onRetryParsing={handleRetryParsing}
                  onOpenLog={handleOpenLog}
                />
              </>
            )}
          </div>
        )}

        {/* Step 2: Parse */}
        {wizardState.currentStep === 'parse' && (
          <Card className="p-6">
            <h2 className="text-2xl font-semibold mb-4">Step 2: Parse PDF</h2>
            <p className="text-sm text-muted-foreground mb-4">
              Document ID: <strong>{wizardState.documentId}</strong>
            </p>

            {showProcessingProgress && wizardState.documentId && (
              <div className="mb-6">
                <ProcessingProgress
                  pdfId={wizardState.documentId}
                  onComplete={handleProcessingComplete}
                  onError={handleProcessingError}
                />
              </div>
            )}

            <div className="flex gap-3">
              <LoadingButton
                onClick={handleParse}
                isLoading={parsing}
                disabled={parsing || wizardState.processingStatus !== 'completed'}
              >
                {parsing ? 'Loading rules…' : 'Parse PDF'}
              </LoadingButton>
              <Button variant="secondary" onClick={resetWizard}>
                Start Over
              </Button>
            </div>
          </Card>
        )}

        {/* Step 3: Review */}
        {wizardState.currentStep === 'review' && ruleSpec && (
          <Card className="p-6">
            <h2 className="text-2xl font-semibold mb-4">Step 3: Review & Edit Rules</h2>

            <div className="bg-muted p-4 rounded-md mb-6 space-y-1 text-sm">
              <p><strong>Game ID:</strong> {ruleSpec.gameId}</p>
              <p><strong>Version:</strong> {ruleSpec.version}</p>
              <p><strong>Total Rules:</strong> {ruleSpec.rules?.length || 0}</p>
            </div>

            <div className="space-y-4 mb-6 max-h-96 overflow-y-auto">
              {(ruleSpec.rules || []).map((rule, index) => (
                <Card key={rule.id ?? index} className="p-4">
                  <div className="flex justify-between items-center mb-3">
                    <h4 className="font-semibold">Rule {index + 1}</h4>
                    <Button
                      variant="destructive"
                      size="sm"
                      onClick={() => deleteRuleAtom(index)}
                    >
                      Delete
                    </Button>
                  </div>
                  <div className="space-y-3">
                    <div>
                      <label className="block text-sm font-medium mb-1">Text</label>
                      <textarea
                        value={rule.text}
                        onChange={(e) => updateRuleAtom(index, 'text', e.target.value)}
                        className="w-full p-2 border rounded-md min-h-[60px] text-sm"
                      />
                    </div>
                    <div className="grid grid-cols-3 gap-3">
                      <div>
                        <label className="block text-sm font-medium mb-1">Section</label>
                        <input
                          value={rule.section ?? ''}
                          onChange={(e) => updateRuleAtom(index, 'section', e.target.value)}
                          className="w-full p-2 border rounded-md text-sm"
                        />
                      </div>
                      <div>
                        <label className="block text-sm font-medium mb-1">Page</label>
                        <input
                          value={rule.page ?? ''}
                          onChange={(e) => updateRuleAtom(index, 'page', e.target.value)}
                          className="w-full p-2 border rounded-md text-sm"
                        />
                      </div>
                      <div>
                        <label className="block text-sm font-medium mb-1">Line</label>
                        <input
                          value={rule.line ?? ''}
                          onChange={(e) => updateRuleAtom(index, 'line', e.target.value)}
                          className="w-full p-2 border rounded-md text-sm"
                        />
                      </div>
                    </div>
                  </div>
                </Card>
              ))}
            </div>

            <Button onClick={addRuleAtom} variant="outline" className="mb-6">
              + Add Rule
            </Button>

            <div className="flex gap-3">
              <LoadingButton onClick={handlePublish} isLoading={publishing}>
                {publishing ? 'Publishing…' : 'Publish RuleSpec'}
              </LoadingButton>
              <Button
                variant="secondary"
                onClick={() => wizardDispatch({ type: 'SET_STEP', step: 'parse' })}
              >
                ← Back
              </Button>
              <Button variant="destructive" onClick={resetWizard}>
                Cancel
              </Button>
            </div>
          </Card>
        )}

        {/* Step 4: Success */}
        {wizardState.currentStep === 'publish' && (
          <Card className="p-6 text-center">
            <h2 className="text-2xl font-semibold mb-4">Step 4: Published Successfully! ✅</h2>
            <p className="text-muted-foreground mb-6">
              Your RuleSpec for <strong>{ruleSpec?.gameId ?? confirmedGameId ?? 'unknown game'}</strong> has been published successfully!
            </p>
            <div className="flex gap-3 justify-center">
              <Button onClick={resetWizard}>
                Import Another PDF
              </Button>
              <Button asChild variant="secondary">
                <Link href={`/editor?gameId=${ruleSpec?.gameId ?? confirmedGameId ?? ''}`}>
                  Edit in RuleSpec Editor
                </Link>
              </Button>
            </div>
          </Card>
        )}
      </main>
    </div>
  );
}
