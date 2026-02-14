/**
 * Notification Settings Page (Issue #4220)
 * Multi-channel notification preferences for PDF processing
 */

'use client';

import React, { useEffect, useState } from 'react';

import { Bell, Mail, Smartphone, MessageSquare, Save, Loader2 } from 'lucide-react';

import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/use-toast';

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
}

export default function NotificationSettingsPage() {
  const [prefs, setPrefs] = useState<NotificationPreferences | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const { toast } = useToast();

  useEffect(() => {
    const fetchPreferences = async () => {
      try {
        const response = await fetch('/api/v1/notifications/preferences');
        if (!response.ok) throw new Error('Failed to fetch preferences');
        const data = await response.json();
        setPrefs(data);
      } catch (error) {
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
    } catch (error) {
      toast({
        title: 'Error',
        description: 'Failed to save notification preferences',
        variant: 'destructive',
      });
    } finally {
      setIsSaving(false);
    }
  };

  const updatePref = (key: keyof Omit<NotificationPreferences, 'userId'>, value: boolean) => {
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
                <p className="text-sm text-muted-foreground">Receive email when document is ready</p>
              </div>
            </div>
            <Switch
              checked={prefs.emailOnDocumentReady}
              onCheckedChange={(checked) => updatePref('emailOnDocumentReady', checked)}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Smartphone className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Push Notification</p>
                <p className="text-sm text-muted-foreground">Browser push notification (coming soon)</p>
              </div>
            </div>
            <Switch
              checked={prefs.pushOnDocumentReady}
              onCheckedChange={(checked) => updatePref('pushOnDocumentReady', checked)}
              disabled
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <MessageSquare className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">In-App Notification</p>
                <p className="text-sm text-muted-foreground">Show notification in notification center</p>
              </div>
            </div>
            <Switch
              checked={prefs.inAppOnDocumentReady}
              onCheckedChange={(checked) => updatePref('inAppOnDocumentReady', checked)}
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
              onCheckedChange={(checked) => updatePref('emailOnDocumentFailed', checked)}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Smartphone className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Push Notification</p>
                <p className="text-sm text-muted-foreground">Browser push notification (coming soon)</p>
              </div>
            </div>
            <Switch
              checked={prefs.pushOnDocumentFailed}
              onCheckedChange={(checked) => updatePref('pushOnDocumentFailed', checked)}
              disabled
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
              onCheckedChange={(checked) => updatePref('inAppOnDocumentFailed', checked)}
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
              onCheckedChange={(checked) => updatePref('emailOnRetryAvailable', checked)}
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Smartphone className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Push Notification</p>
                <p className="text-sm text-muted-foreground">Browser push notification (coming soon)</p>
              </div>
            </div>
            <Switch
              checked={prefs.pushOnRetryAvailable}
              onCheckedChange={(checked) => updatePref('pushOnRetryAvailable', checked)}
              disabled
            />
          </div>

          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <MessageSquare className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">In-App Notification</p>
                <p className="text-sm text-muted-foreground">Show retry status in notification center</p>
              </div>
            </div>
            <Switch
              checked={prefs.inAppOnRetryAvailable}
              onCheckedChange={(checked) => updatePref('inAppOnRetryAvailable', checked)}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
