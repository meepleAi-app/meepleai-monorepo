import React, { useEffect, useState } from 'react';
import { Chrono } from 'react-chrono';
import { useRouter } from 'next/router';

interface VersionNode {
  id: string;
  version: string;
  title: string;
  description: string;
  author: string;
  createdAt: string;
  parentVersionId?: string;
  parentVersion?: string;
  changeCount: number;
  isCurrentVersion: boolean;
}

interface VersionTimelineProps {
  gameId: string;
  onVersionClick?: (version: string) => void;
}

export const VersionTimeline: React.FC<VersionTimelineProps> = ({
  gameId,
  onVersionClick
}) => {
  const [versions, setVersions] = useState<VersionNode[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();

  useEffect(() => {
    const fetchTimeline = async () => {
      try {
        setLoading(true);
        const response = await fetch(
          `${process.env.NEXT_PUBLIC_API_BASE}/api/v1/games/${gameId}/rulespec/versions/timeline`,
          {
            credentials: 'include',
          }
        );

        if (!response.ok) {
          throw new Error('Failed to fetch version timeline');
        }

        const data = await response.json();
        setVersions(data.versions || []);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Unknown error');
      } finally {
        setLoading(false);
      }
    };

    if (gameId) {
      fetchTimeline();
    }
  }, [gameId]);

  if (loading) {
    return (
      <div className="flex items-center justify-center p-8" role="status" aria-live="polite" aria-label="Loading timeline">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" aria-hidden="true"></div>
        <span className="sr-only">Loading version timeline…</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
        <strong className="font-bold">Error:</strong>
        <span className="block sm:inline"> {error}</span>
      </div>
    );
  }

  if (versions.length === 0) {
    return (
      <div className="text-center p-8 text-gray-500">
        No version history available for this game.
      </div>
    );
  }

  const items = versions.map(v => ({
    title: v.createdAt,
    cardTitle: v.version,
    cardSubtitle: `${v.author} • ${v.changeCount} changes`,
    cardDetailedText: `${v.description}${v.parentVersion ? ` (based on ${v.parentVersion})` : ''}${v.isCurrentVersion ? ' [CURRENT]' : ''}`,
  }));

  const handleItemSelected = (data: { index: number }) => {
    const index = data?.index;
    if (index !== undefined && versions[index]) {
      const version = versions[index];
      if (onVersionClick) {
        onVersionClick(version.version);
      } else {
        router.push(`/games/${gameId}/versions/${version.version}`);
      }
    }
  };

  return (
    <div className="version-timeline-container bg-white rounded-lg shadow p-6">
      <h2 className="text-2xl font-bold mb-6 text-gray-800">Version Timeline</h2>
      <div style={{ width: '100%', height: '600px' }}>
        <Chrono
          items={items}
          mode="VERTICAL_ALTERNATING"
          theme={{
            primary: '#3b82f6',
            secondary: '#f3f4f6',
            cardBgColor: '#ffffff',
            titleColor: '#1f2937',
            titleColorActive: '#3b82f6',
          }}
          cardHeight={120}
          slideShow={false}
          scrollable
          onItemSelected={handleItemSelected}
          fontSizes={{
            cardSubtitle: '0.85rem',
            cardText: '0.8rem',
            cardTitle: '1rem',
            title: '0.9rem',
          }}
        />
      </div>
    </div>
  );
};

export default VersionTimeline;
