/**
 * Push Notification subscription management hook (Issue #4416)
 * Handles Web Push API subscription lifecycle with backend sync.
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { useToast } from '@/hooks/use-toast';

interface UsePushNotificationsResult {
  isSubscribed: boolean;
  isSupported: boolean;
  isLoading: boolean;
  subscribe: () => Promise<void>;
  unsubscribe: () => Promise<void>;
}

function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

export function usePushNotifications(): UsePushNotificationsResult {
  const [isSubscribed, setIsSubscribed] = useState(false);
  const [isSupported, setIsSupported] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const { toast } = useToast();

  useEffect(() => {
    const supported =
      typeof window !== 'undefined' &&
      'serviceWorker' in navigator &&
      'PushManager' in window &&
      'Notification' in window;

    setIsSupported(supported);

    if (supported) {
      navigator.serviceWorker.ready
        .then((registration) => registration.pushManager.getSubscription())
        .then((subscription) => {
          setIsSubscribed(subscription !== null);
        })
        .catch(() => {
          // Service worker not ready yet
        });
    }
  }, []);

  const subscribe = useCallback(async () => {
    if (!isSupported) return;

    setIsLoading(true);
    try {
      const permission = await Notification.requestPermission();
      if (permission !== 'granted') {
        toast({
          title: 'Permission Denied',
          description: 'Please enable notifications in your browser settings',
          variant: 'destructive',
        });
        return;
      }

      // Fetch VAPID public key from backend
      const vapidResponse = await fetch('/api/v1/notifications/push/vapid-key');
      if (!vapidResponse.ok) throw new Error('Failed to fetch VAPID key');
      const { publicKey } = await vapidResponse.json();

      if (!publicKey) {
        toast({
          title: 'Configuration Error',
          description: 'Push notifications are not configured on the server',
          variant: 'destructive',
        });
        return;
      }

      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey).buffer as ArrayBuffer,
      });

      const subscriptionJson = subscription.toJSON();

      // Send subscription to backend
      const response = await fetch('/api/v1/notifications/push/subscribe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          endpoint: subscriptionJson.endpoint,
          p256dhKey: subscriptionJson.keys?.p256dh,
          authKey: subscriptionJson.keys?.auth,
        }),
      });

      if (!response.ok) throw new Error('Failed to register push subscription');

      setIsSubscribed(true);
      toast({
        title: 'Push Notifications Enabled',
        description: 'You will now receive browser push notifications',
      });
    } catch (_error) {
      toast({
        title: 'Error',
        description: 'Failed to enable push notifications',
        variant: 'destructive',
      });
    } finally {
      setIsLoading(false);
    }
  }, [isSupported, toast]);

  const unsubscribe = useCallback(async () => {
    setIsLoading(true);
    try {
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();

      if (subscription) {
        await subscription.unsubscribe();
      }

      // Remove subscription from backend
      const response = await fetch('/api/v1/notifications/push/unsubscribe', {
        method: 'DELETE',
      });

      if (!response.ok) throw new Error('Failed to remove push subscription');

      setIsSubscribed(false);
      toast({
        title: 'Push Notifications Disabled',
        description: 'You will no longer receive browser push notifications',
      });
    } catch (_error) {
      toast({
        title: 'Error',
        description: 'Failed to disable push notifications',
        variant: 'destructive',
      });
    } finally {
      setIsLoading(false);
    }
  }, [toast]);

  return {
    isSubscribed,
    isSupported,
    isLoading,
    subscribe,
    unsubscribe,
  };
}
