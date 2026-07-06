import { z } from 'zod';

// Issue #1840 SP5 F4-C7 — alert channel config for the Canali drawer.
// Backend: AlertChannelDto (GetAllAlertChannelsQuery.cs).

export const alertChannelTypeSchema = z.enum(['email', 'slack']);

export const alertChannelTestStatusSchema = z.enum(['ok', 'error']);

export const alertChannelSchema = z.object({
  type: alertChannelTypeSchema,
  // ConfigJson is returned verbatim from the backend today. The Canali drawer
  // parses this into the appropriate per-type shape (EmailChannelConfig vs
  // SlackChannelConfig). Webhook URL masking is a deferred follow-up — see the
  // doc comment on AlertChannelsEndpoints.cs.
  configJson: z.string(),
  isEnabled: z.boolean(),
  lastTestedAt: z.string().datetime({ offset: true }).nullable(),
  lastTestStatus: alertChannelTestStatusSchema.nullable(),
  lastTestMessage: z.string().nullable(),
  updatedAt: z.string().datetime({ offset: true }),
  updatedBy: z.string().nullable(),
  // Postgres xmin system column (numeric optimistic-concurrency token), not base64.
  xmin: z.number(),
});

export const upsertAlertChannelRequestSchema = z.object({
  configJson: z.string(),
  isEnabled: z.boolean(),
  // Required when updating an existing channel, omit on first-time create.
  xmin: z.number().optional().nullable(),
});

// Backend: TestAlertChannelConnectionResult (TestAlertChannelConnectionCommand.cs):
//   internal sealed record TestAlertChannelConnectionResult(
//       bool Success, string Message, int? StatusCode, DateTime TestedAt);
// The handler does NOT echo back `type` (the caller already knows what they
// tested) — the FE keeps the requested type out-of-band.
export const testAlertChannelConnectionResultSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  statusCode: z.number().int().nullable().optional(),
  testedAt: z.string().datetime({ offset: true }),
});

// Per-type config shapes for parsing AlertChannelDto.configJson.
// These match the BE channel write-models — keep in sync with
// SlackWebhookClient / IEmailClient expectations.
export const slackChannelConfigSchema = z.object({
  webhookUrl: z.string().url(),
  channel: z.string().min(1),
});

export const emailChannelConfigSchema = z.object({
  smtpHost: z.string().min(1),
  smtpPort: z.number().int().min(1).max(65535),
  fromAddress: z.string().email(),
  toAddresses: z.array(z.string().email()).min(1),
});

export type AlertChannelType = z.infer<typeof alertChannelTypeSchema>;
export type AlertChannelTestStatus = z.infer<typeof alertChannelTestStatusSchema>;
export type AlertChannel = z.infer<typeof alertChannelSchema>;
export type UpsertAlertChannelRequest = z.infer<typeof upsertAlertChannelRequestSchema>;
export type TestAlertChannelConnectionResult = z.infer<
  typeof testAlertChannelConnectionResultSchema
>;
export type SlackChannelConfig = z.infer<typeof slackChannelConfigSchema>;
export type EmailChannelConfig = z.infer<typeof emailChannelConfigSchema>;
