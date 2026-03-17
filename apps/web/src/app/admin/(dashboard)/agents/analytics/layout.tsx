import { type Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Analytics',
  description: 'Monitor AI agent performance and costs',
};

export default function AgentAnalyticsLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
