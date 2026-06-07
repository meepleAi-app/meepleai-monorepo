/**
 * StatePreview primitive — barrel exports.
 *
 * Asse B WP5 T5 (Issue #1897). DEC-4: barrel re-exports the dynamic loader
 * (not the provider impl) so consumers get tree-shake-safe access. The
 * provider implementation file (state-preview-provider) is NOT re-exported
 * here and is blocked by ESLint `no-restricted-imports`.
 */

// Canonical consumer entry points
export { StatePreviewProvider } from './state-preview-loader';
export { StatePreviewToggle } from './state-preview-toggle';
export { useStatePreview } from './use-state-preview';
export type {
  StateKind,
  StatePreviewContextValue,
  StatePreviewProviderProps,
} from './state-preview-types';
