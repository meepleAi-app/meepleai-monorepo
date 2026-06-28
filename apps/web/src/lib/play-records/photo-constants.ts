/**
 * Shared constants for play-record photo uploads.
 * Used by EndgamePhotoUploadSection and PlayRecordPhotoUploadDialog.
 */

/** Maximum size per photo file (5 MB) — mirrors BE validator */
export const PHOTO_MAX_BYTES = 5 * 1024 * 1024;

/** Maximum number of photos per upload batch */
export const PHOTO_MAX_FILES = 10;

/** Accepted MIME types for photo uploads */
export const PHOTO_ACCEPTED_MIME = ['image/jpeg', 'image/png', 'image/webp', 'image/heic'];
