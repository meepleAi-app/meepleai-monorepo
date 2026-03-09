/**
 * Quick Ask Page — Voice-first rule question interface
 *
 * A minimal, mobile-first page where a player can voice a rule question
 * without navigating to a full chat thread. Auto-creates a chat thread
 * in the background on first question.
 *
 * Flow:
 * 1. Select game from library
 * 2. Tap large mic button (or type fallback)
 * 3. Voice recognition -> transcript -> send to agent
 * 4. Response card appears below
 * 5. "Continue in Chat" or "Ask Again"
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import {
  ArrowLeft,
  ChevronDown,
  HelpCircle,
  Keyboard,
  Loader2,
  MessageSquare,
  RefreshCw,
  Send,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { VoiceMicButton } from '@/components/chat-unified/VoiceMicButton';
import { VoiceTranscriptOverlay } from '@/components/chat-unified/VoiceTranscriptOverlay';
import { useAgentChatStream } from '@/hooks/useAgentChatStream';
import { useVoiceInput } from '@/hooks/useVoiceInput';
import { useVoiceOutput } from '@/hooks/useVoiceOutput';
import { api } from '@/lib/api';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';
import { useVoicePreferencesStore } from '@/store/voice/store';

// ============================================================================
// Types
// ============================================================================

interface GameOption {
  id: string;
  title: string;
  agentId?: string;
}

// ============================================================================
// Constants
// ============================================================================

const LAST_GAME_KEY = 'meepleai-last-game-id';

// ============================================================================
// Sub-components
// ============================================================================

function GameSelector({
  games,
  selectedGameId,
  onSelect,
  isLoading,
}: {
  games: GameOption[];
  selectedGameId: string | null;
  onSelect: (gameId: string) => void;
  isLoading: boolean;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const selectedGame = games.find(g => g.id === selectedGameId);

  // Close dropdown on outside click
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => document.removeEventListener('mousedown', handleClickOutside);
    }
  }, [isOpen]);

  if (isLoading) {
    return (
      <div className="w-full max-w-xs mx-auto">
        <div className="h-10 rounded-xl bg-muted/50 animate-pulse" />
      </div>
    );
  }

  if (games.length === 0) {
    return (
      <p
        className="text-sm text-muted-foreground text-center font-nunito"
        data-testid="no-games-message"
      >
        Nessun gioco con agente AI disponibile.{' '}
        <Link
          href="/library"
          className="text-amber-600 hover:text-amber-700 dark:text-amber-400 dark:hover:text-amber-300 underline"
        >
          Vai alla libreria
        </Link>
      </p>
    );
  }

  return (
    <div className="w-full max-w-xs mx-auto relative" ref={dropdownRef}>
      <label
        className="block text-xs font-medium text-muted-foreground mb-1.5 text-center font-nunito uppercase tracking-wide"
        id="game-selector-label"
      >
        Seleziona gioco
      </label>
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        aria-labelledby="game-selector-label"
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        data-testid="game-selector"
        className={cn(
          'w-full flex items-center justify-between gap-2 px-4 py-2.5 rounded-xl',
          'bg-white/70 dark:bg-neutral-900/70 backdrop-blur-md',
          'border border-border/50 text-sm font-nunito text-foreground',
          'hover:border-amber-300 dark:hover:border-amber-700',
          'focus:outline-none focus:ring-2 focus:ring-amber-500/40',
          'transition-colors duration-150'
        )}
      >
        <span className={!selectedGame ? 'text-muted-foreground' : ''}>
          {selectedGame?.title ?? 'Scegli un gioco...'}
        </span>
        <ChevronDown
          className={cn(
            'h-4 w-4 text-muted-foreground transition-transform duration-150',
            isOpen && 'rotate-180'
          )}
          aria-hidden="true"
        />
      </button>

      {isOpen && (
        <ul
          role="listbox"
          aria-labelledby="game-selector-label"
          data-testid="game-selector-dropdown"
          className={cn(
            'absolute z-50 mt-1 w-full max-h-60 overflow-y-auto',
            'bg-white/90 dark:bg-neutral-900/90 backdrop-blur-md',
            'border border-border/50 rounded-xl shadow-lg',
            'py-1'
          )}
        >
          {games.map(game => (
            <li
              key={game.id}
              role="option"
              aria-selected={game.id === selectedGameId}
              data-testid={`game-option-${game.id}`}
              onClick={() => {
                onSelect(game.id);
                setIsOpen(false);
              }}
              onKeyDown={e => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  onSelect(game.id);
                  setIsOpen(false);
                }
              }}
              tabIndex={0}
              className={cn(
                'px-4 py-2 text-sm font-nunito cursor-pointer',
                'hover:bg-amber-50 dark:hover:bg-amber-500/10',
                'transition-colors duration-100',
                game.id === selectedGameId &&
                  'bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-300 font-medium'
              )}
            >
              {game.title}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ResponseCard({
  question,
  answer,
  isStreaming,
  statusMessage,
  chatThreadId,
  onAskAgain,
}: {
  question: string;
  answer: string;
  isStreaming: boolean;
  statusMessage: string | null;
  chatThreadId: string | null;
  onAskAgain: () => void;
}) {
  return (
    <div
      data-testid="response-card"
      className={cn(
        'w-full max-w-md mx-auto',
        'bg-white/70 dark:bg-neutral-900/70 backdrop-blur-md',
        'rounded-2xl p-6 border border-border/50',
        'shadow-sm',
        'animate-in fade-in slide-in-from-bottom-4 duration-300'
      )}
    >
      {/* Question */}
      <div className="mb-4">
        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide font-nunito mb-1">
          Domanda
        </p>
        <p className="text-sm text-foreground font-nunito" data-testid="response-question">
          {question}
        </p>
      </div>

      {/* Answer */}
      <div className="mb-5">
        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide font-nunito mb-1">
          Risposta
        </p>
        {statusMessage && !answer && (
          <div className="flex items-center gap-2 text-sm text-muted-foreground font-nunito">
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
            <span>{statusMessage}</span>
          </div>
        )}
        <p
          className="text-sm text-foreground font-nunito whitespace-pre-wrap"
          data-testid="response-answer"
        >
          {answer}
          {isStreaming && (
            <span className="inline-block w-1.5 h-4 bg-amber-500 ml-0.5 animate-pulse rounded-sm" />
          )}
        </p>
      </div>

      {/* Actions */}
      {!isStreaming && answer && (
        <div className="flex items-center gap-3">
          {chatThreadId && (
            <Link
              href={`/chat/${chatThreadId}`}
              data-testid="continue-in-chat"
              className={cn(
                'flex-1 inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl',
                'text-sm font-medium font-nunito',
                'bg-amber-500 text-white hover:bg-amber-600',
                'transition-colors duration-150'
              )}
            >
              <MessageSquare className="h-4 w-4" aria-hidden="true" />
              Continua in Chat
            </Link>
          )}
          <button
            type="button"
            onClick={onAskAgain}
            data-testid="ask-again"
            className={cn(
              'flex-1 inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl',
              'text-sm font-medium font-nunito',
              'border border-border/50 text-foreground',
              'hover:bg-muted/50',
              'transition-colors duration-150'
            )}
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Chiedi ancora
          </button>
        </div>
      )}
    </div>
  );
}

