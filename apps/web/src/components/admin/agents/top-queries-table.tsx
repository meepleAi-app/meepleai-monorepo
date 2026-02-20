'use client';

import { Badge } from '@/components/ui/badge';

interface QueryEntry {
  id: string;
  query: string;
  count: number;
  avgResponseTime: number;
  successRate: number;
}

const MOCK_QUERIES: QueryEntry[] = [
  {
    id: '1',
    query: 'What are the rules for Catan?',
    count: 342,
    avgResponseTime: 1.2,
    successRate: 98,
  },
  {
    id: '2',
    query: 'Best strategy games for 2 players',
    count: 287,
    avgResponseTime: 1.8,
    successRate: 95,
  },
  {
    id: '3',
    query: 'How to play Wingspan?',
    count: 251,
    avgResponseTime: 1.5,
    successRate: 97,
  },
  {
    id: '4',
    query: 'Cooperative games for families',
    count: 198,
    avgResponseTime: 2.1,
    successRate: 93,
  },
  {
    id: '5',
    query: 'Ticket to Ride scoring rules',
    count: 176,
    avgResponseTime: 1.3,
    successRate: 99,
  },
];

export function TopQueriesTable() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-amber-200/50 dark:border-zinc-700/50 overflow-hidden">
      <div className="p-6 border-b border-slate-200 dark:border-zinc-700">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100">
          Top Queries
        </h2>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead className="bg-amber-100/50 dark:bg-zinc-900/50 border-b border-amber-200/50 dark:border-zinc-700/50">
            <tr>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Query
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Count
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Avg Time
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Success Rate
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-zinc-700">
            {MOCK_QUERIES.map((query) => (
              <tr key={query.id} className="hover:bg-slate-50/50 dark:hover:bg-zinc-900/50">
                <td className="py-3 px-4 text-sm text-slate-900 dark:text-zinc-100">
                  {query.query}
                </td>
                <td className="py-3 px-4 text-right">
                  <Badge variant="outline" className="bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300">
                    {query.count}
                  </Badge>
                </td>
                <td className="py-3 px-4 text-right text-sm text-slate-600 dark:text-zinc-400">
                  {query.avgResponseTime}s
                </td>
                <td className="py-3 px-4 text-right">
                  <Badge
                    variant="outline"
                    className={
                      query.successRate >= 95
                        ? 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300'
                        : 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300'
                    }
                  >
                    {query.successRate}%
                  </Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
