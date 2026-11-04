import { describe, it, expect } from 'vitest';
import { SETTINGS_SECTIONS, isValidSection, DEFAULT_SECTION } from '../settings-sections';

describe('settings-sections', () => {
  it('defines 7 sections in order', () => {
    expect(SETTINGS_SECTIONS.map(s => s.id)).toEqual([
      'profile',
      'security',
      'ai-consent',
      'notifications',
      'preferences',
      'api-keys',
      'services',
    ]);
  });
  it('default section is profile', () => {
    expect(DEFAULT_SECTION).toBe('profile');
  });
  it('validates known section ids', () => {
    expect(isValidSection('security')).toBe(true);
    expect(isValidSection('xyz')).toBe(false);
    expect(isValidSection(null)).toBe(false);
    expect(isValidSection(undefined)).toBe(false);
  });
  it('marks only services as placeholder (#3961)', () => {
    // `notifications` stopped being a placeholder: it now renders
    // NotificationPreferences, which already existed at its own route.
    // `services` has no implemented counterpart, so it stays an honest placeholder.
    expect(SETTINGS_SECTIONS.find(s => s.id === 'services')?.placeholder).toBe(true);
    expect(SETTINGS_SECTIONS.find(s => s.id === 'notifications')?.placeholder).toBeFalsy();
    expect(SETTINGS_SECTIONS.find(s => s.id === 'security')?.placeholder).toBeFalsy();
  });
});