function TextFallbackInput({
  onSubmit,
  disabled,
}: {
  onSubmit: (text: string) => void;
  disabled: boolean;
}) {
  const [text, setText] = useState('');

  const handleSubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      const trimmed = text.trim();
      if (trimmed && !disabled) {
        onSubmit(trimmed);
        setText('');
      }
    },
    [text, disabled, onSubmit]
  );

  return (
    <form
      onSubmit={handleSubmit}
      className="w-full max-w-xs mx-auto flex items-center gap-2"
      data-testid="text-fallback-form"
    >
      <input
        type="text"
        value={text}
        onChange={e => setText(e.target.value)}
        placeholder="Scrivi la tua domanda..."
        disabled={disabled}
        data-testid="text-fallback-input"
        className={cn(
          'flex-1 px-4 py-2.5 rounded-xl text-sm font-nunito',
          'bg-white/70 dark:bg-neutral-900/70 backdrop-blur-md',
          'border border-border/50 text-foreground',
          'placeholder:text-muted-foreground',
          'focus:outline-none focus:ring-2 focus:ring-amber-500/40',
          'disabled:opacity-50 disabled:cursor-not-allowed'
        )}
      />
      <button
        type="submit"
        disabled={disabled || !text.trim()}
        data-testid="text-fallback-send"
        aria-label="Send question"
        className={cn(
          'inline-flex items-center justify-center h-10 w-10 rounded-xl',
          'bg-amber-500 text-white hover:bg-amber-600',
          'disabled:opacity-50 disabled:cursor-not-allowed',
          'transition-colors duration-150'
        )}
      >
        <Send className="h-4 w-4" aria-hidden="true" />
      </button>
    </form>
  );
}

