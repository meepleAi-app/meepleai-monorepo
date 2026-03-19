/* eslint-disable security/detect-object-injection -- Safe spec data object access */
/**
 * Editor Page - Client Component
 *
 * Issue #1611 Phase 2: SSR Auth Protection Migration
 * Issue #2055: Collaborative editing with optimistic locks
 *
 * This Client Component handles all interactive logic:
 * - RuleSpec editing (rich text + JSON modes)
 * - Auto-save functionality
 * - Undo/Redo history
 * - Validation and publishing
 * - Editor lock management (Issue #2055)
 * - Conflict detection and resolution (Issue #2055)
 *
 * Server Component (page.tsx) handles:
 * - Server-side authentication check
 * - Role authorization
 * - User prop provisioning
 */

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import {
  RichTextEditor,
  ViewModeToggle,
  ConflictResolutionModal,
  PresenceIndicator,
} from '@/components/editor';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useDebounce } from '@/hooks/useDebounce';
import { api } from '@/lib/api';
import type { RuleSpec } from '@/lib/api/schemas';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';
import { sanitizeHtml } from '@/lib/security/sanitize';
import { cn } from '@/lib/utils';
import { getErrorMessage } from '@/lib/utils/errorHandler';
import {
  useRuleSpecLockStore,
  selectHasLock,
  selectLockStatus,
  selectAcquisitionStatus,
  selectLockError,
  selectShowConflictModal,
  selectConflict,
  selectCurrentETag,
  type LockApiClient,
} from '@/stores/RuleSpecLockStore';

type HistoryEntry = {
  content: string;
  timestamp: number;
};

type ViewMode = 'rich' | 'json';

