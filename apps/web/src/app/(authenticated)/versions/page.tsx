'use client';

import { Suspense, useCallback, useEffect, useState } from 'react';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';

import { CommentThread } from '@/components/comments';
import { DiffViewerEnhanced } from '@/components/diff';
import { VersionTimeline, VersionTimelineFilters } from '@/components/versioning';
import { api } from '@/lib/api';
import type { RuleSpecHistory, RuleSpecDiff } from '@/lib/api/schemas';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';
import { getErrorMessage } from '@/lib/utils/errorHandler';

type AuthUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
};

function VersionHistoryContent() {
  const searchParams = useSearchParams();
  const gameId = searchParams?.get('gameId') ?? null;

  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [history, setHistory] = useState<RuleSpecHistory | null>(null);
  const [selectedFromVersion, setSelectedFromVersion] = useState<string>('');
  const [selectedToVersion, setSelectedToVersion] = useState<string>('');
  const [diff, setDiff] = useState<RuleSpecDiff | null>(null);
  const [isLoadingHistory, setIsLoadingHistory] = useState<boolean>(false);
  const [isLoadingDiff, setIsLoadingDiff] = useState<boolean>(false);
  const [isRestoring, setIsRestoring] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string>('');
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [showOnlyChanges, setShowOnlyChanges] = useState<boolean>(true);
  // EDIT-06: Timeline view toggle
  const [viewMode, setViewMode] = useState<'list' | 'timeline'>('list');
  const [timelineFilters, setTimelineFilters] = useState({});
  const [timelineAuthors, setTimelineAuthors] = useState<string[]>([]);

  const loadCurrentUser = useCallback(async () => {
    try {
      const user = await api.auth.getMe();
      setAuthUser(user);
    } catch {
      setAuthUser(null);
    }
  }, []);

  const loadHistory = useCallback(async (gId: string) => {
    setIsLoadingHistory(true);
    setErrorMessage('');
    try {
      const historyData = await api.games.getRuleSpecHistory(gId);
      setHistory(historyData);
      // Auto-select the two most recent versions for diff
      if (historyData && historyData.versions.length >= 2) {
        setSelectedFromVersion(historyData.versions[1].version);
        setSelectedToVersion(historyData.versions[0].version);
      }
    } catch (err) {
      logger.error(
        'Failed to load version history',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('VersionHistoryPage', 'loadHistory', {
          gameId: gId,
          operation: 'load_history',
        })
      );
      setErrorMessage(getErrorMessage(err, 'Impossibile caricare lo storico versioni.'));
    } finally {
      setIsLoadingHistory(false);
    }
  }, []);

  // EDIT-06: Load timeline authors for filters
  const loadTimelineAuthors = useCallback(async (gId: string) => {
    try {
      const response = await api.games.getRuleSpecTimeline(gId);
      if (response && response.authors) {
        setTimelineAuthors(response.authors);
      }
    } catch (err) {
      logger.error(
        'Failed to load timeline authors',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('VersionHistoryPage', 'loadTimelineAuthors', {
          gameId: gId,
          operation: 'load_timeline_authors',
        })
      );
    }
  }, []);

  const loadDiff = useCallback(async () => {
    if (!gameId || typeof gameId !== 'string' || !selectedFromVersion || !selectedToVersion) {
      return;
    }

    setIsLoadingDiff(true);
    setErrorMessage('');
    setDiff(null);
    try {
      const diffData = await api.games.getRuleSpecDiff(
        gameId,
        selectedFromVersion,
        selectedToVersion
      );
      setDiff(diffData);
    } catch (err) {
      logger.error(
        'Failed to load version diff',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('VersionHistoryPage', 'loadDiff', {
          gameId,
          fromVersion: selectedFromVersion,
          toVersion: selectedToVersion,
          operation: 'load_diff',
        })
      );
      setErrorMessage(getErrorMessage(err, 'Impossibile caricare il diff.'));
    } finally {
      setIsLoadingDiff(false);
    }
  }, [gameId, selectedFromVersion, selectedToVersion]);

  useEffect(() => {
    void loadCurrentUser();
  }, [loadCurrentUser]);

  useEffect(() => {
    if (authUser && gameId && typeof gameId === 'string') {
      void loadHistory(gameId);
      // EDIT-06: Load timeline authors
      void loadTimelineAuthors(gameId);
    }
  }, [authUser, gameId, loadHistory, loadTimelineAuthors]);

  const handleRestoreVersion = async (version: string) => {
    if (!gameId || typeof gameId !== 'string') {
      setErrorMessage('gameId mancante');
      return;
    }

    const confirmed = confirm(
      `Sei sicuro di voler ripristinare la versione ${version}? Questo creerà una nuova versione.`
    );
    if (!confirmed) {
      return;
    }

    setIsRestoring(true);
    setErrorMessage('');
    setStatusMessage('');
    try {
      // Get the version to restore
      const versionData = await api.games.getRuleSpecVersion(gameId, Number(version));

      if (!versionData) {
        throw new Error(`Version ${version} not found`);
      }

      // Save it as a new version
      const updated = await api.games.updateRuleSpec(gameId, versionData);
      setStatusMessage(
        `Versione ${version} ripristinata con successo come versione ${updated.version}`
      );

      // Reload history
      await loadHistory(gameId);
    } catch (err) {
      logger.error(
        'Failed to restore version',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('VersionHistoryPage', 'handleRestoreVersion', {
          gameId,
          version,
          operation: 'restore_version',
        })
      );
      setErrorMessage(getErrorMessage(err, 'Impossibile ripristinare la versione'));
    } finally {
      setIsRestoring(false);
    }
  };

  useEffect(() => {
    if (selectedFromVersion && selectedToVersion) {
      void loadDiff();
    }
  }, [loadDiff, selectedFromVersion, selectedToVersion]);

  if (!authUser) {
    return (
      <main className="p-6 font-sans">
        <h1>Storico Versioni RuleSpec</h1>
        <p>Devi effettuare l&apos;accesso per visualizzare lo storico.</p>
        <Link href="/" className="text-blue-600">
          Torna alla home
        </Link>
      </main>
    );
  }

  if (!gameId) {
    return (
      <main className="p-6 font-sans">
        <h1>Storico Versioni RuleSpec</h1>
        <p>Specifica un gameId nella query string: ?gameId=demo-chess</p>
        <Link href="/" className="text-blue-600">
          Torna alla home
        </Link>
      </main>
    );
  }

  return (
    <main className="p-6 font-sans max-w-[1600px] mx-auto">
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="m-0">Storico Versioni RuleSpec</h1>
          <p className="my-2 mx-0 text-gray-600">
            Game: <strong>{gameId}</strong>
          </p>
        </div>
        <div className="flex gap-3">
          <Link
            href={`/editor?gameId=${gameId}`}
            className="px-4 py-2 bg-blue-600 text-white no-underline rounded text-sm"
          >
            Editor
          </Link>
          <Link href="/" className="px-4 py-2 bg-gray-600 text-white no-underline rounded text-sm">
            Home
          </Link>
        </div>
      </div>

      {statusMessage && (
        <div className="p-3 bg-green-50 border border-green-600 rounded mb-4">{statusMessage}</div>
      )}

      {errorMessage && (
        <div className="p-3 bg-red-50 border border-red-600 rounded mb-4">{errorMessage}</div>
      )}

      {/* View mode toggle */}
      <div className="mb-4 border-b-2 border-gray-300">
        <div className="flex gap-0">
          <button
            onClick={() => setViewMode('list')}
            className={cn(
              'px-6 py-3 border-none cursor-pointer text-sm transition-all duration-200',
              viewMode === 'list'
                ? 'bg-blue-600 text-white border-b-2 border-blue-600 font-bold'
                : 'bg-transparent text-gray-600 border-b-2 border-transparent'
            )}
          >
            📋 List View
          </button>
          <button
            onClick={() => setViewMode('timeline')}
            className={cn(
              'px-6 py-3 border-none cursor-pointer text-sm transition-all duration-200',
              viewMode === 'timeline'
                ? 'bg-blue-600 text-white border-b-2 border-blue-600 font-bold'
                : 'bg-transparent text-gray-600 border-b-2 border-transparent'
            )}
          >
            🕒 Timeline View
          </button>
        </div>
      </div>

      {isLoadingHistory ? (
        <p>Caricamento storico...</p>
      ) : viewMode === 'timeline' ? (
        // Timeline view
        <div>
          <VersionTimelineFilters
            authors={timelineAuthors}
            filters={timelineFilters}
            onFiltersChange={setTimelineFilters}
            onReset={() => setTimelineFilters({})}
          />
          <VersionTimeline
            gameId={gameId as string}
            onVersionClick={version => {
              setSelectedToVersion(version);
              setViewMode('list');
            }}
          />
        </div>
      ) : (
        <div className="grid grid-cols-[300px_1fr] gap-6">
          {/* Left sidebar: Version list */}
          <div>
            <h2 className="mt-0">Versioni ({history?.totalVersions || 0})</h2>
            <div className="flex flex-col gap-2">
              {history?.versions.map((version, index) => (
                <div
                  key={version.version}
                  className="p-3 bg-white border border-gray-300 rounded text-sm"
                >
                  <div className="flex justify-between items-center mb-2">
                    <strong className={index === 0 ? 'text-green-600' : 'text-gray-900'}>
                      {version.version}
                      {index === 0 && (
                        <span className="ml-2 text-xs text-green-600">(corrente)</span>
                      )}
                    </strong>
                  </div>
                  <div className="text-xs text-gray-600 mb-2">
                    {new Date(version.createdAt).toLocaleString()}
                  </div>
                  <div className="text-xs text-gray-600 mb-2">{version.atomCount} regole</div>
                  {authUser.role === 'Admin' || authUser.role === 'Editor' ? (
                    <button
                      onClick={() => handleRestoreVersion(version.version)}
                      disabled={isRestoring || index === 0}
                      className={cn(
                        'w-full px-3 py-1.5 text-white border-none rounded text-xs',
                        index === 0 || isRestoring
                          ? 'bg-gray-300 cursor-not-allowed'
                          : 'bg-orange-500 cursor-pointer'
                      )}
                    >
                      {isRestoring ? 'Ripristino...' : 'Ripristina'}
                    </button>
                  ) : null}
                </div>
              ))}
            </div>
          </div>

          {/* Right content: Diff viewer */}
          <div>
            <div className="mb-4">
              <h2 className="mt-0 mb-3">Confronta Versioni</h2>
              <div className="flex gap-3 items-center mb-3">
                <div className="flex-1">
                  <label htmlFor="from-version" className="block mb-1 text-sm font-bold">
                    Da versione:
                  </label>
                  <select
                    id="from-version"
                    value={selectedFromVersion}
                    onChange={e => setSelectedFromVersion(e.target.value)}
                    className="w-full p-2 border border-gray-300 rounded text-sm"
                  >
                    <option value="">Seleziona versione</option>
                    {history?.versions.map(version => (
                      <option key={version.version} value={version.version}>
                        {version.version} - {new Date(version.createdAt).toLocaleString()}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="flex-1">
                  <label htmlFor="to-version" className="block mb-1 text-sm font-bold">
                    A versione:
                  </label>
                  <select
                    id="to-version"
                    value={selectedToVersion}
                    onChange={e => setSelectedToVersion(e.target.value)}
                    className="w-full p-2 border border-gray-300 rounded text-sm"
                  >
                    <option value="">Seleziona versione</option>
                    {history?.versions.map(version => (
                      <option key={version.version} value={version.version}>
                        {version.version} - {new Date(version.createdAt).toLocaleString()}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="mb-3">
                <label className="flex items-center gap-2 text-sm cursor-pointer">
                  <input
                    type="checkbox"
                    checked={showOnlyChanges}
                    onChange={e => setShowOnlyChanges(e.target.checked)}
                  />
                  Mostra solo modifiche
                </label>
              </div>
            </div>

            {isLoadingDiff ? (
              <p>Caricamento diff...</p>
            ) : diff ? (
              <>
                <DiffViewerEnhanced
                  diff={diff}
                  showOnlyChanges={showOnlyChanges}
                  defaultViewMode="side-by-side"
                />

                {/* Comments section for the selected "to" version */}
                {selectedToVersion && authUser && (
                  <div className="mt-8 border-t-2 border-gray-300 pt-6">
                    <CommentThread
                      gameId={gameId as string}
                      version={selectedToVersion}
                      currentUserId={authUser.id}
                      currentUserRole={authUser.role}
                    />
                  </div>
                )}
              </>
            ) : (
              <p className="text-gray-400">Seleziona due versioni per visualizzare le differenze</p>
            )}
          </div>
        </div>
      )}
    </main>
  );
}

export default function VersionHistory() {
  return (
    <Suspense
      fallback={
        <div className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900 text-slate-500 dark:text-slate-300">
          Caricamento cronologia versioni...
        </div>
      }
    >
      <VersionHistoryContent />
    </Suspense>
  );
}
