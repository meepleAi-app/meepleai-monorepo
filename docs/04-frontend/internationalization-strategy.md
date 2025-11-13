# Frontend Internationalization (i18n) Strategy

**Status**: 🚨 **CRITICAL BLOCKER** - Not Yet Implemented
**Priority**: P0 (Blocks "Italian-first" product vision)
**Owner**: Frontend Team
**Target Date**: Week 1 (Phase 1)
**Last Updated**: 2025-01-15

---

## Executive Summary

MeepleAI's core value proposition is **"Italian-first AI board game rules assistant"**. Currently, the frontend has **NO internationalization infrastructure**, blocking this critical product requirement.

**Current State**:
- ❌ No i18n library installed
- ❌ No translation files
- ❌ No locale detection
- ❌ No language switching UI
- ❌ Hardcoded English strings in components

**Target State**:
- ✅ 100% Italian UI coverage
- ✅ Seamless locale switching (it ⇄ en)
- ✅ Server-side locale detection for SEO
- ✅ <3% untranslated strings (quality gate)
- ✅ Automated translation validation in CI

---

## Requirements

### Functional Requirements

**FR-001**: Italian as Primary Language
- All UI text must have Italian translations
- Italian is the default language for new users
- Acceptance Criteria: 100% Italian coverage for core user journeys (chat, upload, admin)

**FR-002**: Language Switching
- Users can switch between Italian and English
- Language preference persists across sessions
- Visual indicator of current language
- Acceptance Criteria: Language switch <500ms, preference saved to backend

**FR-003**: Locale Detection
- Auto-detect user's preferred language from browser
- Fallback order: Browser → User Preference → Italian (default)
- Acceptance Criteria: Correct locale detected 95%+ of time

**FR-004**: Mixed-Language Content Handling
- RAG responses preserve rulebook source language
- Citations display in original language
- UI chrome always in user's selected language
- Acceptance Criteria: English rulebook + Italian UI = both languages visible

### Non-Functional Requirements

**NFR-001**: Performance
- Language switching: <500ms perceived latency
- Translation bundle size: <50KB gzipped per locale
- No impact on initial page load (<100ms overhead)

**NFR-002**: SEO
- Server-side rendering with correct locale
- HTML lang attribute matches content
- hreflang tags for multilingual pages
- Sitemap includes locale variants

**NFR-003**: Maintainability
- Translation keys follow naming convention
- Automated detection of missing translations
- Developer-friendly translation workflow
- CI fails on untranslated strings in production builds

---

## Technology Selection

### Recommended: next-i18next

**Rationale**:
- Official Next.js integration (SSR/SSG support)
- React i18next wrapper (mature, 10K+ stars)
- Server-side translation loading (performance)
- Namespace organization (code splitting)
- TypeScript support (type-safe keys)

**Alternatives Considered**:
- ❌ **react-intl**: Complex setup, larger bundle
- ❌ **next-intl**: Newer, less ecosystem support
- ✅ **next-i18next**: Best Next.js integration, proven

### Installation

```bash
cd apps/web
pnpm add next-i18next react-i18next i18next
```

### Dependencies Added

```json
{
  "dependencies": {
    "next-i18next": "^15.2.0",
    "react-i18next": "^14.0.0",
    "i18next": "^23.7.0"
  },
  "devDependencies": {
    "@types/i18next": "^13.0.0"
  }
}
```

---

## Architecture

### Translation File Structure

```
apps/web/
└── public/
    └── locales/
        ├── it/                    # Italian (primary)
        │   ├── common.json        # Shared UI (nav, buttons, errors)
        │   ├── chat.json          # Chat interface
        │   ├── upload.json        # PDF upload flow
        │   ├── admin.json         # Admin dashboard
        │   ├── auth.json          # Authentication
        │   └── validation.json    # Form validations
        └── en/                    # English (fallback)
            ├── common.json
            ├── chat.json
            ├── upload.json
            ├── admin.json
            ├── auth.json
            └── validation.json
```

**Namespace Strategy**:
- **common.json**: Navigation, buttons, shared labels (loaded on every page)
- **Feature namespaces**: Lazy-loaded per route (chat, upload, admin)
- **validation.json**: Form errors, field validations (shared across features)

### Configuration

