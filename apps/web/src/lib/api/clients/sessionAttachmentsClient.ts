/**
 * Session Attachments API Client
 *
 * GAP-005: Photo capture upload for live game sessions.
 *
 * Uses native fetch (not httpClient.post) because multipart/form-data
 * requires the browser to set the Content-Type boundary automatically —
 * httpClient.post hardcodes Content-Type: application/json.
 *
 * The relative path /api/v1/... is routed via the Next.js catch-all proxy
 * (apps/web/src/app/api/[...path]/route.ts) to the backend.
 */

export interface UploadSessionAttachmentParams {
  sessionId: string;
  playerId: string;
  file: File;
  attachmentType: string;
  caption?: string;
}

/**
 * Upload a file attachment to a live session.
 *
 * @throws {Error} on non-2xx responses
 */
export async function uploadSessionAttachment(
  params: UploadSessionAttachmentParams
): Promise<void> {
  const form = new FormData();
  form.append('file', params.file);
  form.append('attachmentType', params.attachmentType);
  form.append('playerId', params.playerId);
  if (params.caption) {
    form.append('caption', params.caption);
  }

  const response = await fetch(
    `/api/v1/live-sessions/${encodeURIComponent(params.sessionId)}/attachments`,
    {
      method: 'POST',
      credentials: 'include',
      body: form,
      // Do NOT set Content-Type — browser sets it with the multipart boundary
    }
  );

  if (!response.ok) {
    throw new Error(`Attachment upload failed: ${response.status} ${response.statusText}`);
  }
}
