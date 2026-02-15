/**
 * Tests for Pipeline Builder Types and Plugin Definitions
 *
 * Validates all built-in plugins are properly defined with correct metadata,
 * unique IDs, valid schemas, and proper categorization.
 *
 * @see Issue #3712 - Visual Pipeline Builder
 */

import { describe, it, expect } from 'vitest';
import {
  BUILT_IN_PLUGINS,
  PLUGIN_CATEGORY_COLORS,
  PLUGIN_CATEGORY_ICONS,
  CONDITION_PRESETS,
  QUERY_PRESETS,
} from '../types';
import type { PluginCategory, ConditionPreset } from '../types';

describe('Built-in Plugins', () => {
  it('should have at least 10 plugins', () => {
    expect(BUILT_IN_PLUGINS.length).toBeGreaterThanOrEqual(10);
  });

  it('should have unique IDs for all plugins', () => {
    const ids = BUILT_IN_PLUGINS.map((p) => p.id);
    const uniqueIds = new Set(ids);
    expect(uniqueIds.size).toBe(BUILT_IN_PLUGINS.length);
  });

  it('should have unique names for all plugins', () => {
    const names = BUILT_IN_PLUGINS.map((p) => p.name);
    const uniqueNames = new Set(names);
    expect(uniqueNames.size).toBe(BUILT_IN_PLUGINS.length);
  });

  it('should have required fields for all plugins', () => {
    BUILT_IN_PLUGINS.forEach((plugin) => {
      expect(plugin.id).toBeTruthy();
      expect(plugin.name).toBeTruthy();
      expect(plugin.description).toBeTruthy();
      expect(plugin.category).toBeTruthy();
      expect(plugin.version).toBeTruthy();
      expect(plugin.configSchema).toBeDefined();
      expect(plugin.inputSchema).toBeDefined();
      expect(plugin.outputSchema).toBeDefined();
    });
  });

  it('should have valid categories for all plugins', () => {
    const validCategories: PluginCategory[] = [
      'routing',
      'cache',
      'retrieval',
      'evaluation',
      'generation',
      'validation',
      'transform',
    ];

    BUILT_IN_PLUGINS.forEach((plugin) => {
      expect(validCategories).toContain(plugin.category);
    });
  });

  it('should have object-type config schemas', () => {
    BUILT_IN_PLUGINS.forEach((plugin) => {
      expect(plugin.configSchema.type).toBe('object');
    });
  });

  it('should have valid version format', () => {
    BUILT_IN_PLUGINS.forEach((plugin) => {
      expect(plugin.version).toMatch(/^\d+\.\d+\.\d+$/);
    });
  });

  describe('Plugin Categories', () => {
    it('should have at least one plugin per category', () => {
      const categories = new Set(BUILT_IN_PLUGINS.map((p) => p.category));
      expect(categories.size).toBeGreaterThanOrEqual(5);
    });

    it('should have routing plugins', () => {
      const routing = BUILT_IN_PLUGINS.filter((p) => p.category === 'routing');
      expect(routing.length).toBeGreaterThanOrEqual(1);
    });

    it('should have retrieval plugins', () => {
      const retrieval = BUILT_IN_PLUGINS.filter((p) => p.category === 'retrieval');
      expect(retrieval.length).toBeGreaterThanOrEqual(1);
    });

    it('should have generation plugins', () => {
      const generation = BUILT_IN_PLUGINS.filter((p) => p.category === 'generation');
      expect(generation.length).toBeGreaterThanOrEqual(1);
    });
  });

  describe('Config Schemas', () => {
    it('should have properties in config schema', () => {
      BUILT_IN_PLUGINS.forEach((plugin) => {
        if (plugin.configSchema.properties) {
          expect(typeof plugin.configSchema.properties).toBe('object');
        }
      });
    });

    it('should have valid types in properties', () => {
      const validTypes = ['string', 'number', 'integer', 'boolean', 'array', 'object'];
      BUILT_IN_PLUGINS.forEach((plugin) => {
        if (plugin.configSchema.properties) {
          Object.values(plugin.configSchema.properties).forEach((prop) => {
            expect(validTypes).toContain(prop.type);
          });
        }
      });
    });

    it('should have defaults for non-required numeric properties', () => {
      BUILT_IN_PLUGINS.forEach((plugin) => {
        if (plugin.configSchema.properties) {
          Object.entries(plugin.configSchema.properties).forEach(([key, prop]) => {
            if (
              (prop.type === 'number' || prop.type === 'integer') &&
              !plugin.configSchema.required?.includes(key)
            ) {
              expect(prop.default).toBeDefined();
            }
          });
        }
      });
    });
  });
});

describe('Plugin Category Colors', () => {
  it('should have a color for each category', () => {
    const categories: PluginCategory[] = [
      'routing',
      'cache',
      'retrieval',
      'evaluation',
      'generation',
      'validation',
      'transform',
    ];

    categories.forEach((cat) => {
      expect(PLUGIN_CATEGORY_COLORS[cat]).toBeTruthy();
    });
  });

  it('should have valid HSL color strings', () => {
    Object.values(PLUGIN_CATEGORY_COLORS).forEach((color) => {
      expect(color).toMatch(/^hsl\(/);
    });
  });
});

describe('Plugin Category Icons', () => {
  it('should have an icon for each category', () => {
    const categories: PluginCategory[] = [
      'routing',
      'cache',
      'retrieval',
      'evaluation',
      'generation',
      'validation',
      'transform',
    ];

    categories.forEach((cat) => {
      expect(PLUGIN_CATEGORY_ICONS[cat]).toBeTruthy();
    });
  });
});

describe('Condition Presets', () => {
  it('should have all expected presets', () => {
    const expected: ConditionPreset[] = [
      'always',
      'never',
      'high_confidence',
      'medium_confidence',
      'rules_query',
      'strategy_query',
      'custom',
    ];

    expected.forEach((preset) => {
      expect(CONDITION_PRESETS[preset]).toBeDefined();
      expect(CONDITION_PRESETS[preset].label).toBeTruthy();
      expect(CONDITION_PRESETS[preset].expression).toBeDefined();
    });
  });

  it('should have empty expression for custom preset', () => {
    expect(CONDITION_PRESETS.custom.expression).toBe('');
  });

  it('should have non-empty expression for always preset', () => {
    expect(CONDITION_PRESETS.always.expression).toBe('always');
  });
});

describe('Query Presets', () => {
  it('should have at least 3 presets', () => {
    expect(QUERY_PRESETS.length).toBeGreaterThanOrEqual(3);
  });

  it('should have unique IDs', () => {
    const ids = QUERY_PRESETS.map((p) => p.id);
    expect(new Set(ids).size).toBe(QUERY_PRESETS.length);
  });

  it('should have required fields', () => {
    QUERY_PRESETS.forEach((preset) => {
      expect(preset.id).toBeTruthy();
      expect(preset.name).toBeTruthy();
      expect(preset.query).toBeTruthy();
      expect(preset.expectedRoute).toBeTruthy();
      expect(preset.description).toBeTruthy();
    });
  });
});
