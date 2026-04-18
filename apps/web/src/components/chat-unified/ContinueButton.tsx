'use client';

import { ArrowRight } from 'lucide-react';
import { cn } from '@/lib/utils';

interface ContinueButtonProps {
  onContinue: () => void;
  isLoading: boolean;
}

export function ContinueButton({ onContinue, isLoading }: ContinueButtonProps) {
  return (
    <button
      onClick={onContinue}
      disabled={isLoading}
      className={cn(
        'mt-3 inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-nunito font-medium transition-all',
        isLoading
          ? 'bg-muted text-muted-foreground cursor-not-allowed'
          : 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border border-amber-500/30 hover:bg-amber-500/20 hover:border-amber-500/50 cursor-pointer'
      )}
      data-testid="continue-button"
    >
      {isLoading ? (
        <>
          <div className="h-3.5 w-3.5 border-2 border-amber-500/30 border-t-amber-500 rounded-full animate-spin" />
          Continuando...
        </>
      ) : (
        <>
          Continua la risposta
          <ArrowRight className="h-3.5 w-3.5" />
        </>
      )}
    </button>
  );
}
