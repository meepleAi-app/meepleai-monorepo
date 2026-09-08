/**
 * `/notifications/preferences` — redirect to the canonical address (#3961).
 *
 * The preferences panel now lives in the settings hub, at
 * `/settings/notifications` — the address the backend already links to from
 * the footer of every email (`EmailTemplateService.WrapInBaseTemplate`).
 *
 * Keeping both would mean two addresses for the same content, which is the
 * defect #3938 just removed for settings as a whole. `NotificationPreferences`
 * is rendered by `SettingsTab`; this route only forwards.
 *
 * Note the direction: `/settings/notifications` itself must never redirect —
 * `gotoChecked` in `e2e/accessibility.spec.ts` (#3917) asserts the landed
 * pathname equals the requested one.
 */

import { redirect } from 'next/navigation';

export const dynamic = 'force-static';

export default function NotificationPreferencesRedirectPage(): never {
  redirect('/settings/notifications');
}
