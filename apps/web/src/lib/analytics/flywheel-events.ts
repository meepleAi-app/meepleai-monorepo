import { trackEvent } from './track-event';

/**
 * Flywheel Event Tracking
 *
 * Tracks the six key steps of the MeepleAI user engagement flywheel:
 *   1. sign_up → 2. game_added → 3. pdf_uploaded → 4. rules_chat_started
 *   → 5. session_created → 6. session_shared
 */

export function trackSignUp(props: { method: 'email' | 'google' | 'github' }): void {
  trackEvent('flywheel_sign_up', props);
}

export function trackGameAdded(props: {
  gameId: string;
  source: 'catalog' | 'private' | 'bgg';
}): void {
  trackEvent('flywheel_game_added', props);
}

export function trackPdfUploaded(props: { gameId: string; fileSizeKb?: number }): void {
  trackEvent('flywheel_pdf_uploaded', props);
}

export function trackRulesChatStarted(props: {
  gameId?: string;
  promptType: 'rule_dispute' | 'setup' | 'general' | 'suggestion';
}): void {
  trackEvent('flywheel_rules_chat_started', props);
}

export function trackSessionCreated(props: { gameId: string; playerCount: number }): void {
  trackEvent('flywheel_session_created', props);
}

export function trackSessionShared(props: {
  sessionId: string;
  method: 'invite_code' | 'link';
}): void {
  trackEvent('flywheel_session_shared', props);
}
