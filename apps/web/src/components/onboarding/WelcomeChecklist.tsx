'use client';

import { useEffect, useRef } from 'react';

import { useReducedMotion } from 'framer-motion';
import { CheckCircle2, X } from 'lucide-react';
import Link from 'next/link';

import { ONBOARDING_STEPS } from './onboarding-steps';
import { useOnboardingStatus } from './use-onboarding-status';

export function WelcomeChecklist() {
  const { steps, completedCount, totalSteps, dismiss } = useOnboardingStatus();
  const shouldReduceMotion = useReducedMotion();
  const autoDismissRef = useRef<NodeJS.Timeout | null>(null);

  const allComplete = completedCount === totalSteps;
  const progressPct = (completedCount / totalSteps) * 100;

  // Auto-dismiss after 5s when all steps complete
  useEffect(() => {
    if (allComplete) {
      autoDismissRef.current = setTimeout(() => dismiss(), shouldReduceMotion ? 0 : 5000);
    }
    return () => {
      if (autoDismissRef.current) clearTimeout(autoDismissRef.current);
    };
  }, [allComplete, dismiss, shouldReduceMotion]);

  if (allComplete) {
    return (
      <div className="rounded-2xl overflow-hidden border border-green-200 bg-white/70 backdrop-blur-md shadow-sm">
        <div className="bg-gradient-to-r from-green-50 to-emerald-50 px-5 py-4 flex items-center justify-between">
          <div>
            <h3 className="font-quicksand font-bold text-base">🎉 Tutto completato!</h3>
            <p className="text-xs text-green-800 font-nunito mt-0.5">
              {completedCount} di {totalSteps} — sei pronto per giocare!
            </p>
          </div>
          <div className="flex items-center gap-3">
            <div className="w-28 h-1.5 bg-green-200 rounded-full">
              <div className="h-full bg-green-500 rounded-full w-full" />
            </div>
          </div>
        </div>
        <div className="p-5 text-center">
          <p className="text-sm text-muted-foreground font-nunito">
            Hai completato tutti i passaggi. Buon divertimento! 🎲
          </p>
          <button
            onClick={() => dismiss()}
            className="mt-3 inline-flex items-center px-4 py-2 rounded-lg bg-green-500 text-white font-quicksand font-semibold text-sm hover:bg-green-600 transition-colors"
          >
            Chiudi checklist
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-2xl overflow-hidden border border-amber-200/50 bg-white/70 backdrop-blur-md shadow-sm">
      {/* Header */}
      <div className="bg-gradient-to-r from-amber-50 to-yellow-50 px-5 py-4 flex items-center justify-between">
        <div>
          <h3 className="font-quicksand font-bold text-base">🎯 Inizia con MeepleAI</h3>
          <p className="text-xs text-amber-800 font-nunito mt-0.5">
            {completedCount} di {totalSteps} completati
            {completedCount > 0 && ' — ottimo lavoro!'}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="w-28 h-1.5 bg-amber-200/60 rounded-full">
            <div
              className="h-full bg-amber-500 rounded-full transition-all duration-500"
              style={{ width: `${progressPct}%` }}
            />
          </div>
          <button
            onClick={() => dismiss()}
            className="text-muted-foreground/50 hover:text-foreground transition-colors"
            aria-label="Chiudi checklist"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Steps */}
      <div className="px-5 py-3">
        {ONBOARDING_STEPS.map((stepConfig, idx) => {
          const isComplete = steps[stepConfig.id];
          const Icon = stepConfig.icon;
          const isNextPending =
            !isComplete && ONBOARDING_STEPS.slice(0, idx).every(s => steps[s.id]);

          if (isComplete) {
            return (
              <div key={stepConfig.id} className="flex items-center gap-3 py-2.5 opacity-60">
                <CheckCircle2 className="w-5 h-5 text-green-500 shrink-0" />
                <span className="text-sm font-nunito font-semibold line-through text-muted-foreground">
                  {stepConfig.title}
                </span>
              </div>
            );
          }

          return (
            <Link
              key={stepConfig.id}
              href={stepConfig.href}
              className={`flex items-center gap-3 py-2.5 rounded-lg px-2 -mx-2 hover:bg-amber-50/50 transition-colors group ${
                isNextPending ? 'ring-1 ring-amber-300/50' : ''
              }`}
            >
              <span
                className={`w-5 h-5 rounded-full border-2 shrink-0 flex items-center justify-center ${
                  isNextPending ? 'border-amber-400 animate-pulse' : 'border-muted-foreground/30'
                }`}
              >
                <Icon className="w-3 h-3 text-muted-foreground/50" />
              </span>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-nunito font-semibold">{stepConfig.title}</p>
                <p className="text-xs text-muted-foreground font-nunito">
                  {stepConfig.description}
                </p>
              </div>
              <span className="text-muted-foreground/30 group-hover:text-amber-500 transition-colors">
                →
              </span>
            </Link>
          );
        })}
      </div>

      {/* Footer */}
      <div className="px-5 pb-3 text-center">
        <button
          onClick={() => dismiss()}
          className="text-xs text-muted-foreground/50 hover:text-foreground underline transition-colors font-nunito"
        >
          Non mostrare più
        </button>
      </div>
    </div>
  );
}
