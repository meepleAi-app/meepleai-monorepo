/**
 * i18n Legal Keys Validation Tests
 *
 * Ensures all legal page section keys exist in both IT and EN locale files.
 * Prevents the cookie page i18n mismatch bug from recurring.
 *
 * Step 12 of the GDPR integration plan.
 */

import itMessages from '@/locales/it.json';
import enMessages from '@/locales/en.json';

// Section keys must match the page components exactly
const PRIVACY_SECTIONS = [
  'introduction',
  'dataController',
  'dataCollected',
  'purposes',
  'legalBasis',
  'dataSharing',
  'retention',
  'userRights',
  'cookies',
  'security',
  'transfers',
  'changes',
  'contact',
  'aiProcessing',
  'aiProviders',
  'aiDataProtection',
  'aiRetention',
  'aiRights',
  'aiLegalBasis',
];

const TERMS_SECTIONS = [
  'acceptance',
  'serviceDescription',
  'freemiumModel',
  'userAccount',
  'userContent',
  'aiUsage',
  'intellectualProperty',
  'limitations',
  'termination',
  'modifications',
  'governingLaw',
  'contact',
];

const COOKIE_SECTIONS = [
  'whatAreCookies',
  'typesOfCookies',
  'technicalCookies',
  'analyticalCookies',
  'functionalCookies',
  'thirdPartyCookies',
  'cookieManagement',
  'cookieDuration',
  'policyUpdates',
];

type LegalSections = Record<string, { title: string; content: string }>;

describe('Legal i18n key validation', () => {
  describe('Privacy Policy sections', () => {
    const itSections = (
      itMessages as Record<string, unknown> & { legal: { privacy: { sections: LegalSections } } }
    ).legal.privacy.sections;
    const enSections = (
      enMessages as Record<string, unknown> & { legal: { privacy: { sections: LegalSections } } }
    ).legal.privacy.sections;

    it.each(PRIVACY_SECTIONS)('IT: privacy section "%s" exists with title and content', key => {
      // eslint-disable-next-line security/detect-object-injection -- Test code with typed keys
      const section = itSections[key];
      expect(section).toBeDefined();
      expect(section?.title).toBeTruthy();
      expect(section?.content).toBeTruthy();
    });

    it.each(PRIVACY_SECTIONS)('EN: privacy section "%s" exists with title and content', key => {
      // eslint-disable-next-line security/detect-object-injection -- Test code with typed keys
      const section = enSections[key];
      expect(section).toBeDefined();
      expect(section?.title).toBeTruthy();
      expect(section?.content).toBeTruthy();
    });
  });

  describe('Terms of Service sections', () => {
    const itSections = (
      itMessages as Record<string, unknown> & { legal: { terms: { sections: LegalSections } } }
    ).legal.terms.sections;
    const enSections = (
      enMessages as Record<string, unknown> & { legal: { terms: { sections: LegalSections } } }
    ).legal.terms.sections;

    it.each(TERMS_SECTIONS)('IT: terms section "%s" exists with title and content', key => {
      // eslint-disable-next-line security/detect-object-injection -- Test code with typed keys
      const section = itSections[key];
      expect(section).toBeDefined();
      expect(section?.title).toBeTruthy();
      expect(section?.content).toBeTruthy();
    });

    it.each(TERMS_SECTIONS)('EN: terms section "%s" exists with title and content', key => {
      // eslint-disable-next-line security/detect-object-injection -- Test code with typed keys
      const section = enSections[key];
      expect(section).toBeDefined();
      expect(section?.title).toBeTruthy();
      expect(section?.content).toBeTruthy();
    });
  });

  describe('Cookie Policy sections', () => {
    const itSections = (
      itMessages as Record<string, unknown> & { legal: { cookies: { sections: LegalSections } } }
    ).legal.cookies.sections;
    const enSections = (
      enMessages as Record<string, unknown> & { legal: { cookies: { sections: LegalSections } } }
    ).legal.cookies.sections;

    it.each(COOKIE_SECTIONS)('IT: cookie section "%s" exists with title and content', key => {
      // eslint-disable-next-line security/detect-object-injection -- Test code with typed keys
      const section = itSections[key];
      expect(section).toBeDefined();
      expect(section?.title).toBeTruthy();
      expect(section?.content).toBeTruthy();
    });

    it.each(COOKIE_SECTIONS)('EN: cookie section "%s" exists with title and content', key => {
      // eslint-disable-next-line security/detect-object-injection -- Test code with typed keys
      const section = enSections[key];
      expect(section).toBeDefined();
      expect(section?.title).toBeTruthy();
      expect(section?.content).toBeTruthy();
    });
  });

  describe('Common legal keys', () => {
    it('IT: has tableOfContents key', () => {
      expect(
        (itMessages as Record<string, unknown> & { legal: { tableOfContents: string } }).legal
          .tableOfContents
      ).toBeTruthy();
    });

    it('EN: has tableOfContents key', () => {
      expect(
        (enMessages as Record<string, unknown> & { legal: { tableOfContents: string } }).legal
          .tableOfContents
      ).toBeTruthy();
    });

    it('IT: has lastUpdated key', () => {
      expect(
        (itMessages as Record<string, unknown> & { legal: { lastUpdated: string } }).legal
          .lastUpdated
      ).toBeTruthy();
    });

    it('EN: has lastUpdated key', () => {
      expect(
        (enMessages as Record<string, unknown> & { legal: { lastUpdated: string } }).legal
          .lastUpdated
      ).toBeTruthy();
    });
  });

  describe('Page metadata keys', () => {
    const pages = ['privacy', 'terms', 'cookies'] as const;

    it.each(pages)('IT: "%s" has title and description', page => {
      // eslint-disable-next-line security/detect-object-injection -- Test code with typed keys
      const legal = (
        itMessages as Record<string, unknown> & {
          legal: Record<string, { title: string; description: string }>;
        }
      ).legal[page];
      expect(legal?.title).toBeTruthy();
      expect(legal?.description).toBeTruthy();
    });

    it.each(pages)('EN: "%s" has title and description', page => {
      // eslint-disable-next-line security/detect-object-injection -- Test code with typed keys
      const legal = (
        enMessages as Record<string, unknown> & {
          legal: Record<string, { title: string; description: string }>;
        }
      ).legal[page];
      expect(legal?.title).toBeTruthy();
      expect(legal?.description).toBeTruthy();
    });
  });

  describe('No placeholder text remaining', () => {
    const jsonStr = JSON.stringify(itMessages) + JSON.stringify(enMessages);

    it('no [Città] placeholder', () => {
      expect(jsonStr).not.toContain('[Città]');
    });

    it('no [City] placeholder', () => {
      expect(jsonStr).not.toContain('[City]');
    });

    it('no [contact information] placeholder', () => {
      expect(jsonStr).not.toContain('[contact information]');
    });
  });
});
