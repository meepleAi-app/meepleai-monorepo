'use client';

import { useState, useCallback } from 'react';

import { Play, Loader2, CheckCircle2, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';

import type { AutoTestResult } from './types';

interface AutoTestRunnerProps {
  onComplete: (results: AutoTestResult[]) => void;
  disabled: boolean;
}

const AUTO_TEST_QUESTIONS = [
  'Come si prepara il gioco?',
  'Quanti giocatori possono giocare?',
  'Come si vince?',
  'Quali sono le regole base?',
  'Come funziona il turno di gioco?',
  'Ci sono espansioni?',
  'Quanto dura una partita?',
  'Quali componenti include il gioco?',
] as const;

const MOCK_ANSWERS = [
  'Il gioco si prepara distribuendo le carte e posizionando il tabellone al centro del tavolo.',
  'Il gioco supporta da 2 a 5 giocatori.',
  'Vince il giocatore che accumula il maggior numero di punti vittoria.',
  'Ogni turno si compone di tre fasi: pesca, azione e risoluzione.',
  'Il turno si articola in fase di pesca, azione principale e fase di mantenimento.',
  'Sono disponibili due espansioni che aggiungono nuove meccaniche.',
  'Una partita dura tipicamente 45-60 minuti.',
  'Il gioco include carte, segnalini, dadi e un tabellone.',
];

export function AutoTestRunner({ onComplete, disabled }: AutoTestRunnerProps) {
  const [isRunning, setIsRunning] = useState(false);
  const [progress, setProgress] = useState(0);
  const [currentResults, setCurrentResults] = useState<AutoTestResult[]>([]);

  const runTests = useCallback(async () => {
    setIsRunning(true);
    setProgress(0);
    setCurrentResults([]);

    const results: AutoTestResult[] = [];

    for (let i = 0; i < AUTO_TEST_QUESTIONS.length; i++) {
      await new Promise(resolve => setTimeout(resolve, 300));

      const confidence = Math.round((0.3 + Math.random() * 0.6) * 100) / 100;
      const latencyMs = Math.round(400 + Math.random() * 800);

      const result: AutoTestResult = {
        question: AUTO_TEST_QUESTIONS[i],
        answer: MOCK_ANSWERS[i],
        confidence,
        passed: confidence > 0.5,
        latencyMs,
      };

      results.push(result);
      setCurrentResults([...results]);
      setProgress(((i + 1) / AUTO_TEST_QUESTIONS.length) * 100);
    }

    setIsRunning(false);
    onComplete(results);
  }, [onComplete]);

  return (
    <div className="space-y-3" data-testid="auto-test-runner">
      {/* Control bar */}
      <div className="flex items-center gap-3">
        <Button
          onClick={runTests}
          disabled={disabled || isRunning}
          className="bg-amber-500 hover:bg-amber-600 text-white"
          size="sm"
          data-testid="run-auto-test-button"
        >
          {isRunning ? (
            <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
          ) : (
            <Play className="mr-1.5 h-4 w-4" />
          )}
          {isRunning ? 'Esecuzione...' : 'Esegui Auto-Test'}
        </Button>

        {isRunning && (
          <span className="font-nunito text-xs text-muted-foreground" data-testid="progress-label">
            {currentResults.length}/{AUTO_TEST_QUESTIONS.length}
          </span>
        )}
      </div>

      {/* Progress bar */}
      {isRunning && <Progress value={progress} className="h-2" data-testid="auto-test-progress" />}

      {/* Live results */}
      {currentResults.length > 0 && isRunning && (
        <div className="space-y-1.5" data-testid="live-results">
          {currentResults.map((result, i) => (
            <div
              key={i}
              className="flex items-center gap-2 rounded-md border bg-white/50 px-2.5 py-1.5 text-xs font-nunito"
            >
              {result.passed ? (
                <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-green-600" />
              ) : (
                <XCircle className="h-3.5 w-3.5 shrink-0 text-red-500" />
              )}
              <span className="flex-1 truncate">{result.question}</span>
              <Badge
                variant="outline"
                className={`text-[10px] px-1.5 py-0 ${
                  result.passed ? 'border-green-300 text-green-700' : 'border-red-300 text-red-700'
                }`}
              >
                {(result.confidence * 100).toFixed(0)}%
              </Badge>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
