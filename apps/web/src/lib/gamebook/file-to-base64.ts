/**
 * Gamebook — File → base64 helper (Sprint 1, Task 1.8)
 *
 * Converts a browser File object to a base64-encoded string
 * (data URI stripped — only the raw base64 payload is returned).
 */

import type { PhotoUploadItem } from './schemas';

/**
 * Read a File as a base64 string (without the data URI prefix).
 *
 * @example
 *   const b64 = await fileToBase64(file);
 *   // => "iVBORw0KGgo..."
 */
export function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();

    reader.onload = () => {
      const result = reader.result as string;
      // Strip "data:<mime>;base64," prefix
      const base64 = result.split(',')[1];
      if (!base64) {
        reject(new Error(`Failed to read base64 from file: ${file.name}`));
        return;
      }
      resolve(base64);
    };

    reader.onerror = () => {
      reject(new Error(`FileReader error for: ${file.name}`));
    };

    reader.readAsDataURL(file);
  });
}

/**
 * Convert an array of File objects to PhotoUploadItem array.
 * Reads all files in parallel.
 */
export async function filesToPhotoItems(files: File[]): Promise<PhotoUploadItem[]> {
  return Promise.all(
    files.map(async file => ({
      base64Data: await fileToBase64(file),
      fileName: file.name,
      mimeType: file.type || 'image/jpeg',
    }))
  );
}
