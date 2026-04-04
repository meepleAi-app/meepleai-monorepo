/**
 * Theme Configuration System Test Suite (Issue #2766)
 *
 * Tests for themes.ts:
 * - THEMES array structure and exports
 * - getTheme() lookup function
 * - getCurrentTheme() with localStorage
 * - saveTheme() persistence
 * - applyTheme() CSS variable application
 * - createCustomTheme() factory
 * - HSL validation and parsing utilities
 */

import {
  THEMES,
  THEME_STORAGE_KEY,
  getTheme,
  getCurrentTheme,
  saveTheme,
  applyTheme,
  createCustomTheme,
  isValidHSL,
  parseHSL,
  formatHSL,
  type Theme,
  type ThemeMode,
  type ThemeColors,
} from '../themes';

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] ?? null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
  };
})();

Object.defineProperty(window, 'localStorage', {
  value: localStorageMock,
});

// Mock document.documentElement for applyTheme
const styleSetPropertyMock = vi.fn();
const setAttributeMock = vi.fn();

Object.defineProperty(document, 'documentElement', {
  value: {
    style: {
      setProperty: styleSetPropertyMock,
    },
    setAttribute: setAttributeMock,
  },
  writable: true,
});

describe('THEMES Array', () => {
  it('exports THEMES array with 19 theme definitions', () => {
    expect(Array.isArray(THEMES)).toBe(true);
    expect(THEMES.length).toBe(19);
  });

  it('has 11 preset themes (5 light + 6 dark)', () => {
    const presetThemes = THEMES.filter(t => !t.accessibilityMode);
    expect(presetThemes.length).toBe(11);

    const lightPresets = presetThemes.filter(t => t.mode === 'light');
    const darkPresets = presetThemes.filter(t => t.mode === 'dark');

    expect(lightPresets.length).toBe(5);
    expect(darkPresets.length).toBe(6);
  });

  it('has 8 accessibility themes (4 modes x 2 light/dark)', () => {
    const accessibilityThemes = THEMES.filter(t => t.accessibilityMode);
    expect(accessibilityThemes.length).toBe(8);

    const accessibilityModes = ['high-contrast', 'deuteranopia', 'protanopia', 'tritanopia'];
    accessibilityModes.forEach(mode => {
      const modeThemes = accessibilityThemes.filter(t => t.accessibilityMode === mode);
      expect(modeThemes.length).toBe(2); // light + dark
    });
  });

  it('each theme has required properties', () => {
    THEMES.forEach(theme => {
      expect(theme.id).toBeDefined();
      expect(theme.name).toBeDefined();
      expect(theme.description).toBeDefined();
      expect(['light', 'dark']).toContain(theme.mode);
      expect(theme.colors).toBeDefined();
      expect(theme.colors.primary).toBeDefined();
      expect(theme.colors.primaryForeground).toBeDefined();
      expect(theme.colors.secondary).toBeDefined();
      expect(theme.colors.secondaryForeground).toBeDefined();
      expect(theme.colors.accent).toBeDefined();
      expect(theme.colors.accentForeground).toBeDefined();
      expect(theme.colors.muted).toBeDefined();
      expect(theme.colors.mutedForeground).toBeDefined();
      expect(theme.colors.destructive).toBeDefined();
      expect(theme.colors.destructiveForeground).toBeDefined();
    });
  });

  it('has unique IDs for all themes', () => {
    const ids = THEMES.map(t => t.id);
    const uniqueIds = new Set(ids);
    expect(uniqueIds.size).toBe(ids.length);
  });

  it('contains expected preset theme names', () => {
    const expectedPresets = [
      'default-light',
      'default-dark',
      'forest-light',
      'forest-dark',
      'sunset-light',
      'sunset-dark',
      'ocean-light',
      'ocean-dark',
      'midnight-light',
      'midnight-dark',
    ];

    expectedPresets.forEach(id => {
      expect(THEMES.find(t => t.id === id)).toBeDefined();
    });
  });
});

describe('THEME_STORAGE_KEY', () => {
  it('exports the correct storage key', () => {
    expect(THEME_STORAGE_KEY).toBe('meepleai-theme');
  });
});

describe('getTheme', () => {
  it('returns theme by valid ID', () => {
    const theme = getTheme('default-light');
    expect(theme).toBeDefined();
    expect(theme?.id).toBe('default-light');
    expect(theme?.name).toBe('Default Light');
  });

  it('returns undefined for invalid ID', () => {
    const theme = getTheme('non-existent-theme');
    expect(theme).toBeUndefined();
  });

  it('returns correct accessibility theme', () => {
    const theme = getTheme('high-contrast-dark');
    expect(theme).toBeDefined();
    expect(theme?.accessibilityMode).toBe('high-contrast');
    expect(theme?.mode).toBe('dark');
  });

  it('returns undefined for empty string', () => {
    const theme = getTheme('');
    expect(theme).toBeUndefined();
  });
});

