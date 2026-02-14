/**
 * Tag Presets - Entity-Specific Tag Configurations
 * Issue #4181 - Vertical Tag Component
 *
 * Provides pre-configured tags for different entity types (Game, Agent, Document)
 * with colors, icons, and labels optimized for the vertical tag strip.
 */

import {
  AlertCircle,
  Brain,
  CheckCheck,
  CheckCircle,
  Code,
  Eye,
  FileText,
  Heart,
  Loader2,
  Sparkles,
  Tag as TagIcon,
  type LucideIcon,
} from 'lucide-react';

export type TagPresetKey =
  // Game tags
  | 'new'
  | 'sale'
  | 'owned'
  | 'wishlist'
  // Agent tags
  | 'rag'
  | 'vision'
  | 'code'
  // Document tags
  | 'pdf'
  | 'processing'
  | 'ready'
  | 'failed';

export interface TagConfig {
  key: TagPresetKey;
  label: string;
  abbr: string; // abbreviated label for tablet
  color: string; // Tailwind color class (e.g., 'green')
  bgClass: string;
  textClass: string;
  icon: LucideIcon;
  description: string;
}

/**
 * Game entity tag presets
 */
export const gameTagPresets: Record<string, TagConfig> = {
  new: {
    key: 'new',
    label: 'New',
    abbr: 'New',
    color: 'green',
    bgClass: 'bg-green-500 dark:bg-green-600',
    textClass: 'text-white',
    icon: Sparkles,
    description: 'Nuova aggiunta',
  },
  sale: {
    key: 'sale',
    label: 'Sale',
    abbr: 'Sale',
    color: 'red',
    bgClass: 'bg-red-500 dark:bg-red-600',
    textClass: 'text-white',
    icon: TagIcon,
    description: 'In promozione',
  },
  owned: {
    key: 'owned',
    label: 'Owned',
    abbr: 'Own',
    color: 'blue',
    bgClass: 'bg-blue-500 dark:bg-blue-600',
    textClass: 'text-white',
    icon: CheckCircle,
    description: 'Posseduto',
  },
  wishlist: {
    key: 'wishlist',
    label: 'Wishlist',
    abbr: 'Wish',
    color: 'rose',
    bgClass: 'bg-rose-500 dark:bg-rose-600',
    textClass: 'text-white',
    icon: Heart,
    description: 'Nella wishlist',
  },
};

/**
 * Agent entity tag presets
 */
export const agentTagPresets: Record<string, TagConfig> = {
  rag: {
    key: 'rag',
    label: 'RAG',
    abbr: 'RAG',
    color: 'amber',
    bgClass: 'bg-amber-500 dark:bg-amber-600',
    textClass: 'text-white',
    icon: Brain,
    description: 'Retrieval Augmented Generation',
  },
  vision: {
    key: 'vision',
    label: 'Vision',
    abbr: 'Vis',
    color: 'purple',
    bgClass: 'bg-purple-500 dark:bg-purple-600',
    textClass: 'text-white',
    icon: Eye,
    description: 'Computer Vision capabilities',
  },
  code: {
    key: 'code',
    label: 'Code',
    abbr: 'Cod',
    color: 'slate',
    bgClass: 'bg-slate-500 dark:bg-slate-600',
    textClass: 'text-white',
    icon: Code,
    description: 'Code generation & execution',
  },
};

/**
 * Document entity tag presets
 */
export const documentTagPresets: Record<string, TagConfig> = {
  pdf: {
    key: 'pdf',
    label: 'PDF',
    abbr: 'PDF',
    color: 'red',
    bgClass: 'bg-red-500 dark:bg-red-600',
    textClass: 'text-white',
    icon: FileText,
    description: 'PDF Document',
  },
  processing: {
    key: 'processing',
    label: 'Processing',
    abbr: 'Proc',
    color: 'yellow',
    bgClass: 'bg-yellow-500 dark:bg-yellow-600',
    textClass: 'text-white',
    icon: Loader2,
    description: 'Document processing in progress',
  },
  ready: {
    key: 'ready',
    label: 'Ready',
    abbr: 'Rdy',
    color: 'green',
    bgClass: 'bg-green-500 dark:bg-green-600',
    textClass: 'text-white',
    icon: CheckCheck,
    description: 'Ready for use',
  },
  failed: {
    key: 'failed',
    label: 'Failed',
    abbr: 'Err',
    color: 'red',
    bgClass: 'bg-red-500 dark:bg-red-600',
    textClass: 'text-white',
    icon: AlertCircle,
    description: 'Processing failed',
  },
};

/**
 * Combined tag presets for all entity types
 */
export const tagPresets = {
  ...gameTagPresets,
  ...agentTagPresets,
  ...documentTagPresets,
} as const;

/**
 * Get tag preset by key with fallback to custom tag
 */
export function getTagPreset(key: string): TagConfig | undefined {
  return tagPresets[key as TagPresetKey];
}

/**
 * Entity type to tag preset mapping
 */
export const entityTagMapping = {
  game: Object.keys(gameTagPresets),
  agent: Object.keys(agentTagPresets),
  document: Object.keys(documentTagPresets),
} as const;
