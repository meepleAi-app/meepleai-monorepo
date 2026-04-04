'use client';

/**
 * Step 3: Chat Setup
 *
 * Wait for PDF processing to complete, then create a chat thread with the RAG agent.
 */

import { useState, useCallback, useEffect, useRef } from 'react';

import { toast } from '@/components/layout';
import { Spinner } from '@/components/loading';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

interface ChatSetupStepProps {
  gameId: string;
  gameName: string;
  pdfId?: string | null; // Optional for user wizard (Issue #4)
  onComplete: (chatThreadId: string | null) => void;
  onBack: () => void;
}

type ProcessingStatus = 'pending' | 'processing' | 'completed' | 'failed';

export function ChatSetupStep({ gameId, gameName, pdfId, onComplete, onBack }: ChatSetupStepProps) {
  const [processingStatus, setProcessingStatus] = useState<ProcessingStatus>('pending');
  const [processingProgress, setProcessingProgress] = useState(0);
  const [processingMessage, setProcessingMessage] = useState('Inizializzazione...');
  const [creatingChat, setCreatingChat] = useState(false);
  const [chatThreadId, setChatThreadId] = useState<string | null>(null);

  const pollingRef = useRef<NodeJS.Timeout | null>(null);

  // Poll for processing status
  useEffect(() => {
    if (!pdfId) return;

    const checkStatus = async () => {
      try {
        const progress = await api.pdf.getProcessingProgress(pdfId);

        if (!progress) {
          // If no progress endpoint, assume completed for demo
          setProcessingStatus('completed');
          setProcessingProgress(100);
          setProcessingMessage('Elaborazione completata!');
          return;
        }

        // Map backend ProcessingStep to local ProcessingStatus
        switch (progress.currentStep) {
          case 'Uploading':
            setProcessingStatus('pending');
            setProcessingProgress(progress.percentComplete ?? 0);
            setProcessingMessage('Caricamento in corso...');
            break;
          case 'Extracting':
          case 'Chunking':
          case 'Embedding':
          case 'Indexing':
            setProcessingStatus('processing');
            setProcessingProgress(progress.percentComplete ?? 50);
            setProcessingMessage(
              `Elaborazione pagina ${progress.pagesProcessed ?? '?'}/${progress.totalPages ?? '?'}`
            );
            break;
          case 'Completed':
            setProcessingStatus('completed');
            setProcessingProgress(100);
            setProcessingMessage('Elaborazione completata!');
            if (pollingRef.current) {
              clearInterval(pollingRef.current);
              pollingRef.current = null;
            }
            break;
          case 'Failed':
            setProcessingStatus('failed');
            setProcessingMessage(progress.errorMessage ?? 'Elaborazione fallita');
            if (pollingRef.current) {
              clearInterval(pollingRef.current);
              pollingRef.current = null;
            }
            break;
        }
      } catch (_err) {
        // On poll error, show failure state instead of silently assuming completion
        setProcessingStatus('failed');
        setProcessingProgress(0);
        setProcessingMessage('Errore durante il controllo dello stato. Riprova.');
        if (pollingRef.current) {
          clearInterval(pollingRef.current);
          pollingRef.current = null;
        }
      }
    };

    // Initial check
    void checkStatus();

    // Start polling every 2 seconds
    pollingRef.current = setInterval(checkStatus, 2000);

    return () => {
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
      }
    };
  }, [pdfId]);

  const handleCreateChat = useCallback(async () => {
    setCreatingChat(true);

    try {
      // Create a new chat thread for the game
      // Note: This uses the chat store or API to create a thread
      const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

      const response = await fetch(`${API_BASE}/api/v1/chat-threads`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({
          gameId,
          title: `Chat: ${gameName}`,
        }),
      });

      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        throw new Error(error.error ?? 'Creazione chat fallita');
      }

      const data = await response.json();
      const threadId = data.threadId ?? data.id;

      setChatThreadId(threadId);
      toast.success('Chat creata con successo!');
      onComplete(threadId);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Errore: ${message}`);
    } finally {
      setCreatingChat(false);
    }
  }, [gameId, gameName, onComplete]);

  // Auto-create chat when processing completes
  useEffect(() => {
    if (processingStatus === 'completed' && !chatThreadId && !creatingChat) {
      void handleCreateChat();
    }
  }, [processingStatus, chatThreadId, creatingChat, handleCreateChat]);

  // Issue #4: If no PDF, skip agent setup
  if (!pdfId) {
    return (
      <div className="space-y-6">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
            Setup Agente RAG
          </h2>
          <p className="text-slate-600 dark:text-slate-400">
            Nessun PDF caricato. L&apos;agente RAG richiede un PDF per funzionare.
          </p>
        </div>

        <Card className="p-6 bg-amber-50 dark:bg-amber-900/20 border-amber-200 dark:border-amber-800">
          <p className="text-amber-800 dark:text-amber-200">
            Per creare un agente RAG, è necessario caricare un PDF con le regole del gioco.
          </p>
        </Card>

        <div className="flex justify-between gap-3">
          <Button variant="outline" onClick={onBack}>
            Indietro
          </Button>
          <Button onClick={() => onComplete(null)}>Salta Agente</Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Setup Agente RAG
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Attendere il completamento dell'elaborazione del PDF per creare la chat.
        </p>
      </div>

      {/* Processing Status Card */}
      <Card className="p-6 space-y-4">
        <div className="flex items-center gap-4">
          {processingStatus === 'completed' ? (
            <div className="w-12 h-12 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
              <span className="text-2xl">✅</span>
            </div>
          ) : processingStatus === 'failed' ? (
            <div className="w-12 h-12 rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
              <span className="text-2xl">❌</span>
            </div>
          ) : (
            <div className="w-12 h-12 rounded-full bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center">
              <Spinner size="md" />
            </div>
          )}
          <div className="flex-1">
            <h3 className="font-semibold text-slate-900 dark:text-white">Elaborazione PDF</h3>
            <p className="text-sm text-slate-600 dark:text-slate-400">{processingMessage}</p>
          </div>
        </div>

        {/* Progress bar */}
        {processingStatus !== 'failed' && (
          <div className="space-y-1">
            <div className="flex justify-between text-sm">
              <span className="text-slate-600 dark:text-slate-400">Progresso</span>
              <span className="font-medium">{processingProgress}%</span>
            </div>
            <div className="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
              <div
                className={`h-full transition-all duration-500 ${
                  processingStatus === 'completed' ? 'bg-green-600' : 'bg-blue-600'
                }`}
                style={{ width: `${processingProgress}%` }}
              />
            </div>
          </div>
        )}
      </Card>

      {/* Chat Creation Status */}
      {processingStatus === 'completed' && (
        <Card className="p-6 space-y-4">
          <div className="flex items-center gap-4">
            {chatThreadId ? (
              <div className="w-12 h-12 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
                <span className="text-2xl">💬</span>
              </div>
            ) : creatingChat ? (
              <div className="w-12 h-12 rounded-full bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center">
                <Spinner size="md" />
              </div>
            ) : (
              <div className="w-12 h-12 rounded-full bg-slate-100 dark:bg-slate-700 flex items-center justify-center">
                <span className="text-2xl">💬</span>
              </div>
            )}
            <div className="flex-1">
              <h3 className="font-semibold text-slate-900 dark:text-white">Chat con Agente RAG</h3>
              <p className="text-sm text-slate-600 dark:text-slate-400">
                {chatThreadId
                  ? 'Chat creata! Puoi procedere al Q&A.'
                  : creatingChat
                    ? 'Creazione chat in corso...'
                    : 'Pronto per creare la chat.'}
              </p>
            </div>
          </div>
        </Card>
      )}

      {/* Error state */}
      {processingStatus === 'failed' && (
        <Card className="p-6 bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-800">
          <p className="text-red-700 dark:text-red-300">{processingMessage}</p>
          <p className="text-sm text-red-600 dark:text-red-400 mt-2">
            Puoi riprovare ricaricando il PDF nel primo step.
          </p>
        </Card>
      )}

      {/* Actions */}
      <div className="flex justify-between gap-3">
        <Button variant="outline" onClick={onBack} disabled={creatingChat}>
          ← Indietro
        </Button>
        {processingStatus === 'completed' && !chatThreadId && (
          <Button onClick={handleCreateChat} disabled={creatingChat} className="min-w-32">
            {creatingChat ? (
              <>
                <Spinner size="sm" className="mr-2" />
                Creazione...
              </>
            ) : (
              'Crea Chat →'
            )}
          </Button>
        )}
        {chatThreadId && (
          <Button onClick={() => onComplete(chatThreadId)} className="min-w-32">
            Vai al Q&A →
          </Button>
        )}
      </div>
    </div>
  );
}
