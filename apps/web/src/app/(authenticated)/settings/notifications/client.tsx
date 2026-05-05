/**
 * Notification Settings Page (Issue #4220, #4416)
 * Multi-channel notification preferences for PDF processing
 *
 * Migrated to Claude Design v1 (M6 Task 9): uses SettingsList + SettingsRow + ToggleSwitch.
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

import { Button } from '@/components/ui/primitives/button';
import { SettingsList } from '@/components/ui/v2/settings-list';
import { SettingsRow } from '@/components/ui/v2/settings-row';
import { ToggleSwitch } from '@/components/ui/v2/toggle-switch';
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

type PrefKey = keyof Omit<NotificationPreferences, 'userId' | 'hasPushSubscription'>;

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

  const updatePref = (key: PrefKey, value: boolean) => {
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

  // Render helper to keep the switch + row tests green.
  // SettingsRow exposes label as plain text; trailing slot hosts the ToggleSwitch.
  const renderToggleRow = (params: {
    icon: React.ReactNode;
    label: string;
    description: string;
    prefKey: PrefKey;
    disabled?: boolean;
    ariaLabel: string;
  }) => (
    <SettingsRow
      icon={params.icon}
      label={params.label}
      description={params.description}
      trailing={
        <ToggleSwitch
          checked={prefs[params.prefKey] as boolean}
          onCheckedChange={checked => updatePref(params.prefKey, checked)}
          disabled={params.disabled}
          ariaLabel={params.ariaLabel}
          entity="agent"
        />
      }
    />
  );

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
      <div className="mb-6">
        <div className="flex items-start gap-4 mb-3">
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
        <SettingsList ariaLabel="Document Ready notifications">
          {renderToggleRow({
            icon: <Mail className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Email Notification',
            description: 'Receive email when document is ready',
            prefKey: 'emailOnDocumentReady',
            ariaLabel: 'Email — document ready',
          })}
          {renderToggleRow({
            icon: <Smartphone className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Push Notification',
            description: pushEnabled
              ? 'Browser push notification when document is ready'
              : 'Enable push notifications above to use this',
            prefKey: 'pushOnDocumentReady',
            disabled: !pushEnabled,
            ariaLabel: 'Push — document ready',
          })}
          {renderToggleRow({
            icon: <MessageSquare className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'In-App Notification',
            description: 'Show notification in notification center',
            prefKey: 'inAppOnDocumentReady',
            ariaLabel: 'In-app — document ready',
          })}
        </SettingsList>
      </div>

      {/* Document Failed Notifications */}
      <div className="mb-6">
        <div className="flex items-start gap-4 mb-3">
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
        <SettingsList ariaLabel="Processing Failed notifications">
          {renderToggleRow({
            icon: <Mail className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Email Notification',
            description: 'Receive email with error details',
            prefKey: 'emailOnDocumentFailed',
            ariaLabel: 'Email — processing failed',
          })}
          {renderToggleRow({
            icon: <Smartphone className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Push Notification',
            description: pushEnabled
              ? 'Browser push notification on processing failure'
              : 'Enable push notifications above to use this',
            prefKey: 'pushOnDocumentFailed',
            disabled: !pushEnabled,
            ariaLabel: 'Push — processing failed',
          })}
          {renderToggleRow({
            icon: <MessageSquare className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'In-App Notification',
            description: 'Show error in notification center',
            prefKey: 'inAppOnDocumentFailed',
            ariaLabel: 'In-app — processing failed',
          })}
        </SettingsList>
      </div>

      {/* Game Night Invitations (Issue #33) */}
      <div className="mb-6">
        <div className="flex items-start gap-4 mb-3">
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
        <SettingsList ariaLabel="Game night invitations">
          {renderToggleRow({
            icon: <Mail className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Email',
            description: 'Ricevi email per nuovi inviti',
            prefKey: 'emailOnGameNightInvitation',
            ariaLabel: 'Email — inviti serate',
          })}
          {renderToggleRow({
            icon: <Smartphone className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Push',
            description: pushEnabled ? 'Notifica push per nuovi inviti' : 'Abilita le push sopra',
            prefKey: 'pushOnGameNightInvitation',
            disabled: !pushEnabled,
            ariaLabel: 'Push — inviti serate',
          })}
          {renderToggleRow({
            icon: <MessageSquare className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'In-App',
            description: 'Mostra nel centro notifiche',
            prefKey: 'inAppOnGameNightInvitation',
            ariaLabel: 'In-app — inviti serate',
          })}
        </SettingsList>
      </div>

      {/* Game Night Reminders (Issue #33) */}
      <div className="mb-6">
        <div className="flex items-start gap-4 mb-3">
          <div className="p-2 rounded-lg bg-amber-100 dark:bg-amber-900/20">
            <Calendar className="h-6 w-6 text-amber-600 dark:text-amber-400" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">Serate Giochi — Promemoria</h2>
            <p className="text-sm text-muted-foreground">Promemoria 24h e 1h prima della serata</p>
          </div>
        </div>
        <SettingsList ariaLabel="Game night reminders">
          {renderToggleRow({
            icon: <Mail className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Email',
            description: 'Ricevi email di promemoria',
            prefKey: 'emailOnGameNightReminder',
            ariaLabel: 'Email — promemoria serate',
          })}
          {renderToggleRow({
            icon: <Smartphone className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Push',
            description: pushEnabled ? 'Notifica push di promemoria' : 'Abilita le push sopra',
            prefKey: 'pushOnGameNightReminder',
            disabled: !pushEnabled,
            ariaLabel: 'Push — promemoria serate',
          })}
        </SettingsList>
      </div>

      {/* Retry Available Notifications */}
      <div>
        <div className="flex items-start gap-4 mb-3">
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
        <SettingsList ariaLabel="Retry available notifications">
          {renderToggleRow({
            icon: <Mail className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Email Notification',
            description: 'Receive email when retry starts',
            prefKey: 'emailOnRetryAvailable',
            ariaLabel: 'Email — retry available',
          })}
          {renderToggleRow({
            icon: <Smartphone className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'Push Notification',
            description: pushEnabled
              ? 'Browser push notification on retry'
              : 'Enable push notifications above to use this',
            prefKey: 'pushOnRetryAvailable',
            disabled: !pushEnabled,
            ariaLabel: 'Push — retry available',
          })}
          {renderToggleRow({
            icon: <MessageSquare className="h-5 w-5 text-muted-foreground" aria-hidden="true" />,
            label: 'In-App Notification',
            description: 'Show retry status in notification center',
            prefKey: 'inAppOnRetryAvailable',
            ariaLabel: 'In-app — retry available',
          })}
        </SettingsList>
      </div>
    </div>
  );
}
