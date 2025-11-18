# Frontend Implementation Plan - Board Game AI

**Last Updated**: 2025-11-12
**Status**: 🟡 MONTH 4-6 IN PROGRESS - Foundation Complete!
**Timeline**: 28 settimane (~7 mesi) - **Weeks 15-28 remaining**
**Focus**: BGAI Components → Q&A UI → Italian UI → Launch

---

## 📊 Current Status

**Overall Frontend Progress**: ~18/~35 issues complete (~51%)

| Phase | Issues | Complete | Status |
|-------|--------|----------|--------|
| **Phase 0** | ~3 | ~3 | ✅ COMPLETE (100%) |
| **Month 4** | ~7 | ~4 | 🟡 IN PROGRESS (50%) |
| **Month 5** | ~9 | ~5 | 🟡 IN PROGRESS (50%) |
| **Month 6** | ~16 | ~6 | 🟡 IN PROGRESS (40%) |

**Phase 0 Complete**: ✅
- #988 shadcn/ui installation
- #928 Design tokens migration
- #929 Theming system
- #930 Component migration (20-30 components)

**Current Sprint**: Month 4-6 (BGAI components + Q&A UI + Italian UI)

---

## PHASE 0: FRONTEND FOUNDATION (Settimane 1-4)

### Sprint 1-2: Design System Foundation

#### Issue #988: Install shadcn/ui ✅ COMPLETED
**Status**: ✅ DONE
**Completed**: 2025-11-12
**Deliverables**:
- shadcn/ui installed and configured
- 5 core components (Button, Card, Input, Select, Dialog)
- Demo page `/shadcn-demo`
- Documentation in `docs/04-frontend/shadcn-ui-installation.md`

---

