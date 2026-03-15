/**
 * RagAccessBadge Component
 *
 * Visual indicator for RAG access status.
 * 3 states: locked (gray), unlocked (green), public (blue).
 */

'use client';

import React from 'react';

import { Globe, Lock, Unlock } from 'lucide-react';

export interface RagAccessBadgeProps {
  hasRagAccess: boolean;
  isRagPublic: boolean;
}

export function RagAccessBadge({ hasRagAccess, isRagPublic }: RagAccessBadgeProps) {
  if (isRagPublic) {
    return (
      <span
        className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-800"
        data-testid="rag-badge-public"
        title="RAG accessibile a tutti i possessori"
      >
        <Globe className="h-3 w-3" />
        Pubblico
      </span>
    );
  }

  if (hasRagAccess) {
    return (
      <span
        className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800"
        data-testid="rag-badge-unlocked"
        title="Accesso RAG sbloccato"
      >
        <Unlock className="h-3 w-3" />
        Sbloccato
      </span>
    );
  }

  return (
    <span
      className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600"
      data-testid="rag-badge-locked"
      title="Dichiara il possesso per sbloccare"
    >
      <Lock className="h-3 w-3" />
      Bloccato
    </span>
  );
}
