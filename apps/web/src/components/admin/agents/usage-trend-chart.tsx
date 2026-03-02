'use client';

const MOCK_USAGE_DATA = [
  { day: 'Mon', queries: 1850, height: 74 },
  { day: 'Tue', queries: 2240, height: 90 },
  { day: 'Wed', queries: 1920, height: 77 },
  { day: 'Thu', queries: 2480, height: 99 },
  { day: 'Fri', queries: 2100, height: 84 },
  { day: 'Sat', queries: 1680, height: 67 },
  { day: 'Sun', queries: 1560, height: 62 },
];

export function UsageTrendChart() {
  const _maxQueries = Math.max(...MOCK_USAGE_DATA.map(d => d.queries));

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-amber-200/50 dark:border-zinc-700/50">
      <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-4">
        Usage Trend (7 Days)
      </h2>
      <div className="flex items-end justify-between gap-4 h-[200px]">
        {MOCK_USAGE_DATA.map(item => (
          <div key={item.day} className="flex flex-col items-center flex-1 gap-2">
            <div
              className="w-full bg-gradient-to-t from-amber-500 to-amber-400 rounded-t-md transition-all hover:opacity-80 hover:translate-y-[-2px] cursor-pointer relative group"
              style={{ height: `${item.height}%` }}
            >
              <div className="absolute -top-8 left-1/2 transform -translate-x-1/2 opacity-0 group-hover:opacity-100 transition-opacity bg-slate-900 dark:bg-zinc-700 text-white text-xs px-2 py-1 rounded whitespace-nowrap">
                {item.queries.toLocaleString()} queries
              </div>
            </div>
            <div className="text-xs font-medium text-gray-600 dark:text-zinc-400">{item.day}</div>
          </div>
        ))}
      </div>
    </div>
  );
}
