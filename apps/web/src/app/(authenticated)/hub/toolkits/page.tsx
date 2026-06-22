/**
 * /hub/toolkits — redirect to canonical `/toolkits` (Issue #1480).
 *
 * The toolkit hub was originally shipped at `/hub/toolkits` (#1166) using
 * `HubCatalogView`. The SP4 mockup (sp4-hub-toolkits) requires `/toolkits` as
 * the canonical path with a richer 7-component implementation; this redirect
 * preserves back-links from chat/email/external sources.
 */

import { redirect } from 'next/navigation';

export default function HubToolkitsLegacyRedirect(): never {
  redirect('/toolkits');
}
