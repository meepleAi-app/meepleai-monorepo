# Welcome Page Redesign — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current 5-section landing page with a minimal 4-section "Progressive Reveal" design focused on the AI game companion value proposition.

**Architecture:** Four new server components replace five client/server components. No client-side JS needed. Anchor-based smooth scroll via CSS. Existing PublicLayout and PublicFooter remain unchanged.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, shadcn/ui (`Button` from `@/components/ui/primitives/button`), `next/link`

**Spec:** `docs/superpowers/specs/2026-03-12-welcome-page-redesign.md`

---

## File Map

### Create
| File | Responsibility |
|------|---------------|
| `apps/web/src/components/landing/WelcomeHero.tsx` | Hero section: kicker, heading, subheading, 2 CTAs |
| `apps/web/src/components/landing/HowItWorksSteps.tsx` | 4-step progressive reveal with connectors |
| `apps/web/src/components/landing/SocialProofBar.tsx` | 3 stats row |
| `apps/web/src/components/landing/WelcomeCTA.tsx` | Final CTA section |
| `apps/web/src/components/landing/__tests__/WelcomeHero.test.tsx` | Unit test |
| `apps/web/src/components/landing/__tests__/HowItWorksSteps.test.tsx` | Unit test |
| `apps/web/src/components/landing/__tests__/SocialProofBar.test.tsx` | Unit test |
| `apps/web/src/components/landing/__tests__/WelcomeCTA.test.tsx` | Unit test |

### Modify
| File | Change |
|------|--------|
| `apps/web/src/app/(public)/page.tsx` | Replace imports, update metadata, update structured data, simplify JSX |
| `apps/web/src/components/landing/index.ts` | Update barrel exports |
| `apps/web/src/app/globals.css` | Add `scroll-behavior: smooth` to `html` |

### Delete (after verification)
| File | Reason |
|------|--------|
| `apps/web/src/components/landing/HeroSection.tsx` | Replaced |
| `apps/web/src/components/landing/HeroSection.stories.tsx` | Replaced |
| `apps/web/src/components/landing/__tests__/HeroSection.test.tsx` | Replaced |
| `apps/web/src/components/landing/GamesCarouselSection.tsx` | Removed |
| `apps/web/src/components/landing/FeaturesSection.tsx` | Replaced |
| `apps/web/src/components/landing/FeaturesSection.stories.tsx` | Replaced |
| `apps/web/src/components/landing/__tests__/FeaturesSection.test.tsx` | Replaced |
| `apps/web/src/components/landing/HowItWorksSection.tsx` | Replaced |
| `apps/web/src/components/landing/HowItWorksSection.stories.tsx` | Replaced |
| `apps/web/src/components/landing/__tests__/HowItWorksSection.test.tsx` | Replaced |
| `apps/web/src/components/landing/CallToActionSection.tsx` | Replaced |
| `apps/web/src/components/landing/__tests__/CallToActionSection.test.tsx` | Replaced (may not exist on disk) |

---

## Prerequisite: Verify jest-axe dependency

Run: `cd apps/web && grep jest-axe package.json`

If not found, install it: `cd apps/web && pnpm add -D jest-axe @types/jest-axe`

---

## Chunk 1: New Components + Tests

### Task 1: WelcomeHero

**Files:**
- Create: `apps/web/src/components/landing/WelcomeHero.tsx`
- Create: `apps/web/src/components/landing/__tests__/WelcomeHero.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/landing/__tests__/WelcomeHero.test.tsx
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { WelcomeHero } from '../WelcomeHero';

expect.extend(toHaveNoViolations);

describe('WelcomeHero', () => {
  it('renders heading', () => {
    render(<WelcomeHero />);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
      'Ogni serata giochi merita un arbitro'
    );
  });

  it('renders kicker text', () => {
    render(<WelcomeHero />);
    expect(screen.getByText('Il tuo compagno di gioco AI')).toBeInTheDocument();
  });

  it('renders primary CTA linking to /register', () => {
    render(<WelcomeHero />);
    const cta = screen.getByRole('link', { name: /inizia gratis/i });
    expect(cta).toHaveAttribute('href', '/register');
  });

  it('renders secondary CTA as anchor to #come-funziona', () => {
    render(<WelcomeHero />);
    const cta = screen.getByRole('link', { name: /scopri come funziona/i });
    expect(cta).toHaveAttribute('href', '#come-funziona');
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<WelcomeHero />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/landing/__tests__/WelcomeHero.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write minimal implementation**

```tsx
// apps/web/src/components/landing/WelcomeHero.tsx
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

