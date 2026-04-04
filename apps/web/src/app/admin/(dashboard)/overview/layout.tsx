import { type Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Overview',
  description: 'Admin dashboard overview with platform stats and quick actions',
};

export default function OverviewLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
