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
