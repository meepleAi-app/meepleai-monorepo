import { Metadata } from 'next';
import { RateLimitConfigClient } from './client';

export const metadata: Metadata = {
  title: 'Rate Limit Configuration | Admin | MeepleAI',
  description: 'Configure rate limit settings for user tiers and manage individual user overrides',
};

export default function RateLimitConfigPage() {
  return <RateLimitConfigClient />;
}
