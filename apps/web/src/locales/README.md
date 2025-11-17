# i18n (Internationalization) Setup

**Issue #990: BGAI-049 - React Intl Setup for Italian Localization**

This directory contains the internationalization (i18n) setup for MeepleAI, using [React Intl](https://formatjs.io/docs/react-intl) (FormatJS).

## Overview

- **Primary Language**: Italian (`it`)
- **Default Locale**: `it` (Italian)
- **Library**: `react-intl` v7.1.14
- **Format**: Nested JSON with flattening support

## Directory Structure

```
src/locales/
├── README.md          # This file
├── index.ts           # Locale configuration and utilities
├── it.json            # Italian translations (primary)
└── en.json            # English translations (future - currently empty)
```

## Quick Start

### 1. Using the `useTranslation` Hook

The simplest way to access translations in your components:

```tsx
import { useTranslation } from '@/hooks/useTranslation';

function MyComponent() {
  const { t } = useTranslation();

  return (
    <div>
      <h1>{t('app.title')}</h1>
      <p>{t('common.loading')}</p>
    </div>
  );
}
```

### 2. With Interpolation (Variables)

Pass dynamic values into your translations:

```tsx
function SessionWarning({ minutes }: { minutes: number }) {
  const { t } = useTranslation();

  return (
    <div>
      {t('auth.session.expiringSoon', { minutes })}
    </div>
  );
}
```

**Translation string in `it.json`:**
```json
{
  "auth": {
    "session": {
      "expiringSoon": "La tua sessione scadrà tra {minutes} minuti."
    }
  }
}
```

### 3. Using FormattedMessage Component

For more complex scenarios with rich text formatting:

```tsx
import { FormattedMessage } from '@/hooks/useTranslation';

function Greeting() {
  return (
    <FormattedMessage
      id="app.tagline"
      defaultMessage="Your intelligent board game assistant"
      values={{ name: 'MeepleAI' }}
    />
  );
}
```

### 4. Date, Time, and Number Formatting

The hook provides locale-aware formatting utilities:

```tsx
function DateExample() {
  const { formatDate, formatTime, formatNumber, formatRelativeTime } = useTranslation();

  return (
    <div>
      <p>Date: {formatDate(new Date())}</p>
      <p>Time: {formatTime(new Date())}</p>
      <p>Number: {formatNumber(1234.56)}</p>
      <p>Relative: {formatRelativeTime(-2, 'hours')}</p>
    </div>
  );
}
```

## Translation Files

### Message Structure

Translation files use nested JSON for organization:

```json
{
  "common": {
    "loading": "Caricamento...",
    "error": "Errore",
    "success": "Successo"
  },
  "auth": {
    "login": {
      "title": "Accedi",
      "submit": "Accedi"
    }
  }
}
```

Messages are accessed using dot notation: `common.loading`, `auth.login.title`

### Available Message Categories

| Category | Description | Examples |
|----------|-------------|----------|
| `common.*` | Common UI elements | Loading, errors, buttons |
| `navigation.*` | Navigation items | Home, games, chat, settings |
| `app.*` | Application metadata | Title, description, tagline |
| `auth.*` | Authentication | Login, register, 2FA, sessions |
| `games.*` | Game management | Game list, details, filters |
| `chat.*` | Chat interface | Placeholder, messages, sources |
| `upload.*` | PDF upload | Drag/drop, processing, metadata |
| `settings.*` | User settings | Profile, preferences, privacy |
| `admin.*` | Admin panel | Users, games, analytics |
| `errors.*` | Error messages | Generic, network, validation |
| `validation.*` | Form validation | Required, email, min/max length |
| `dates.*` | Date/time labels | Today, yesterday, time units |
| `accessibility.*` | A11y labels | Skip links, ARIA labels |

## Adding New Translations

### Step 1: Add to `it.json`

Add your new message to the appropriate category:

```json
{
  "games": {
    "addToFavorites": "Aggiungi ai preferiti",
    "removeFromFavorites": "Rimuovi dai preferiti"
  }
}
```

### Step 2: Use in Component

```tsx
function GameCard({ game }) {
  const { t } = useTranslation();

  return (
    <button onClick={() => toggleFavorite(game.id)}>
      {game.isFavorite
        ? t('games.removeFromFavorites')
        : t('games.addToFavorites')
      }
    </button>
  );
}
```

### Step 3: Type Safety (Optional)

TypeScript will infer types from `it.json`, providing autocomplete for message IDs.

## Advanced Usage

### Pluralization

React Intl supports ICU MessageFormat for pluralization:

```json
{
  "games": {
    "playerCount": "{count, plural, =0 {No players} =1 {One player} other {# players}}"
  }
}
```

```tsx
const { t } = useTranslation();
return <p>{t('games.playerCount', { count: 3 })}</p>;
// Output: "3 players"
```

### Rich Text Formatting

Use HTML tags in translations:

```json
{
  "auth": {
    "terms": "By signing up, you agree to our <a>Terms of Service</a>"
  }
}
```

```tsx
import { FormattedMessage } from 'react-intl';

<FormattedMessage
  id="auth.terms"
  values={{
    a: (chunks) => <a href="/terms">{chunks}</a>
  }}
/>
```

### Conditional Locale

Programmatically set locale based on user preference:

```tsx
import { IntlProvider } from '@/components/providers/IntlProvider';

function App() {
  const userLocale = getUserLocalePreference(); // 'it' or 'en'

  return (
    <IntlProvider locale={userLocale}>
      <YourApp />
    </IntlProvider>
  );
}
```

## Testing

### Testing Components with Translations

Wrap your test components with `IntlProvider`:

```tsx
import { render } from '@testing-library/react';
import { IntlProvider } from '@/components/providers/IntlProvider';

test('renders translated content', () => {
  const { getByText } = render(
    <IntlProvider locale="it">
      <MyComponent />
    </IntlProvider>
  );

  expect(getByText('Caricamento...')).toBeInTheDocument();
});
```

### Using Test Utils

Create a test utility wrapper:

```tsx
// src/test-utils/intl-wrapper.tsx
import { ReactNode } from 'react';
import { IntlProvider } from '@/components/providers/IntlProvider';

export function IntlWrapper({ children }: { children: ReactNode }) {
  return <IntlProvider locale="it">{children}</IntlProvider>;
}

// In your test:
import { IntlWrapper } from '@/test-utils/intl-wrapper';

test('my test', () => {
  render(<MyComponent />, { wrapper: IntlWrapper });
});
```

## Future Enhancements

- [ ] Add English (`en.json`) translations
- [ ] Add language switcher component
- [ ] Persist locale preference in user settings
- [ ] Extract translation keys from codebase automatically
- [ ] Set up translation management workflow (e.g., Crowdin, Lokalise)

## Resources

- [React Intl Documentation](https://formatjs.io/docs/react-intl)
- [ICU MessageFormat Guide](https://formatjs.io/docs/core-concepts/icu-syntax)
- [Next.js i18n Routing](https://nextjs.org/docs/app/building-your-application/routing/internationalization)

## Support

For questions or issues with i18n:
1. Check this README
2. Review existing translations in `it.json`
3. Consult [React Intl docs](https://formatjs.io/docs/react-intl)
4. Open an issue with label `i18n`

---

**Last Updated**: 2025-11-17
**Owner**: Frontend Team
**Related Issues**: #990 (BGAI-049)
