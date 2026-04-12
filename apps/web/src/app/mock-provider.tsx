'use client';

/**
 * MockProvider — avvia MSW ServiceWorker in browser dev mode
 *
 * Attivato solo quando NEXT_PUBLIC_MOCK_MODE=true.
 * Blocca il render fino a worker.start() completato per evitare
 * race condition con le prime fetch dell'app.
 *
 * Prerequisito: public/mockServiceWorker.js deve esistere.
 * Generarlo con: cd apps/web && npx msw init public/ --save
 */

import { useEffect, useRef, useState, type ComponentType } from 'react';

import { QueryClient } from '@tanstack/react-query';

const IS_DEV_MOCK =
  process.env.NODE_ENV === 'development' && process.env.NEXT_PUBLIC_MOCK_MODE === 'true';

/**
 * Local opaque type for dev-tools state. The real `InstalledDevTools` interface
 * lives under @/dev-tools, which is dynamically imported (and absent in
 * production builds). We avoid a static `import type` so that the dev-tools
 * module path can be removed entirely without breaking the prod typecheck —
 * see .github/workflows/dev-tools-isolation.yml.
 */
type DevToolsBundle = {
  controlStore: unknown;
  scenarioStore: unknown;
  authStore: unknown;
};

type DevBadgeComponent = ComponentType<{
  controlStore: unknown;
  scenarioStore: unknown;
  authStore: unknown;
}>;

type DevPanelMountComponent = ComponentType<{
  uiStore: unknown;
  mockControlStore: unknown;
  handlerGroups: Array<{ name: string; handlers: unknown[] }>;
  worker: { resetHandlers: (...h: unknown[]) => void };
  scenarioStore: unknown;
  authStore: unknown;
  queryClient: unknown;
  inspectorStore: unknown;
}>;

interface MockProviderProps {
  children: React.ReactNode;
}

/**
 * Dedicated QueryClient for the DevPanel (scenario switching, auth invalidation).
 * Created outside the component so it's a stable singleton across re-renders.
 * MockProvider renders outside the app's QueryClientProvider, so we can't use
 * useQueryClient() here — a dedicated instance is the correct approach.
 */
const devQueryClient = new QueryClient();

/**
 * Unregister any service workers that are not MSW's mockServiceWorker.js.
 * This prevents the PWA sw.js from intercepting requests instead of MSW.
 */
async function unregisterConflictingSWs() {
  if (typeof window === 'undefined' || !('serviceWorker' in navigator)) return;
  const registrations = await navigator.serviceWorker.getRegistrations();
  for (const reg of registrations) {
    const swUrl =
      reg.active?.scriptURL || reg.installing?.scriptURL || reg.waiting?.scriptURL || '';
    if (!swUrl.includes('mockServiceWorker')) {
      await reg.unregister();
    }
  }
}

async function initMocks(skipControllerCheck = false) {
  await unregisterConflictingSWs();
  const { worker } = await import('@/mocks/browser');
  await worker.start({
    onUnhandledRequest: 'bypass', // Non bloccare richieste non gestite (es. font, next internals)
  });

  // MSW requires the Service Worker to control the current page.
  // If controller is null, the SW was just registered for the first time and
  // doesn't yet control this page (browser spec: newly-registered SWs only
  // control pages opened after registration, unless clients.claim() is called).
  // MSW's mockServiceWorker.js calls clients.claim() in the activate event,
  // but there can be a race on first load. Reloading ensures full SW control.
  // Skip this check on HMR restarts: the SW was already active before the restart
  // and will reclaim the page without a full reload.
  if (
    !skipControllerCheck &&
    typeof window !== 'undefined' &&
    !navigator.serviceWorker.controller
  ) {
    window.location.reload();
    // Never resolve: page is reloading, no need to unblock rendering
    return new Promise<void>(() => {});
  }
}

