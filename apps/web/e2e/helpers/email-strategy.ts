import { type Page } from '@playwright/test';

import { env } from './onboarding-environment';

export interface InvitationResult {
  invitationToken: string;
  invitationUrl: string;
}

async function extractInvitationLocal(page: Page, apiURL: string): Promise<InvitationResult> {
  const response = await page.request.get(
    `${apiURL}/api/v1/admin/users/invitations?pageSize=1&sortBy=createdAt&sortDirection=desc`
  );

  if (!response.ok()) {
    throw new Error(`Failed to fetch invitations: ${response.status()}`);
  }

  const data = await response.json();
  const invitation = data.items?.[0] ?? data[0];

  if (!invitation) {
    throw new Error('No invitation found after sending invite');
  }

  const token = invitation.token ?? invitation.id;
  return {
    invitationToken: token,
    invitationUrl: `${env.baseURL}/accept-invite?token=${token}`,
  };
}

/**
 * Delete all previous messages for this email to avoid stale token matches.
 * Then wait for the fresh invitation email.
 */
async function extractInvitationMailosaur(testUserEmail: string): Promise<InvitationResult> {
  const Mailosaur = (await import('mailosaur')).default;
  const client = new Mailosaur(env.email.mailosaurApiKey!);
  const serverId = env.email.mailosaurServerId!;

  // Delete any previous messages for this address (avoid stale tokens)
  try {
    const existing = await client.messages.list(serverId, { sentTo: testUserEmail } as any);
    for (const msg of existing.items ?? []) {
      await client.messages.del(msg.id!);
    }
  } catch {
    // Ignore cleanup errors
  }

  // Wait for the fresh email (sent AFTER cleanup)
  const message = await client.messages.get(
    serverId,
    { sentTo: testUserEmail },
    { timeout: env.timeouts.email, receivedAfter: new Date(Date.now() - 60_000) }
  );

  const html = message.html?.body ?? '';
  console.log(
    `[Mailosaur] Subject: ${message.subject}, From: ${message.from?.[0]?.email}, Received: ${message.received}`
  );

  // Try multiple link patterns (href with quotes, or plain text URLs)
  const linkMatch =
    html.match(/href="([^"]*accept-invite[^"]*)"/) ??
    html.match(/href='([^']*accept-invite[^']*)'/) ??
    html.match(/(https?:\/\/[^\s<"]*accept-invite[^\s<"]*)/);

  if (!linkMatch) {
    // Log first 500 chars of HTML for debugging
    console.log(`[Mailosaur] No accept-invite link. HTML preview: ${html.slice(0, 500)}`);
    throw new Error(`No accept-invite link found in invitation email. Subject: ${message.subject}`);
  }

  const invitationUrl = linkMatch[1];
  console.log(`[Mailosaur] Invitation URL: ${invitationUrl}`);

  const tokenMatch = invitationUrl.match(/token=([^&"'\s]+)/);

  if (!tokenMatch) {
    throw new Error(`No token found in invitation URL: ${invitationUrl}`);
  }

  const token = tokenMatch[1];

  // The backend may generate URLs with internal hostname (e.g., localhost:3000).
  // Replace with the actual staging baseURL.
  const correctedUrl = `${env.baseURL}/accept-invite?token=${token}`;
  console.log(`[Mailosaur] Token: ${token.slice(0, 20)}... → ${correctedUrl}`);

  return {
    invitationToken: token,
    invitationUrl: correctedUrl,
  };
}

export async function extractInvitation(
  page: Page,
  testUserEmail: string
): Promise<InvitationResult> {
  if (env.email.strategy === 'mailosaur') {
    return extractInvitationMailosaur(testUserEmail);
  }
  return extractInvitationLocal(page, env.apiURL);
}
