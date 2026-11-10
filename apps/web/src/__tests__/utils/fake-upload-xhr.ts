/**
 * A scripted `XMLHttpRequest` for the upload zones that post the file with XHR (progress events).
 *
 *   beforeEach(() => { FakeUploadXhr.reset(); vi.stubGlobal('XMLHttpRequest', FakeUploadXhr); });
 *   afterEach(() => vi.unstubAllGlobals());
 *   FakeUploadXhr.respondWith(200, JSON.stringify(body));
 *
 * `send()` answers asynchronously with the scripted status and body, firing the `load` listeners the way the
 * browser does; the request it was opened with is kept in `FakeUploadXhr.opened`.
 */
export class FakeUploadXhr {
  static status = 200;
  static responseBody = '';
  static opened: Array<{ method: string; url: string }> = [];

  static reset(): void {
    FakeUploadXhr.status = 200;
    FakeUploadXhr.responseBody = '';
    FakeUploadXhr.opened = [];
  }

  static respondWith(status: number, body: string): void {
    FakeUploadXhr.status = status;
    FakeUploadXhr.responseBody = body;
  }

  status = 0;
  statusText = '';
  responseText = '';
  withCredentials = false;
  /** The `onload` / `onerror` property style (`xhr.onload = () => …`) next to `addEventListener`. */
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  upload: { addEventListener: () => void; onprogress: unknown } = {
    addEventListener: () => undefined,
    onprogress: null,
  };
  private readonly listeners: Record<string, Array<() => void>> = {};

  addEventListener(type: string, callback: () => void): void {
    (this.listeners[type] ??= []).push(callback);
  }

  open(method: string, url: string): void {
    FakeUploadXhr.opened.push({ method, url });
  }

  abort(): void {}

  send(): void {
    setTimeout(() => {
      this.status = FakeUploadXhr.status;
      this.responseText = FakeUploadXhr.responseBody;
      this.onload?.();
      this.listeners.load?.forEach(callback => callback());
    }, 0);
  }
}
