/**
 * Contact Form API Client
 *
 * Public endpoint for contact form submissions.
 * No authentication required.
 */

import { type HttpClient } from '../core/httpClient';

// ============================================================================
// Types
// ============================================================================

export interface SendContactMessageRequest {
  name: string;
  email: string;
  subject: string;
  message: string;
}

// ============================================================================
// Client Factory
// ============================================================================

export interface CreateContactClientParams {
  httpClient: HttpClient;
}

export function createContactClient({ httpClient }: CreateContactClientParams) {
  return {
    /**
     * Submit a contact form message
     * POST /api/v1/contact
     *
     * Public endpoint — no auth required.
     *
     * @param data - Contact form data (name, email, subject, message)
     * @returns The message tracking ID (GUID string)
     */
    async send(data: SendContactMessageRequest): Promise<string> {
      return httpClient.post<string>('/api/v1/contact', data);
    },
  };
}

export type ContactClient = ReturnType<typeof createContactClient>;
