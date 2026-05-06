# Nav UX Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Risolvere 3 problemi UX evidenziati nella pagina Library: emoji nella sidebar, sub-nav tab ridondanti/etichette poco chiare, CTA ambigua.

**Architecture:** Tre modifiche indipendenti su file di configurazione e componenti di layout. Nessuna modifica al contenuto delle pagine o alle API. Retrocompatibile con Alpha Mode. Ogni task è deployabile autonomamente.

**Tech Stack:** Next.js 16 · React 19 · Lucide React · Vitest · Tailwind 4

---

## File Map

| File | Tipo | Task |
|------|------|------|
| `apps/web/src/components/layout/UserShell/HybridSidebar.tsx` | Modify | T1 |
| `apps/web/src/config/library-navigation.ts` | Modify | T2 |
| `apps/web/src/app/(authenticated)/library/_content.tsx` | Modify | T2 |
| `apps/web/src/config/contextual-cta.ts` | Modify | T3 |
| `apps/web/src/__tests__/config/library-navigation.test.ts` | Create | T2 |
| `apps/web/src/__tests__/config/contextual-cta.test.ts` | Create | T3 |

---

## Task 1: HybridSidebar — Emoji → Lucide Icons

**Problema:** Icone emoji (🏠📚🎲👥🕐✨🤖⚙️) hanno rendering cross-platform inconsistente e aspetto informale. `lucide-react` è già una dipendenza del progetto (usata in `library-navigation.ts`).

**File:**
- Modify: `apps/web/src/components/layout/UserShell/HybridSidebar.tsx`

---

- [ ] **1.1 Aggiorna interfaccia NavItem — `icon: string` → `icon: LucideIcon`**

Sostituisci la definizione dell'interfaccia a riga 20:

```tsx
// PRIMA (riga 20-28)
interface NavItem {
  label: string;
  icon: string;
  href: string;
  activeMatch?: string;
  activeSearchParam?: { key: string; value: string };
}

// DOPO
import type { LucideIcon } from 'lucide-react';

interface NavItem {
  label: string;
  icon: LucideIcon;
  href: string;
  activeMatch?: string;
  activeSearchParam?: { key: string; value: string };
}
```

---

- [ ] **1.2 Aggiungi import Lucide e sostituisci emoji nei dati**

Aggiungi import dopo `import Link from 'next/link';`:

```tsx
import {
  Bot,
  Dices,
  History,
  LayoutDashboard,
  Library,
  Settings,
  Sparkles,
  Users,
} from 'lucide-react';
```

Sostituisci `NAV_SECTIONS` (riga 35-58) e `BOTTOM_ITEMS` (riga 60):

```tsx
const NAV_SECTIONS: NavSection[] = [
  {
    title: 'Navigazione',
    items: [
      { label: 'Dashboard', icon: LayoutDashboard, href: '/dashboard' },
      {
        label: 'Libreria',
        icon: Library,
        href: '/library',
        activeMatch: '/library',
      },
      { label: 'Sessioni', icon: Dices, href: '/sessions', activeMatch: '/sessions' },
      { label: 'Giocatori', icon: Users, href: '/players', activeMatch: '/players' },
      { label: 'Storico', icon: History, href: '/play-records', activeMatch: '/play-records' },
    ],
  },
  {
    title: 'AI',
    items: [
      { label: 'Chat RAG', icon: Sparkles, href: '/chat', activeMatch: '/chat' },
      { label: 'Agenti', icon: Bot, href: '/agents', activeMatch: '/agents' },
    ],
  },
];

const BOTTOM_ITEMS: NavItem[] = [{ label: 'Impostazioni', icon: Settings, href: '/settings' }];
```

---

- [ ] **1.3 Aggiorna `SidebarLink` per renderizzare `LucideIcon` come componente**

Sostituisci la funzione `SidebarLink` (riga 83-115):

```tsx
function SidebarLink({ item }: { item: NavItem }) {
  const isActive = useIsActive(item);
  const Icon = item.icon;

  return (
    <Link
      href={item.href}
      className={cn(
        'relative flex items-center gap-3 px-[9px] py-2 mx-1 rounded-lg',
        'transition-colors duration-200',
        isActive
          ? 'bg-primary text-white shadow-[0_2px_8px_rgba(180,80,0,0.3)]'
          : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'
      )}
      aria-label={item.label}
      aria-current={isActive ? 'page' : undefined}
    >
      <span className="flex items-center justify-center w-[34px] h-[34px] shrink-0">
        <Icon className="h-5 w-5" aria-hidden="true" />
      </span>
      <span
        className={cn(
          'text-sm font-medium whitespace-nowrap',
          'opacity-0 group-hover:opacity-100 group-focus-within:opacity-100',
          'transition-opacity duration-300'
        )}
      >
        {item.label}
      </span>
    </Link>
  );
}
```

