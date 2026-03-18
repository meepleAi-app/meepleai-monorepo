/**
 * Notification Settings Page (Issue #4220, #4416)
 * Multi-channel notification preferences for PDF processing
 */

'use client';

import React, { useEffect, useState } from 'react';

import {
  Bell,
  Calendar,
  Mail,
  Smartphone,
  MessageSquare,
  Save,
  Loader2,
  BellRing,
  BellOff,
  SendHorizontal,
} from 'lucide-react';

import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { usePushNotifications } from '@/hooks/usePushNotifications';
import { useToast } from '@/hooks/useToast';

interface NotificationPreferences {
  userId: string;
  emailOnDocumentReady: boolean;
  emailOnDocumentFailed: boolean;
  emailOnRetryAvailable: boolean;
  pushOnDocumentReady: boolean;
  pushOnDocumentFailed: boolean;
  pushOnRetryAvailable: boolean;
  inAppOnDocumentReady: boolean;
  inAppOnDocumentFailed: boolean;
  inAppOnRetryAvailable: boolean;
  hasPushSubscription: boolean;
  // Game Night preferences (Issue #33)
  inAppOnGameNightInvitation: boolean;
  emailOnGameNightInvitation: boolean;
  pushOnGameNightInvitation: boolean;
  emailOnGameNightReminder: boolean;
  pushOnGameNightReminder: boolean;
}

