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

async function extractInvitationMailosaur(testUserEmail: string): Promise<InvitationResult> {
  const Mailosaur = (await import('mailosaur')).default;
  const client = new Mailosaur(env.email.mailosaurApiKey!);

  const message = await client.messages.get(
    env.email.mailosaurServerId!,
    { sentTo: testUserEmail },
    { timeout: env.timeouts.email }
  );

  const html = message.html?.body ?? '';
  const linkMatch = html.match(/href="([^"]*accept-invite[^"]*)"/);

  if (!linkMatch) {
    throw new Error('No accept-invite link found in invitation email');
  }

  const invitationUrl = linkMatch[1];
  const tokenMatch = invitationUrl.match(/token=([^&"]+)/);

  if (!tokenMatch) {
    throw new Error('No token found in invitation URL');
  }

  return {
    invitationToken: tokenMatch[1],
    invitationUrl,
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
