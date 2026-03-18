/**
 * Game Detail Components Export (Issue #3152, #3156)
 */

export {
  AgentChatPanel,
  type AgentChatPanelProps,
  type ChatMessage,
  type AgentMode,
  type GamePdf,
} from './AgentChatPanel';
export {
  MeeplePdfReferenceCard,
  type MeeplePdfReferenceCardProps,
  type PdfReference,
} from './MeeplePdfReferenceCard';
/**
 * @deprecated Use MeeplePdfReferenceCard instead. Kept for backward compatibility.
 */
export { PdfReferenceCard, type PdfReferenceCardProps } from './PdfReferenceCard';
export { TypingIndicator } from './TypingIndicator';
export { SplitViewLayout, type SplitViewLayoutProps } from './SplitViewLayout';
export { GameHeroSection } from './game-hero-section';
export { StatsGrid } from './stats-grid';
