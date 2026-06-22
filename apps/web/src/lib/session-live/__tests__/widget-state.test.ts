import { describe, it, expect, vi } from 'vitest';
import { defaultConfigFor, parseWidgetConfig } from '../widget-state';

describe('widget-state', () => {
  describe('defaultConfigFor', () => {
    it('returns sensible RandomGenerator default', () => {
      const config = defaultConfigFor('RandomGenerator');
      expect(config.name).toBe('Generatore');
      expect(config.faces).toEqual([1, 2, 3, 4, 5, 6]);
      expect(config.quantity).toBe(1);
      expect(config.last).toBeNull();
    });

    it('returns sensible defaults for all 6 types', () => {
      const types = [
        'RandomGenerator',
        'TurnManager',
        'ScoreTracker',
        'ResourceManager',
        'NoteManager',
        'Whiteboard',
      ] as const;
      for (const type of types) {
        const config = defaultConfigFor(type);
        expect(config).toBeDefined();
      }
    });
  });

  describe('parseWidgetConfig', () => {
    it('parses valid JSON into typed config', () => {
      const config = parseWidgetConfig('NoteManager', JSON.stringify({ text: 'hello' }));
      expect(config.text).toBe('hello');
    });

    it('falls back to default + warns on invalid JSON', () => {
      const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
      const config = parseWidgetConfig('NoteManager', 'not valid json');
      expect(config.text).toBe('');
      expect(warn).toHaveBeenCalled();
      warn.mockRestore();
    });

    it('merges partial JSON with defaults', () => {
      const config = parseWidgetConfig('RandomGenerator', JSON.stringify({ last: 5 }));
      expect(config.last).toBe(5);
      expect(config.name).toBe('Generatore');
      expect(config.faces).toEqual([1, 2, 3, 4, 5, 6]);
    });
  });
});
