/**
 * VoiceInputButton - Voice-to-text input using Web Speech API (Issue #3351)
 *
 * Features:
 * - Web Speech API integration (SpeechRecognition)
 * - Visual feedback while listening (pulsing animation)
 * - Automatic language detection (IT/EN)
 * - Graceful fallback for unsupported browsers
 * - Interim results for live transcription
 */

import React, { useState, useCallback, useEffect, useRef } from 'react';

import { Mic, MicOff, Loader2 } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/data-display/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// Types for Web Speech API
interface SpeechRecognitionEvent extends Event {
  results: SpeechRecognitionResultList;
  resultIndex: number;
}

interface SpeechRecognitionErrorEvent extends Event {
  error: string;
  message?: string;
}

interface SpeechRecognitionResult {
  isFinal: boolean;
  0: SpeechRecognitionAlternative;
  length: number;
}

interface SpeechRecognitionAlternative {
  transcript: string;
  confidence: number;
}

interface SpeechRecognitionResultList {
  length: number;
  item(index: number): SpeechRecognitionResult;
  [index: number]: SpeechRecognitionResult;
}

interface SpeechRecognition extends EventTarget {
  continuous: boolean;
  interimResults: boolean;
  lang: string;
  maxAlternatives: number;
  start(): void;
  stop(): void;
  abort(): void;
  onresult: ((event: SpeechRecognitionEvent) => void) | null;
  onerror: ((event: SpeechRecognitionErrorEvent) => void) | null;
  onend: (() => void) | null;
  onstart: (() => void) | null;
}

declare global {
  interface Window {
    SpeechRecognition?: new () => SpeechRecognition;
    webkitSpeechRecognition?: new () => SpeechRecognition;
  }
}

export interface VoiceInputButtonProps {
  /** Callback when transcription is received */
  onTranscript: (transcript: string) => void;
  /** Callback when interim results are received (optional) */
  onInterimTranscript?: (transcript: string) => void;
  /** Whether input is disabled */
  disabled?: boolean;
  /** Preferred language (defaults to browser language or 'it-IT') */
  language?: string;
  /** Custom class name */
  className?: string;
}

/**
 * Check if Web Speech API is supported
 */
function isSpeechRecognitionSupported(): boolean {
  return typeof window !== 'undefined' &&
    (!!window.SpeechRecognition || !!window.webkitSpeechRecognition);
}

/**
 * Get SpeechRecognition constructor
 */
function getSpeechRecognition(): (new () => SpeechRecognition) | null {
  if (typeof window === 'undefined') return null;
  return window.SpeechRecognition || window.webkitSpeechRecognition || null;
}

/**
 * Detect browser language, defaulting to Italian
 */
function detectLanguage(): string {
  if (typeof navigator === 'undefined') return 'it-IT';
  const browserLang = navigator.language || 'it-IT';
  // Support IT and EN primarily
  if (browserLang.startsWith('en')) return 'en-US';
  if (browserLang.startsWith('it')) return 'it-IT';
  return browserLang;
}

export function VoiceInputButton({
  onTranscript,
  onInterimTranscript,
  disabled = false,
  language,
  className,
}: VoiceInputButtonProps) {
  const [isListening, setIsListening] = useState(false);
  const [isSupported, setIsSupported] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const recognitionRef = useRef<SpeechRecognition | null>(null);

  // Check support on mount
  useEffect(() => {
    setIsSupported(isSpeechRecognitionSupported());
  }, []);

  /**
   * Initialize speech recognition
   */
  const initRecognition = useCallback(() => {
    const SpeechRecognitionClass = getSpeechRecognition();
    if (!SpeechRecognitionClass) return null;

    const recognition = new SpeechRecognitionClass();
    recognition.continuous = false;
    recognition.interimResults = true;
    recognition.lang = language || detectLanguage();
    recognition.maxAlternatives = 1;

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      let interimTranscript = '';
      let finalTranscript = '';

      for (let i = event.resultIndex; i < event.results.length; i++) {
        // eslint-disable-next-line security/detect-object-injection
        const result = event.results[i];
        const transcript = result[0].transcript;

        if (result.isFinal) {
          finalTranscript += transcript;
        } else {
          interimTranscript += transcript;
        }
      }

      // Emit interim results for live preview
      if (interimTranscript && onInterimTranscript) {
        onInterimTranscript(interimTranscript);
      }

      // Emit final transcript
      if (finalTranscript) {
        onTranscript(finalTranscript);
      }
    };

    recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
      console.error('Speech recognition error:', event.error);

      // Map error codes to user-friendly messages
      const errorMessages: Record<string, string> = {
        'no-speech': 'Nessun audio rilevato. Riprova.',
        'audio-capture': 'Microfono non disponibile.',
        'not-allowed': 'Accesso al microfono negato.',
        'network': 'Errore di rete. Controlla la connessione.',
        'aborted': 'Riconoscimento interrotto.',
        'language-not-supported': 'Lingua non supportata.',
      };

      setError(errorMessages[event.error] || `Errore: ${event.error}`);
      setIsListening(false);
    };

    recognition.onend = () => {
      setIsListening(false);
    };

    recognition.onstart = () => {
      setError(null);
      setIsListening(true);
    };

    return recognition;
  }, [language, onTranscript, onInterimTranscript]);

  /**
   * Start listening
   */
  const startListening = useCallback(() => {
    if (!isSupported || disabled) return;

    // Stop any existing recognition
    if (recognitionRef.current) {
      recognitionRef.current.abort();
    }

    // Initialize and start new recognition
    const recognition = initRecognition();
    if (recognition) {
      recognitionRef.current = recognition;
      try {
        recognition.start();
      } catch (err) {
        console.error('Failed to start recognition:', err);
        setError('Impossibile avviare il riconoscimento vocale.');
      }
    }
  }, [isSupported, disabled, initRecognition]);

  /**
   * Stop listening
   */
  const stopListening = useCallback(() => {
    if (recognitionRef.current) {
      recognitionRef.current.stop();
      recognitionRef.current = null;
    }
    setIsListening(false);
  }, []);

  /**
   * Toggle listening state
   */
  const handleToggle = useCallback(() => {
    if (isListening) {
      stopListening();
    } else {
      startListening();
    }
  }, [isListening, startListening, stopListening]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (recognitionRef.current) {
        recognitionRef.current.abort();
      }
    };
  }, []);

  // Not supported - show disabled button with tooltip
  if (!isSupported) {
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              type="button"
              variant="outline"
              size="icon"
              disabled
              className={className}
              aria-label="Riconoscimento vocale non supportato"
            >
              <MicOff className="h-4 w-4 text-muted-foreground" />
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>Il riconoscimento vocale non è supportato in questo browser</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            type="button"
            variant={isListening ? 'default' : 'outline'}
            size="icon"
            onClick={handleToggle}
            disabled={disabled}
            className={cn(
              className,
              isListening && 'bg-red-500 hover:bg-red-600 animate-pulse',
              error && 'border-red-500'
            )}
            aria-label={isListening ? 'Interrompi ascolto' : 'Inizia riconoscimento vocale'}
            aria-pressed={isListening}
          >
            {isListening ? (
              <Loader2 className="h-4 w-4 animate-spin text-white" />
            ) : (
              <Mic className={cn('h-4 w-4', error && 'text-red-500')} />
            )}
          </Button>
        </TooltipTrigger>
        <TooltipContent>
          {error ? (
            <p className="text-red-500">{error}</p>
          ) : isListening ? (
            <p>In ascolto... Clicca per fermare</p>
          ) : (
            <p>Clicca per parlare</p>
          )}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
