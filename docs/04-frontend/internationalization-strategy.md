# Frontend Internationalization Strategy

**Status**: ✅ Production  
**Primary Locale**: Italian (`it`) with English (`en`) as secondary draft  
**Stack**: Next.js 16 App Router + React Intl 7.1 + custom translation utilities  
**Owners**: Frontend Team (Issue #990 – BGAI-049)

---

## 1. Why React Intl + App Router

| Requirement | Decision |
|-------------|----------|
| **Italian-first UX** (default locale, fallbacks, ICU formatting) | React Intl provides battle-tested ICU message parsing, number/date formatting, and rich text placeholders without server round-trips. |
| **App Router compatibility** | We avoid `_app.tsx` entirely; `<AppProviders>` ( `apps/web/src/app/providers.tsx` ) wraps every route with `<IntlProvider>`, Theme, Query, Auth, and Error boundaries. |
| **Streaming + RSC** | Messages are loaded on the server and serialized with the rendered React Server Component tree. Client islands reuse the same provider via hydration. |
| **Testing** | Providers + hooks live in the repo, so Jest + React Testing Library run without extra mocks or next-i18next helpers. |

> Previous plans referenced next-i18next. The migration to React Intl (Issue #990, Nov 2025) supersedes that design. This document is the canonical reference for the current implementation.

---

## 2. Architecture Overview

```
apps/web/src/
├── app/
│   ├── layout.tsx            # Server layout → renders <AppProviders>
│   └── providers.tsx         # Client providers (Intl, Theme, Query, Auth, Error boundaries)
├── components/providers/
│   └── IntlProvider.tsx      # React Intl wrapper + locale detection
├── hooks/
│   └── useTranslation.ts     # Type-safe translation + formatting helpers
├── locales/
│   ├── it.json               # Primary copy
│   ├── en.json               # Secondary copy (optional)
│   └── index.ts              # Message loader + flattening utilities
└── __tests__/components/providers/IntlProvider.test.tsx  # Regression suite
```

### AppProviders (client)
```tsx
// apps/web/src/app/providers.tsx
import { IntlProvider } from '@/components/providers/IntlProvider';

export function AppProviders({ children }: { children: React.ReactNode }) {
  return (
    <IntlProvider>
      {/* ThemeProvider → QueryProvider → AuthProvider → Error boundaries */}
      {children}
    </IntlProvider>
  );
}
```

### IntlProvider (client wrapper)
```tsx
// Detects browser locale (guards for SSR/test) and flattens nested JSON
const currentLocale = locale ?? getBrowserLocale();
const flatMessages = useMemo(() => flattenMessages(getMessages(currentLocale)), [currentLocale]);

return (
  <ReactIntlProvider
    messages={flatMessages}
    locale={currentLocale}
    defaultLocale={DEFAULT_LOCALE}
  >
    {children}
  </ReactIntlProvider>
);
```

### useTranslation hook
```tsx
const intl = useIntl();
const t = (id: string, values?: Record<string, unknown>) =>
  intl.formatMessage({ id }, values);

return {
  t,
  locale: intl.locale,
  formatNumber: intl.formatNumber,
  formatDate: intl.formatDate,
  formatRelativeTime: intl.formatRelativeTime,
};
```

---

## 3. Adding or Updating Copy

1. **Create or update keys** in `apps/web/src/locales/it.json`. Use dot-notated namespaces (`chat.sidebar.newThread`).  
   - If an English draft exists, mirror the key in `en.json`.  
   - Keep entries sorted alphabetically per namespace.
2. **Regenerate types** – not required; we rely on runtime keys, but run `pnpm lint` to ensure JSON syntax.
3. **Use the hook** inside any component (server or client) by importing `useTranslation`:
   ```tsx
   import { useTranslation } from '@/hooks/useTranslation';

   export default function UploadCTA() {
     const { t } = useTranslation();
     return <Button>{t('upload.cta.start')}</Button>;
   }
   ```
4. **Format numbers/dates** with the provided helpers instead of `Intl.NumberFormat` directly.
5. **Write tests** when adding new critical copy:
   - React component tests: `apps/web/src/__tests__/components/...`
   - Hook tests: `apps/web/src/__tests__/hooks/useTranslation.test.tsx`

---

## 4. Server Components vs Client Components

- **Server Components** can call `useTranslation` inside client wrappers only. Pattern:
  ```tsx
  // server: src/app/chat/page.tsx
  import { ChatShell } from '@/components/chat/ChatShell';

  export default async function ChatPage() {
    return <ChatShell />; // ChatShell is a client component that calls useTranslation()
  }
  ```
- **Client Components** declare `"use client"` and call `useTranslation` directly.
- **Streaming**: Because translations live inside `AppProviders`, streaming responses already include localized content—no additional suspense boundaries needed.

---

## 5. Testing & Tooling

| Area | Command / File | Purpose |
|------|----------------|---------|
| **Unit Tests** | `pnpm test -- src/__tests__/components/providers/IntlProvider.test.tsx` | Validates locale detection, fallbacks, and formatting helpers. |
| **Hook Tests** | `pnpm test -- src/__tests__/hooks/useTranslation.test.tsx` | Verifies formatting helpers, pluralization, interpolation. |
| **Storybook** | `pnpm storybook` | Stories automatically inherit translations via preview decorator (loads `<AppProviders>`). |
| **E2E** | `pnpm test:e2e -- --project=web` | Important flows (login, upload, chat) assert localized strings exist. |

> Accessibility: run `pnpm test:a11y` after changing copy for modal/dialog components to confirm aria-labels still map to translated text.

---

## 6. Content Workflow & Governance

1. Designers / PMs draft copy in Italian.  
2. Engineers add keys to `it.json`, optionally stub `en.json`.  
3. Submit PR referencing the related issue. CI enforces:
   - JSON linting via ESLint (`jsonc` plugin).  
   - Jest suites for providers + hooks.  
   - Playwright smoke tests (`apps/web/e2e`) for login and chat localized labels.
4. Localization review: Frontend reviewer checks copy accuracy and ensures fallback text is safe (never blank).  
5. Release notes: Document changes in `apps/web/versions/page.tsx` when user-visible copy changes across the product.

Key principles:
- Prefer semantic IDs (`nav.settings.security`) over raw sentences.
- Never interpolate HTML—use ICU placeholders and `<FormattedMessage>` when rich text is required.
- Default locale remains Italian; unknown locales fall back to Italian automatically (see `getBrowserLocale` guard in `IntlProvider.tsx`).

---

## 7. References

- `apps/web/src/components/providers/IntlProvider.tsx`
- `apps/web/src/app/providers.tsx`
- `apps/web/src/hooks/useTranslation.ts`
- `apps/web/src/locales/README.md` (hands-on examples + formatting guide)
- `docs/04-frontend/accessibility-standards.md` (§WCAG-Text) for copy review checklist

---

**Last Updated**: 2025-11-18 (App Router + React Intl consolidation)
