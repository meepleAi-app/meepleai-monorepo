/**
 * Game Templates Tests (Issue #3164 - GST-005)
 *
 * Tests for game template configuration and lookup functions
 */

import { describe, it, expect } from 'vitest';

import {
  GAME_TEMPLATES,
  getGameTemplate,
  getGameTemplateByName,
  hasGameTemplate,
  getAvailableTemplates,
} from '../game-templates';

describe('game-templates', () => {
  describe('GAME_TEMPLATES', () => {
    it('should have templates for popular games', () => {
      expect(GAME_TEMPLATES['7-wonders']).toBeDefined();
      expect(GAME_TEMPLATES.splendor).toBeDefined();
      expect(GAME_TEMPLATES.catan).toBeDefined();
    });

    it('should have correct structure for 7 Wonders', () => {
      const template = GAME_TEMPLATES['7-wonders'];

      expect(template.name).toBe('7 Wonders');
      expect(template.icon).toBe('🏛️');
      expect(template.rounds).toEqual([1, 2, 3]);
      expect(template.categories).toContain('Military');
      expect(template.categories).toContain('Science');
      expect(template.scoringRules).toBeTruthy();
      expect(template.playerCount.min).toBe(3);
      expect(template.playerCount.max).toBe(7);
    });

    it('should have at least 3 templates', () => {
      const templateCount = Object.keys(GAME_TEMPLATES).length;
      expect(templateCount).toBeGreaterThanOrEqual(3);
    });
  });

  describe('getGameTemplate', () => {
    it('should return template by exact slug', () => {
      const template = getGameTemplate('7-wonders');
      expect(template).toBeDefined();
      expect(template?.name).toBe('7 Wonders');
    });

    it('should be case-insensitive', () => {
      const template = getGameTemplate('SPLENDOR');
      expect(template).toBeDefined();
      expect(template?.name).toBe('Splendor');
    });

    it('should return undefined for unknown game', () => {
      const template = getGameTemplate('unknown-game');
      expect(template).toBeUndefined();
    });
  });

  describe('getGameTemplateByName', () => {
    it('should find template by exact name', () => {
      const template = getGameTemplateByName('7 Wonders');
      expect(template).toBeDefined();
      expect(template?.icon).toBe('🏛️');
    });

    it('should find template with fuzzy matching', () => {
      const template = getGameTemplateByName('Wonders');
      expect(template).toBeDefined();
      expect(template?.name).toBe('7 Wonders');
    });

    it('should normalize special characters', () => {
      const template = getGameTemplateByName('Ticket to Ride: Europe');
      expect(template).toBeDefined();
      expect(template?.name).toBe('Ticket to Ride');
    });

    it('should return undefined for non-existent game', () => {
      const template = getGameTemplateByName('Monopoly');
      expect(template).toBeUndefined();
    });

    it('should match partial names', () => {
      const template = getGameTemplateByName('Catan Settlers');
      expect(template).toBeDefined();
      expect(template?.name).toBe('Catan');
    });
  });

  describe('hasGameTemplate', () => {
    it('should return true for games with templates', () => {
      expect(hasGameTemplate('7 Wonders')).toBe(true);
      expect(hasGameTemplate('Splendor')).toBe(true);
      expect(hasGameTemplate('wingspan')).toBe(true);
    });

    it('should return false for games without templates', () => {
      expect(hasGameTemplate('Monopoly')).toBe(false);
      expect(hasGameTemplate('Risk')).toBe(false);
    });
  });

  describe('getAvailableTemplates', () => {
    it('should return all template slugs', () => {
      const templates = getAvailableTemplates();

      expect(templates).toContain('7-wonders');
      expect(templates).toContain('splendor');
      expect(templates).toContain('catan');
      expect(templates.length).toBeGreaterThanOrEqual(3);
    });

    it('should return array of strings', () => {
      const templates = getAvailableTemplates();

      expect(Array.isArray(templates)).toBe(true);
      templates.forEach(slug => {
        expect(typeof slug).toBe('string');
      });
    });
  });

  describe('template validation', () => {
    it('all templates should have required fields', () => {
      Object.entries(GAME_TEMPLATES).forEach(([slug, template]) => {
        expect(template.name, `${slug} missing name`).toBeTruthy();
        expect(template.icon, `${slug} missing icon`).toBeTruthy();
        expect(Array.isArray(template.rounds), `${slug} rounds not array`).toBe(true);
        expect(Array.isArray(template.categories), `${slug} categories not array`).toBe(true);
        expect(template.categories.length, `${slug} has no categories`).toBeGreaterThan(0);
        expect(template.scoringRules, `${slug} missing scoring rules`).toBeTruthy();
        expect(template.playerCount.min, `${slug} missing min players`).toBeGreaterThan(0);
        expect(template.playerCount.max, `${slug} missing max players`).toBeGreaterThanOrEqual(
          template.playerCount.min
        );
      });
    });

    it('all template icons should be emojis', () => {
      Object.values(GAME_TEMPLATES).forEach(template => {
        // Emoji check: 1-4 characters (some emojis are multi-byte), non-ASCII
        expect(template.icon.length).toBeLessThanOrEqual(4);
        expect(template.icon.charCodeAt(0)).toBeGreaterThan(127);
      });
    });
  });
});
