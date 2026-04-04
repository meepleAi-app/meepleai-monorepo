/**
 * Notification Preferences Page (US-41)
 *
 * Route: /notifications/preferences
 * Wraps the NotificationPreferences component with page layout.
 */

export const dynamic = 'force-dynamic';

import { NotificationPreferences } from '@/components/notifications/NotificationPreferences';

export default function NotificationPreferencesPage() {
  return (
    <div className="container max-w-3xl mx-auto py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-2">Preferenze notifiche</h1>
        <p className="text-muted-foreground">Configura come e quando ricevere le notifiche</p>
      </div>
      <NotificationPreferences />
    </div>
  );
}