---

- [ ] **1.4 Verifica typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -30
```

Expected: nessun errore TS relativo a HybridSidebar.

---

- [ ] **1.5 Commit**

```bash
git add apps/web/src/components/layout/UserShell/HybridSidebar.tsx
git commit -m "feat(sidebar): sostituisci emoji con Lucide icons"
```

---

## Task 2: LibraryNavTabs — 3 Tab con Label Italiane

**Problema:** 4 tab ("Collection", "Games", "Wishlist", "Proposals") — etichette in inglese, semantica sovrapposta tra "Collection" e "Games" (entrambi renderizzano `PersonalLibraryPage`), "Proposals" non esiste in Alpha Mode.

**Soluzione:** Ridurre a 3 tab con label italiane chiare. Rendere "I Miei Giochi" il tab di default. Rinominare "Collection" in "Catalogo" (shared catalog). In Alpha Mode: solo 2 tab ("I Miei Giochi" + "Wishlist"). Aggiornare `_content.tsx` per il nuovo tab ID `catalogo`.

**File:**
- Modify: `apps/web/src/config/library-navigation.ts`
- Modify: `apps/web/src/app/(authenticated)/library/_content.tsx`
- Create: `apps/web/src/__tests__/config/library-navigation.test.ts`

---

- [ ] **2.1 Scrivi i test per `getActiveLibraryTab` con i nuovi ID**

Crea `apps/web/src/__tests__/config/library-navigation.test.ts`:

```ts
/**
 * Library Navigation Config Tests
 * Verifica la logica di attivazione tab per i nuovi ID (games/wishlist/catalogo).
 */

import { describe, it, expect } from 'vitest';

import { LIBRARY_TABS, getActiveLibraryTab } from '@/config/library-navigation';

describe('getActiveLibraryTab', () => {
  it('ritorna "games" per /library senza tab param (default)', () => {
    expect(getActiveLibraryTab('/library')).toBe('games');
  });

  it('ritorna "games" per /library?tab=games', () => {
    expect(getActiveLibraryTab('/library', '?tab=games')).toBe('games');
  });

  it('ritorna "wishlist" per ?tab=wishlist', () => {
    expect(getActiveLibraryTab('/library', '?tab=wishlist')).toBe('wishlist');
  });

  it('ritorna "catalogo" per ?tab=catalogo', () => {
    expect(getActiveLibraryTab('/library', '?tab=catalogo')).toBe('catalogo');
  });

  it('ritorna "games" per tab sconosciuto (fallback)', () => {
    expect(getActiveLibraryTab('/library', '?tab=unknown')).toBe('games');
  });

  it('ritorna "games" per stringa search vuota', () => {
    expect(getActiveLibraryTab('/library', '')).toBe('games');
  });
});

describe('LIBRARY_TABS struttura', () => {
  it('ha esattamente 3 tab in modalità non-alpha', () => {
    // In test environment NEXT_PUBLIC_ALPHA_MODE non è impostata → non-alpha
    expect(LIBRARY_TABS.length).toBeGreaterThanOrEqual(2);
    expect(LIBRARY_TABS.length).toBeLessThanOrEqual(3);
  });

  it('contiene tab con id "games"', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'games')).toBe(true);
  });

  it('contiene tab con id "wishlist"', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'wishlist')).toBe(true);
  });

  it('label del tab "games" è "I Miei Giochi"', () => {
    const tab = LIBRARY_TABS.find(t => t.id === 'games');
    expect(tab?.label).toBe('I Miei Giochi');
  });

  it('label del tab "wishlist" è "Wishlist"', () => {
    const tab = LIBRARY_TABS.find(t => t.id === 'wishlist');
    expect(tab?.label).toBe('Wishlist');
  });

  it('href del tab "games" è "/library" (default, nessun param)', () => {
    const tab = LIBRARY_TABS.find(t => t.id === 'games');
    expect(tab?.href).toBe('/library');
  });

  it('href del tab "wishlist" è "/library?tab=wishlist"', () => {
    const tab = LIBRARY_TABS.find(t => t.id === 'wishlist');
    expect(tab?.href).toBe('/library?tab=wishlist');
  });

  it('non contiene più il tab "proposals"', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'proposals')).toBe(false);
  });

  it('non contiene più il tab "collection" (rinominato in "catalogo")', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'collection')).toBe(false);
  });

  it('non contiene più il tab "private" (rinominato in "games")', () => {
    expect(LIBRARY_TABS.some(t => t.id === 'private')).toBe(false);
  });
});
```

---

- [ ] **2.2 Esegui i test — verifica che falliscano**

```bash
cd apps/web && pnpm test src/__tests__/config/library-navigation.test.ts --reporter=verbose 2>&1
```

Expected: FAIL — i tab ID `games`, `catalogo` non esistono ancora nel config.

---

- [ ] **2.3 Aggiorna `library-navigation.ts`**

Sostituisci l'intero contenuto del file:

```ts
/**
 * Library Section Navigation Configuration
 * Issue #5167 — Tab rename: Games (personal) / Collection (shared catalog)
 * Nav UX Simplification — riduzione a 3 tab con label italiane
 *
 * Tab ID mapping (nuovo → vecchio):
 *   games    ← private  (personal library, default)
 *   wishlist ← wishlist (unchanged)
 *   catalogo ← collection (shared catalog, ex-"public")
 *
 * Alpha Mode: solo games + wishlist (catalogo nascosto)
 */

