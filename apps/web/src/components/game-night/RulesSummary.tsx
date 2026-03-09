/**
 * RulesSummary — Step 3 of Setup Wizard
 *
 * Issue #5583: Setup Wizard — guided game preparation
 *
 * Features:
 * - Game summary from RulebookAnalysis
 * - Main mechanics as chips/badges
 * - Victory conditions
 * - Most frequent FAQs
 * - "Start Game" button (handled by parent)
 * - Graceful fallback for games without analysis
 */

'use client';

import { AlertTriangle, BookOpen, HelpCircle, Lightbulb, Star, Trophy } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import type { RulebookAnalysisDto } from '@/lib/api';

interface RulesSummaryProps {
  analysis: RulebookAnalysisDto | null;
  /** Game title fallback when analysis is null */
  gameTitle?: string;
}

export function RulesSummary({ analysis, gameTitle }: RulesSummaryProps) {
  if (!analysis) {
    return (
      <div className="space-y-4">
        <div>
          <h2 className="text-lg font-semibold font-quicksand">Riepilogo Regole</h2>
          <p className="text-sm text-muted-foreground">
            Panoramica delle regole principali del gioco
          </p>
        </div>

        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 dark:bg-amber-950/20 dark:border-amber-800">
          <div className="flex items-start gap-3">
            <AlertTriangle className="h-5 w-5 text-amber-500 shrink-0 mt-0.5" />
            <div>
              <p className="font-medium text-sm text-amber-800 dark:text-amber-200">
                Nessuna analisi disponibile
              </p>
              <p className="text-sm text-amber-700 dark:text-amber-300 mt-1">
                {gameTitle
                  ? `Il gioco "${gameTitle}" non ha un regolamento analizzato.`
                  : 'Questo gioco non ha un regolamento analizzato.'}{' '}
                Il riepilogo regole non e disponibile. Puoi comunque avviare la sessione.
              </p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  const topFaqs = analysis.generatedFaqs.sort((a, b) => b.confidence - a.confidence).slice(0, 5);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-lg font-semibold font-quicksand">Riepilogo Regole</h2>
        <p className="text-sm text-muted-foreground">
          {analysis.gameTitle} — panoramica dal regolamento
        </p>
      </div>

      {/* Game summary */}
      <div className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center gap-2 mb-2">
          <BookOpen className="h-4 w-4 text-indigo-500" />
          <h3 className="text-sm font-medium">Sommario</h3>
        </div>
        <p className="text-sm text-muted-foreground leading-relaxed">{analysis.summary}</p>
      </div>

      {/* Key mechanics as badges */}
      {analysis.keyMechanics.length > 0 && (
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <Lightbulb className="h-4 w-4 text-indigo-500" />
            <h3 className="text-sm font-medium">Meccaniche Principali</h3>
          </div>
          <div className="flex flex-wrap gap-2">
            {analysis.keyMechanics.map(mechanic => (
              <Badge key={mechanic} variant="secondary" className="text-sm">
                {mechanic}
              </Badge>
            ))}
          </div>
        </div>
      )}

      {/* Victory conditions */}
      {analysis.victoryConditions && (
        <div className="rounded-xl border border-border bg-card p-4">
          <div className="flex items-center gap-2 mb-2">
            <Trophy className="h-4 w-4 text-amber-500" />
            <h3 className="text-sm font-medium">Condizioni di Vittoria</h3>
          </div>
          <p className="text-sm font-medium">{analysis.victoryConditions.primary}</p>
          {analysis.victoryConditions.alternatives.length > 0 && (
            <div className="mt-2 space-y-1">
              <p className="text-xs text-muted-foreground uppercase tracking-wide">Alternative</p>
              <ul className="list-disc list-inside space-y-0.5">
                {analysis.victoryConditions.alternatives.map((alt, i) => (
                  <li key={i} className="text-sm text-muted-foreground">
                    {alt}
                  </li>
                ))}
              </ul>
            </div>
          )}
          <div className="flex gap-2 mt-2">
            {analysis.victoryConditions.isPointBased && (
              <Badge variant="outline" className="text-xs">
                <Star className="h-3 w-3 mr-1" />A Punti
              </Badge>
            )}
            {analysis.victoryConditions.targetPoints !== null && (
              <Badge variant="outline" className="text-xs">
                Obiettivo: {analysis.victoryConditions.targetPoints} punti
              </Badge>
            )}
          </div>
        </div>
      )}

      {/* Most frequent FAQs */}
      {topFaqs.length > 0 && (
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <HelpCircle className="h-4 w-4 text-indigo-500" />
            <h3 className="text-sm font-medium">FAQ Frequenti</h3>
          </div>
          <div className="space-y-2">
            {topFaqs.map((faq, i) => (
              <details
                key={i}
                className="rounded-lg border border-border bg-card overflow-hidden group"
              >
                <summary className="flex items-start gap-2 p-3 cursor-pointer hover:bg-muted/50 transition-colors">
                  <HelpCircle className="h-4 w-4 text-muted-foreground shrink-0 mt-0.5" />
                  <span className="text-sm font-medium">{faq.question}</span>
                </summary>
                <div className="px-3 pb-3 pl-9">
                  <p className="text-sm text-muted-foreground">{faq.answer}</p>
                  {faq.tags.length > 0 && (
                    <div className="flex gap-1 mt-2">
                      {faq.tags.map(tag => (
                        <Badge key={tag} variant="outline" className="text-xs">
                          {tag}
                        </Badge>
                      ))}
                    </div>
                  )}
                </div>
              </details>
            ))}
          </div>
        </div>
      )}

      {/* Common questions (simple list from analysis) */}
      {analysis.commonQuestions.length > 0 && topFaqs.length === 0 && (
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <HelpCircle className="h-4 w-4 text-indigo-500" />
            <h3 className="text-sm font-medium">Domande Comuni</h3>
          </div>
          <ul className="list-disc list-inside space-y-1">
            {analysis.commonQuestions.slice(0, 5).map((q, i) => (
              <li key={i} className="text-sm text-muted-foreground">
                {q}
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Confidence indicator */}
      <div className="text-xs text-muted-foreground text-right">
        Affidabilita analisi: {Math.round(analysis.confidenceScore * 100)}%
      </div>
    </div>
  );
}
