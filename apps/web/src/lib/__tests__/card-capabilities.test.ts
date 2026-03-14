import {
  type CardCapabilities,
  type CardCapabilityContext,
  resolveCapabilities,
  requires,
} from '../card-capabilities';

describe('CardCapabilityProvider', () => {
  const baseContext: CardCapabilityContext = {
    entity: 'game',
    hasAgent: true,
    isOnline: true,
    isAdmin: false,
    isEditor: true,
    agentStatus: 'ready',
    kbStatus: 'indexed',
    sessionState: undefined,
  };

  describe('resolveCapabilities', () => {
    it('should enable AI chat when agent is ready and online', () => {
      const caps = resolveCapabilities(baseContext);
      expect(caps.aiChat).toBe(true);
    });

    it('should disable AI chat when no agent', () => {
      const caps = resolveCapabilities({ ...baseContext, hasAgent: false });
      expect(caps.aiChat).toBe(false);
    });

    it('should disable AI chat when offline', () => {
      const caps = resolveCapabilities({ ...baseContext, isOnline: false });
      expect(caps.aiChat).toBe(false);
    });

    it('should enable reindex for admin when not processing', () => {
      const ctx: CardCapabilityContext = { ...baseContext, entity: 'kb', isAdmin: true };
      const caps = resolveCapabilities(ctx);
      expect(caps.reindex).toBe(true);
    });

    it('should disable reindex for non-admin', () => {
      const ctx: CardCapabilityContext = { ...baseContext, entity: 'kb', isAdmin: false };
      const caps = resolveCapabilities(ctx);
      expect(caps.reindex).toBe(false);
    });

    it('should enable session scoring during active session', () => {
      const ctx: CardCapabilityContext = {
        ...baseContext,
        entity: 'session',
        sessionState: 'active',
      };
      const caps = resolveCapabilities(ctx);
      expect(caps.scoring).toBe(true);
    });

    it('should disable session scoring when session is completed', () => {
      const ctx: CardCapabilityContext = {
        ...baseContext,
        entity: 'session',
        sessionState: 'completed',
      };
      const caps = resolveCapabilities(ctx);
      expect(caps.scoring).toBe(false);
    });
  });

  describe('requires helper', () => {
    it('should return true when all required capabilities are enabled', () => {
      const caps: CardCapabilities = {
        aiChat: true, reindex: false, scoring: true,
        download: true, delete: false, flip: true,
        drawer: true, navigate: true, quickActions: true,
      };
      expect(requires(caps, 'aiChat', 'scoring')).toBe(true);
    });

    it('should return false when any required capability is disabled', () => {
      const caps: CardCapabilities = {
        aiChat: true, reindex: false, scoring: true,
        download: true, delete: false, flip: true,
        drawer: true, navigate: true, quickActions: true,
      };
      expect(requires(caps, 'aiChat', 'reindex')).toBe(false);
    });
  });
});