#### Issue #928: Design Tokens → CSS Variables
**Status**: 🟡 READY TO START (Created 2025-11-11)
**Priority**: 🔴 CRITICAL (FOUNDATION)
**Duration**: 2-3 giorni
**Dependencies**: #988 ✅ COMPLETE
**Blocks**: #929 (theming), #930 (component migration), all Month 4-6 UI
**Can Start**: IMMEDIATELY (parallel with backend #925)
**Deliverables**:
- Migrate all hardcoded colors to CSS variables
- Create design token system
- Documentation of token usage

**Tasks**:
1. Audit existing color usage (2h)
   ```bash
   grep -r "bg-\|text-\|border-" apps/web/src
   ```
2. Define CSS variable structure (3h)
   ```css
   /* apps/web/src/styles/tokens.css */
   :root {
     /* Brand colors */
     --color-brand-primary: oklch(0.205 0 0);
     --color-brand-secondary: oklch(0.34 0.15 145);

     /* Semantic colors */
     --color-success: oklch(0.6 0.15 145);
     --color-error: oklch(0.55 0.22 25);
     --color-warning: oklch(0.75 0.15 85);

     /* Surface colors */
     --surface-base: oklch(1 0 0);
     --surface-elevated: oklch(0.98 0 0);
     --surface-overlay: oklch(0.95 0 0);

     /* Text colors */
     --text-primary: oklch(0.2 0 0);
     --text-secondary: oklch(0.45 0 0);
     --text-disabled: oklch(0.65 0 0);
   }

   [data-theme="dark"] {
     --surface-base: oklch(0.15 0 0);
     --text-primary: oklch(0.95 0 0);
   }
   ```

3. Create Tailwind config mapping (2h)
   ```js
   // tailwind.config.js
   module.exports = {
     theme: {
       extend: {
         colors: {
           brand: {
             primary: 'var(--color-brand-primary)',
             secondary: 'var(--color-brand-secondary)'
           },
           semantic: {
             success: 'var(--color-success)',
             error: 'var(--color-error)'
           }
         }
       }
     }
   }
   ```

4. Migrate existing components (6h)
   - Update `globals.css`
   - Replace hardcoded colors with tokens
   - Test visual consistency

5. Documentation (2h)
   - Token naming conventions
   - Usage guide
   - Migration examples

**Success Criteria**:
- ✅ All colors reference CSS variables
- ✅ No hardcoded hex/rgb values in components
- ✅ Dark mode works automatically
- ✅ Documentation complete

---

#### Issue #929: Theming System (Dark/Light/Auto)
**Priority**: 🔴 CRITICAL
**Duration**: 3-4 giorni
**Dependencies**: #928 (design tokens ready)
**Deliverables**:
- Theme provider component
- Theme switcher UI
- Persistent theme preference
- System preference detection

**Tasks**:
1. Create `ThemeProvider` context (3h)
   ```tsx
   // src/contexts/ThemeContext.tsx
   import { createContext, useContext, useEffect, useState } from 'react';

   type Theme = 'light' | 'dark' | 'auto';

   const ThemeContext = createContext<{
     theme: Theme;
     setTheme: (theme: Theme) => void;
     resolvedTheme: 'light' | 'dark';
   }>(undefined!);

   export function ThemeProvider({ children }: { children: React.ReactNode }) {
     const [theme, setTheme] = useState<Theme>('auto');
     const [resolvedTheme, setResolvedTheme] = useState<'light' | 'dark'>('light');

     useEffect(() => {
       const savedTheme = localStorage.getItem('theme') as Theme;
       if (savedTheme) setTheme(savedTheme);
     }, []);

     useEffect(() => {
       if (theme === 'auto') {
         const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
         setResolvedTheme(mediaQuery.matches ? 'dark' : 'light');

         const listener = (e: MediaQueryListEvent) => {
           setResolvedTheme(e.matches ? 'dark' : 'light');
         };
         mediaQuery.addEventListener('change', listener);
         return () => mediaQuery.removeEventListener('change', listener);
       } else {
         setResolvedTheme(theme);
       }
     }, [theme]);

     useEffect(() => {
       document.documentElement.setAttribute('data-theme', resolvedTheme);
       localStorage.setItem('theme', theme);
     }, [theme, resolvedTheme]);

     return (
       <ThemeContext.Provider value={{ theme, setTheme, resolvedTheme }}>
         {children}
       </ThemeContext.Provider>
     );
   }

   export const useTheme = () => useContext(ThemeContext);
   ```

2. Create `ThemeSwitcher` component (2h)
   ```tsx
   // src/components/ThemeSwitcher.tsx
   import { useTheme } from '@/contexts/ThemeContext';
   import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';

   export function ThemeSwitcher() {
     const { theme, setTheme } = useTheme();

     return (
       <Select value={theme} onValueChange={setTheme}>
         <SelectTrigger className="w-32">
           <SelectValue />
         </SelectTrigger>
         <SelectContent>
           <SelectItem value="light">☀️ Chiaro</SelectItem>
           <SelectItem value="dark">🌙 Scuro</SelectItem>
           <SelectItem value="auto">💻 Auto</SelectItem>
         </SelectContent>
       </Select>
     );
   }
   ```

3. Integrate in `AppProviders` (`src/app/providers.tsx`) (1h)
4. Update all pages to support dark mode (4h)
5. Test theme persistence (2h)
6. Accessibility testing (WCAG 2.1 AA) (3h)

**Success Criteria**:
- ✅ 3 theme modes work correctly
- ✅ Theme persists across sessions
- ✅ System preference auto-detection
- ✅ Smooth transitions (no flash)
- ✅ WCAG 2.1 AA color contrast in both themes

---

#### Issue #930: Component Migration (20-30 Components)
**Priority**: 🔴 CRITICAL
**Duration**: 1-2 settimane
**Dependencies**: #928, #929 (foundation ready)
**Deliverables**:
- Migrate 20-30 existing components to shadcn/ui
- Consistent styling across app
- Component documentation

**Migration Strategy**:

**Priority 1 - Critical Components (Week 1)**:
1. Layout components (3h)
   - Header/Navbar
   - Footer
   - Sidebar
   - Container/Grid

2. Form components (4h)
   - Button (✅ already from shadcn)
   - Input (✅ already from shadcn)
   - Select (✅ already from shadcn)
   - Textarea
   - Checkbox
   - Radio
   - Label

3. Feedback components (3h)
   - Toast/Notification
   - Alert
   - Progress
   - Spinner/Loading

4. Navigation components (3h)
   - Tabs
   - Breadcrumb
   - Pagination
   - Dropdown Menu

**Priority 2 - UI Components (Week 2)**:
5. Data display (4h)
   - Card (✅ already from shadcn)
   - Table
   - List
   - Badge
   - Avatar

6. Overlay components (3h)
   - Dialog (✅ already from shadcn)
   - Drawer/Sheet
   - Popover
   - Tooltip
   - Command Palette

7. Specialized components (4h)
   - DatePicker
   - Slider
   - Switch
   - Accordion
   - Skeleton

**Migration Process per Component**:
1. Install shadcn component: `pnpm dlx shadcn@latest add <component>`
2. Update existing usages
3. Apply design tokens
4. Test light/dark mode
5. Write Jest tests
6. Update Storybook (if exists)

**Success Criteria**:
- ✅ 20-30 components migrated
- ✅ Visual consistency
- ✅ All components support dark mode
- ✅ 90%+ test coverage maintained
- ✅ No regressions in existing functionality

---

## MONTH 4: BGAI QUALITY FRAMEWORK UI (Settimane 15-18)

### Sprint 9-10: Base Components & i18n

#### Issue #989: Base Components (Button, Card, Input, Form)
**Priority**: 🟡 HIGH
**Duration**: 3-4 giorni
**Dependencies**: #930 (component migration)
**Deliverables**:
- BGAI-specific component variants
- Form validation components
- Responsive layouts

**Note**: Leverage shadcn/ui installed in #988

**Tasks**:
1. Create `BoardGameAILayout` component (3h)
   ```tsx
   // src/components/bgai/BoardGameAILayout.tsx
   import { Card } from '@/components/ui/card';
   import { ThemeSwitcher } from '@/components/ThemeSwitcher';

   export function BoardGameAILayout({ children }: { children: React.ReactNode }) {
     return (
       <div className="min-h-screen bg-surface-base">
         <header className="border-b border-slate-200 dark:border-slate-800">
           <div className="container mx-auto px-4 py-4 flex justify-between items-center">
             <h1 className="text-2xl font-bold">MeepleAI - Board Game Assistant</h1>
             <ThemeSwitcher />
           </div>
         </header>

         <main className="container mx-auto px-4 py-8">
           {children}
         </main>
       </div>
     );
   }
   ```

2. Create form components with react-hook-form (4h)
   ```tsx
   // src/components/bgai/QuestionForm.tsx
   import { useForm } from 'react-hook-form';
   import { zodResolver } from '@hookform/resolvers/zod';
   import * as z from 'zod';
   import { Input } from '@/components/ui/input';
   import { Button } from '@/components/ui/button';

   const schema = z.object({
     question: z.string().min(10, 'La domanda deve avere almeno 10 caratteri'),
     gameId: z.number().positive()
   });

   export function QuestionForm({ onSubmit }: { onSubmit: (data: any) => void }) {
     const form = useForm({
       resolver: zodResolver(schema)
     });

     return (
       <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
         <Input
           {...form.register('question')}
           placeholder="Fai una domanda sul gioco..."
           error={form.formState.errors.question?.message}
         />
         <Button type="submit" disabled={form.formState.isSubmitting}>
           Chiedi all'AI
         </Button>
       </form>
     );
   }
   ```

3. Create responsive card layouts (3h)
4. Accessibility testing (2h)

---

#### Issue #990: i18n Setup (React Intl, it.json)
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: None
**Deliverables**:
- React Intl configured
- Italian translations (base set)
- Translation helper utilities

**Tasks**:
1. Install dependencies (30min)
   ```bash
   pnpm add react-intl
   pnpm add -D @formatjs/cli
   ```

2. Create `IntlProvider` wrapper (2h)
   ```tsx
   // src/contexts/IntlContext.tsx
   import { IntlProvider as ReactIntlProvider } from 'react-intl';
   import { useState, useEffect } from 'react';
   import it from '@/locales/it.json';
   import en from '@/locales/en.json';

   const messages = { it, en };

   export function IntlProvider({ children }: { children: React.ReactNode }) {
     const [locale, setLocale] = useState<'it' | 'en'>('it');

     useEffect(() => {
       const savedLocale = localStorage.getItem('locale') as 'it' | 'en';
       if (savedLocale) setLocale(savedLocale);
     }, []);

     return (
       <ReactIntlProvider messages={messages[locale]} locale={locale} defaultLocale="it">
         {children}
       </ReactIntlProvider>
     );
   }
   ```

3. Create initial `it.json` (3h)
   ```json
   {
     "bgai.title": "MeepleAI - Assistente Board Game",
     "bgai.ask.placeholder": "Fai una domanda sul gioco...",
     "bgai.ask.submit": "Chiedi all'AI",
     "bgai.response.confidence": "Confidenza: {confidence}%",
     "bgai.response.citations": "Fonti",
     "bgai.error.generic": "Si è verificato un errore. Riprova.",
     "bgai.loading": "Sto elaborando la tua domanda..."
   }
   ```

4. Create translation helper hook (2h)
   ```tsx
   // src/hooks/useTranslation.ts
   import { useIntl } from 'react-intl';

   export function useTranslation() {
     const intl = useIntl();

     const t = (id: string, values?: Record<string, any>) => {
       return intl.formatMessage({ id }, values);
     };

     return { t, locale: intl.locale };
   }
   ```

5. Integrate in `AppProviders` (`src/app/providers.tsx`) (1h)
6. Update components to use translations (4h)

**Success Criteria**:
- ✅ React Intl configured
- ✅ Italian as default locale
- ✅ 50+ initial translation keys
- ✅ Easy to add new translations

---

#### Issue #992: Frontend Component Testing (Jest 90%+)
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #989, #990
**Deliverables**:
- Jest tests for all BGAI components
- 90%+ coverage
- Accessibility tests

**Test Structure**:
```tsx
// src/components/bgai/__tests__/QuestionForm.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QuestionForm } from '../QuestionForm';

describe('QuestionForm', () => {
  it('renders input and submit button', () => {
    render(<QuestionForm onSubmit={jest.fn()} />);

    expect(screen.getByPlaceholderText(/fai una domanda/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /chiedi/i })).toBeInTheDocument();
  });

  it('validates minimum question length', async () => {
    render(<QuestionForm onSubmit={jest.fn()} />);

    const input = screen.getByPlaceholderText(/fai una domanda/i);
    fireEvent.change(input, { target: { value: 'test' } });
    fireEvent.submit(screen.getByRole('button'));

    await waitFor(() => {
      expect(screen.getByText(/almeno 10 caratteri/i)).toBeInTheDocument();
    });
  });

  it('calls onSubmit with valid data', async () => {
    const onSubmit = jest.fn();
    render(<QuestionForm onSubmit={onSubmit} />);

    const input = screen.getByPlaceholderText(/fai una domanda/i);
    fireEvent.change(input, { target: { value: 'Come si gioca?' } });
    fireEvent.submit(screen.getByRole('button'));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        question: 'Come si gioca?',
        gameId: expect.any(Number)
      });
    });
  });
});
```

**Coverage Targets**:
- Statements: >90%
- Branches: >85%
- Functions: >90%
- Lines: >90%

---

#### Issue #993: Responsive Design Testing (320px-1920px)
**Priority**: 🟡 HIGH
**Duration**: 2 giorni
**Dependencies**: #989
**Deliverables**:
- Responsive layouts tested
- Mobile-first approach validated
- Cross-browser testing

**Breakpoints**:
- Mobile: 320px - 640px
- Tablet: 641px - 1024px
- Desktop: 1025px - 1920px

**Test Viewports**:
```tsx
// jest.setup.js
const viewports = {
  mobile: { width: 375, height: 667 },
  tablet: { width: 768, height: 1024 },
  desktop: { width: 1920, height: 1080 }
};

// Responsive test example
describe('BoardGameAILayout - Responsive', () => {
  it.each([
    ['mobile', 375],
    ['tablet', 768],
    ['desktop', 1920]
  ])('renders correctly at %s viewport (%ipx)', (name, width) => {
    global.innerWidth = width;
    global.dispatchEvent(new Event('resize'));

    const { container } = render(<BoardGameAILayout />);
    expect(container).toMatchSnapshot(name);
  });
});
```

---

#### Issue #994: Frontend Build Optimization
**Priority**: 🟢 MEDIUM
**Duration**: 2 giorni
**Dependencies**: #989-993
**Deliverables**:
- Bundle size reduction (target: <200KB gzipped)
- Code splitting optimization
- Image optimization
- Performance budgets

**Optimizations**:
1. Dynamic imports for heavy components (2h)
   ```tsx
   const PDFViewer = dynamic(() => import('@/components/PDFViewer'), {
     loading: () => <Skeleton className="h-96" />,
     ssr: false
   });
   ```

2. Image optimization with Next.js Image (2h)
3. Bundle analysis (1h)
   ```bash
   pnpm add -D @next/bundle-analyzer
   ANALYZE=true pnpm build
   ```

4. Performance budgets in `next.config.js` (1h)
   ```js
   module.exports = {
     experimental: {
       optimizeCss: true
     },
     performance: {
       maxAssetSize: 200000, // 200KB
       maxEntrypointSize: 200000
     }
   };
   ```

---

#### Issue #995: Month 4 Integration Testing
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #989-994
**Deliverables**: 20 integration test scenarios

---

## MONTH 5: GOLDEN DATASET UI (Settimane 19-22)

### Sprint 11-12: Q&A Interface

#### Issue #1001: QuestionInputForm Component
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: #989 (base components)
**Deliverables**:
- Question input with validation
- Game selector integration
- Loading states
- Error handling

**Component Structure**:
```tsx
// src/components/bgai/QuestionInputForm.tsx
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useTranslation } from '@/hooks/useTranslation';

interface QuestionInputFormProps {
  games: Array<{ id: number; name: string }>;
  onSubmit: (question: string, gameId: number) => Promise<void>;
  isLoading?: boolean;
}

export function QuestionInputForm({ games, onSubmit, isLoading }: QuestionInputFormProps) {
  const { t } = useTranslation();
  const [selectedGame, setSelectedGame] = useState<number | null>(null);
  const { register, handleSubmit, formState: { errors }, reset } = useForm();

  const handleFormSubmit = async (data: any) => {
    if (!selectedGame) return;

    await onSubmit(data.question, selectedGame);
    reset();
  };

  return (
    <form onSubmit={handleSubmit(handleFormSubmit)} className="space-y-4">
      <div>
        <label className="block text-sm font-medium mb-2">
          {t('bgai.select.game')}
        </label>
        <Select
          value={selectedGame?.toString()}
          onValueChange={(value) => setSelectedGame(parseInt(value))}
        >
          <SelectTrigger>
            <SelectValue placeholder={t('bgai.select.game.placeholder')} />
          </SelectTrigger>
          <SelectContent>
            {games.map((game) => (
              <SelectItem key={game.id} value={game.id.toString()}>
                {game.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div>
        <label className="block text-sm font-medium mb-2">
          {t('bgai.question.label')}
        </label>
        <Input
          {...register('question', {
            required: t('bgai.question.required'),
            minLength: {
              value: 10,
              message: t('bgai.question.minLength')
            }
          })}
          placeholder={t('bgai.question.placeholder')}
          disabled={isLoading}
          error={errors.question?.message as string}
        />
      </div>

      <Button type="submit" disabled={!selectedGame || isLoading} className="w-full">
        {isLoading ? t('bgai.loading') : t('bgai.ask.submit')}
      </Button>
    </form>
  );
}
```

**Tests**: 10 test cases covering validation, game selection, loading states

---

#### Issue #1002: ResponseCard Component
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: #989
**Deliverables**:
- Answer display with formatting
- Confidence indicator
- Citation links
- Copy functionality

**Component**:
```tsx
// src/components/bgai/ResponseCard.tsx
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useTranslation } from '@/hooks/useTranslation';

interface ResponseCardProps {
  answer: string;
  confidence: number;
  citations: Array<{ page: number; text: string }>;
  onCitationClick?: (page: number) => void;
}

export function ResponseCard({ answer, confidence, citations, onCitationClick }: ResponseCardProps) {
  const { t } = useTranslation();

  const confidenceColor = confidence >= 0.8 ? 'bg-green-500' : confidence >= 0.6 ? 'bg-yellow-500' : 'bg-red-500';

  return (
    <Card>
      <CardHeader>
        <div className="flex justify-between items-center">
          <CardTitle>{t('bgai.response.title')}</CardTitle>
          <Badge className={confidenceColor}>
            {t('bgai.response.confidence', { confidence: Math.round(confidence * 100) })}
          </Badge>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        <div className="prose dark:prose-invert">
          {answer}
        </div>

        {citations.length > 0 && (
          <div>
            <h4 className="text-sm font-medium mb-2">{t('bgai.response.citations')}</h4>
            <ul className="space-y-1">
              {citations.map((citation, idx) => (
                <li key={idx} className="text-sm">
                  <Button
                    variant="link"
                    onClick={() => onCitationClick?.(citation.page)}
                    className="p-0 h-auto"
                  >
                    Pagina {citation.page}: "{citation.text.substring(0, 50)}..."
                  </Button>
                </li>
              ))}
            </ul>
          </div>
        )}

        <Button
          variant="outline"
          onClick={() => navigator.clipboard.writeText(answer)}
        >
          {t('bgai.response.copy')}
        </Button>
      </CardContent>
    </Card>
  );
}
```

---

#### Issue #1003: GameSelector Dropdown
**Priority**: 🟢 MEDIUM
**Duration**: 1-2 giorni
**Dependencies**: #989
**Deliverables**:
- Game selection dropdown
- Game metadata display (image, description)
- Search/filter functionality

**Enhanced Selector**:
```tsx
// src/components/bgai/GameSelector.tsx
import { useState } from 'react';
import { Command, CommandInput, CommandList, CommandEmpty, CommandGroup, CommandItem } from '@/components/ui/command';
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover';
import { Button } from '@/components/ui/button';

interface Game {
  id: number;
  name: string;
  description: string;
  imageUrl?: string;
}

export function GameSelector({ games, value, onChange }: {
  games: Game[];
  value: number | null;
  onChange: (gameId: number) => void;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');

  const selectedGame = games.find(g => g.id === value);

  const filtered = games.filter(g =>
    g.name.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="outline" className="w-full justify-between">
          {selectedGame ? selectedGame.name : 'Seleziona un gioco...'}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-80">
        <Command>
          <CommandInput
            placeholder="Cerca gioco..."
            value={search}
            onValueChange={setSearch}
          />
          <CommandList>
            <CommandEmpty>Nessun gioco trovato.</CommandEmpty>
            <CommandGroup>
              {filtered.map((game) => (
                <CommandItem
                  key={game.id}
                  onSelect={() => {
                    onChange(game.id);
                    setOpen(false);
                  }}
                >
                  <div className="flex items-center gap-2">
                    {game.imageUrl && (
                      <img src={game.imageUrl} alt={game.name} className="w-10 h-10 rounded" />
                    )}
                    <div>
                      <div className="font-medium">{game.name}</div>
                      <div className="text-xs text-slate-500">{game.description}</div>
                    </div>
                  </div>
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
```

---

#### Issue #1004: Loading and Error States
**Priority**: 🟢 MEDIUM
**Duration**: 1-2 giorni
**Dependencies**: #1001-1003
**Deliverables**:
- Loading skeleton components
- Error boundaries
- Retry mechanisms
- User-friendly error messages

**Loading States**:
```tsx
// src/components/bgai/LoadingStates.tsx
import { Skeleton } from '@/components/ui/skeleton';

export function ResponseSkeleton() {
  return (
    <div className="space-y-3">
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-5/6" />
      <Skeleton className="h-4 w-4/6" />
      <Skeleton className="h-20 w-full" />
    </div>
  );
}
```

**Error Handling**:
```tsx
// src/components/bgai/ErrorDisplay.tsx
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';

export function ErrorDisplay({ error, onRetry }: { error: Error; onRetry?: () => void }) {
  return (
    <Alert variant="destructive">
      <AlertTitle>Si è verificato un errore</AlertTitle>
      <AlertDescription>
        {error.message}
        {onRetry && (
          <Button variant="outline" size="sm" onClick={onRetry} className="mt-2">
            Riprova
          </Button>
        )}
      </AlertDescription>
    </Alert>
  );
}
```

---

#### Issue #1005: Jest Tests for Q&A Components (20 tests)
**Priority**: 🟡 HIGH
**Duration**: 2 giorni
**Dependencies**: #1001-1004
**Deliverables**: 20 comprehensive test cases

**Test Coverage**:
- QuestionInputForm: 8 tests
- ResponseCard: 6 tests
- GameSelector: 4 tests
- Error/Loading states: 2 tests

---

#### Issue #1006: Backend API Integration (handled in backend plan)

#### Issue #1007: Streaming SSE Support
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: Backend #1007
**Deliverables**:
- SSE client implementation
- Real-time response streaming
- Partial response rendering

**SSE Client**:
```tsx
// src/hooks/useStreamingResponse.ts
import { useState, useCallback } from 'react';

export function useStreamingResponse() {
  const [response, setResponse] = useState('');
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const streamQuestion = useCallback(async (question: string, gameId: number) => {
    setIsStreaming(true);
    setResponse('');
    setError(null);

    try {
      const eventSource = new EventSource(
        `/api/v1/board-game-ai/ask/stream?question=${encodeURIComponent(question)}&gameId=${gameId}`
      );

      eventSource.onmessage = (event) => {
        const chunk = JSON.parse(event.data);
        setResponse((prev) => prev + chunk.text);
      };

      eventSource.onerror = (err) => {
        setError(new Error('Streaming failed'));
        eventSource.close();
        setIsStreaming(false);
      };

      eventSource.addEventListener('done', () => {
        eventSource.close();
        setIsStreaming(false);
      });
    } catch (err) {
      setError(err as Error);
      setIsStreaming(false);
    }
  }, []);

  return { response, isStreaming, error, streamQuestion };
}
```

**Usage**:
```tsx
// src/app/board-game-ai/page.tsx (ask flow)
export default function AskPage() {
  const { response, isStreaming, streamQuestion } = useStreamingResponse();

  return (
    <div>
      <QuestionInputForm
        onSubmit={streamQuestion}
        isLoading={isStreaming}
      />

      {response && (
        <ResponseCard answer={response} />
      )}
    </div>
  );
}
```

---

#### Issue #1008: Error Handling and Retry Logic
**Priority**: 🟢 MEDIUM
**Duration**: 2 giorni
**Dependencies**: #1007
**Deliverables**:
- Automatic retry on transient failures
- Exponential backoff
- User-friendly error messages

**Retry Logic**:
```tsx
// src/hooks/useRetry.ts
import { useState, useCallback } from 'react';

export function useRetry<T>(
  fn: () => Promise<T>,
  maxRetries = 3,
  delayMs = 1000
) {
  const [retryCount, setRetryCount] = useState(0);

  const executeWithRetry = useCallback(async (): Promise<T> => {
    try {
      const result = await fn();
      setRetryCount(0);
      return result;
    } catch (error) {
      if (retryCount < maxRetries) {
        await new Promise(resolve => setTimeout(resolve, delayMs * Math.pow(2, retryCount)));
        setRetryCount(prev => prev + 1);
        return executeWithRetry();
      }
      throw error;
    }
  }, [fn, retryCount, maxRetries, delayMs]);

  return { executeWithRetry, retryCount };
}
```

---

#### Issue #1009: Month 5 E2E Testing
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #1001-1008
**Deliverables**: 15 Playwright E2E scenarios

**E2E Tests**:
```tsx
// e2e/board-game-ai-qa.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Board Game AI Q&A Flow', () => {
  test('should submit question and display answer', async ({ page }) => {
    await page.goto('/board-game-ai/ask');

    // Select game
    await page.click('[data-testid="game-selector"]');
    await page.click('text=Terraforming Mars');

    // Enter question
    await page.fill('[placeholder="Fai una domanda..."]', 'Come si calcola il punteggio finale?');

    // Submit
    await page.click('button:has-text("Chiedi")');

    // Wait for response
    await expect(page.locator('[data-testid="response-card"]')).toBeVisible({ timeout: 10000 });

    // Verify response content
    const response = await page.locator('[data-testid="response-answer"]').textContent();
    expect(response).toContain('punteggio');
  });

  test('should handle streaming response', async ({ page }) => {
    // Similar to above but verify incremental updates
  });

  test('should display citations', async ({ page }) => {
    // Verify citation links appear
  });
});
```

---

## MONTH 6: ITALIAN UI & PDF VIEWER (Settimane 23-28)

### Sprint 13-14: Final Polish

#### Issue #1013: PDF Viewer Integration (react-pdf)
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: Month 5 complete
**Deliverables**:
- PDF viewer component
- Page navigation
- Zoom controls
- Mobile-responsive

**PDF Viewer Component**:
```tsx
// src/components/bgai/PDFViewer.tsx
import { useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import { Button } from '@/components/ui/button';
import 'react-pdf/dist/esm/Page/AnnotationLayer.css';
import 'react-pdf/dist/esm/Page/TextLayer.css';

pdfjs.GlobalWorkerOptions.workerSrc = `//cdnjs.cloudflare.com/ajax/libs/pdf.js/${pdfjs.version}/pdf.worker.min.js`;

interface PDFViewerProps {
  pdfUrl: string;
  initialPage?: number;
}

export function PDFViewer({ pdfUrl, initialPage = 1 }: PDFViewerProps) {
  const [numPages, setNumPages] = useState<number>(0);
  const [pageNumber, setPageNumber] = useState(initialPage);
  const [scale, setScale] = useState(1.0);

  return (
    <div className="flex flex-col items-center space-y-4">
      <div className="flex gap-2">
        <Button
          onClick={() => setPageNumber(prev => Math.max(1, prev - 1))}
          disabled={pageNumber <= 1}
        >
          ← Indietro
        </Button>
        <span className="px-4 py-2">
          Pagina {pageNumber} di {numPages}
        </span>
        <Button
          onClick={() => setPageNumber(prev => Math.min(numPages, prev + 1))}
          disabled={pageNumber >= numPages}
        >
          Avanti →
        </Button>

        <Button onClick={() => setScale(prev => Math.min(2, prev + 0.1))}>
          Zoom +
        </Button>
        <Button onClick={() => setScale(prev => Math.max(0.5, prev - 0.1))}>
          Zoom -
        </Button>
      </div>

      <Document
        file={pdfUrl}
        onLoadSuccess={({ numPages }) => setNumPages(numPages)}
      >
        <Page
          pageNumber={pageNumber}
          scale={scale}
          renderTextLayer={true}
          renderAnnotationLayer={true}
        />
      </Document>
    </div>
  );
}
```

---

#### Issue #1014: Citation Click → Jump to Page
**Priority**: 🟡 HIGH
**Duration**: 2-3 giorni
**Dependencies**: #1013
**Deliverables**:
- Citation click handler
- Smooth scroll to page
- Highlight cited text

**Integration**:
```tsx
// src/app/board-game-ai/page.tsx (ask flow)
export default function AskPage() {
  const [pdfPage, setPdfPage] = useState(1);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);

  const handleCitationClick = (page: number) => {
    setPdfPage(page);
    // Scroll to PDF viewer
    document.getElementById('pdf-viewer')?.scrollIntoView({ behavior: 'smooth' });
  };

  return (
    <div className="grid md:grid-cols-2 gap-8">
      <div>
        <QuestionInputForm onSubmit={handleSubmit} />

        {response && (
          <ResponseCard
            answer={response.answer}
            citations={response.citations}
            onCitationClick={handleCitationClick}
          />
        )}
      </div>

      <div id="pdf-viewer">
        {pdfUrl && <PDFViewer pdfUrl={pdfUrl} initialPage={pdfPage} />}
      </div>
    </div>
  );
}
```

---

#### Issue #1015: PDF Viewer Tests (Jest + Playwright)
**Priority**: 🟢 MEDIUM
**Duration**: 2 giorni
**Dependencies**: #1013, #1014
**Deliverables**: 8 test cases

---

#### Issue #1016: Complete Italian UI Strings (200+ Translations)
**Priority**: 🔴 CRITICAL
**Duration**: 2-3 giorni
**Dependencies**: #990 (i18n setup)
**Deliverables**:
- Complete `it.json` with 200+ keys
- Translation review and QA
- Fallback to English for missing keys

**Translation Categories**:
1. Navigation (20 keys)
2. Forms and inputs (40 keys)
3. Errors and validation (30 keys)
4. BGAI Q&A interface (50 keys)
5. PDF viewer (20 keys)
6. Game catalog (20 keys)
7. Common UI (20 keys)

---

#### Issue #1017: Game Catalog Page (/board-game-ai/games)
**Priority**: 🟢 MEDIUM
**Duration**: 2-3 giorni
**Dependencies**: Backend #1017
**Deliverables**:
- Game catalog page
- Game cards with metadata
- Search and filter
- Responsive grid layout

**Page Component**:
```tsx
// src/app/board-game-ai/games/page.tsx
import { useState, useEffect } from 'react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';

