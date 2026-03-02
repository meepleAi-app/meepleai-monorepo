'use client';

import { useEffect, useState, useCallback } from 'react';

import { PartyPopper, ArrowRight, Sparkles } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';

const REDIRECT_DELAY_MS = 2000;
const PROGRESS_INTERVAL_MS = 50;

export function WelcomeFallback() {
  return (
    <main className="min-h-dvh flex items-center justify-center bg-gradient-to-br from-amber-50 via-orange-50 to-red-50 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900">
      <div className="text-center space-y-4">
        <div className="animate-pulse">
          <PartyPopper className="h-16 w-16 text-primary/50 mx-auto" />
        </div>
        <p className="text-slate-500 animate-pulse">Caricamento...</p>
      </div>
    </main>
  );
}

export function WelcomeContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const redirectTo = searchParams?.get('redirectTo') ?? '/dashboard';

  const [progress, setProgress] = useState(0);
  const [mounted, setMounted] = useState(false);

  const handleRedirect = useCallback(() => {
    router.push(redirectTo);
  }, [router, redirectTo]);

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (!mounted) return;

    // Progress bar animation
    const progressIncrement = (PROGRESS_INTERVAL_MS / REDIRECT_DELAY_MS) * 100;
    const progressTimer = setInterval(() => {
      setProgress(prev => {
        const next = prev + progressIncrement;
        if (next >= 100) {
          clearInterval(progressTimer);
          return 100;
        }
        return next;
      });
    }, PROGRESS_INTERVAL_MS);

    // Redirect timer
    const redirectTimer = setTimeout(() => {
      handleRedirect();
    }, REDIRECT_DELAY_MS);

    return () => {
      clearInterval(progressTimer);
      clearTimeout(redirectTimer);
    };
  }, [mounted, handleRedirect]);

  if (!mounted) {
    return <WelcomeFallback />;
  }

  return (
    <main
      className="min-h-dvh flex items-center justify-center bg-gradient-to-br from-amber-50 via-orange-50 to-red-50 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 p-4"
      role="main"
      aria-label="Welcome page"
    >
      <div className="max-w-md w-full text-center space-y-8">
        {/* Celebration Icon */}
        <div className="relative inline-flex items-center justify-center">
          <div className="absolute inset-0 animate-ping rounded-full bg-primary/20" />
          <div className="relative bg-primary/10 dark:bg-primary/20 rounded-full p-6">
            <PartyPopper className="h-16 w-16 text-primary animate-bounce" aria-hidden="true" />
          </div>
          <Sparkles
            className="absolute -top-2 -right-2 h-8 w-8 text-yellow-500 animate-pulse"
            aria-hidden="true"
          />
        </div>

        {/* Welcome Message */}
        <div className="space-y-3">
          <h1 className="text-3xl md:text-4xl font-bold font-quicksand text-slate-900 dark:text-white">
            Benvenuto in MeepleAI! 🎉
          </h1>
          <p className="text-lg text-slate-600 dark:text-slate-300">
            Il tuo account è stato creato con successo.
          </p>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Tra pochi secondi verrai reindirizzato alla dashboard...
          </p>
        </div>

        {/* Progress Bar */}
        <div className="space-y-2" role="status" aria-live="polite">
          <Progress value={progress} className="h-2 w-full" aria-label="Redirect progress" />
          <p className="text-xs text-slate-400 dark:text-slate-500">Reindirizzamento in corso...</p>
        </div>

        {/* Manual Redirect Button */}
        <Button
          onClick={handleRedirect}
          size="lg"
          className="group"
          data-testid="welcome-go-dashboard"
        >
          Vai alla Dashboard
          <ArrowRight className="ml-2 h-4 w-4 transition-transform group-hover:translate-x-1" />
        </Button>

        {/* Features Preview */}
        <div className="pt-4 border-t border-slate-200 dark:border-slate-700">
          <p className="text-sm text-slate-500 dark:text-slate-400 mb-3">
            Cosa puoi fare con MeepleAI:
          </p>
          <div className="flex flex-wrap justify-center gap-2 text-xs">
            <span className="px-3 py-1 bg-white dark:bg-slate-800 rounded-full text-slate-600 dark:text-slate-300 shadow-sm">
              📚 Regole dei giochi
            </span>
            <span className="px-3 py-1 bg-white dark:bg-slate-800 rounded-full text-slate-600 dark:text-slate-300 shadow-sm">
              🤖 Assistente AI
            </span>
            <span className="px-3 py-1 bg-white dark:bg-slate-800 rounded-full text-slate-600 dark:text-slate-300 shadow-sm">
              🎲 Libreria giochi
            </span>
          </div>
        </div>
      </div>
    </main>
  );
}
