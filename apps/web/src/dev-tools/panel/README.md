# dev-tools/panel

Dev Panel sub-module for MeepleDev Phase 2. Provides a fixed right-side drawer with 4 tabs (Toggles, Scenarios, Auth, Inspector). Tab content is filled in M1-M3.

## Files

| File | Purpose |
|------|---------|
| `DevPanel.tsx` | Drawer UI shell — inline styles only, no Tailwind, ARIA compliant |
| `DevPanelMount.tsx` | Mounts DevPanel + wires keyboard shortcut (Ctrl+Shift+M) + URL param (`?devpanel=1`) |
| `installPanel.ts` | Bootstrap function — creates panelUiStore, fires prefetch |
| `index.ts` | Barrel export |
| `api/devPanelErrors.ts` | `DevPanelClientError` class |
| `api/devPanelClient.ts` | HTTP client for `/dev/toggles` (GET/PATCH/POST reset) |
| `hooks/useStoreSlice.ts` | Zustand vanilla → React bridge |
| `hooks/useKeyboardShortcut.ts` | Generic keyboard shortcut hook |
| `hooks/useQueryStringPanelOpen.ts` | Opens panel when `?devpanel=1` is present in URL |
| `stores/panelUiStore.ts` | Zustand vanilla store with sessionStorage persistence |

## Architecture

- **No global styles**: DevPanel uses inline styles to avoid polluting app CSS
- **Vanilla Zustand**: `StoreApi<PanelUiState>` is passed as prop — no React context required
- **Tree-shakeable**: entire panel module is dynamically imported by `mock-provider.tsx`
- **Keyboard**: `Ctrl+Shift+M` (or `Cmd+Shift+M` on macOS) toggles the drawer
- **URL**: `?devpanel=1` opens the drawer on load and cleans the query string

## Usage

```tsx
// Automatically mounted by mock-provider.tsx when NEXT_PUBLIC_MOCK_MODE=true
// Manual open: Ctrl+Shift+M or append ?devpanel=1 to any dev URL
```

## Phase 2 Complete — Tab Reference

Phase 2 (M0-M4) added the full 4-tab drawer. All tabs are populated and wired.

### Tab 1 — Toggles (`panel-tab-toggles`)

MSW handler groups can be enabled/disabled per-session without a page reload.

| Element | `data-testid` | Behaviour |
|---------|--------------|-----------|
| Tab button | `panel-tab-toggles` | Activates Toggles view (default) |
| Individual toggle | `toggle-msw-<group>` | `data-state="on|off"`, `role="switch"`, `aria-checked` |

- Fetches current state from `GET /dev/toggles` on mount via `useBackendTogglesQuery`
- Mutations via `PATCH /dev/toggles` (`useBackendTogglesMutation`)
- Reset button posts `POST /dev/toggles/reset`

### Tab 2 — Scenarios (`panel-tab-scenarios`)

Switch the active MSW fixture scenario atomically (mutex-protected).

| Element | `data-testid` | Behaviour |
|---------|--------------|-----------|
| Tab button | `panel-tab-scenarios` | Activates Scenarios view |
| Scenario select | `scenario-select` | `<select>` with one `<option>` per scenario |

- Switching a scenario reloads all MSW handlers for the new fixture set
- The DevBadge (`dev-badge`) updates to reflect the active scenario name
- Implemented in `ScenariosSection.tsx` backed by `scenarioSwitcher` (mutex, no parallel switches)

### Tab 3 — Auth (`panel-tab-auth`)

Override the mock auth role for the current session.

| Element | `data-testid` | Behaviour |
|---------|--------------|-----------|
| Tab button | `panel-tab-auth` | Activates Auth view |
| Role select | `role-select` | `<select>` with roles available in the active scenario |

- Available roles are derived from the current scenario's user fixture
- Changing role updates the `dev-badge` immediately
- Implemented in `AuthSection.tsx`

### Tab 4 — Inspector (`panel-tab-inspector`)

Real-time log of MSW-intercepted HTTP requests.

| Element | `data-testid` | Behaviour |
|---------|--------------|-----------|
| Tab button | `panel-tab-inspector` | Activates Inspector view |
| Request row | `inspector-row` | One row per captured request (method + URL + status) |
| Clear button | `inspector-clear` | Empties the captured list |

- Requests are captured via `fetchInterceptor` (wraps `window.fetch` in mock mode)
- Stored in `inspectorStore` (Zustand, max 200 entries, FIFO eviction)
- Implemented in `InspectorSection.tsx`

### ARIA Contract

| Attribute | Value |
|-----------|-------|
| `[data-testid="dev-panel"][role]` | `dialog` |
| `[data-testid="dev-panel"][aria-label]` | `MeepleDev Panel` |
| Tab container `[role]` | `tablist` |
| Each tab `[role]` | `tab` |
| Active tab `[aria-selected]` | `true` |
| Each toggle `[role]` | `switch` |
| Each toggle `[aria-checked]` | `true` or `false` |

### E2E Specs

All specs live in `apps/web/e2e/dev-loop/` and are tagged `@dev-loop`.
They require `NEXT_PUBLIC_MOCK_MODE=true` and a running dev server (`pnpm dev`).

| Spec file | Covers |
|-----------|--------|
| `devpanel-opens.spec.ts` | Ctrl+Shift+M, `?devpanel=1`, badge click, Escape |
| `toggle-runtime.spec.ts` | MSW group toggle on/off |
| `scenario-switch.spec.ts` | Scenario select → badge update |
| `role-switch.spec.ts` | Role select → badge update |
| `inspector-capture.spec.ts` | Inspector rows appear + clear button |
| `devpanel-a11y.spec.ts` | ARIA roles, tab count, switch aria-checked, keyboard nav |
