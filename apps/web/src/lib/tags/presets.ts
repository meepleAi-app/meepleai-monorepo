import { Sparkles, Tag as TagIcon, Check, Heart, Brain, Eye, Code2, FileText, Clock, AlertCircle } from 'lucide-react';
import type { Tag } from '@/types/tags';

export const GAME_TAG_PRESETS: Record<string, Omit<Tag, 'id'>> = {
  new: { label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)', color: 'hsl(0 0% 100%)' },
  sale: { label: 'Sale', icon: TagIcon, bgColor: 'hsl(0 84% 60%)', color: 'hsl(0 0% 100%)' },
  owned: { label: 'Owned', icon: Check, bgColor: 'hsl(221 83% 53%)', color: 'hsl(0 0% 100%)' },
  wishlisted: { label: 'Wishlist', icon: Heart, bgColor: 'hsl(350 89% 60%)', color: 'hsl(0 0% 100%)' }
};

export const AGENT_TAG_PRESETS: Record<string, Omit<Tag, 'id'>> = {
  rag: { label: 'RAG', icon: Brain, bgColor: 'hsl(38 92% 50%)', color: 'hsl(0 0% 100%)' },
  vision: { label: 'Vision', icon: Eye, bgColor: 'hsl(262 83% 58%)', color: 'hsl(0 0% 100%)' },
  code: { label: 'Code', icon: Code2, bgColor: 'hsl(210 40% 55%)', color: 'hsl(0 0% 100%)' }
};

export const DOCUMENT_TAG_PRESETS: Record<string, Omit<Tag, 'id'>> = {
  pdf: { label: 'PDF', icon: FileText, bgColor: 'hsl(0 84% 60%)', color: 'hsl(0 0% 100%)' },
  processing: { label: 'Processing', icon: Clock, bgColor: 'hsl(38 92% 50%)', color: 'hsl(0 0% 100%)' },
  ready: { label: 'Ready', icon: Check, bgColor: 'hsl(142 76% 36%)', color: 'hsl(0 0% 100%)' },
  failed: { label: 'Failed', icon: AlertCircle, bgColor: 'hsl(0 84% 60%)', color: 'hsl(0 0% 100%)' }
};

export function createTagsFromKeys(entityType: string, keys: string[]): Tag[] {
  const presets = { game: GAME_TAG_PRESETS, agent: AGENT_TAG_PRESETS, document: DOCUMENT_TAG_PRESETS }[entityType];
  if (!presets) return [];
  return keys.map(k => ({ id: k, ...presets[k] })).filter(Boolean) as Tag[];
}
