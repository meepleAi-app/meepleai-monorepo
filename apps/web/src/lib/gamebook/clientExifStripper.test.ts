/**
 * Unit tests for clientExifStripper (issue #834 — privacy AC-2..AC-7).
 *
 * Test fixtures are generated programmatically (no binary commits) by injecting
 * GPS metadata into a minimal 1x1 JPEG seed via piexifjs. This keeps the test
 * suite hermetic and avoids EXIF generator dependencies.
 */
import { describe, expect, it } from 'vitest';
// @ts-expect-error -- piexifjs ships JS only, no @types/piexifjs
import piexif from 'piexifjs';

import { stripExif, type StripResult } from './clientExifStripper';

// ---------------------------------------------------------------------------
// Fixture generators
// ---------------------------------------------------------------------------

/**
 * Minimal 1x1 white-pixel JPEG (Baseline DCT, no EXIF).
 * Captured from `ffmpeg -f lavfi -i color=white:1x1 -frames:v 1 -f mjpeg`
 * and base64-encoded. Stable across environments.
 */
const MINIMAL_JPEG_NO_EXIF_BASE64 =
  '/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEB' +
  'AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQH/2wBDAQEBAQEBAQEBAQEBAQEBAQEBAQEB' +
  'AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQH/wAARCAABAAEDASIA' +
  'AhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAr/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFAEB' +
  'AAAAAAAAAAAAAAAAAAAAAP/EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAMAwEAAhEDEQA/AL+AB//Z';

