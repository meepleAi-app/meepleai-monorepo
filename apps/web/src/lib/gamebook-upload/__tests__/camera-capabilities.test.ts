/**
 * Unit tests for camera-capabilities (SP6 Phase C.2.A Interactions).
 *
 * Coverage matrix:
 *   - detectCameraPermissionState: 4-state (granted/denied/prompt/unsupported)
 *   - SSR safety (no window/navigator)
 *   - getUserMedia missing → unsupported
 *   - Permissions API success path
 *   - Permissions API throws → fallback to prompt
 *   - Permissions API returns unknown state → fallback to prompt
 *   - requestCameraStream: success, NotAllowedError → null, other errors rethrow
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { detectCameraPermissionState, requestCameraStream } from '../camera-capabilities';

// ---------------------------------------------------------------------------
// Test helpers — install / restore navigator + window globals
// ---------------------------------------------------------------------------

interface FakeNavigatorOptions {
  hasMediaDevices?: boolean;
  hasGetUserMedia?: boolean;
  hasPermissions?: boolean;
  permissionsState?: PermissionState | 'invalid';
  permissionsThrows?: boolean;
  getUserMediaImpl?: (constraints: MediaStreamConstraints) => Promise<MediaStream>;
}

function installFakeNavigator(opts: FakeNavigatorOptions = {}): void {
  const {
    hasMediaDevices = true,
    hasGetUserMedia = true,
    hasPermissions = true,
    permissionsState = 'prompt',
    permissionsThrows = false,
    getUserMediaImpl,
  } = opts;

  const mediaDevices = hasMediaDevices
    ? {
        getUserMedia: hasGetUserMedia
          ? (getUserMediaImpl ?? (() => Promise.resolve({} as MediaStream)))
          : undefined,
      }
    : undefined;

  const permissions = hasPermissions
    ? {
        query: vi.fn(() => {
          if (permissionsThrows) {
            return Promise.reject(new TypeError('camera not supported'));
          }
          return Promise.resolve({ state: permissionsState } as PermissionStatus);
        }),
      }
    : undefined;

  vi.stubGlobal('window', {});
  vi.stubGlobal('navigator', {
    mediaDevices,
    permissions,
  });
}

function uninstallFakeNavigator(): void {
  vi.unstubAllGlobals();
}

beforeEach(() => {
  // Default install — overridden per test as needed
  installFakeNavigator();
});

afterEach(() => {
  uninstallFakeNavigator();
});

// ---------------------------------------------------------------------------
// detectCameraPermissionState
// ---------------------------------------------------------------------------

describe('detectCameraPermissionState', () => {
  it('returns "unsupported" when window is undefined (SSR)', async () => {
    vi.unstubAllGlobals();
    vi.stubGlobal('window', undefined);
    vi.stubGlobal('navigator', undefined);
    expect(await detectCameraPermissionState()).toBe('unsupported');
  });

  it('returns "unsupported" when navigator is undefined', async () => {
    vi.unstubAllGlobals();
    vi.stubGlobal('window', {});
    vi.stubGlobal('navigator', undefined);
    expect(await detectCameraPermissionState()).toBe('unsupported');
  });

  it('returns "unsupported" when navigator.mediaDevices is missing', async () => {
    installFakeNavigator({ hasMediaDevices: false });
    expect(await detectCameraPermissionState()).toBe('unsupported');
  });

  it('returns "unsupported" when getUserMedia is missing on mediaDevices', async () => {
    installFakeNavigator({ hasGetUserMedia: false });
    expect(await detectCameraPermissionState()).toBe('unsupported');
  });

  it('returns "granted" when Permissions API resolves to granted', async () => {
    installFakeNavigator({ permissionsState: 'granted' });
    expect(await detectCameraPermissionState()).toBe('granted');
  });

  it('returns "denied" when Permissions API resolves to denied', async () => {
    installFakeNavigator({ permissionsState: 'denied' });
    expect(await detectCameraPermissionState()).toBe('denied');
  });

  it('returns "prompt" when Permissions API resolves to prompt', async () => {
    installFakeNavigator({ permissionsState: 'prompt' });
    expect(await detectCameraPermissionState()).toBe('prompt');
  });

  it('falls back to "prompt" when Permissions API throws', async () => {
    installFakeNavigator({ permissionsThrows: true });
    expect(await detectCameraPermissionState()).toBe('prompt');
  });

  it('falls back to "prompt" when Permissions API returns an unknown state', async () => {
    installFakeNavigator({ permissionsState: 'invalid' });
    expect(await detectCameraPermissionState()).toBe('prompt');
  });

  it('falls back to "prompt" when navigator.permissions is missing', async () => {
    installFakeNavigator({ hasPermissions: false });
    expect(await detectCameraPermissionState()).toBe('prompt');
  });
});

// ---------------------------------------------------------------------------
// requestCameraStream
// ---------------------------------------------------------------------------

describe('requestCameraStream', () => {
  it('returns null when navigator is undefined (SSR)', async () => {
    vi.unstubAllGlobals();
    vi.stubGlobal('window', undefined);
    vi.stubGlobal('navigator', undefined);
    expect(await requestCameraStream()).toBeNull();
  });

  it('returns null when mediaDevices is missing', async () => {
    installFakeNavigator({ hasMediaDevices: false });
    expect(await requestCameraStream()).toBeNull();
  });

  it('returns null when getUserMedia is missing', async () => {
    installFakeNavigator({ hasGetUserMedia: false });
    expect(await requestCameraStream()).toBeNull();
  });

  it('returns the MediaStream on success', async () => {
    const stream = { id: 'mock-stream' } as unknown as MediaStream;
    installFakeNavigator({
      getUserMediaImpl: () => Promise.resolve(stream),
    });
    expect(await requestCameraStream()).toBe(stream);
  });

  it('calls getUserMedia with environment facingMode + audio:false', async () => {
    const spy = vi.fn(() => Promise.resolve({} as MediaStream));
    installFakeNavigator({ getUserMediaImpl: spy });
    await requestCameraStream();
    expect(spy).toHaveBeenCalledTimes(1);
    const arg = spy.mock.calls[0][0];
    expect(arg.audio).toBe(false);
    expect((arg.video as MediaTrackConstraints).facingMode).toBe('environment');
  });

  it('returns null when getUserMedia rejects with NotAllowedError', async () => {
    const denied = new DOMException('User denied permission', 'NotAllowedError');
    installFakeNavigator({
      getUserMediaImpl: () => Promise.reject(denied),
    });
    expect(await requestCameraStream()).toBeNull();
  });

  it('rethrows non-NotAllowedError DOMException (e.g., NotFoundError)', async () => {
    const hardware = new DOMException('No camera hardware', 'NotFoundError');
    installFakeNavigator({
      getUserMediaImpl: () => Promise.reject(hardware),
    });
    await expect(requestCameraStream()).rejects.toThrow('No camera hardware');
  });

  it('rethrows generic non-DOMException errors', async () => {
    installFakeNavigator({
      getUserMediaImpl: () => Promise.reject(new Error('boom')),
    });
    await expect(requestCameraStream()).rejects.toThrow('boom');
  });
});