export default function GamesPage() {
  const [games, setGames] = useState([]);
  const [search, setSearch] = useState('');

  useEffect(() => {
    fetch('/api/v1/board-game-ai/games')
      .then(res => res.json())
      .then(setGames);
  }, []);

  const filtered = games.filter(g =>
    g.name.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div>
      <Input
        placeholder="Cerca gioco..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="mb-6"
      />

      <div className="grid md:grid-cols-3 gap-6">
        {filtered.map((game) => (
          <Card key={game.id}>
            <CardHeader>
              <CardTitle>{game.name}</CardTitle>
              <CardDescription>{game.players} giocatori</CardDescription>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-600">{game.description}</p>
              <Button className="mt-4" asChild>
                <Link href={`/board-game-ai/ask?game=${game.id}`}>
                  Fai una domanda
                </Link>
              </Button>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
```

---

#### Issue #1018: E2E Testing (Question → PDF Citation)
**Priority**: 🟡 HIGH
**Duration**: 2 giorni
**Dependencies**: #1013-1017
**Deliverables**: 20 comprehensive E2E tests

---

## SUCCESS METRICS

### Technical Metrics
- ✅ Component test coverage: ≥90%
- ✅ E2E test coverage: 50+ scenarios
- ✅ Bundle size: <200KB gzipped
- ✅ Lighthouse score: ≥90
- ✅ WCAG 2.1 AA compliance: 100%

### UX Metrics
- ✅ First Contentful Paint: <1.5s
- ✅ Time to Interactive: <3s
- ✅ Mobile usability: 100%
- ✅ Italian UI complete: 200+ translations

---

**End of Frontend Implementation Plan**
