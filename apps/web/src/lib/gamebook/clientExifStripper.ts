/**
 * Gamebook — Client-side EXIF GPS stripper (privacy)
 *
 * Strips EXIF metadata from JPEG photos before upload so user home location
 * (GPS coordinates) is not leaked to server-side storage, AI vision pipelines,
 * or admin incident-triage logs. Spec §6 privacy requirement; tracked by #834.
 *
 * Strategy: WHITELIST > BLACKLIST (default-secure).
 * Strip ALL EXIF tags except an explicit allow-list of OCR/rendering-critical
 * tags. Any new EXIF tag introduced by a future spec is stripped by default,
 * so we never accidentally leak data via tags we did not know about.
 *
 * @see https://github.com/hMatoba/piexifjs
 */
// @ts-expect-error -- piexifjs ships JS only, no @types/piexifjs
import piexif from 'piexifjs';

// ---------------------------------------------------------------------------
// Whitelist — tags preserved on strip
// ---------------------------------------------------------------------------

/**
 * EXIF tags preserved on strip. Everything else (including GPS, timestamps,
 * device fingerprint, OEM custom data) is removed.
 *
 * Numeric tag IDs per EXIF 2.32 spec (matches piexifjs constants):
 * - 0x0112 Orientation       (OCR needs rotation hint, values 1..8)
 * - 0xA002 PixelXDimension   (image width)
 * - 0xA003 PixelYDimension   (image height)
 * - 0xA001 ColorSpace        (sRGB / Adobe RGB)
 */
const PRESERVED_IFD0_TAG_IDS = new Set<number>([0x0112]);
const PRESERVED_EXIF_TAG_IDS = new Set<number>([0xa002, 0xa003, 0xa001]);

// ---------------------------------------------------------------------------
// Result discriminated union
// ---------------------------------------------------------------------------

export type StripResult =
  | { kind: 'success'; file: File; tagsRemoved: number }
  | { kind: 'fallback-no-exif'; file: File }
  | { kind: 'error-corrupted'; reason: string }
  | { kind: 'error-unsupported'; format: string };

// ---------------------------------------------------------------------------
// Format detection
// ---------------------------------------------------------------------------

const UNSUPPORTED_EXTENSIONS = new Set(['heic', 'heif', 'avif']);

function detectUnsupportedFormat(file: File): string | null {
  const mime = file.type.toLowerCase();
  if (mime === 'image/heic' || mime === 'image/heif') return 'heic';
  if (mime === 'image/avif') return 'avif';

  const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
  if (UNSUPPORTED_EXTENSIONS.has(ext)) return ext;

  return null;
}

function isJpeg(file: File): boolean {
  const mime = file.type.toLowerCase();
  if (mime === 'image/jpeg' || mime === 'image/jpg') return true;
  const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
  return ext === 'jpg' || ext === 'jpeg';
}

// ---------------------------------------------------------------------------
// FileReader → data URI helper
// ---------------------------------------------------------------------------

function fileToDataUri(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result;
      if (typeof result !== 'string') {
        reject(new Error('FileReader returned non-string result'));
        return;
      }
      resolve(result);
    };
    reader.onerror = () => reject(new Error(`FileReader error: ${file.name}`));
    reader.readAsDataURL(file);
  });
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Strip EXIF privacy-sensitive tags from an image File.
 *
 * @param file - Input File (typically from <input type="file"> or drop event)
 * @returns StripResult — caller must inspect `.kind` to decide upload behaviour
 *
 * @example
 *   const result = await stripExif(file);
 *   switch (result.kind) {
 *     case 'success':
 *     case 'fallback-no-exif':
 *       await uploadPhoto(result.file);
 *       break;
 *     case 'error-corrupted':
 *       toast({ title: 'Image format not supported', description: result.reason });
 *       break;
 *     case 'error-unsupported':
 *       toast({ title: 'Please convert to JPEG', description: `Format: ${result.format}` });
 *       break;
 *   }
 */
export async function stripExif(file: File): Promise<StripResult> {
  const unsupported = detectUnsupportedFormat(file);
  if (unsupported) {
    return { kind: 'error-unsupported', format: unsupported };
  }

  // Non-JPEG files (typically PNG): no EXIF to strip via piexifjs.
  // PNG can carry GPS in tEXt/iTXt textual chunks but stripping that is out
  // of scope for this PR (see issue #834 "Out of scope").
  if (!isJpeg(file)) {
    return { kind: 'fallback-no-exif', file };
  }

  let dataUri: string;
  try {
    dataUri = await fileToDataUri(file);
  } catch (err) {
    return {
      kind: 'error-corrupted',
      reason: err instanceof Error ? err.message : 'Failed to read file',
    };
  }

  let exifObj: Record<string, Record<number, unknown>>;
  try {
    exifObj = piexif.load(dataUri);
  } catch (err) {
    const reason = err instanceof Error ? err.message : String(err);
    if (/given data is not jpeg/i.test(reason)) {
      return { kind: 'error-corrupted', reason: 'Not a valid JPEG file' };
    }
    return { kind: 'error-corrupted', reason };
  }

  // Count tags before strip (excluding thumbnail metadata buckets we ignore)
  const originalCount =
    Object.keys(exifObj['0th'] ?? {}).length +
    Object.keys(exifObj.Exif ?? {}).length +
    Object.keys(exifObj.GPS ?? {}).length +
    Object.keys(exifObj.Interop ?? {}).length +
    Object.keys(exifObj['1st'] ?? {}).length;

  if (originalCount === 0) {
    // JPEG without EXIF block (rare but valid) — no-op pass-through
    return { kind: 'fallback-no-exif', file };
  }

  // Whitelist filter
  const filteredIfd0: Record<number, unknown> = {};
  for (const [tag, value] of Object.entries(exifObj['0th'] ?? {})) {
    const tagId = Number(tag);
    if (PRESERVED_IFD0_TAG_IDS.has(tagId)) {
      filteredIfd0[tagId] = value;
    }
  }

  const filteredExif: Record<number, unknown> = {};
  for (const [tag, value] of Object.entries(exifObj.Exif ?? {})) {
    const tagId = Number(tag);
    if (PRESERVED_EXIF_TAG_IDS.has(tagId)) {
      filteredExif[tagId] = value;
    }
  }

  const stripped = {
    '0th': filteredIfd0,
    Exif: filteredExif,
    GPS: {},
    Interop: {},
    '1st': {},
    thumbnail: null,
  };

  const newCount = Object.keys(filteredIfd0).length + Object.keys(filteredExif).length;

  let newDataUri: string;
  try {
    const exifBytes = piexif.dump(stripped);
    newDataUri = piexif.insert(exifBytes, dataUri);
  } catch (err) {
    return {
      kind: 'error-corrupted',
      reason: err instanceof Error ? err.message : 'EXIF rewrite failed',
    };
  }

  const newFile = dataUriToFile(newDataUri, file);
  return {
    kind: 'success',
    file: newFile,
    tagsRemoved: originalCount - newCount,
  };
}

// ---------------------------------------------------------------------------
// data URI → File (internal helper)
// ---------------------------------------------------------------------------

function dataUriToFile(dataUri: string, original: File): File {
  const [header, base64] = dataUri.split(',');
  if (!header || !base64) {
    throw new Error('Invalid data URI structure');
  }
  const mimeMatch = header.match(/:([^;]+);/);
  const mime = mimeMatch?.[1] ?? original.type ?? 'image/jpeg';

  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }

  return new File([bytes], original.name, {
    type: mime,
    lastModified: original.lastModified,
  });
}