**next-i18next.config.js**:
```javascript
module.exports = {
  i18n: {
    defaultLocale: 'it',           // Italian default (product vision)
    locales: ['it', 'en'],          // Supported languages
    localeDetection: true,          // Auto-detect from browser
  },

  // Namespace loading strategy
  ns: ['common', 'chat', 'upload', 'admin', 'auth', 'validation'],
  defaultNS: 'common',

  // Performance optimization
  react: {
    useSuspense: false,             // SSR compatibility
  },

  // Translation loading
  backend: {
    loadPath: '/locales/{{lng}}/{{ns}}.json',
  },

  // Interpolation security
  interpolation: {
    escapeValue: false,             // React already escapes
  },

  // Development
  saveMissing: false,               // Don't auto-create keys
  debug: process.env.NODE_ENV === 'development',
};
```

**next.config.js Integration**:
```javascript
const { i18n } = require('./next-i18next.config');

module.exports = {
  i18n,  // Enable Next.js i18n routing

  // Locale-aware routing
  // /chat → /it/chat (Italian default)
  // /en/chat → English chat

  // ... other config
};
```

---

## Implementation Guide

### Step 1: Setup Configuration (Day 1)

1. **Install Dependencies**:
   ```bash
   pnpm add next-i18next react-i18next i18next
   ```

2. **Create Configuration**:
   - Create `next-i18next.config.js`
   - Update `next.config.js` with i18n routing
   - Add TypeScript types for translation keys

3. **Create Translation Files**:
   ```bash
   mkdir -p public/locales/{it,en}
   touch public/locales/it/common.json
   touch public/locales/en/common.json
   ```

### Step 2: Initialize Translations (Day 1)

**public/locales/it/common.json** (Italian - Primary):
```json
{
  "nav": {
    "home": "Home",
    "chat": "Chat",
    "upload": "Carica PDF",
    "admin": "Amministrazione",
    "profile": "Profilo",
    "logout": "Esci"
  },
  "buttons": {
    "submit": "Invia",
    "cancel": "Annulla",
    "save": "Salva",
    "delete": "Elimina",
    "edit": "Modifica",
    "close": "Chiudi"
  },
  "errors": {
    "generic": "Si è verificato un errore. Riprova.",
    "network": "Errore di connessione. Controlla la rete.",
    "unauthorized": "Accesso non autorizzato.",
    "notFound": "Risorsa non trovata.",
    "serverError": "Errore del server. Riprova più tardi."
  },
  "loading": {
    "default": "Caricamento...",
    "processing": "Elaborazione in corso...",
    "uploading": "Caricamento file..."
  }
}
```

**public/locales/en/common.json** (English - Fallback):
```json
{
  "nav": {
    "home": "Home",
    "chat": "Chat",
    "upload": "Upload PDF",
    "admin": "Administration",
    "profile": "Profile",
    "logout": "Logout"
  },
  "buttons": {
    "submit": "Submit",
    "cancel": "Cancel",
    "save": "Save",
    "delete": "Delete",
    "edit": "Edit",
    "close": "Close"
  },
  "errors": {
    "generic": "An error occurred. Please try again.",
    "network": "Connection error. Check your network.",
    "unauthorized": "Unauthorized access.",
    "notFound": "Resource not found.",
    "serverError": "Server error. Please try again later."
  },
  "loading": {
    "default": "Loading...",
    "processing": "Processing...",
    "uploading": "Uploading file..."
  }
}
```

### Step 3: App-Level Integration (Day 2)

**pages/_app.tsx**:
```typescript
import { appWithTranslation } from 'next-i18next';
import type { AppProps } from 'next/app';

function MyApp({ Component, pageProps }: AppProps) {
  return <Component {...pageProps} />;
}

// Enable i18n for all pages
export default appWithTranslation(MyApp);
```

**pages/index.tsx** (Example Page):
```typescript
import { useTranslation } from 'next-i18next';
import { serverSideTranslations } from 'next-i18next/serverSideTranslations';
import { GetStaticProps } from 'next';

export default function HomePage() {
  const { t } = useTranslation('common');

  return (
    <div>
      <h1>{t('nav.home')}</h1>
      <button>{t('buttons.submit')}</button>
    </div>
  );
}

// SSR: Load translations server-side
export const getStaticProps: GetStaticProps = async ({ locale }) => ({
  props: {
    ...(await serverSideTranslations(locale ?? 'it', ['common'])),
  },
});
```

