/**
 * SyncUnavailableBanner — the live sync of a toolkit session is down (#4114).
 *
 * The session itself still works: its data is loaded and its actions answer. The toolkit pages
 * used to replace their whole content with «Error · SSE connection failed» when the stream
 * failed; this banner states the sync problem next to the content instead.
 */

import type { JSX } from 'react';

export function SyncUnavailableBanner(): JSX.Element {
  return (
    <div
      role="status"
      data-testid="sync-unavailable-banner"
      className="border-b border-border bg-muted px-4 py-2 text-center text-sm text-muted-foreground"
    >
      Live sync is unavailable. The session still works: reload the page to see the other
      players&apos; changes.
    </div>
  );
}