export function EditorClient() {
  const { user } = useAuthUser();
  const _router = useRouter();
  const searchParams = useSearchParams();
  const gameId = searchParams?.get('gameId') ?? null;

  const [ruleSpec, setRuleSpec] = useState<RuleSpec | null>(null);
  const [jsonContent, setJsonContent] = useState<string>('');
  const [richContent, setRichContent] = useState<string>('');
  const [viewMode, setViewMode] = useState<ViewMode>('rich');
  const [isValid, setIsValid] = useState<boolean>(true);
  const [validationError, setValidationError] = useState<string>('');
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [errorMessage, setErrorMessage] = useState<string>('');
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState<boolean>(false);

  // Undo/Redo state
  const [history, setHistory] = useState<HistoryEntry[]>([]);
  const [historyIndex, setHistoryIndex] = useState<number>(-1);

  // Issue #2055: Lock state from Zustand store
  const hasLock = useRuleSpecLockStore(selectHasLock);
  const lockStatus = useRuleSpecLockStore(selectLockStatus);
  const acquisitionStatus = useRuleSpecLockStore(selectAcquisitionStatus);
  const lockError = useRuleSpecLockStore(selectLockError);
  const showConflictModal = useRuleSpecLockStore(selectShowConflictModal);
  const conflict = useRuleSpecLockStore(selectConflict);
  const currentETag = useRuleSpecLockStore(selectCurrentETag);
  const {
    initializeLock,
    releaseLock,
    setCurrentETag,
    handleConflict,
    resolveConflict,
    closeConflictModal,
    cleanup: cleanupLock,
  } = useRuleSpecLockStore();

  // Issue #2055: Lock API client adapter
  const lockApiClient: LockApiClient = useMemo(
    () => ({
      acquireEditorLock: api.games.acquireEditorLock.bind(api.games),
      releaseEditorLock: api.games.releaseEditorLock.bind(api.games),
      refreshEditorLock: api.games.refreshEditorLock.bind(api.games),
      getEditorLockStatus: api.games.getEditorLockStatus.bind(api.games),
    }),
    []
  );

  // Debounced content for auto-save (2 second delay)
  const debouncedContent = useDebounce(viewMode === 'rich' ? richContent : jsonContent, 2000);

  // SEC-I3: Sanitized rich content using centralized sanitization library
  const sanitizedRichContent = useMemo(() => {
    if (!richContent) return '';
    return sanitizeHtml(richContent);
  }, [richContent]);

  const initializeHistory = useCallback((content: string) => {
    const entry: HistoryEntry = { content, timestamp: Date.now() };
    setHistory([entry]);
    setHistoryIndex(0);
  }, []);

  const validateJson = useCallback((content: string) => {
    try {
      const parsed = JSON.parse(content);

      // Basic validation against RuleSpec schema
      if (!parsed.gameId || typeof parsed.gameId !== 'string') {
        throw new Error('gameId è richiesto e deve essere una stringa');
      }
      if (!parsed.version || typeof parsed.version !== 'string') {
        throw new Error('version è richiesto e deve essere una stringa');
      }
      if (!parsed.createdAt || typeof parsed.createdAt !== 'string') {
        throw new Error('createdAt è richiesto e deve essere una stringa');
      }
      if (!Array.isArray(parsed.atoms)) {
        throw new Error('atoms deve essere un array');
      }

      // Validate each rule atom
      for (let i = 0; i < parsed.atoms.length; i++) {
        const rule = parsed.atoms[i];
        if (!rule.id || typeof rule.id !== 'string') {
          throw new Error(`atoms[${i}].id è richiesto e deve essere una stringa`);
        }
        if (!rule.text || typeof rule.text !== 'string') {
          throw new Error(`atoms[${i}].text è richiesto e deve essere una stringa`);
        }
      }

      setIsValid(true);
      setValidationError('');
    } catch (err) {
      setIsValid(false);
      setValidationError(getErrorMessage(err, 'JSON non valido'));
    }
  }, []);

  const loadRuleSpec = useCallback(
    async (gId: string) => {
      setIsLoading(true);
      setErrorMessage('');
      try {
        const spec = await api.games.getRuleSpec(gId);
        setRuleSpec(spec);
        const formatted = JSON.stringify(spec, null, 2);
        setJsonContent(formatted);
        initializeHistory(formatted);
        validateJson(formatted);
      } catch (err) {
        logger.error(
          'Failed to load rule spec',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('EditorPage', 'loadRuleSpec', {
            gameId: gId,
            operation: 'load_rule_spec',
          })
        );
        setErrorMessage(getErrorMessage(err, 'Impossibile caricare RuleSpec.'));
      } finally {
        setIsLoading(false);
      }
    },
    [initializeHistory, validateJson]
  );

  // Load RuleSpec when user and gameId available
  useEffect(() => {
    if (user && gameId && typeof gameId === 'string') {
      void loadRuleSpec(gameId);
    }
  }, [user, gameId, loadRuleSpec]);

  // Issue #2055: Initialize lock when entering editor
  useEffect(() => {
    if (user && gameId && typeof gameId === 'string') {
      void initializeLock(gameId, lockApiClient);
    }

    // Cleanup lock on unmount
    return () => {
      if (gameId) {
        void releaseLock(lockApiClient);
      }
      cleanupLock();
    };
  }, [user, gameId, initializeLock, releaseLock, cleanupLock, lockApiClient]);

  // Issue #2055: Handle beforeunload to release lock
  useEffect(() => {
    const handleBeforeUnload = () => {
      if (gameId && hasLock) {
        // Use sendBeacon for reliable delivery on page unload
        const url = `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/lock`;
        navigator.sendBeacon?.(url, JSON.stringify({ _method: 'DELETE' }));
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [gameId, hasLock]);

  // Auto-save effect - triggers only on debounced content change
  /* eslint-disable react-hooks/exhaustive-deps */
  useEffect(() => {
    if (hasUnsavedChanges && isValid && debouncedContent) {
      void handleAutoSave();
    }
  }, [debouncedContent]);
  /* eslint-enable react-hooks/exhaustive-deps */

  const handleAutoSave = async () => {
    if (!isValid || !gameId || typeof gameId !== 'string') {
      return;
    }

    try {
      const contentToSave = viewMode === 'rich' ? convertRichToJson(richContent) : jsonContent;
      const parsed: RuleSpec = JSON.parse(contentToSave);

      setIsSaving(true);
      setErrorMessage('');

      const updated = await api.games.updateRuleSpec(gameId, parsed);
      setRuleSpec(updated);
      setHasUnsavedChanges(false);
      setStatusMessage(
        `Auto-salvato (versione ${updated.version}) alle ${new Date().toLocaleTimeString()}`
      );

      // Clear success message after 3 seconds
      setTimeout(() => setStatusMessage(''), 3000);
    } catch (err) {
      logger.error(
        'Auto-save failed',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('EditorPage', 'autoSave', { gameId, operation: 'auto_save' })
      );
      // Don't show error for auto-save failures to avoid interrupting user
    } finally {
      setIsSaving(false);
    }
  };

  const convertRichToJson = (html: string): string => {
    // For now, store rich content in a special field
    // In a real implementation, you'd parse HTML to extract structured data
    if (!ruleSpec) return jsonContent;

    const updated = {
      ...ruleSpec,
      // Store rich content in a metadata field for now
      richText: html,
    };
    return JSON.stringify(updated, null, 2);
  };

  const handleViewModeChange = (newMode: ViewMode) => {
    if (newMode === 'json' && viewMode === 'rich') {
      // Convert rich text to JSON before switching
      const converted = convertRichToJson(richContent);
      setJsonContent(converted);
    } else if (newMode === 'rich' && viewMode === 'json') {
      // Parse JSON to extract rich content
      try {
        const parsed = JSON.parse(jsonContent);
        setRichContent(parsed.richText || '<p>Nessun contenuto formattato disponibile</p>');
      } catch {
        // If parsing fails, keep current rich content
      }
    }
    setViewMode(newMode);
  };

  const handleRichContentChange = (html: string) => {
    setRichContent(html);
    setHasUnsavedChanges(true);
    // Validate that we can convert to JSON
    try {
      convertRichToJson(html);
      setIsValid(true);
      setValidationError('');
    } catch (err) {
      setIsValid(false);
      setValidationError(getErrorMessage(err, 'Impossibile convertire in JSON'));
    }
  };

  const addToHistory = (content: string) => {
    // Remove any history entries after the current index
    const newHistory = history.slice(0, historyIndex + 1);
    const entry: HistoryEntry = { content, timestamp: Date.now() };
    newHistory.push(entry);
    setHistory(newHistory);
    setHistoryIndex(newHistory.length - 1);
  };

  const handleUndo = () => {
    if (historyIndex > 0) {
      const newIndex = historyIndex - 1;
      setHistoryIndex(newIndex);
      const previousContent = history[newIndex].content;
      setJsonContent(previousContent);
      validateJson(previousContent);
    }
  };

  const handleRedo = () => {
    if (historyIndex < history.length - 1) {
      const newIndex = historyIndex + 1;
      setHistoryIndex(newIndex);
      const nextContent = history[newIndex].content;
      setJsonContent(nextContent);
      validateJson(nextContent);
    }
  };

  const handleJsonChange = (newContent: string) => {
    setJsonContent(newContent);
    setHasUnsavedChanges(true);
    validateJson(newContent);
  };

  const handleJsonBlur = () => {
    // Add to history when user finishes editing (on blur)
    if (history.length === 0 || jsonContent !== history[historyIndex].content) {
      addToHistory(jsonContent);
    }
  };

  const handleSave = async () => {
    if (!isValid) {
      setErrorMessage('Impossibile salvare: JSON non valido');
      return;
    }

    if (!gameId || typeof gameId !== 'string') {
      setErrorMessage('gameId mancante');
      return;
    }

    // Issue #2055: Check if we have the lock
    if (!hasLock) {
      setErrorMessage("Non hai il controllo esclusivo. Attendi che l'altro utente finisca.");
      return;
    }

    try {
      const contentToSave = viewMode === 'rich' ? convertRichToJson(richContent) : jsonContent;
      const parsed: RuleSpec = JSON.parse(contentToSave);
      setIsSaving(true);
      setErrorMessage('');
      setStatusMessage('');

      // Issue #2055: Use ETag for optimistic concurrency
      const updated = await api.games.updateRuleSpecWithETag(
        gameId,
        parsed,
        currentETag ?? undefined
      );
      setRuleSpec(updated);
      setHasUnsavedChanges(false);

      // Issue #2055: Update ETag after successful save
      // Extract ETag from response if available (backend returns it in RuleSpecDto)
      const newETag = (updated as RuleSpec & { etag?: string }).etag;
      if (newETag) {
        setCurrentETag(newETag);
      }

      setStatusMessage(`RuleSpec salvato con successo (versione ${updated.version})`);
    } catch (err) {
      // Issue #2055: Handle conflict errors
      const errorMsg = getErrorMessage(err, 'Impossibile salvare RuleSpec');

      if (errorMsg.includes('Conflict detected') || errorMsg.includes('modified by another user')) {
        // Fetch the latest version from server
        try {
          const remoteVersion = await api.games.getRuleSpec(gameId);
          if (remoteVersion) {
            const contentToSave =
              viewMode === 'rich' ? convertRichToJson(richContent) : jsonContent;
            const localVersion: RuleSpec = JSON.parse(contentToSave);
            handleConflict(localVersion, remoteVersion, errorMsg);
          }
        } catch (_fetchErr) {
          setErrorMessage('Conflitto rilevato ma impossibile recuperare la versione remota');
        }
      } else {
        logger.error(
          'Failed to save rule spec',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('EditorPage', 'handleSave', { gameId, operation: 'save_rule_spec' })
        );
        setErrorMessage(errorMsg);
      }
    } finally {
      setIsSaving(false);
    }
  };

  // Issue #2055: Handle conflict resolution
  const handleConflictResolve = async (choice: 'local' | 'remote' | 'merge') => {
    const resolvedVersion = resolveConflict(choice);

    if (!resolvedVersion || !gameId) {
      return;
    }

    // Update local state with resolved version
    setRuleSpec(resolvedVersion);
    const formatted = JSON.stringify(resolvedVersion, null, 2);
    setJsonContent(formatted);
    validateJson(formatted);

    if (choice !== 'remote') {
      // Need to save the resolved version
      setHasUnsavedChanges(true);
      // Trigger save with the resolved content
      try {
        setIsSaving(true);
        const updated = await api.games.updateRuleSpecWithETag(gameId, resolvedVersion);
        setRuleSpec(updated);
        setHasUnsavedChanges(false);
        const newETag = (updated as RuleSpec & { etag?: string }).etag;
        if (newETag) {
          setCurrentETag(newETag);
        }
        setStatusMessage(`Conflitto risolto e salvato (versione ${updated.version})`);
      } catch (err) {
        setErrorMessage(getErrorMessage(err, 'Impossibile salvare la versione risolta'));
      } finally {
        setIsSaving(false);
      }
    } else {
      // Remote was chosen, just update ETag
      const newETag = (resolvedVersion as RuleSpec & { etag?: string }).etag;
      if (newETag) {
        setCurrentETag(newETag);
      }
      setStatusMessage('Versione remota accettata');
      setHasUnsavedChanges(false);
    }
  };

  const canUndo = historyIndex > 0;
  const canRedo = historyIndex < history.length - 1;

  if (!user) {
    return (
      <main className="p-6 font-sans">
        <h1>Editor RuleSpec</h1>
        <p>Devi effettuare l&apos;accesso per utilizzare l&apos;editor.</p>
        <Link href="/" className="text-blue-600">
          Torna alla home
        </Link>
      </main>
    );
  }

  // RequireRole already checked permissions, this is redundant but kept for safety
  if (user && user.role !== 'Admin' && user.role !== 'Editor') {
    return (
      <main className="p-6 font-sans">
        <h1>Editor RuleSpec</h1>
        <p>Non hai i permessi necessari per utilizzare l&apos;editor.</p>
        <Link href="/" className="text-blue-600">
          Torna alla home
        </Link>
      </main>
    );
  }

  if (!gameId) {
    return (
      <main className="p-6 font-sans">
        <h1>Editor RuleSpec</h1>
        <p>Specifica un gameId nella query string: ?gameId=demo-chess</p>
        <Link href="/" className="text-blue-600">
          Torna alla home
        </Link>
      </main>
    );
  }

  return (
    <main className="p-6 font-sans max-w-[1400px] mx-auto">
      {/* Issue #2055: Conflict Resolution Modal */}
      <ConflictResolutionModal
        open={showConflictModal}
        onOpenChange={closeConflictModal}
        conflict={conflict}
        onResolve={handleConflictResolve}
        isResolving={isSaving}
      />

      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="m-0">Editor RuleSpec</h1>
          <p className="my-2 mx-0 text-gray-600">
            Game: <strong>{gameId}</strong>
            {hasUnsavedChanges && (
              <span className="ml-3 text-orange-500 text-sm">• Modifiche non salvate</span>
            )}
          </p>
        </div>
        <div className="flex gap-3 items-center">
          {/* Issue #2055: Presence Indicator */}
          <PresenceIndicator
            lockStatus={lockStatus}
            acquisitionStatus={acquisitionStatus}
            lockError={lockError}
          />
          <ViewModeToggle mode={viewMode} onModeChange={handleViewModeChange} />
          <Link
            href={`/versions?gameId=${gameId}`}
            className="px-4 py-2 bg-orange-500 text-white no-underline rounded text-sm"
          >
            Storico Versioni
          </Link>
          <Link href="/" className="px-4 py-2 bg-gray-600 text-white no-underline rounded text-sm">
            Home
          </Link>
        </div>
      </div>

      {/* Status Messages */}
      {statusMessage && (
        <div className="p-3 bg-green-50 border border-green-600 rounded mb-4 flex items-center gap-2">
          <span>✓</span>
          <span>{statusMessage}</span>
        </div>
      )}

      {errorMessage && (
        <div className="p-3 bg-red-50 border border-red-600 rounded mb-4">{errorMessage}</div>
      )}

      {isLoading ? (
        <p>Caricamento...</p>
      ) : (
        <div className="flex gap-6">
          {/* Editor Panel */}
          <div className="flex-[1_1_50%] flex flex-col">
            <div className="flex justify-between items-center mb-3">
              <h2 className="m-0">{viewMode === 'rich' ? 'Editor Visuale' : 'Editor JSON'}</h2>
              <div className="flex gap-2">
                {viewMode === 'json' && (
                  <>
                    <button
                      onClick={handleUndo}
                      disabled={!canUndo}
                      className={cn(
                        'px-3 py-1.5 text-white border-none rounded cursor-pointer text-sm',
                        canUndo ? 'bg-blue-600' : 'bg-gray-300 cursor-not-allowed'
                      )}
                      title="Annulla (Ctrl+Z)"
                    >
                      ← Annulla
                    </button>
                    <button
                      onClick={handleRedo}
                      disabled={!canRedo}
                      className={cn(
                        'px-3 py-1.5 text-white border-none rounded cursor-pointer text-sm',
                        canRedo ? 'bg-blue-600' : 'bg-gray-300 cursor-not-allowed'
                      )}
                      title="Ripeti (Ctrl+Y)"
                    >
                      Ripeti →
                    </button>
                  </>
                )}
                <button
                  onClick={handleSave}
                  disabled={!isValid || isSaving || !hasUnsavedChanges}
                  className={cn(
                    'px-4 py-1.5 text-white border-none rounded text-sm font-bold',
                    isValid && !isSaving && hasUnsavedChanges
                      ? 'bg-green-600 cursor-pointer'
                      : 'bg-gray-300 cursor-not-allowed'
                  )}
                >
                  {isSaving ? 'Salvataggio...' : hasUnsavedChanges ? 'Salva Ora' : 'Salvato'}
                </button>
              </div>
            </div>

            {/* Validation Status */}
            <div
              className={cn(
                'p-2 rounded mb-2 text-sm',
                isValid ? 'bg-green-50 border border-green-600' : 'bg-red-50 border border-red-600'
              )}
            >
              {isValid ? '✓ Contenuto valido' : `✗ ${validationError}`}
            </div>

            {/* Editor */}
            {viewMode === 'rich' ? (
              <RichTextEditor
                content={richContent}
                onChange={handleRichContentChange}
                isValid={isValid}
                autoFocus={true}
              />
            ) : (
              <textarea
                value={jsonContent}
                onChange={e => handleJsonChange(e.target.value)}
                onBlur={handleJsonBlur}
                className={cn(
                  'flex-1 min-h-[600px] font-mono text-sm p-3 rounded resize-y',
                  isValid ? 'border-2 border-gray-300' : 'border-2 border-red-600'
                )}
                spellCheck={false}
              />
            )}
          </div>

          {/* Preview Panel */}
          <div className="flex-[1_1_50%] flex flex-col">
            <h2 className="mt-0">Preview</h2>
            <div className="flex-1 p-4 bg-gray-50 border border-gray-300 rounded overflow-y-auto min-h-[600px]">
              {isValid && (viewMode === 'rich' ? richContent : jsonContent) ? (
                viewMode === 'rich' ? (
                  <div dangerouslySetInnerHTML={{ __html: sanitizedRichContent }} />
                ) : (
                  <RuleSpecPreview ruleSpec={JSON.parse(jsonContent)} />
                )
              ) : (
                <p className="text-gray-400">
                  Correggi gli errori per visualizzare l&apos;anteprima
                </p>
              )}
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

function RuleSpecPreview({ ruleSpec }: { ruleSpec: RuleSpec }) {
  return (
    <div>
      <div className="mb-6">
        <h3 className="mt-0 mb-2">Informazioni Gioco</h3>
        <table className="w-full border-collapse">
          <tbody>
            <tr>
              <td className="p-1 px-2 font-bold w-[120px]">Game ID:</td>
              <td className="p-1 px-2">{ruleSpec.gameId}</td>
            </tr>
            <tr>
              <td className="p-1 px-2 font-bold">Versione:</td>
              <td className="p-1 px-2">{ruleSpec.version}</td>
            </tr>
            <tr>
              <td className="p-1 px-2 font-bold">Creato:</td>
              <td className="p-1 px-2">{new Date(ruleSpec.createdAt).toLocaleString()}</td>
            </tr>
            <tr>
              <td className="p-1 px-2 font-bold">N. Regole:</td>
              <td className="p-1 px-2">{ruleSpec.atoms.length}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div>
        <h3 className="mb-3">Regole</h3>
        {ruleSpec.atoms.map((rule, index) => (
          <div key={rule.id} className="mb-4 p-3 bg-white border border-gray-300 rounded">
            <div className="flex justify-between mb-2">
              <strong className="text-blue-600">
                {index + 1}. {rule.id}
              </strong>
              <div className="text-xs text-gray-600">
                {rule.section && <span className="mr-3">Sezione: {rule.section}</span>}
                {rule.page && <span className="mr-3">Pag. {rule.page}</span>}
                {rule.line && <span>Riga {rule.line}</span>}
              </div>
            </div>
            <p className="m-0 leading-relaxed">{rule.text}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