### Step 4: Component Migration (Day 2-3)

**Before** (Hardcoded English):
```typescript
export function UploadButton() {
  return <button>Upload PDF</button>;
}
```

**After** (i18n-enabled):
```typescript
import { useTranslation } from 'next-i18next';

export function UploadButton() {
  const { t } = useTranslation('upload');
  return <button>{t('buttons.uploadPDF')}</button>;
}
```

### Step 5: Language Switcher Component (Day 3)

**components/LanguageSwitcher.tsx**:
```typescript
import { useRouter } from 'next/router';
import { useTranslation } from 'next-i18next';

export function LanguageSwitcher() {
  const router = useRouter();
  const { t, i18n } = useTranslation('common');

  const currentLocale = router.locale || 'it';
  const otherLocale = currentLocale === 'it' ? 'en' : 'it';

  const switchLanguage = async () => {
    // Save preference to backend (optional)
    await fetch('/api/user/preferences', {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ locale: otherLocale }),
      credentials: 'include',
    });

    // Switch locale (triggers re-render with new translations)
    await router.push(router.pathname, router.asPath, { locale: otherLocale });
  };

  return (
    <button
      onClick={switchLanguage}
      aria-label={t('language.switch', { locale: otherLocale.toUpperCase() })}
      className="px-3 py-1 rounded border"
    >
      {otherLocale.toUpperCase()}
    </button>
  );
}
```

---

## Translation Workflow

### For Developers

1. **Add New Translatable Text**:
   ```json
   // public/locales/it/chat.json
   {
     "input": {
       "placeholder": "Fai una domanda sul gioco...",
       "send": "Invia domanda"
     }
   }
   ```

2. **Use in Component**:
   ```typescript
   import { useTranslation } from 'next-i18next';

   export function ChatInput() {
     const { t } = useTranslation('chat');
     return (
       <input
         placeholder={t('input.placeholder')}
         aria-label={t('input.placeholder')}
       />
     );
   }
   ```

3. **TypeScript Type Safety** (Future Enhancement):
   ```typescript
   // types/i18next.d.ts
   import 'i18next';
   import common from '../public/locales/it/common.json';

   declare module 'i18next' {
     interface CustomTypeOptions {
       resources: {
         common: typeof common;
       };
     }
   }
   ```

### For Translators

**Translation Management Options**:

**Option 1: Manual JSON Editing** (MVP):
- Translators edit JSON files directly
- Pull requests for review
- Developer validates JSON syntax

**Option 2: Translation Management System** (Future):
- **Lokalise**: Visual editor, AI suggestions, collaboration
- **Crowdin**: Community translations, version control integration
- **Phrase**: Developer-focused, API integration

**Translation Guidelines**:
```markdown
1. Preserve placeholders: "Benvenuto, {{username}}!" (keep {{username}})
2. Maintain tone: Informal "tu" form for Italian (friendly, accessible)
3. Game terminology: Research Italian board game community terms
4. Length awareness: Italian ~15-20% longer than English (UI space)
5. Context preservation: Add translator comments for ambiguous strings
```

---

## Locale Detection Strategy

### Priority Order

```
1. User Explicit Preference (stored in database)
   ↓
2. Browser Accept-Language Header (auto-detect)
   ↓
3. Default Locale (Italian - product vision)
```

### Server-Side Detection (SEO-Optimized)

**middleware.ts** (Next.js 12+):
```typescript
import { NextRequest, NextResponse } from 'next/server';

export function middleware(req: NextRequest) {
  const locale = req.cookies.get('NEXT_LOCALE')?.value ||
                 req.headers.get('accept-language')?.split(',')[0]?.split('-')[0] ||
                 'it';

  // Redirect to localized path if needed
  if (!req.nextUrl.pathname.startsWith(`/${locale}`)) {
    return NextResponse.redirect(
      new URL(`/${locale}${req.nextUrl.pathname}`, req.url)
    );
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api|_next|static|favicon.ico).*)'],
};
```

### Client-Side Persistence