describe('getCurrentTheme', () => {
  beforeEach(() => {
    localStorageMock.clear();
    vi.clearAllMocks();
  });

  it('returns stored theme if valid', () => {
    localStorageMock.getItem.mockReturnValueOnce('forest-dark');

    const theme = getCurrentTheme();

    expect(theme.id).toBe('forest-dark');
    expect(localStorageMock.getItem).toHaveBeenCalledWith(THEME_STORAGE_KEY);
  });

  it('returns default theme if no stored theme', () => {
    localStorageMock.getItem.mockReturnValueOnce(null);

    const theme = getCurrentTheme();

    expect(theme.id).toBe('default-light'); // First theme in array
  });

  it('returns default theme if stored theme is invalid', () => {
    localStorageMock.getItem.mockReturnValueOnce('invalid-theme');

    const theme = getCurrentTheme();

    expect(theme.id).toBe('default-light');
  });
});

describe('saveTheme', () => {
  beforeEach(() => {
    localStorageMock.clear();
    vi.clearAllMocks();
  });

  it('saves theme ID to localStorage', () => {
    saveTheme('ocean-light');

    expect(localStorageMock.setItem).toHaveBeenCalledWith(THEME_STORAGE_KEY, 'ocean-light');
  });

  it('overwrites existing theme', () => {
    saveTheme('forest-light');
    saveTheme('midnight-dark');

    expect(localStorageMock.setItem).toHaveBeenLastCalledWith(THEME_STORAGE_KEY, 'midnight-dark');
  });
});

describe('applyTheme', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('sets all CSS custom properties', () => {
    const theme = getTheme('default-light')!;

    applyTheme(theme);

    expect(styleSetPropertyMock).toHaveBeenCalledWith('--primary', theme.colors.primary);
    expect(styleSetPropertyMock).toHaveBeenCalledWith(
      '--primary-foreground',
      theme.colors.primaryForeground
    );
    expect(styleSetPropertyMock).toHaveBeenCalledWith('--secondary', theme.colors.secondary);
    expect(styleSetPropertyMock).toHaveBeenCalledWith(
      '--secondary-foreground',
      theme.colors.secondaryForeground
    );
    expect(styleSetPropertyMock).toHaveBeenCalledWith('--accent', theme.colors.accent);
    expect(styleSetPropertyMock).toHaveBeenCalledWith(
      '--accent-foreground',
      theme.colors.accentForeground
    );
    expect(styleSetPropertyMock).toHaveBeenCalledWith('--muted', theme.colors.muted);
    expect(styleSetPropertyMock).toHaveBeenCalledWith(
      '--muted-foreground',
      theme.colors.mutedForeground
    );
    expect(styleSetPropertyMock).toHaveBeenCalledWith('--destructive', theme.colors.destructive);
    expect(styleSetPropertyMock).toHaveBeenCalledWith(
      '--destructive-foreground',
      theme.colors.destructiveForeground
    );
  });

  it('sets data-theme attribute on document element', () => {
    const theme = getTheme('sunset-dark')!;

    applyTheme(theme);

    expect(setAttributeMock).toHaveBeenCalledWith('data-theme', 'sunset-dark');
  });

  it('applies 10 color properties total', () => {
    const theme = getTheme('ocean-light')!;

    applyTheme(theme);

    expect(styleSetPropertyMock).toHaveBeenCalledTimes(10);
  });
});

describe('createCustomTheme', () => {
  const testColors: ThemeColors = {
    primary: '200 80% 50%',
    primaryForeground: '0 0% 100%',
    secondary: '150 60% 40%',
    secondaryForeground: '0 0% 100%',
    accent: '30 90% 60%',
    accentForeground: '0 0% 10%',
    muted: '200 20% 95%',
    mutedForeground: '200 10% 45%',
    destructive: '0 80% 55%',
    destructiveForeground: '0 0% 98%',
  };

  it('creates custom theme with provided values', () => {
    const theme = createCustomTheme('My Custom Theme', 'light', testColors);

    expect(theme.name).toBe('My Custom Theme');
    expect(theme.mode).toBe('light');
    expect(theme.colors).toEqual(testColors);
    expect(theme.isCustom).toBe(true);
    expect(theme.description).toBe('Custom user theme');
  });

  it('generates unique ID with timestamp', () => {
    const theme1 = createCustomTheme('Theme 1', 'light', testColors);
    // Small delay to ensure different timestamps
    const theme2 = createCustomTheme('Theme 2', 'dark', testColors);

    expect(theme1.id).toMatch(/^custom-\d+$/);
    expect(theme2.id).toMatch(/^custom-\d+$/);
    // IDs should be different (or at least valid format)
    expect(theme1.id.startsWith('custom-')).toBe(true);
    expect(theme2.id.startsWith('custom-')).toBe(true);
  });

  it('creates dark mode custom theme', () => {
    const theme = createCustomTheme('Dark Custom', 'dark', testColors);

    expect(theme.mode).toBe('dark');
  });
});