import { type LucideIcon, BookOpenIcon, Gamepad2, Heart } from 'lucide-react';

/**
 * Tab definition for library section navigation
 */
export interface LibraryTab {
  /** URL-safe tab identifier */
  id: string;
  /** Display label */
  label: string;
  /** Tab icon */
  icon: LucideIcon;
  /** Route path */
  href: string;
}

/**
 * Library section tabs — query-param based navigation
 *
 * Default (/library, no tab param) renders personal library ("I Miei Giochi").
 * Alpha Mode: catalogo tab nascosto.
 */
const _ALL_LIBRARY_TABS: LibraryTab[] = [
  {
    id: 'games',
    label: 'I Miei Giochi',
    icon: Gamepad2,
    href: '/library',
  },
  {
    id: 'wishlist',
    label: 'Wishlist',
    icon: Heart,
    href: '/library?tab=wishlist',
  },
  {
    id: 'catalogo',
    label: 'Catalogo',
    icon: BookOpenIcon,
    href: '/library?tab=catalogo',
  },
];

// ─── Alpha Mode Filtering ────────────────────────────────────────────────────

const isAlphaMode = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';

const ALPHA_LIBRARY_TAB_IDS = new Set(['games', 'wishlist']);

/** Library tabs — filtered by ALPHA_MODE when active */
export const LIBRARY_TABS: LibraryTab[] = isAlphaMode
  ? _ALL_LIBRARY_TABS.filter(tab => ALPHA_LIBRARY_TAB_IDS.has(tab.id))
  : _ALL_LIBRARY_TABS;

/**
 * Determines the active tab ID from pathname + search params.
 *
 * Default (/library with no ?tab) → 'games'.
 * Accepts full URL (pathname + search) or just pathname.
 */
export function getActiveLibraryTab(pathname: string, search?: string): string {
  const tab = search
    ? new URLSearchParams(search.startsWith('?') ? search.slice(1) : search).get('tab')
    : null;

  if (tab === 'wishlist') return 'wishlist';
  if (tab === 'catalogo') return 'catalogo';
  return 'games';
}
```

---

- [ ] **2.4 Esegui i test — verifica che passino**

```bash
cd apps/web && pnpm test src/__tests__/config/library-navigation.test.ts --reporter=verbose 2>&1
```

Expected: tutti i test PASS.

---

- [ ] **2.5 Aggiorna `_content.tsx` — routing per il nuovo tab ID `catalogo`**

Sostituisci il blocco ternario nel return di `LibraryContent` (riga 96-102):

```tsx
// PRIMA
{tab === 'wishlist' ? (
  <WishlistPageClient />
) : tab === 'public' ? (
  <PublicLibraryPageClient />
) : (
  <PersonalLibraryPageClient />
)}

// DOPO
{tab === 'wishlist' ? (
  <WishlistPageClient />
) : tab === 'catalogo' ? (
  <PublicLibraryPageClient />
) : (
  <PersonalLibraryPageClient />
)}
```

---

- [ ] **2.6 Verifica typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -E "(error|warning)" | head -20
```

Expected: nessun errore TS.

---

- [ ] **2.7 Commit**

```bash
git add apps/web/src/config/library-navigation.ts \
        apps/web/src/app/(authenticated)/library/_content.tsx \
        apps/web/src/__tests__/config/library-navigation.test.ts
git commit -m "feat(library-nav): 3 tab con label italiane, rimuove Proposals/Collection"
```

---

## Task 3: ContextualCTA — Disambigua Azione su /library

**Problema:** Su `/library`, il `ContextualCTA` mostra `"+ Aggiungi gioco"` e naviga a `/catalog`, mentre `LibraryPageHeader` ha già un pulsante `"Aggiungi Gioco"` che apre `AddGameDrawer`. Stesso label, comportamenti diversi → confusione.

**Soluzione:** Rinominare il CTA in `"Esplora Catalogo"` — chiarisce che è un link al catalogo condiviso, non un'azione di aggiunta.

**File:**
- Modify: `apps/web/src/config/contextual-cta.ts`
- Create: `apps/web/src/__tests__/config/contextual-cta.test.ts`

