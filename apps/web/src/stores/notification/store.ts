/**
 * Notification Store (Issue #2053)
 *
 * Manages user notifications state with persistence:
 * - Notifications list
 * - Unread count for badge
 * - Mark read/unread operations
 * - Polling for new notifications
 *
 * Middleware Stack:
 * - devtools: Browser DevTools integration
 * - persist: localStorage for offline support
 * - immer: Mutable state updates
 */

import { create } from 'zustand';
import { devtools, persist, createJSONStorage } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import { api } from '@/lib/api';
import type { NotificationDto } from '@/lib/api';

// ============================================================================
// Store State Interface
// ============================================================================

export interface NotificationState {
  // Data
  notifications: NotificationDto[];
  unreadCount: number;

  // Loading states
  isLoading: boolean;
  isFetching: boolean;
  isMarkingRead: boolean;

  // Error state
  error: string | null;

  // Actions
  fetchNotifications: (params?: { unreadOnly?: boolean; limit?: number }) => Promise<void>;
  fetchUnreadCount: () => Promise<void>;
  markAsRead: (notificationId: string) => Promise<void>;
  markAllAsRead: () => Promise<void>;
  addNotification: (notification: NotificationDto) => void; // For real-time updates
  clearError: () => void;
  reset: () => void;
}

// ============================================================================
// Store Creation
// ============================================================================

export const useNotificationStore = create<NotificationState>()(
  devtools(
    persist(
      immer((set, _get) => ({
        // Initial state
        notifications: [],
        unreadCount: 0,
        isLoading: false,
        isFetching: false,
        isMarkingRead: false,
        error: null,

        // Fetch notifications
        fetchNotifications: async (params?) => {
          set(state => {
            state.isFetching = true;
            state.error = null;
          });

          try {
            const notifications = await api.notifications.getNotifications(params);

            set(state => {
              state.notifications = notifications;
              state.unreadCount = notifications.filter(n => !n.isRead).length;
              state.isFetching = false;
            });
          } catch (error) {
            set(state => {
              state.error =
                error instanceof Error ? error.message : 'Failed to fetch notifications';
              state.isFetching = false;
            });
          }
        },

        // Fetch unread count (optimized for badge)
        fetchUnreadCount: async () => {
          try {
            const count = await api.notifications.getUnreadCount();

            set(state => {
              state.unreadCount = count;
            });
          } catch {
            // Silent fail for badge count (non-critical)
            // Error logged by httpClient, no need to duplicate
          }
        },

        // Mark single notification as read
        markAsRead: async (notificationId: string) => {
          set(state => {
            state.isMarkingRead = true;
            state.error = null;
          });

          try {
            const success = await api.notifications.markNotificationRead(notificationId);

            if (success) {
              set(state => {
                const notification = state.notifications.find(n => n.id === notificationId);
                if (notification && !notification.isRead) {
                  notification.isRead = true;
                  notification.readAt = new Date().toISOString();
                  state.unreadCount = Math.max(0, state.unreadCount - 1);
                }
                state.isMarkingRead = false;
              });
            } else {
              throw new Error('Failed to mark notification as read');
            }
          } catch (error) {
            set(state => {
              state.error =
                error instanceof Error ? error.message : 'Failed to mark notification as read';
              state.isMarkingRead = false;
            });
          }
        },

        // Mark all notifications as read
        markAllAsRead: async () => {
          set(state => {
            state.isMarkingRead = true;
            state.error = null;
          });

          try {
            const _updatedCount = await api.notifications.markAllNotificationsRead();

            set(state => {
              state.notifications.forEach(n => {
                if (!n.isRead) {
                  n.isRead = true;
                  n.readAt = new Date().toISOString();
                }
              });
              state.unreadCount = 0;
              state.isMarkingRead = false;
            });
          } catch (error) {
            set(state => {
              state.error =
                error instanceof Error ? error.message : 'Failed to mark all notifications as read';
              state.isMarkingRead = false;
            });
          }
        },

        // Add notification (for real-time updates or testing)
        addNotification: (notification: NotificationDto) => {
          set(state => {
            // Prevent duplicates
            const exists = state.notifications.some(n => n.id === notification.id);
            if (!exists) {
              state.notifications.unshift(notification); // Add to start
              if (!notification.isRead) {
                state.unreadCount += 1;
              }
            }
          });
        },

        // Clear error
        clearError: () => {
          set(state => {
            state.error = null;
          });
        },

        // Reset store
        reset: () => {
          set(state => {
            state.notifications = [];
            state.unreadCount = 0;
            state.isLoading = false;
            state.isFetching = false;
            state.isMarkingRead = false;
            state.error = null;
          });
        },
      })),
      {
        // Persistence configuration
        name: 'meepleai-notification-store',
        // SSR-safe storage
        storage: createJSONStorage(() =>
          typeof window !== 'undefined'
            ? localStorage
            : {
                getItem: () => null,
                setItem: () => {},
                removeItem: () => {},
              }
        ),
        // Only persist unread count (not full notification bodies — list re-fetches on mount)
        partialize: state => ({
          unreadCount: state.unreadCount,
        }),
      }
    ),
    {
      name: 'NotificationStore', // DevTools name
    }
  )
);

// ============================================================================
// Selectors for Optimized Subscriptions
// ============================================================================

export const selectNotifications = (state: NotificationState) => state.notifications;
export const selectUnreadCount = (state: NotificationState) => state.unreadCount;
export const selectUnreadNotifications = (state: NotificationState) =>
  state.notifications.filter(n => !n.isRead);
export const selectIsLoading = (state: NotificationState) => state.isLoading || state.isFetching;
export const selectError = (state: NotificationState) => state.error;