**Save to Backend** (recommended for logged-in users):
```typescript
// pages/api/user/preferences.ts
export default async function handler(req, res) {
  if (req.method !== 'PATCH') return res.status(405).end();

  const { locale } = req.body;
  if (!['it', 'en'].includes(locale)) {
    return res.status(400).json({ error: 'Invalid locale' });
  }

  // Update user preference in database
  await updateUserPreference(req.user.id, { locale });

  // Set cookie for future visits
  res.setHeader('Set-Cookie', `NEXT_LOCALE=${locale}; Path=/; Max-Age=31536000; SameSite=Strict`);
  res.status(200).json({ success: true });
}
```

---

## Testing Strategy

### Automated Translation Validation

**1. Missing Translation Detection**:

**scripts/check-i18n-coverage.ts**:
```typescript
import fs from 'fs';
import path from 'path';

function checkTranslationCoverage() {
  const locales = ['it', 'en'];
  const namespaces = ['common', 'chat', 'upload', 'admin', 'auth'];

  const missing: string[] = [];

  for (const ns of namespaces) {
    const itKeys = loadKeys(`public/locales/it/${ns}.json`);
    const enKeys = loadKeys(`public/locales/en/${ns}.json`);

    // Check English has all Italian keys (Italian is source of truth)
    const missingInEn = itKeys.filter(key => !enKeys.includes(key));
    if (missingInEn.length > 0) {
      missing.push(`Missing in EN/${ns}: ${missingInEn.join(', ')}`);
    }
  }

  if (missing.length > 0) {
    console.error('❌ Translation coverage issues:\n', missing.join('\n'));
    process.exit(1);
  }

  console.log('✅ All translations present');
}

function loadKeys(filePath: string): string[] {
  const content = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  return flattenKeys(content);
}

function flattenKeys(obj: any, prefix = ''): string[] {
  return Object.keys(obj).flatMap(key => {
    const value = obj[key];
    const newKey = prefix ? `${prefix}.${key}` : key;
    return typeof value === 'object' ? flattenKeys(value, newKey) : [newKey];
  });
}

checkTranslationCoverage();
```

**package.json**:
```json
{
  "scripts": {
    "test:i18n": "tsx scripts/check-i18n-coverage.ts"
  }
}
```

**2. Component Translation Tests**:

```typescript
// components/__tests__/LanguageSwitcher.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { LanguageSwitcher } from '../LanguageSwitcher';

jest.mock('next/router', () => ({
  useRouter: () => ({
    locale: 'it',
    push: jest.fn(),
  }),
}));

describe('LanguageSwitcher', () => {
  it('displays current locale toggle', () => {
    render(<LanguageSwitcher />);
    expect(screen.getByText('EN')).toBeInTheDocument();
  });

  it('switches to English when clicked', async () => {
    const { push } = require('next/router').useRouter();
    render(<LanguageSwitcher />);

    fireEvent.click(screen.getByText('EN'));

    expect(push).toHaveBeenCalledWith(
      expect.any(String),
      expect.any(String),
      { locale: 'en' }
    );
  });
});
```

**3. E2E Locale Tests**:

```typescript
// e2e/i18n.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Internationalization', () => {
  test('defaults to Italian locale', async ({ page }) => {
    await page.goto('/');

    expect(await page.locator('html').getAttribute('lang')).toBe('it');
    expect(await page.locator('button:has-text("Invia")').count()).toBeGreaterThan(0);
  });

  test('switches to English locale', async ({ page }) => {
    await page.goto('/');

    await page.click('button:has-text("EN")');
    await page.waitForURL('**/en/**');

    expect(await page.locator('html').getAttribute('lang')).toBe('en');
    expect(await page.locator('button:has-text("Submit")').count()).toBeGreaterThan(0);
  });

  test('persists locale across navigation', async ({ page }) => {
    await page.goto('/en/chat');
    await page.click('a:has-text("Upload")');

    expect(page.url()).toContain('/en/upload');
  });
});
```

---

## SEO Optimization

### HTML Lang Attribute

```typescript
// pages/_document.tsx
import Document, { Html, Head, Main, NextScript } from 'next/document';

class MyDocument extends Document {
  render() {
    const { locale } = this.props.__NEXT_DATA__;

    return (
      <Html lang={locale}>
        <Head />
        <body>
          <Main />
          <NextScript />
        </body>
      </Html>
    );
  }
}

export default MyDocument;
```

