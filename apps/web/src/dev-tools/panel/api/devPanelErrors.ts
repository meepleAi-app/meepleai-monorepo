export class DevPanelClientError extends Error {
  public readonly status: number;
  public readonly traceId?: string;

  constructor(message: string, status: number, options?: { cause?: unknown; traceId?: string }) {
    super(message, options?.cause ? { cause: options.cause } : undefined);
    this.name = 'DevPanelClientError';
    this.status = status;
    this.traceId = options?.traceId;
  }
}