function base64ToBytes(b64: string): Uint8Array {
  const binary = atob(b64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

function bytesToFile(bytes: Uint8Array, name: string, type: string): File {
  return new File([bytes], name, { type, lastModified: 0 });
}

function makeJpegNoExif(name = 'plain.jpg'): File {
  return bytesToFile(base64ToBytes(MINIMAL_JPEG_NO_EXIF_BASE64), name, 'image/jpeg');
}

/**
 * Build a JPEG fixture with GPS coordinates + Orientation injected via piexifjs.
 * GPS=(45.46, 9.19) ≈ Milan; Orientation=6 (90° CW, common iPhone landscape).
 */
function makeJpegWithGpsAndOrientation(name = 'with-gps.jpg'): File {
  const baseDataUri = `data:image/jpeg;base64,${MINIMAL_JPEG_NO_EXIF_BASE64}`;
  const gpsIfd: Record<number, unknown> = {
    [piexif.GPSIFD.GPSLatitudeRef]: 'N',
    [piexif.GPSIFD.GPSLatitude]: [
      [45, 1],
      [27, 1],
      [3600, 100],
    ],
    [piexif.GPSIFD.GPSLongitudeRef]: 'E',
    [piexif.GPSIFD.GPSLongitude]: [
      [9, 1],
      [11, 1],
      [2400, 100],
    ],
  };
  const zerothIfd: Record<number, unknown> = {
    [piexif.ImageIFD.Orientation]: 6,
    [piexif.ImageIFD.Make]: 'Apple',
    [piexif.ImageIFD.Model]: 'iPhone 15 Pro',
  };
  const exifIfd: Record<number, unknown> = {
    [piexif.ExifIFD.DateTimeOriginal]: '2026:05:18 14:30:00',
  };

  const exifObj = {
    '0th': zerothIfd,
    Exif: exifIfd,
    GPS: gpsIfd,
    Interop: {},
    '1st': {},
    thumbnail: null,
  };

  const exifBytes = piexif.dump(exifObj);
  const newDataUri = piexif.insert(exifBytes, baseDataUri);
  const newBase64 = newDataUri.split(',')[1];
  return bytesToFile(base64ToBytes(newBase64), name, 'image/jpeg');
}

/**
 * Corrupted JPEG: valid SOI marker (FFD8) but garbage afterward.
 * piexifjs.load() throws "given data is not jpeg" on this.
 */
function makeCorruptedJpeg(name = 'corrupt.jpg'): File {
  const bytes = new Uint8Array([0xff, 0xd8, 0x00, 0x00, 0x00, 0x00]);
  return bytesToFile(bytes, name, 'image/jpeg');
}

// ---------------------------------------------------------------------------
// Tests — AC-2..AC-7 from issue #834
// ---------------------------------------------------------------------------

describe('stripExif', () => {
  describe('happy path — JPEG with GPS (AC-2, AC-3, AC-4)', () => {
    it('returns kind=success with tagsRemoved > 0 when GPS present', async () => {
      const input = makeJpegWithGpsAndOrientation('vacation.jpg');
      const result = await stripExif(input);

      expect(result.kind).toBe('success');
      if (result.kind === 'success') {
        // GPS lat/lon ref + lat + lon ref + lon + Make + Model + DateTimeOriginal = 7 tags
        expect(result.tagsRemoved).toBeGreaterThanOrEqual(7);
        expect(result.file.name).toBe('vacation.jpg');
        expect(result.file.type).toBe('image/jpeg');
      }
    });

    it('removes GPS tags from output (AC-2)', async () => {
      const input = makeJpegWithGpsAndOrientation();
      const result = await stripExif(input);

      expect(result.kind).toBe('success');
      if (result.kind !== 'success') return;

      const outputBytes = await result.file.arrayBuffer();
      const outputBase64 = btoa(String.fromCharCode(...new Uint8Array(outputBytes)));
      const outputDataUri = `data:image/jpeg;base64,${outputBase64}`;
      const exif = piexif.load(outputDataUri);

      expect(Object.keys(exif.GPS ?? {})).toHaveLength(0);
    });

    it('preserves Orientation tag for OCR rotation (AC-4)', async () => {
      const input = makeJpegWithGpsAndOrientation();
      const result = await stripExif(input);

      expect(result.kind).toBe('success');
      if (result.kind !== 'success') return;

      const outputBytes = await result.file.arrayBuffer();
      const outputBase64 = btoa(String.fromCharCode(...new Uint8Array(outputBytes)));
      const outputDataUri = `data:image/jpeg;base64,${outputBase64}`;
      const exif = piexif.load(outputDataUri);

      expect(exif['0th']?.[piexif.ImageIFD.Orientation]).toBe(6);
    });

    it('removes device fingerprint tags (Make, Model)', async () => {
      const input = makeJpegWithGpsAndOrientation();
      const result = await stripExif(input);

      expect(result.kind).toBe('success');
      if (result.kind !== 'success') return;

      const outputBytes = await result.file.arrayBuffer();
      const outputBase64 = btoa(String.fromCharCode(...new Uint8Array(outputBytes)));
      const outputDataUri = `data:image/jpeg;base64,${outputBase64}`;
      const exif = piexif.load(outputDataUri);

      expect(exif['0th']?.[piexif.ImageIFD.Make]).toBeUndefined();
      expect(exif['0th']?.[piexif.ImageIFD.Model]).toBeUndefined();
    });

    it('removes DateTimeOriginal (timezone leak)', async () => {
      const input = makeJpegWithGpsAndOrientation();
      const result = await stripExif(input);

      expect(result.kind).toBe('success');
      if (result.kind !== 'success') return;

      const outputBytes = await result.file.arrayBuffer();
      const outputBase64 = btoa(String.fromCharCode(...new Uint8Array(outputBytes)));
      const outputDataUri = `data:image/jpeg;base64,${outputBase64}`;
      const exif = piexif.load(outputDataUri);

      expect(exif.Exif?.[piexif.ExifIFD.DateTimeOriginal]).toBeUndefined();
    });
  });

  describe('fallback — image without EXIF (AC-5)', () => {
    it('returns kind=fallback-no-exif for JPEG without EXIF block', async () => {
      const input = makeJpegNoExif('plain.jpg');
      const result = await stripExif(input);

      expect(result.kind).toBe('fallback-no-exif');
      if (result.kind === 'fallback-no-exif') {
        expect(result.file).toBe(input);
      }
    });

    it('returns kind=fallback-no-exif for PNG (out of scope, no re-encode)', async () => {
      const pngBytes = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
      const input = bytesToFile(pngBytes, 'screenshot.png', 'image/png');
      const result = await stripExif(input);

      expect(result.kind).toBe('fallback-no-exif');
      if (result.kind === 'fallback-no-exif') {
        expect(result.file).toBe(input);
      }
    });
  });

  describe('error-corrupted (AC-6)', () => {
    it('returns kind=error-corrupted for malformed JPEG', async () => {
      const input = makeCorruptedJpeg();
      const result = await stripExif(input);

      expect(result.kind).toBe('error-corrupted');
      if (result.kind === 'error-corrupted') {
        expect(result.reason).toBeTruthy();
        expect(typeof result.reason).toBe('string');
      }
    });
  });

  describe('error-unsupported (AC-7)', () => {
    it('returns kind=error-unsupported for HEIC mime type', async () => {
      const bytes = new Uint8Array([0x00, 0x00, 0x00, 0x18]);
      const input = bytesToFile(bytes, 'live-photo.heic', 'image/heic');
      const result = await stripExif(input);

      expect(result).toEqual<StripResult>({
        kind: 'error-unsupported',
        format: 'heic',
      });
    });

    it('returns kind=error-unsupported for HEIC extension without mime', async () => {
      const bytes = new Uint8Array([0x00, 0x00, 0x00, 0x18]);
      const input = bytesToFile(bytes, 'live-photo.heic', '');
      const result = await stripExif(input);

      expect(result).toEqual<StripResult>({
        kind: 'error-unsupported',
        format: 'heic',
      });
    });

    it('returns kind=error-unsupported for AVIF', async () => {
      const bytes = new Uint8Array([0x00, 0x00, 0x00, 0x18]);
      const input = bytesToFile(bytes, 'photo.avif', 'image/avif');
      const result = await stripExif(input);

      expect(result).toEqual<StripResult>({
        kind: 'error-unsupported',
        format: 'avif',
      });
    });
  });
});
