'use client';

import React from 'react';

import { Upload, MessageCircle, Loader2, Sparkles } from 'lucide-react';
import Link from 'next/link';

import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { GlassCard } from '@/components/ui/surfaces/GlassCard';
import { cn } from '@/lib/utils';

export interface AiReadySectionProps {
  gameId: string;
  hasCustomPdf: boolean;
  hasRagAccess: boolean;
  onUploadClick: () => void;
}

export function AiReadySection({
  gameId,
  hasCustomPdf,
  hasRagAccess,
  onUploadClick,
}: AiReadySectionProps) {
  if (!hasCustomPdf) {
    return (
      <GlassCard className="p-4">
        <div className="flex items-center gap-3 mb-3">
          <Sparkles className="h-5 w-5 text-purple-400" />
          <h3 className="text-sm font-semibold text-[var(--gaming-text-primary)]">Regole & AI</h3>
        </div>
        <p className="mb-3 text-sm text-[var(--gaming-text-secondary)]">
          Carica il regolamento per chiedere all&apos;AI come si gioca
        </p>
        <GradientButton fullWidth onClick={onUploadClick}>
          <Upload className="h-4 w-4" />
          Carica Regolamento
        </GradientButton>
      </GlassCard>
    );
  }

  if (!hasRagAccess) {
    return (
      <GlassCard className="p-4">
        <div className="flex items-center gap-3 mb-2">
          <Loader2 className="h-5 w-5 animate-spin text-amber-400" />
          <h3 className="text-sm font-semibold text-[var(--gaming-text-primary)]">Regole & AI</h3>
        </div>
        <p className="text-sm text-[var(--gaming-text-secondary)]">
          Elaborazione del regolamento in corso...
        </p>
      </GlassCard>
    );
  }

  return (
    <GlassCard className="p-4">
      <div className="flex items-center gap-3 mb-3">
        <div className="flex h-6 w-6 items-center justify-center rounded-full bg-green-500/20">
          <Sparkles className="h-3.5 w-3.5 text-green-400" />
        </div>
        <h3 className="text-sm font-semibold text-[var(--gaming-text-primary)]">AI Pronta</h3>
      </div>
      <Link
        href={`/chat?gameId=${gameId}`}
        aria-label="Chiedi alle Regole"
        className={cn(
          'flex items-center justify-center gap-2 w-full',
          'rounded-lg bg-purple-500/20 border border-purple-500/30',
          'px-4 py-2.5 text-sm font-medium text-purple-300',
          'transition-colors hover:bg-purple-500/30'
        )}
      >
        <MessageCircle className="h-4 w-4" />
        Chiedi alle Regole
      </Link>
    </GlassCard>
  );
}