export function MockProvider({ children }: MockProviderProps) {
  const [ready, setReady] = useState(false);
  const [tools, setTools] = useState<DevToolsBundle | null>(null);
  const [DevBadge, setDevBadge] = useState<DevBadgeComponent | null>(null);
  const [DevPanelMountComp, setDevPanelMountComp] = useState<DevPanelMountComponent | null>(null);
  const [mswWorker, setMswWorker] = useState<{ resetHandlers: (...h: unknown[]) => void } | null>(
    null
  );
  const [handlerGroups, setHandlerGroups] = useState<Array<{ name: string; handlers: unknown[] }>>(
    []
  );
  // Track if panel mount has been loaded to avoid duplicate imports
  const panelMountLoaded = useRef(false);

  useEffect(() => {
    if (!IS_DEV_MOCK) return;
    // Dynamic imports — the @/dev-tools module is intentionally absent from
    // production builds, so we never reference it via static import.
    Promise.all([
      // Use string template + variable to defeat Next.js's static analysis,
      // ensuring the bundler does not try to resolve the path at build time.
      import(/* webpackIgnore: true */ `${'@'}/dev-tools/index.ts` as string).catch(
        () => import(`${'@'}/dev-tools` as string)
      ),
      import(`${'@'}/dev-tools/devBadge` as string),
    ])
      .then(([devTools, badgeModule]) => {
        const installed = (
          devTools as { installDevTools: () => DevToolsBundle & { panel?: { uiStore: unknown } } }
        ).installDevTools();
        setTools(installed);
        setDevBadge(() => (badgeModule as { DevBadge: DevBadgeComponent }).DevBadge);
        // Load DevPanelMount lazily (same bundle, just isolated via dynamic import)
        if (installed.panel && !panelMountLoaded.current) {
          panelMountLoaded.current = true;
          Promise.all([
            import(`${'@'}/dev-tools/panel/DevPanelMount` as string),
            import(`${'@'}/mocks/browser` as string),
            import(`${'@'}/mocks/handlers/registry` as string),
          ])
            .then(([mountMod, browserMod, registryMod]) => {
              setDevPanelMountComp(
                () => (mountMod as { DevPanelMount: DevPanelMountComponent }).DevPanelMount
              );
              setMswWorker(
                (browserMod as { worker: { resetHandlers: (...h: unknown[]) => void } }).worker
              );
              setHandlerGroups(
                (registryMod as { HANDLER_GROUPS: Array<{ name: string; handlers: unknown[] }> })
                  .HANDLER_GROUPS
              );
            })
            .catch(() => {});
        }
      })
      .catch(() => {
        // dev-tools module not present (e.g. tree-shaken in prod) — silently skip
      });
  }, []);

  useEffect(() => {
    initMocks().then(() => setReady(true));

    // Re-initialize MSW after Turbopack/Webpack Fast Refresh replaces browser.ts
    // or its handler dependencies. The old worker is stopped and this event is
    // dispatched by the module.hot.dispose hook in browser.ts. By the time this
    // handler runs, the new worker module has been evaluated and the dynamic
    // import below will return the replacement worker instance.
    function handleWorkerInvalidated() {
      setReady(false);
      initMocks(/* skipControllerCheck */ true).then(() => setReady(true));
    }

    window.addEventListener('msw:worker-invalidated', handleWorkerInvalidated);
    return () => {
      window.removeEventListener('msw:worker-invalidated', handleWorkerInvalidated);
    };
  }, []);

  if (!ready) {
    // Blocca il render fino all'avvio del worker per evitare race condition
    return null;
  }

  return (
    <>
      {children}
      {IS_DEV_MOCK && tools && DevBadge && (
        <DevBadge
          controlStore={tools.controlStore}
          scenarioStore={tools.scenarioStore}
          authStore={tools.authStore}
        />
      )}
      {IS_DEV_MOCK &&
        tools &&
        DevPanelMountComp &&
        mswWorker &&
        (tools as DevToolsBundle & { panel?: { uiStore: unknown; inspectorStore: unknown } })
          .panel && (
          <DevPanelMountComp
            uiStore={
              (
                tools as DevToolsBundle & {
                  panel?: { uiStore: unknown; inspectorStore: unknown };
                }
              ).panel!.uiStore
            }
            mockControlStore={tools.controlStore}
            handlerGroups={handlerGroups}
            worker={mswWorker}
            scenarioStore={tools.scenarioStore}
            authStore={tools.authStore}
            queryClient={devQueryClient}
            inspectorStore={
              (
                tools as DevToolsBundle & {
                  panel?: { uiStore: unknown; inspectorStore: unknown };
                }
              ).panel!.inspectorStore
            }
          />
        )}
    </>
  );
}
