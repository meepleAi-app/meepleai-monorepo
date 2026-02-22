/**
 * Admin AI Hub Layout
 * Issue #5040 — Admin Route Consolidation
 *
 * Consolidates: agents, typologies, definitions, ai-lab, prompts,
 *               ai-models, ai-requests, rag, rag-executions
 */

'use client';

import { type ReactNode } from 'react';

import { Bot, Cpu, Database, FlaskConical, Layers, MessageSquare, Sliders, Zap } from 'lucide-react';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function AdminAiLayout({ children }: { children: ReactNode }) {
  useSetNavConfig({
    miniNav: [
      { id: 'agents', label: 'Agenti', href: '/admin/ai', icon: Bot },
      { id: 'typologies', label: 'Tipologie', href: '/admin/ai?tab=typologies', icon: Layers },
      { id: 'definitions', label: 'Definizioni', href: '/admin/ai?tab=definitions', icon: Sliders },
      { id: 'lab', label: 'Lab', href: '/admin/ai?tab=lab', icon: FlaskConical },
      { id: 'prompts', label: 'Prompt', href: '/admin/ai?tab=prompts', icon: MessageSquare },
      { id: 'models', label: 'Modelli', href: '/admin/ai?tab=models', icon: Cpu },
      { id: 'requests', label: 'Richieste', href: '/admin/ai?tab=requests', icon: Zap },
      { id: 'rag', label: 'RAG', href: '/admin/ai?tab=rag', icon: Database },
    ],
  });

  return <>{children}</>;
}