---

- [ ] **3.1 Scrivi i test per `getCtaForPathname`**

Crea `apps/web/src/__tests__/config/contextual-cta.test.ts`:

```ts
/**
 * Contextual CTA Config Tests
 * Verifica label e href per ogni sezione, con focus su /library.
 */

import { describe, it, expect } from 'vitest';

import { getCtaForPathname } from '@/config/contextual-cta';

describe('getCtaForPathname', () => {
  describe('/library', () => {
    it('label è "Esplora Catalogo" (disambiguata da "+ Aggiungi gioco" del page header)', () => {
      expect(getCtaForPathname('/library')?.label).toBe('Esplora Catalogo');
    });

    it('href è "/catalog"', () => {
      expect(getCtaForPathname('/library')?.href).toBe('/catalog');
    });

    it('gradient è definito', () => {
      expect(getCtaForPathname('/library')?.gradient).toBeTruthy();
    });

    it('match anche per sotto-percorsi come /library?tab=wishlist', () => {
      expect(getCtaForPathname('/library?tab=wishlist')?.label).toBe('Esplora Catalogo');
    });
  });

  describe('altre sezioni invariate', () => {
    it('/sessions → label "+ Nuova sessione"', () => {
      expect(getCtaForPathname('/sessions')?.label).toBe('+ Nuova sessione');
    });

    it('/chat → label "+ Nuova chat"', () => {
      expect(getCtaForPathname('/chat')?.label).toBe('+ Nuova chat');
    });

    it('/game-nights → label "+ Organizza serata"', () => {
      expect(getCtaForPathname('/game-nights')?.label).toBe('+ Organizza serata');
    });

    it('/agents → label "+ Nuovo agente"', () => {
      expect(getCtaForPathname('/agents')?.label).toBe('+ Nuovo agente');
    });

    it('/settings → null (nessun CTA definito)', () => {
      expect(getCtaForPathname('/settings')).toBeNull();
    });

    it('/dashboard → null', () => {
      expect(getCtaForPathname('/dashboard')).toBeNull();
    });
  });
});
```

---

- [ ] **3.2 Esegui i test — verifica che il test label `/library` fallisca**

```bash
cd apps/web && pnpm test src/__tests__/config/contextual-cta.test.ts --reporter=verbose 2>&1
```

Expected: primo test FAIL (label è ancora `"+ Aggiungi gioco"`), altri PASS.

---

- [ ] **3.3 Aggiorna label in `contextual-cta.ts`**

Modifica solo il campo `label` per `/library` (riga 21):

```ts
// PRIMA
{
  prefix: '/library',
  config: {
    label: '+ Aggiungi gioco',
    href: '/catalog',
    gradient: 'from-orange-600 to-amber-500',
  },
},

// DOPO
{
  prefix: '/library',
  config: {
    label: 'Esplora Catalogo',
    href: '/catalog',
    gradient: 'from-orange-600 to-amber-500',
  },
},
```

---

- [ ] **3.4 Esegui i test — verifica che passino tutti**

```bash
cd apps/web && pnpm test src/__tests__/config/contextual-cta.test.ts --reporter=verbose 2>&1
```

Expected: tutti PASS.

---

- [ ] **3.5 Run suite completa — verifica nessuna regressione**

```bash
cd apps/web && pnpm test 2>&1 | tail -20
```

Expected: tutti i test esistenti passano (nessuna regressione).

---

- [ ] **3.6 Commit**

```bash
git add apps/web/src/config/contextual-cta.ts \
        apps/web/src/__tests__/config/contextual-cta.test.ts
git commit -m "feat(cta): disambigua azione library — 'Esplora Catalogo' vs 'Aggiungi Gioco'"
```

---

## PR Finale

- [ ] **Push branch e apri PR verso `main-dev`**

```bash
git push -u origin feature/nav-ux-simplification
gh pr create \
  --base main-dev \
  --title "feat(nav): UX simplification — Lucide icons, tab italiani, CTA disambiguata" \
  --body "$(cat <<'EOF'
## Summary
- Replace emoji icons with Lucide icons in HybridSidebar (consistent rendering)
- Simplify LibraryNavTabs: 3 tabs with Italian labels (I Miei Giochi / Wishlist / Catalogo)
- Disambiguate ContextualCTA on /library: 'Esplora Catalogo' vs 'Aggiungi Gioco' in page header

## Test plan
- [ ] HybridSidebar renders Lucide icons visually (manual check in browser)
- [ ] `library-navigation.test.ts` — 11 test nuovi PASS
- [ ] `contextual-cta.test.ts` — 8 test nuovi PASS
- [ ] Suite completa senza regressioni
- [ ] Alpha Mode: library mostra solo 2 tab (I Miei Giochi + Wishlist)
- [ ] Typecheck pulito

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