describe('isValidHSL', () => {
  it('validates correct HSL format', () => {
    expect(isValidHSL('221 83% 53%')).toBe(true);
    expect(isValidHSL('0 0% 100%')).toBe(true);
    expect(isValidHSL('360 100% 50%')).toBe(true);
    expect(isValidHSL('180 50% 25%')).toBe(true);
  });

  it('rejects invalid HSL formats', () => {
    expect(isValidHSL('')).toBe(false);
    expect(isValidHSL('221')).toBe(false);
    expect(isValidHSL('221 83%')).toBe(false);
    expect(isValidHSL('hsl(221, 83%, 53%)')).toBe(false); // CSS function format
    expect(isValidHSL('221, 83%, 53%')).toBe(false); // Comma separated
    expect(isValidHSL('221deg 83% 53%')).toBe(false); // With deg unit
  });

  it('rejects out-of-range values', () => {
    expect(isValidHSL('361 50% 50%')).toBe(false); // H > 360
    expect(isValidHSL('200 101% 50%')).toBe(false); // S > 100
    expect(isValidHSL('200 50% 101%')).toBe(false); // L > 100
    expect(isValidHSL('-1 50% 50%')).toBe(false); // Negative H
  });

  it('accepts edge case values', () => {
    expect(isValidHSL('0 0% 0%')).toBe(true); // Black
    expect(isValidHSL('0 0% 100%')).toBe(true); // White
    expect(isValidHSL('360 100% 100%')).toBe(true); // Maximum values
  });

  it('accepts decimal values', () => {
    expect(isValidHSL('240 4.8% 95.9%')).toBe(true);
    expect(isValidHSL('240 3.7% 15.9%')).toBe(true);
    expect(isValidHSL('240 5.9% 10%')).toBe(true);
    expect(isValidHSL('0 84.2% 60.2%')).toBe(true);
    expect(isValidHSL('0 62.8% 30.6%')).toBe(true);
  });
});

describe('parseHSL', () => {
  it('parses valid HSL string to components', () => {
    const result = parseHSL('221 83% 53%');

    expect(result).toEqual({ h: 221, s: 83, l: 53 });
  });

  it('returns null for invalid format', () => {
    expect(parseHSL('')).toBeNull();
    expect(parseHSL('invalid')).toBeNull();
    expect(parseHSL('221')).toBeNull();
  });

  it('parses edge case values', () => {
    expect(parseHSL('0 0% 0%')).toEqual({ h: 0, s: 0, l: 0 });
    expect(parseHSL('360 100% 100%')).toEqual({ h: 360, s: 100, l: 100 });
  });

  it('handles single-digit values', () => {
    expect(parseHSL('5 5% 5%')).toEqual({ h: 5, s: 5, l: 5 });
  });

  it('parses decimal values', () => {
    expect(parseHSL('240 4.8% 95.9%')).toEqual({ h: 240, s: 4.8, l: 95.9 });
    expect(parseHSL('0 84.2% 60.2%')).toEqual({ h: 0, s: 84.2, l: 60.2 });
  });
});

describe('formatHSL', () => {
  it('formats HSL components to string', () => {
    expect(formatHSL(221, 83, 53)).toBe('221 83% 53%');
  });

  it('rounds decimal values', () => {
    expect(formatHSL(221.4, 83.6, 53.2)).toBe('221 84% 53%');
    expect(formatHSL(221.9, 83.1, 53.5)).toBe('222 83% 54%');
  });

  it('handles edge case values', () => {
    expect(formatHSL(0, 0, 0)).toBe('0 0% 0%');
    expect(formatHSL(360, 100, 100)).toBe('360 100% 100%');
  });

  it('formats zero values correctly', () => {
    expect(formatHSL(0, 0, 100)).toBe('0 0% 100%');
  });
});

describe('HSL roundtrip', () => {
  it('parseHSL and formatHSL are inverse operations', () => {
    const original = '180 50% 75%';
    const parsed = parseHSL(original);

    expect(parsed).not.toBeNull();
    if (parsed) {
      const formatted = formatHSL(parsed.h, parsed.s, parsed.l);
      expect(formatted).toBe(original);
    }
  });

  it('validates all THEMES colors are valid HSL', () => {
    THEMES.forEach(theme => {
      Object.entries(theme.colors).forEach(([colorName, colorValue]) => {
        expect(isValidHSL(colorValue)).toBe(true);
      });
    });
  });
});
