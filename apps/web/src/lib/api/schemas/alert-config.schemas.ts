/**
 * Alert Configuration schemas (Issue #915)
 * TypeScript types for alert configuration API
 */

export interface AlertConfiguration {
  id: string;
  configKey: string;
  configValue: string;
  category: AlertConfigCategory;
  isEncrypted: boolean;
  description?: string;
  updatedAt: string;
  updatedBy: string;
}

export type AlertConfigCategory = 'Email' | 'Slack' | 'PagerDuty' | 'Global';

export interface UpdateAlertConfiguration {
  configKey: string;
  configValue: string;
  category: AlertConfigCategory;
  description?: string;
}

export interface EmailConfiguration {
  smtpHost: string;
  smtpPort: number;
  from: string;
  to: string[];
  useTls: boolean;
  username?: string;
  password?: string;
}

export interface SlackConfiguration {
  webhookUrl: string;
  channel: string;
}

export interface PagerDutyConfiguration {
  integrationKey: string;
}

export interface GeneralConfiguration {
  enabled: boolean;
  throttleMinutes: number;
}