export default function NotificationSettingsPage() {
  const [prefs, setPrefs] = useState<NotificationPreferences | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isSendingTest, setIsSendingTest] = useState(false);
  const { toast } = useToast();
  const push = usePushNotifications();

  useEffect(() => {
    const fetchPreferences = async () => {
      try {
        const response = await fetch('/api/v1/notifications/preferences');
        if (!response.ok) throw new Error('Failed to fetch preferences');
        const data = await response.json();
        setPrefs(data);
      } catch (_error) {
        toast({
          title: 'Error',
          description: 'Failed to load notification preferences',
          variant: 'destructive',
        });
      } finally {
        setIsLoading(false);
      }
    };

    void fetchPreferences();
  }, [toast]);

  const handleSave = async () => {
    if (!prefs) return;

    setIsSaving(true);
    try {
      const response = await fetch('/api/v1/notifications/preferences', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(prefs),
      });

      if (!response.ok) throw new Error('Failed to save preferences');

      toast({
        title: 'Success',
        description: 'Notification preferences saved successfully',
      });
    } catch (_error) {
      toast({
        title: 'Error',
        description: 'Failed to save notification preferences',
        variant: 'destructive',
      });
    } finally {
      setIsSaving(false);
    }
  };

  const handleSendTest = async () => {
    setIsSendingTest(true);
    try {
      const response = await fetch('/api/v1/notifications/push/test', {
        method: 'POST',
      });

      if (!response.ok) {
        const data = await response.json().catch(() => ({}));
        throw new Error(data.error || 'Failed to send test notification');
      }

      toast({
        title: 'Test Sent',
        description: 'Check your browser for the test notification',
      });
    } catch (error) {
      toast({
        title: 'Error',
        description: error instanceof Error ? error.message : 'Failed to send test notification',
        variant: 'destructive',
      });
    } finally {
      setIsSendingTest(false);
    }
  };

  const updatePref = (
    key: keyof Omit<NotificationPreferences, 'userId' | 'hasPushSubscription'>,
    value: boolean
  ) => {
    if (!prefs) return;
    setPrefs({ ...prefs, [key]: value });
  };

  if (isLoading) {
    return (
      <div className="container max-w-3xl mx-auto py-8">
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      </div>
    );
  }

  if (!prefs) {
    return (
      <div className="container max-w-3xl mx-auto py-8">
        <p className="text-center text-muted-foreground">Failed to load notification preferences</p>
      </div>
    );
  }

  const pushEnabled = push.isSupported && push.isSubscribed;

  return (
    <div className="container max-w-3xl mx-auto py-8">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold mb-2">Notification Settings</h1>
          <p className="text-muted-foreground">
            Control how you receive notifications for PDF processing events
          </p>
        </div>
        <Button onClick={handleSave} disabled={isSaving}>
          {isSaving ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" />
              Save Changes
            </>
          )}
        </Button>
      </div>

      {/* Push Notification Subscription Card */}
      {push.isSupported && (
        <div className="bg-card rounded-xl p-6 border border-border/50 mb-6">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <div
                className={`p-2 rounded-lg ${push.isSubscribed ? 'bg-green-100 dark:bg-green-900/20' : 'bg-muted'}`}
              >
                {push.isSubscribed ? (
                  <BellRing className="h-6 w-6 text-green-600 dark:text-green-400" />
                ) : (
                  <BellOff className="h-6 w-6 text-muted-foreground" />
                )}
              </div>
              <div>
                <h2 className="text-lg font-bold">Browser Push Notifications</h2>
                <p className="text-sm text-muted-foreground">
                  {push.isSubscribed
                    ? 'Push notifications are enabled for this browser'
                    : 'Enable push notifications to receive alerts even when the app is in the background'}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              {push.isSubscribed && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleSendTest}
                  disabled={isSendingTest}
                  data-testid="test-push-button"
                >
                  {isSendingTest ? (
                    <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                  ) : (
                    <SendHorizontal className="mr-1 h-4 w-4" />
                  )}
                  Send Test
                </Button>
              )}
              <Button
                variant={push.isSubscribed ? 'outline' : 'default'}
                onClick={push.isSubscribed ? push.unsubscribe : push.subscribe}
                disabled={push.isLoading}
              >
                {push.isLoading ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : push.isSubscribed ? (
                  'Disable'
                ) : (
                  'Enable'
                )}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Document Ready Notifications */}
      <div className="bg-card rounded-xl p-6 border border-border/50 mb-6">
        <div className="flex items-start gap-4 mb-6">
          <div className="p-2 rounded-lg bg-green-100 dark:bg-green-900/20">
            <Bell className="h-6 w-6 text-green-600 dark:text-green-400" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">Document Ready</h2>
            <p className="text-sm text-muted-foreground">
              Notifications when your PDF is successfully processed and ready for AI queries
            </p>
          </div>
        </div>

        <div className="space-y-4">
          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Mail className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Email Notification</p>
                <p className="text-sm text-muted-foreground">
                  Receive email when document is ready
                </p>
              </div>
            </div>
            <Switch
              checked={prefs.emailOnDocumentReady}
              onCheckedChange={checked => updatePref('emailOnDocumentReady', checked)}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Smartphone className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Push Notification</p>
                <p className="text-sm text-muted-foreground">
                  {pushEnabled
                    ? 'Browser push notification when document is ready'
                    : 'Enable push notifications above to use this'}
                </p>
              </div>
            </div>
            <Switch
              checked={prefs.pushOnDocumentReady}
              onCheckedChange={checked => updatePref('pushOnDocumentReady', checked)}
              disabled={!pushEnabled}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <MessageSquare className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">In-App Notification</p>
                <p className="text-sm text-muted-foreground">
                  Show notification in notification center
                </p>
              </div>
            </div>
            <Switch
              checked={prefs.inAppOnDocumentReady}
              onCheckedChange={checked => updatePref('inAppOnDocumentReady', checked)}
            />
          </div>
        </div>
      </div>

      {/* Document Failed Notifications */}
      <div className="bg-card rounded-xl p-6 border border-border/50 mb-6">
        <div className="flex items-start gap-4 mb-6">
          <div className="p-2 rounded-lg bg-red-100 dark:bg-red-900/20">
            <Bell className="h-6 w-6 text-red-600 dark:text-red-400" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">Processing Failed</h2>
            <p className="text-sm text-muted-foreground">
              Notifications when PDF processing encounters an error
            </p>
          </div>
        </div>

        <div className="space-y-4">
          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Mail className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Email Notification</p>
                <p className="text-sm text-muted-foreground">Receive email with error details</p>
              </div>
            </div>
            <Switch
              checked={prefs.emailOnDocumentFailed}
              onCheckedChange={checked => updatePref('emailOnDocumentFailed', checked)}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Smartphone className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Push Notification</p>
                <p className="text-sm text-muted-foreground">
                  {pushEnabled
                    ? 'Browser push notification on processing failure'
                    : 'Enable push notifications above to use this'}
                </p>
              </div>
            </div>
            <Switch
              checked={prefs.pushOnDocumentFailed}
              onCheckedChange={checked => updatePref('pushOnDocumentFailed', checked)}
              disabled={!pushEnabled}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <MessageSquare className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">In-App Notification</p>
                <p className="text-sm text-muted-foreground">Show error in notification center</p>
              </div>
            </div>
            <Switch
              checked={prefs.inAppOnDocumentFailed}
              onCheckedChange={checked => updatePref('inAppOnDocumentFailed', checked)}
            />
          </div>
        </div>
      </div>

      {/* Game Night Invitations (Issue #33) */}
      <div className="bg-card rounded-xl p-6 border border-border/50 mb-6">
        <div className="flex items-start gap-4 mb-6">
          <div className="p-2 rounded-lg bg-amber-100 dark:bg-amber-900/20">
            <Calendar className="h-6 w-6 text-amber-600 dark:text-amber-400" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">Serate Giochi — Inviti</h2>
            <p className="text-sm text-muted-foreground">
              Notifiche quando vieni invitato a una serata giochi
            </p>
          </div>
        </div>

        <div className="space-y-4">
          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Mail className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Email</p>
                <p className="text-sm text-muted-foreground">Ricevi email per nuovi inviti</p>
              </div>
            </div>
            <Switch
              checked={prefs.emailOnGameNightInvitation}
              onCheckedChange={checked => updatePref('emailOnGameNightInvitation', checked)}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Smartphone className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Push</p>
                <p className="text-sm text-muted-foreground">
                  {pushEnabled ? 'Notifica push per nuovi inviti' : 'Abilita le push sopra'}
                </p>
              </div>
            </div>
            <Switch
              checked={prefs.pushOnGameNightInvitation}
              onCheckedChange={checked => updatePref('pushOnGameNightInvitation', checked)}
              disabled={!pushEnabled}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <MessageSquare className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">In-App</p>
                <p className="text-sm text-muted-foreground">Mostra nel centro notifiche</p>
              </div>
            </div>
            <Switch
              checked={prefs.inAppOnGameNightInvitation}
              onCheckedChange={checked => updatePref('inAppOnGameNightInvitation', checked)}
            />
          </div>
        </div>
      </div>

      {/* Game Night Reminders (Issue #33) */}
      <div className="bg-card rounded-xl p-6 border border-border/50 mb-6">
        <div className="flex items-start gap-4 mb-6">
          <div className="p-2 rounded-lg bg-amber-100 dark:bg-amber-900/20">
            <Calendar className="h-6 w-6 text-amber-600 dark:text-amber-400" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">Serate Giochi — Promemoria</h2>
            <p className="text-sm text-muted-foreground">Promemoria 24h e 1h prima della serata</p>
          </div>
        </div>

        <div className="space-y-4">
          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Mail className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Email</p>
                <p className="text-sm text-muted-foreground">Ricevi email di promemoria</p>
              </div>
            </div>
            <Switch
              checked={prefs.emailOnGameNightReminder}
              onCheckedChange={checked => updatePref('emailOnGameNightReminder', checked)}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Smartphone className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Push</p>
                <p className="text-sm text-muted-foreground">
                  {pushEnabled ? 'Notifica push di promemoria' : 'Abilita le push sopra'}
                </p>
              </div>
            </div>
            <Switch
              checked={prefs.pushOnGameNightReminder}
              onCheckedChange={checked => updatePref('pushOnGameNightReminder', checked)}
              disabled={!pushEnabled}
            />
          </div>
        </div>
      </div>

      {/* Retry Available Notifications */}
      <div className="bg-card rounded-xl p-6 border border-border/50">
        <div className="flex items-start gap-4 mb-6">
          <div className="p-2 rounded-lg bg-blue-100 dark:bg-blue-900/20">
            <Bell className="h-6 w-6 text-blue-600 dark:text-blue-400" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">Retry Available</h2>
            <p className="text-sm text-muted-foreground">
              Notifications when system retries processing a failed document
            </p>
          </div>
        </div>

        <div className="space-y-4">
          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Mail className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Email Notification</p>
                <p className="text-sm text-muted-foreground">Receive email when retry starts</p>
              </div>
            </div>
            <Switch
              checked={prefs.emailOnRetryAvailable}
              onCheckedChange={checked => updatePref('emailOnRetryAvailable', checked)}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Smartphone className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Push Notification</p>
                <p className="text-sm text-muted-foreground">
                  {pushEnabled
                    ? 'Browser push notification on retry'
                    : 'Enable push notifications above to use this'}
                </p>
              </div>
            </div>
            <Switch
              checked={prefs.pushOnRetryAvailable}
              onCheckedChange={checked => updatePref('pushOnRetryAvailable', checked)}
              disabled={!pushEnabled}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <MessageSquare className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">In-App Notification</p>
                <p className="text-sm text-muted-foreground">
                  Show retry status in notification center
                </p>
              </div>
            </div>
            <Switch
              checked={prefs.inAppOnRetryAvailable}
              onCheckedChange={checked => updatePref('inAppOnRetryAvailable', checked)}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