// ============================================================================
// Main Page Component
// ============================================================================

export default function QuickAskPage() {
  const router = useRouter();

  // Voice preferences from store
  const voiceLang = useVoicePreferencesStore(s => s.language);
  const ttsEnabled = useVoicePreferencesStore(s => s.ttsEnabled);
  const autoSend = useVoicePreferencesStore(s => s.autoSend);
  const voiceURI = useVoicePreferencesStore(s => s.voiceURI);
  const rate = useVoicePreferencesStore(s => s.rate);

  // Game data
  const [games, setGames] = useState<GameOption[]>([]);
  const [isLoadingGames, setIsLoadingGames] = useState(true);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [selectedAgentId, setSelectedAgentId] = useState<string | null>(null);

  // UI state
  const [currentQuestion, setCurrentQuestion] = useState('');
  const [showTextInput, setShowTextInput] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);
  const [editedTranscript, setEditedTranscript] = useState('');

  // Agent chat stream
  const chatStream = useAgentChatStream({
    onComplete: answer => {
      // Read the answer aloud via TTS if enabled
      if (ttsEnabled && answer) {
        voiceOutput.speak(answer);
      }
    },
  });

  // Voice input
  const voiceInput = useVoiceInput({
    language: voiceLang,
    onTranscript: text => {
      setEditedTranscript(text);
      if (autoSend) {
        handleSendQuestion(text);
      }
    },
  });

  // Voice output (TTS)
  const voiceOutput = useVoiceOutput({
    language: voiceLang,
    preferredVoiceURI: voiceURI ?? undefined,
    rate: rate,
  });

  // ------------------------------------------------------------------
  // Load games with agents from library
  // ------------------------------------------------------------------

  useEffect(() => {
    let cancelled = false;

    async function loadGames() {
      setIsLoadingGames(true);
      try {
        // Fetch library games and available agents in parallel
        const [libraryResponse, agentsResponse] = await Promise.all([
          api.library.getLibrary({ pageSize: 100 }),
          api.agents.getAvailable(),
        ]);

        if (cancelled) return;

        const libraryEntries: UserLibraryEntry[] = libraryResponse?.items ?? [];
        const agents: AgentDto[] = agentsResponse ?? [];

        // Build game options — only include games that have at least
        // one agent available (either game-specific or a system agent)
        const hasSystemAgent = agents.length > 0;
        const agentsByGame = new Map<string, string>();

        // Index agents by gameId for quick lookup
        for (const agent of agents) {
          if (agent.gameId) {
            agentsByGame.set(agent.gameId, agent.id);
          }
        }

        const gameOptions: GameOption[] = libraryEntries
          .filter(entry => {
            // Include if game has a dedicated agent OR system agents exist
            return agentsByGame.has(entry.gameId) || hasSystemAgent;
          })
          .map(entry => ({
            id: entry.gameId,
            title: entry.gameTitle,
            agentId: agentsByGame.get(entry.gameId) ?? agents[0]?.id,
          }));

        setGames(gameOptions);

        // Pre-select: try localStorage last game, or first game
        const lastGameId =
          typeof window !== 'undefined' ? localStorage.getItem(LAST_GAME_KEY) : null;

        const preselect = lastGameId ? gameOptions.find(g => g.id === lastGameId) : null;

        if (preselect) {
          setSelectedGameId(preselect.id);
          setSelectedAgentId(preselect.agentId ?? null);
        } else if (gameOptions.length > 0) {
          setSelectedGameId(gameOptions[0].id);
          setSelectedAgentId(gameOptions[0].agentId ?? null);
        }
      } catch {
        if (!cancelled) {
          setPageError('Errore nel caricamento dei giochi');
        }
      } finally {
        if (!cancelled) {
          setIsLoadingGames(false);
        }
      }
    }

    void loadGames();

    return () => {
      cancelled = true;
    };
  }, []);

  // ------------------------------------------------------------------
  // Game selection handler
  // ------------------------------------------------------------------

  const handleGameSelect = useCallback(
    (gameId: string) => {
      setSelectedGameId(gameId);
      const game = games.find(g => g.id === gameId);
      setSelectedAgentId(game?.agentId ?? null);

      // Persist last selected game
      try {
        localStorage.setItem(LAST_GAME_KEY, gameId);
      } catch {
        // localStorage may be unavailable
      }
    },
    [games]
  );

  // ------------------------------------------------------------------
  // Send question to agent
  // ------------------------------------------------------------------

  const handleSendQuestion = useCallback(
    (text: string) => {
      if (!text.trim() || !selectedAgentId) return;

      setCurrentQuestion(text);
      voiceOutput.stop(); // Stop any current TTS

      chatStream.sendMessage(selectedAgentId, text, chatStream.state.chatThreadId ?? undefined);
    },
    [selectedAgentId, chatStream, voiceOutput]
  );

  // ------------------------------------------------------------------
  // Mic button handler
  // ------------------------------------------------------------------

  const handleMicTap = useCallback(() => {
    if (voiceInput.state === 'listening') {
      voiceInput.stopListening();
    } else {
      voiceInput.startListening();
    }
  }, [voiceInput]);

  // ------------------------------------------------------------------
  // Transcript overlay handlers
  // ------------------------------------------------------------------

  const handleTranscriptEdit = useCallback((text: string) => {
    setEditedTranscript(text);
  }, []);

  const handleTranscriptSend = useCallback(() => {
    const text = editedTranscript || voiceInput.finalText;
    if (text.trim()) {
      handleSendQuestion(text);
    }
    voiceInput.cancelListening();
  }, [editedTranscript, voiceInput, handleSendQuestion]);

  const handleTranscriptCancel = useCallback(() => {
    voiceInput.cancelListening();
    setEditedTranscript('');
  }, [voiceInput]);

  // ------------------------------------------------------------------
  // Ask again
  // ------------------------------------------------------------------

  const handleAskAgain = useCallback(() => {
    chatStream.reset();
    setCurrentQuestion('');
    setEditedTranscript('');
    voiceOutput.stop();
  }, [chatStream, voiceOutput]);

  // ------------------------------------------------------------------
  // Derived state
  // ------------------------------------------------------------------

  const hasResponse = !!chatStream.state.currentAnswer || chatStream.state.isStreaming;
  const isVoiceActive = voiceInput.state === 'listening' || voiceInput.state === 'processing';
  const noAgentAvailable = !selectedAgentId && !isLoadingGames;

  // Ambient glow style for listening state
  const micGlowClass =
    voiceInput.state === 'listening' ? 'shadow-[0_0_60px_rgba(239,68,68,0.3)]' : '';

  return (
    <div className="flex flex-col items-center min-h-dvh px-4 pb-8">
      {/* Header */}
      <header className="w-full max-w-md flex items-center justify-between py-4">
        <button
          type="button"
          onClick={() => router.back()}
          aria-label="Go back"
          data-testid="back-button"
          className={cn(
            'inline-flex items-center justify-center h-9 w-9 rounded-lg',
            'text-muted-foreground hover:text-foreground hover:bg-muted/50',
            'transition-colors duration-150'
          )}
        >
          <ArrowLeft className="h-5 w-5" aria-hidden="true" />
        </button>

        <h1 className="text-lg font-semibold font-quicksand text-foreground">Quick Ask</h1>

        <Link
          href="/help"
          aria-label="Help"
          data-testid="help-button"
          className={cn(
            'inline-flex items-center justify-center h-9 w-9 rounded-lg',
            'text-muted-foreground hover:text-foreground hover:bg-muted/50',
            'transition-colors duration-150'
          )}
        >
          <HelpCircle className="h-5 w-5" aria-hidden="true" />
        </Link>
      </header>

      {/* Main content */}
      <main className="flex-1 flex flex-col items-center justify-center w-full max-w-md gap-8">
        {/* Error */}
        {(pageError || chatStream.state.error) && (
          <div
            role="alert"
            data-testid="page-error"
            className={cn(
              'w-full p-3 rounded-xl text-sm font-nunito text-center',
              'bg-red-50 dark:bg-red-500/10',
              'text-red-700 dark:text-red-400',
              'border border-red-200 dark:border-red-500/20'
            )}
          >
            {pageError || chatStream.state.error}
          </div>
        )}

        {/* Game Selector */}
        <GameSelector
          games={games}
          selectedGameId={selectedGameId}
          onSelect={handleGameSelect}
          isLoading={isLoadingGames}
        />

        {/* No agent message */}
        {noAgentAvailable && (
          <p
            className="text-sm text-muted-foreground text-center font-nunito"
            data-testid="no-agent-message"
          >
            Seleziona un gioco con un agente AI configurato
          </p>
        )}

        {/* Mic Button */}
        {voiceInput.isSupported && (
          <div
            className={cn(
              'flex flex-col items-center gap-3',
              'transition-all duration-300',
              micGlowClass && 'rounded-full'
            )}
          >
            <div className={cn('rounded-full', micGlowClass)}>
              <VoiceMicButton
                state={voiceInput.state}
                onTap={handleMicTap}
                disabled={noAgentAvailable || chatStream.state.isStreaming || isLoadingGames}
                size="lg"
                className={cn(
                  voiceInput.state === 'idle' &&
                    !noAgentAvailable &&
                    'bg-amber-500 text-white hover:bg-amber-600 shadow-lg shadow-amber-500/25'
                )}
              />
            </div>

            {/* Instruction text */}
            {!isVoiceActive && !hasResponse && (
              <p
                className="text-sm text-muted-foreground font-nunito"
                data-testid="mic-instruction"
              >
                Tocca per fare una domanda
              </p>
            )}
          </div>
        )}

        {/* Voice Transcript Overlay */}
        {(isVoiceActive || voiceInput.finalText) && (
          <div className="w-full max-w-xs mx-auto">
            <VoiceTranscriptOverlay
              interimText={voiceInput.interimText}
              finalText={voiceInput.finalText}
              state={voiceInput.state}
              onEdit={handleTranscriptEdit}
              onSend={handleTranscriptSend}
              onCancel={handleTranscriptCancel}
              autoSend={autoSend}
            />
          </div>
        )}

        {/* Voice error */}
        {voiceInput.error && (
          <p
            className="text-xs text-red-500 font-nunito text-center"
            role="alert"
            data-testid="voice-error"
          >
            {voiceInput.error.message}
          </p>
        )}

        {/* Text input fallback */}
        {(!voiceInput.isSupported || showTextInput) && (
          <TextFallbackInput
            onSubmit={handleSendQuestion}
            disabled={noAgentAvailable || chatStream.state.isStreaming || isLoadingGames}
          />
        )}

        {/* Toggle text input when voice is supported */}
        {voiceInput.isSupported && !showTextInput && !hasResponse && (
          <button
            type="button"
            onClick={() => setShowTextInput(true)}
            data-testid="show-text-input"
            className={cn(
              'inline-flex items-center gap-1.5 text-xs text-muted-foreground',
              'hover:text-foreground font-nunito',
              'transition-colors duration-150'
            )}
          >
            <Keyboard className="h-3.5 w-3.5" aria-hidden="true" />
            Usa la tastiera
          </button>
        )}

        {/* Response Card */}
        {(hasResponse || currentQuestion) && (
          <ResponseCard
            question={currentQuestion}
            answer={chatStream.state.currentAnswer}
            isStreaming={chatStream.state.isStreaming}
            statusMessage={chatStream.state.statusMessage}
            chatThreadId={chatStream.state.chatThreadId}
            onAskAgain={handleAskAgain}
          />
        )}
      </main>

      {/* Footer — screen reader only context */}
      <footer className="sr-only" aria-label="Quick Ask page information">
        <p>
          Quick Ask permette di fare domande vocali sulle regole del gioco selezionato. Tocca il
          microfono per iniziare.
        </p>
      </footer>
    </div>
  );
}
