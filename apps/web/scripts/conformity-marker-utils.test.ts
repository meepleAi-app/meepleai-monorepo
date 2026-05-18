import { describe, it, expect } from 'vitest';

import {
  getViewportFromProjectName,
  buildMarkerSelector,
  validateMarkerCount,
} from './conformity-marker-utils';

describe('conformity-marker-utils', () => {
  describe('getViewportFromProjectName', () => {
    it('returns "desktop" for conformity-bootstrap-desktop', () => {
      expect(getViewportFromProjectName('conformity-bootstrap-desktop')).toBe('desktop');
    });

    it('returns "mobile" for conformity-bootstrap-mobile', () => {
      expect(getViewportFromProjectName('conformity-bootstrap-mobile')).toBe('mobile');
    });

    it('returns "desktop" for conformity-verify-desktop', () => {
      expect(getViewportFromProjectName('conformity-verify-desktop')).toBe('desktop');
    });

    it('returns "mobile" for conformity-verify-mobile', () => {
      expect(getViewportFromProjectName('conformity-verify-mobile')).toBe('mobile');
    });

    it('throws for unknown project name', () => {
      expect(() => getViewportFromProjectName('desktop-chrome')).toThrow(
        /Unknown conformity project: desktop-chrome/
      );
    });

    it('throws for empty string', () => {
      expect(() => getViewportFromProjectName('')).toThrow(/Unknown conformity project/);
    });
  });

  describe('buildMarkerSelector', () => {
    it('builds selector for default-desktop', () => {
      expect(buildMarkerSelector('desktop')).toBe('[data-conformity-screen="default-desktop"]');
    });

    it('builds selector for default-mobile', () => {
      expect(buildMarkerSelector('mobile')).toBe('[data-conformity-screen="default-mobile"]');
    });
  });

  describe('validateMarkerCount', () => {
    it('passes silently when count is 1', () => {
      expect(() =>
        validateMarkerCount(1, 'sp4-library-desktop.html', 'default-desktop')
      ).not.toThrow();
    });

    it('throws actionable message when count is 0', () => {
      expect(() => validateMarkerCount(0, 'sp4-future-route.html', 'default-desktop')).toThrow(
        'Mockup sp4-future-route.html missing required marker. ' +
          'Add data-conformity-screen="default-desktop" to the element that ' +
          'represents the canonical default desktop screen.'
      );
    });

    it('throws when count is 2+ (duplicate marker)', () => {
      expect(() => validateMarkerCount(2, 'sp4-library-desktop.html', 'default-desktop')).toThrow(
        'Mockup sp4-library-desktop.html has 2 elements with data-conformity-screen="default-desktop". ' +
          'Exactly one is required.'
      );
    });

    it('throws with correct count for 5 duplicates', () => {
      expect(() => validateMarkerCount(5, 'sp4-library-desktop.html', 'default-mobile')).toThrow(
        'Mockup sp4-library-desktop.html has 5 elements with data-conformity-screen="default-mobile". ' +
          'Exactly one is required.'
      );
    });
  });
});
