/**
 * Notification Deep Link Tests
 *
 * Tests for getNotificationDeepLink utility:
 * - pdf_upload_completed → /library/private/{privateGameId}
 * - Missing metadata returns null
 * - Invalid JSON metadata returns null
 * - Unknown notification types return null
 */

import { describe, it, expect } from 'vitest';

import type { NotificationDto } from '@/lib/api';

import { getNotificationDeepLink } from '../NotificationItem';

// ============================================================================
// Helpers
// ============================================================================

function createNotification(overrides: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    userId: '00000000-0000-0000-0000-000000000002',
    type: 'pdf_upload_completed',
    severity: 'success',
    title: 'PDF Ready',
    message: 'Your PDF has been processed',
    link: null,
    metadata: null,
    isRead: false,
    createdAt: new Date().toISOString(),
    readAt: null,
    ...overrides,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('getNotificationDeepLink', () => {
  it('generates correct href for PdfUploadCompleted notification with privateGameId', () => {
    const notification = createNotification({
      type: 'pdf_upload_completed',
      metadata: JSON.stringify({ privateGameId: 'game-1' }),
    });

    const href = getNotificationDeepLink(notification);

    expect(href).toBe('/library/private/game-1');
  });

  it('returns null when metadata is null', () => {
    const notification = createNotification({
      type: 'pdf_upload_completed',
      metadata: null,
    });

    expect(getNotificationDeepLink(notification)).toBeNull();
  });

  it('returns null when metadata has no privateGameId', () => {
    const notification = createNotification({
      type: 'pdf_upload_completed',
      metadata: JSON.stringify({ someOtherField: 'value' }),
    });

    expect(getNotificationDeepLink(notification)).toBeNull();
  });

  it('returns null for invalid JSON metadata', () => {
    const notification = createNotification({
      type: 'pdf_upload_completed',
      metadata: 'not-json',
    });

    expect(getNotificationDeepLink(notification)).toBeNull();
  });

  it('returns null for unknown notification types', () => {
    const notification = createNotification({
      type: 'some_unknown_type',
      metadata: JSON.stringify({ privateGameId: 'game-1' }),
    });

    expect(getNotificationDeepLink(notification)).toBeNull();
  });

  it('handles UUID-style privateGameId correctly', () => {
    const uuid = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';
    const notification = createNotification({
      type: 'pdf_upload_completed',
      metadata: JSON.stringify({ privateGameId: uuid }),
    });

    const href = getNotificationDeepLink(notification);

    expect(href).toBe(`/library/private/${uuid}`);
  });
});