export function WelcomeHero() {
  return (
    <section className="flex min-h-[80vh] flex-col items-center justify-center px-4 py-20 text-center">
      <p className="mb-4 text-sm uppercase tracking-widest text-muted-foreground">
        Il tuo compagno di gioco AI
      </p>
      <h1 className="mb-4 max-w-2xl text-4xl font-extrabold leading-tight text-foreground sm:text-5xl lg:text-6xl">
        Ogni serata giochi merita un arbitro
      </h1>
      <p className="mb-10 max-w-lg text-lg text-muted-foreground">
        Setup, regole, punteggi, dispute — un agente AI che conosce il tuo gioco
        e vi aiuta al tavolo.
      </p>
      <div className="flex flex-col gap-3 sm:flex-row">
        <Button asChild size="lg">
          <Link href="/register">Inizia gratis</Link>
        </Button>
        <Button asChild variant="outline" size="lg">
          <Link href="#come-funziona">Scopri come funziona ↓</Link>
        </Button>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/landing/__tests__/WelcomeHero.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/landing/WelcomeHero.tsx apps/web/src/components/landing/__tests__/WelcomeHero.test.tsx
git commit -m "feat(landing): add WelcomeHero server component + tests"
```

---

### Task 2: HowItWorksSteps

**Files:**
- Create: `apps/web/src/components/landing/HowItWorksSteps.tsx`
- Create: `apps/web/src/components/landing/__tests__/HowItWorksSteps.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/landing/__tests__/HowItWorksSteps.test.tsx
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { HowItWorksSteps } from '../HowItWorksSteps';

expect.extend(toHaveNoViolations);

describe('HowItWorksSteps', () => {
  it('renders section with id="come-funziona"', () => {
    const { container } = render(<HowItWorksSteps />);
    expect(container.querySelector('#come-funziona')).toBeInTheDocument();
  });

  it('renders all 4 step titles', () => {
    render(<HowItWorksSteps />);
    expect(screen.getByText('Trova il gioco')).toBeInTheDocument();
    expect(screen.getByText('Carica le regole')).toBeInTheDocument();
    expect(screen.getByText("Gioca con l'arbitro AI")).toBeInTheDocument();
    expect(screen.getByText('Salva e riprendi')).toBeInTheDocument();
  });

  it('renders section heading', () => {
    render(<HowItWorksSteps />);
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(
      'Come funziona'
    );
  });

  it('renders 4 step numbers', () => {
    render(<HowItWorksSteps />);
    for (const num of ['1', '2', '3', '4']) {
      expect(screen.getByText(num)).toBeInTheDocument();
    }
  });

  it('renders 4 step headings as h3', () => {
    render(<HowItWorksSteps />);
    expect(screen.getAllByRole('heading', { level: 3 })).toHaveLength(4);
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<HowItWorksSteps />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/landing/__tests__/HowItWorksSteps.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write minimal implementation**

```tsx
// apps/web/src/components/landing/HowItWorksSteps.tsx
const steps = [
  { num: '1', icon: '🎲', title: 'Trova il gioco', desc: 'Cerca nel catalogo o su BGG. Non c\'è? Aggiungilo come gioco privato.' },
  { num: '2', icon: '📄', title: 'Carica le regole', desc: 'Upload del PDF del regolamento. L\'AI lo indicizza automaticamente.' },
  { num: '3', icon: '🤖', title: "Gioca con l'arbitro AI", desc: 'Setup, regole, punteggi, dispute — l\'agente vi assiste al tavolo.' },
  { num: '4', icon: '💾', title: 'Salva e riprendi', desc: 'Non finite? L\'agente ricorda lo stato della partita per la prossima volta.' },
];

export function HowItWorksSteps() {
  return (
    <section id="come-funziona" className="bg-muted/30 px-4 py-20">
      <div className="mx-auto max-w-5xl">
        <h2 className="mb-12 text-center text-3xl font-bold text-foreground">
          Come funziona
        </h2>
        <div className="grid grid-cols-1 gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {steps.map((step) => (
            <div key={step.num} className="flex flex-col items-center text-center">
              <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-foreground">
                {step.num}
              </div>
              <span className="mb-2 text-2xl">{step.icon}</span>
              <h3 className="mb-1 text-lg font-semibold text-foreground">
                {step.title}
              </h3>
              <p className="text-sm text-muted-foreground">{step.desc}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/landing/__tests__/HowItWorksSteps.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/landing/HowItWorksSteps.tsx apps/web/src/components/landing/__tests__/HowItWorksSteps.test.tsx
git commit -m "feat(landing): add HowItWorksSteps server component + tests"
```

---

### Task 3: SocialProofBar

**Files:**
- Create: `apps/web/src/components/landing/SocialProofBar.tsx`
- Create: `apps/web/src/components/landing/__tests__/SocialProofBar.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/landing/__tests__/SocialProofBar.test.tsx
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { SocialProofBar } from '../SocialProofBar';

expect.extend(toHaveNoViolations);

describe('SocialProofBar', () => {
  it('renders three stats', () => {
    render(<SocialProofBar />);
    expect(screen.getByText('2.400+')).toBeInTheDocument();
    expect(screen.getByText('95%+')).toBeInTheDocument();
    expect(screen.getByText('Gratis')).toBeInTheDocument();
  });

  it('renders stat labels', () => {
    render(<SocialProofBar />);
    expect(screen.getByText('Giochi nel catalogo')).toBeInTheDocument();
    expect(screen.getByText('Accuratezza citazioni')).toBeInTheDocument();
    expect(screen.getByText('Per iniziare')).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<SocialProofBar />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/landing/__tests__/SocialProofBar.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write minimal implementation**

```tsx
// apps/web/src/components/landing/SocialProofBar.tsx
const stats = [
  { value: '2.400+', label: 'Giochi nel catalogo' },
  { value: '95%+', label: 'Accuratezza citazioni' },
  { value: 'Gratis', label: 'Per iniziare' },
];

export function SocialProofBar() {
  return (
    <section className="bg-muted/50 px-4 py-16">
      <div className="mx-auto flex max-w-3xl flex-col items-center gap-8 sm:flex-row sm:justify-center sm:gap-16">
        {stats.map((stat) => (
          <div key={stat.label} className="text-center">
            <p className="text-4xl font-bold text-foreground">{stat.value}</p>
            <p className="text-sm text-muted-foreground">{stat.label}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/landing/__tests__/SocialProofBar.test.tsx`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/landing/SocialProofBar.tsx apps/web/src/components/landing/__tests__/SocialProofBar.test.tsx
git commit -m "feat(landing): add SocialProofBar server component + tests"
```

---

### Task 4: WelcomeCTA

**Files:**
- Create: `apps/web/src/components/landing/WelcomeCTA.tsx`
- Create: `apps/web/src/components/landing/__tests__/WelcomeCTA.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/landing/__tests__/WelcomeCTA.test.tsx
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { WelcomeCTA } from '../WelcomeCTA';

expect.extend(toHaveNoViolations);

describe('WelcomeCTA', () => {
  it('renders heading', () => {
    render(<WelcomeCTA />);
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(
      'Pronto per la prossima serata giochi?'
    );
  });

  it('renders primary CTA linking to /register', () => {
    render(<WelcomeCTA />);
    const cta = screen.getByRole('link', { name: /inizia gratis/i });
    expect(cta).toHaveAttribute('href', '/register');
  });

  it('renders secondary CTA linking to /games', () => {
    render(<WelcomeCTA />);
    const cta = screen.getByRole('link', { name: /esplora il catalogo/i });
    expect(cta).toHaveAttribute('href', '/games');
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<WelcomeCTA />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/landing/__tests__/WelcomeCTA.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write minimal implementation**

```tsx
// apps/web/src/components/landing/WelcomeCTA.tsx
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

export function WelcomeCTA() {
  return (
    <section className="px-4 py-20 text-center">
      <h2 className="mb-4 text-3xl font-bold text-foreground">
        Pronto per la prossima serata giochi?
      </h2>
      <p className="mb-8 text-lg text-muted-foreground">
        Registrati gratis e prepara il tuo primo agente AI in 5 minuti
      </p>
      <div className="flex flex-col items-center gap-3 sm:flex-row sm:justify-center">
        <Button asChild size="lg">
          <Link href="/register">Inizia gratis</Link>
        </Button>
        <Button asChild variant="outline" size="lg">
          <Link href="/games">Esplora il catalogo</Link>
        </Button>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/landing/__tests__/WelcomeCTA.test.tsx`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/landing/WelcomeCTA.tsx apps/web/src/components/landing/__tests__/WelcomeCTA.test.tsx
git commit -m "feat(landing): add WelcomeCTA server component + tests"
```

---

## Chunk 2: Wire Up Page + Cleanup

### Task 5: Add scroll-behavior smooth

**Files:**
- Modify: `apps/web/src/app/globals.css`

- [ ] **Step 1: Add `scroll-behavior: smooth` to the html rule in globals.css**

Find the `html` or `:root` rule and add `scroll-behavior: smooth`. If no html rule exists, add:

```css
html {
  scroll-behavior: smooth;
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/app/globals.css
git commit -m "fix(web): add scroll-behavior smooth to html"
```

---

### Task 6: Update page.tsx — metadata, structured data, imports

**Files:**
- Modify: `apps/web/src/app/(public)/page.tsx`

- [ ] **Step 1: Update metadata**

Replace the existing `metadata` export with updated title, description, keywords. Remove `alternates.languages['en-US']`. Update OpenGraph and Twitter descriptions.

Key changes:
- `title`: `'MeepleAI — Il tuo arbitro AI per le serate giochi da tavolo'`
- `description`: `'Un agente AI che conosce le regole del tuo gioco. Setup, punteggi, dispute — ti assiste al tavolo. Gratis per iniziare.'`
- `keywords`: add `'arbitro'`, `'agente AI'`, `'serata giochi'`
- Remove `alternates.languages` `'en-US'` entry (keep `'it-IT'` only)
- `openGraph.title`: `'MeepleAI — Il tuo arbitro AI per le serate giochi da tavolo'`
- `openGraph.description`: `'Un agente AI che conosce le regole del tuo gioco. Setup, punteggi, dispute — ti assiste al tavolo. Gratis per iniziare.'`
- `twitter.title`: `'MeepleAI — Il tuo arbitro AI per le serate giochi da tavolo'`
- `twitter.description`: `'Un agente AI che conosce le regole del tuo gioco. Setup, punteggi, dispute — ti assiste al tavolo.'`

- [ ] **Step 2: Update structured data**

Remove `aggregateRating` block entirely. Update `featureList`:

```typescript
featureList: [
  'Cerca giochi nel catalogo o importa da BGG',
  'Carica il PDF del regolamento per indicizzazione AI',
  'Agente AI per setup, regole, punteggi e dispute al tavolo',
  'Salva lo stato della partita e riprendi quando vuoi',
],
```

- [ ] **Step 3: Replace imports and JSX**

Replace old imports:
```typescript
// Remove these:
import { CallToActionSection } from '@/components/landing/CallToActionSection';
import { FeaturesSection } from '@/components/landing/FeaturesSection';
import { GamesCarouselSection } from '@/components/landing/GamesCarouselSection';
import { HeroSection } from '@/components/landing/HeroSection';
import { HowItWorksSection } from '@/components/landing/HowItWorksSection';

// Add these:
import { HowItWorksSteps } from '@/components/landing/HowItWorksSteps';
import { SocialProofBar } from '@/components/landing/SocialProofBar';
import { WelcomeCTA } from '@/components/landing/WelcomeCTA';
import { WelcomeHero } from '@/components/landing/WelcomeHero';
```

Replace JSX body:
```tsx
<>
  <WelcomeHero />
  <HowItWorksSteps />
  <SocialProofBar />
  <WelcomeCTA />
</>
```

- [ ] **Step 4: Run all landing tests**

Run: `cd apps/web && pnpm vitest run src/components/landing/`
Expected: all new tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/(public)/page.tsx
git commit -m "feat(landing): wire up new welcome page components, update SEO metadata"
```

---

### Task 7: Update barrel exports

**Files:**
- Modify: `apps/web/src/components/landing/index.ts`

- [ ] **Step 1: Verify no external file imports old components from barrel**

Run: `grep -r "from '@/components/landing'" apps/web/src/ --include="*.tsx" --include="*.ts" -l`

Check that no file outside `components/landing/` imports `HeroSection`, `FeaturesSection`, `HowItWorksSection`, `CallToActionSection`, or `GamesCarouselSection` from the barrel. If found, update those imports first.

- [ ] **Step 2: Replace barrel exports**

```typescript
// apps/web/src/components/landing/index.ts
export { AuthRedirect } from './AuthRedirect';
export { HowItWorksSteps } from './HowItWorksSteps';
export { LandingFooter } from './LandingFooter';
export { SocialProofBar } from './SocialProofBar';
export { WelcomeCTA } from './WelcomeCTA';
export { WelcomeHero } from './WelcomeHero';
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/landing/index.ts
git commit -m "refactor(landing): update barrel exports for new components"
```

---

### Task 8: Delete old components

**Files to delete:**
- `apps/web/src/components/landing/HeroSection.tsx`
- `apps/web/src/components/landing/HeroSection.stories.tsx`
- `apps/web/src/components/landing/__tests__/HeroSection.test.tsx`
- `apps/web/src/components/landing/GamesCarouselSection.tsx`
- `apps/web/src/components/landing/FeaturesSection.tsx`
- `apps/web/src/components/landing/FeaturesSection.stories.tsx`
- `apps/web/src/components/landing/__tests__/FeaturesSection.test.tsx`
- `apps/web/src/components/landing/HowItWorksSection.tsx`
- `apps/web/src/components/landing/HowItWorksSection.stories.tsx`
- `apps/web/src/components/landing/__tests__/HowItWorksSection.test.tsx`
- `apps/web/src/components/landing/CallToActionSection.tsx`

- [ ] **Step 1: Verify no other imports reference old components**

Run: `grep -r "HeroSection\|GamesCarouselSection\|FeaturesSection\|HowItWorksSection\|CallToActionSection" apps/web/src/ --include="*.tsx" --include="*.ts" -l`

Expected: only the files being deleted (and possibly stories). If any other file imports them, update those imports first.

- [ ] **Step 2: Delete files**

```bash
git rm apps/web/src/components/landing/HeroSection.tsx
git rm apps/web/src/components/landing/HeroSection.stories.tsx
git rm apps/web/src/components/landing/__tests__/HeroSection.test.tsx
git rm apps/web/src/components/landing/GamesCarouselSection.tsx
git rm apps/web/src/components/landing/FeaturesSection.tsx
git rm apps/web/src/components/landing/FeaturesSection.stories.tsx
git rm apps/web/src/components/landing/__tests__/FeaturesSection.test.tsx
git rm apps/web/src/components/landing/HowItWorksSection.tsx
git rm apps/web/src/components/landing/HowItWorksSection.stories.tsx
git rm apps/web/src/components/landing/__tests__/HowItWorksSection.test.tsx
git rm apps/web/src/components/landing/CallToActionSection.tsx
git rm -f apps/web/src/components/landing/__tests__/CallToActionSection.test.tsx 2>/dev/null || true
```

- [ ] **Step 3: Check for orphaned carousel hooks**

Run: `grep -r "useFeaturedGames\|useTrendingGames\|useUserLibraryGames" apps/web/src/ --include="*.tsx" --include="*.ts" -l`

If hooks are still used by `gallery/page.tsx` or other files, leave them. If only referenced by deleted `GamesCarouselSection.tsx`, delete the hook file too.

- [ ] **Step 4: Run full test suite**

Run: `cd apps/web && pnpm vitest run src/components/landing/`
Expected: all tests PASS (only new component tests remain)

- [ ] **Step 5: Commit**

```bash
git add -A apps/web/src/components/landing/
git commit -m "refactor(landing): remove old landing components and stories"
```

---

### Task 9: Final verification

- [ ] **Step 1: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors

- [ ] **Step 2: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: no errors

- [ ] **Step 3: Run full test suite**

Run: `cd apps/web && pnpm test`
Expected: all tests pass

- [ ] **Step 4: Visual check (if dev server available)**

Run: `cd apps/web && pnpm dev` and visit `http://localhost:3000`
Verify: 4 sections render, CTAs link correctly, dark mode works, responsive layout correct.
