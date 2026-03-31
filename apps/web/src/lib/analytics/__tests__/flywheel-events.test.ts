import { vi, describe, it, expect, beforeEach } from 'vitest';

vi.mock('@/lib/analytics/track-event', () => ({
  trackEvent: vi.fn(),
}));

import { trackEvent } from '@/lib/analytics/track-event';
import {
  trackSignUp,
  trackGameAdded,
  trackPdfUploaded,
  trackRulesChatStarted,
  trackSessionCreated,
  trackSessionShared,
} from '../flywheel-events';

beforeEach(() => vi.clearAllMocks());

describe('flywheel events', () => {
  it('trackSignUp calls trackEvent with flywheel_sign_up', () => {
    trackSignUp({ method: 'email' });
    expect(trackEvent).toHaveBeenCalledWith('flywheel_sign_up', { method: 'email' });
  });

  it('trackGameAdded calls trackEvent with flywheel_game_added', () => {
    trackGameAdded({ gameId: 'g1', source: 'catalog' });
    expect(trackEvent).toHaveBeenCalledWith('flywheel_game_added', {
      gameId: 'g1',
      source: 'catalog',
    });
  });

  it('trackPdfUploaded calls trackEvent with flywheel_pdf_uploaded', () => {
    trackPdfUploaded({ gameId: 'g1' });
    expect(trackEvent).toHaveBeenCalledWith('flywheel_pdf_uploaded', { gameId: 'g1' });
  });

  it('trackRulesChatStarted calls trackEvent with flywheel_rules_chat_started', () => {
    trackRulesChatStarted({ promptType: 'rule_dispute' });
    expect(trackEvent).toHaveBeenCalledWith('flywheel_rules_chat_started', {
      promptType: 'rule_dispute',
    });
  });

  it('trackSessionCreated calls trackEvent with flywheel_session_created', () => {
    trackSessionCreated({ gameId: 'g1', playerCount: 4 });
    expect(trackEvent).toHaveBeenCalledWith('flywheel_session_created', {
      gameId: 'g1',
      playerCount: 4,
    });
  });

  it('trackSessionShared calls trackEvent with flywheel_session_shared', () => {
    trackSessionShared({ sessionId: 's1', method: 'invite_code' });
    expect(trackEvent).toHaveBeenCalledWith('flywheel_session_shared', {
      sessionId: 's1',
      method: 'invite_code',
    });
  });
});