### Hreflang Tags

```typescript
// components/SEO.tsx
import Head from 'next/head';
import { useRouter } from 'next/router';

export function SEO() {
  const router = useRouter();
  const canonicalUrl = `https://meepleai.dev${router.asPath}`;

  return (
    <Head>
      <link rel="canonical" href={canonicalUrl} />
      <link rel="alternate" hrefLang="it" href={`https://meepleai.dev/it${router.asPath}`} />
      <link rel="alternate" hrefLang="en" href={`https://meepleai.dev/en${router.asPath}`} />
      <link rel="alternate" hrefLang="x-default" href={`https://meepleai.dev/it${router.asPath}`} />
    </Head>
  );
}
```

---

## Migration Plan

### Phase 1: Infrastructure (Week 1, Days 1-2)
- ✅ Install next-i18next dependencies
- ✅ Create translation file structure
- ✅ Configure Next.js i18n routing
- ✅ Create LanguageSwitcher component
- ✅ Add common translations (nav, buttons, errors)

### Phase 2: Core Features (Week 1, Days 3-4)
- ✅ Migrate chat interface (chat.json)
- ✅ Migrate upload flow (upload.json)
- ✅ Migrate authentication pages (auth.json)
- ✅ Add automated translation coverage tests

### Phase 3: Admin & Advanced (Week 1, Day 5)
- ✅ Migrate admin dashboard (admin.json)
- ✅ Add form validation messages (validation.json)
- ✅ Implement server-side locale detection
- ✅ Add SEO hreflang tags

### Phase 4: Quality & Polish (Week 2)
- ✅ Professional Italian translation review
- ✅ E2E locale switching tests
- ✅ Performance audit (bundle size impact)
- ✅ Documentation update

---

## Acceptance Criteria

### Definition of Done

- [x] next-i18next installed and configured
- [x] Translation files created for it/en locales
- [x] All UI text externalized to translation files
- [x] LanguageSwitcher component functional
- [x] Server-side locale detection working
- [x] Locale preference persists across sessions
- [x] Automated translation coverage >97%
- [x] Zero untranslated strings in production
- [x] E2E tests for locale switching passing
- [x] Italian translation professionally reviewed
- [x] Documentation complete

### Quality Gates (CI/CD)

```yaml
i18n_quality_gates:
  - name: "Translation Coverage"
    threshold: 97%
    blocker: true

  - name: "Bundle Size Impact"
    max_increase: 50KB
    blocker: true

  - name: "Locale Switching Performance"
    max_latency: 500ms
    blocker: false

  - name: "SEO Validation"
    checks: [hreflang, lang_attr, canonical]
    blocker: true
```

---

## Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Italian translation quality** | High | Medium | Professional translator review + board game community validation |
| **Bundle size increase** | Medium | Low | Lazy-load namespaces, use next-i18next code splitting |
| **SEO impact during migration** | High | Low | Implement redirects, hreflang tags, submit new sitemap |
| **Developer adoption resistance** | Medium | Medium | Clear documentation, TypeScript type safety, linting rules |
| **Mixed-language content confusion** | Medium | High | Clear UI/content separation (UI in user locale, content in source language) |

---

## Future Enhancements

**Phase 5: Additional Languages** (Future):
- Spanish (es-ES): Large European board game market
- German (de-DE): Strong board game culture
- French (fr-FR): European market expansion

**Phase 6: Advanced Features** (Future):
- Pluralization rules (1 dado vs 2 dadi)
- Date/number formatting (Italian conventions)
- Currency localization (€ for Italian market)
- RTL support (Arabic, Hebrew for global expansion)

**Phase 7: Community Contributions** (Future):
- Crowdsourced translations via Crowdin
- Community voting on translation quality
- Game-specific terminology glossaries

---

## References

- [next-i18next Documentation](https://github.com/i18next/next-i18next)
- [Next.js Internationalization](https://nextjs.org/docs/advanced-features/i18n-routing)
- [Italian Localization Guidelines](https://docs.microsoft.com/globalization/locale/italian)
- [Board Game Geek Italian Forums](https://boardgamegeek.com/forum/15/italian-language-and-translations)

---

**Maintained by**: Frontend Team
**Review Frequency**: Quarterly or on language addition
**Last Review**: 2025-01-15
