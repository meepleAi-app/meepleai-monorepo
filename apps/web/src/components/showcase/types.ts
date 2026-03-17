/**
 * Showcase Component Types
 *
 * Core type definitions for the MeepleAI component showcase system.
 * Inspired by Storybook but integrated directly into Next.js app router.
 */

import type { ReactNode } from 'react';
import type React from 'react';

// ============================================================================
// Control Definitions
// ============================================================================

export type ControlDef =
  | { type: 'select'; options: string[]; default: string; label: string }
  | { type: 'boolean'; default: boolean; label: string }
  | { type: 'range'; min: number; max: number; step?: number; default: number; label: string }
  | { type: 'text'; default: string; label: string }
  | { type: 'number'; min?: number; max?: number; default: number; label: string };

export type ControlValue = string | boolean | number;

// ============================================================================
// Category Types
// ============================================================================

export type ShowcaseCategory =
  | 'Data Display'
  | 'Navigation'
  | 'Feedback'
  | 'Tags'
  | 'Animations'
  | 'Gates'
  | 'Forms'
  | 'Charts'
  | 'Layout'
  | 'Overlays'
  | 'Meeple'
  | 'Agent';

/** Alias for use in component registry */
export type ComponentCategory = ShowcaseCategory;

// ============================================================================
// Story Interface
// ============================================================================

export interface ShowcasePreset<TProps> {
  label: string;
  props: Partial<TProps>;
}

export interface ShowcaseStory<TProps = Record<string, unknown>> {
  /** URL slug used for routing: 'meeple-card' → /showcase/meeple-card */
  id: string;
  /** Display name shown in sidebar and page title */
  title: string;
  /** Category for grouping in sidebar */
  category: ShowcaseCategory;
  /** Optional description shown below the title */
  description?: string;
  /** The actual component to render */
  component: React.ComponentType<TProps>;
  /** Default props used on first render */
  defaultProps: TProps;
  /** Control panel definitions mapped by prop name */
  controls: Partial<Record<keyof TProps & string, ControlDef>>;
  /** Named preset configurations */
  presets?: Record<string, ShowcasePreset<TProps>>;
  /** Optional wrapper for the component (providers, padding, etc.) */
  decorator?: React.ComponentType<{ children: ReactNode }>;
}

// ============================================================================
// Runtime Props State
// ============================================================================

export type PropsState = Record<string, ControlValue>;
