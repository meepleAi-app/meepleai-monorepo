/**
 * Email Template Placeholders Configuration
 *
 * Maps template names to their available placeholders
 * and provides default test data for preview rendering.
 */

export const TEMPLATE_PLACEHOLDERS: Record<string, string[]> = {
  // Common (available for all templates)
  _common: ['userName', 'appUrl', 'unsubscribeUrl'],
  // PDF
  pdf_ready: ['fileName', 'documentUrl'],
  document_failed: ['fileName', 'errorMessage'],
  retry_available: ['fileName', 'retryCount'],
  // Game Night
  game_night_invitation: ['eventTitle', 'eventDate', 'organizerName', 'eventLocation', 'rsvpUrl'],
  game_night_reminder: ['eventTitle', 'eventDate', 'eventLocation'],
  game_night_cancelled: ['eventTitle', 'organizerName'],
  // Share Request
  share_request_created: ['gameTitle', 'requestStatus'],
  share_request_approved: ['gameTitle'],
  share_request_rejected: ['gameTitle'],
  // Admin
  admin_digest: ['alertType', 'alertMessage'],
  // Badge
  badge_earned: ['badgeName', 'badgeDescription'],
};

export const DEFAULT_TEST_DATA: Record<string, Record<string, string>> = {
  _common: {
    userName: 'Mario Rossi',
    appUrl: 'https://meepleai.app',
    unsubscribeUrl: '#',
  },
  pdf_ready: { fileName: 'catan-rules.pdf', documentUrl: '#' },
  game_night_invitation: {
    eventTitle: 'Serata Catan',
    eventDate: 'Sabato 15 Mar, 20:00',
    organizerName: 'Mario',
    eventLocation: 'Casa di Mario',
    rsvpUrl: '#',
  },
};

/**
 * Get all placeholders applicable to a template by name.
 * Returns common placeholders + template-specific ones.
 */
export function getPlaceholdersForType(templateName: string): string[] {
  const common = TEMPLATE_PLACEHOLDERS._common ?? [];
  const specific = TEMPLATE_PLACEHOLDERS[templateName] ?? [];
  return [...common, ...specific];
}

/**
 * Get default test data for a template (for preview rendering).
 */
export function getDefaultTestData(templateName: string): Record<string, string> {
  return {
    ...(DEFAULT_TEST_DATA._common ?? {}),
    ...(DEFAULT_TEST_DATA[templateName] ?? {}),
  };
}
