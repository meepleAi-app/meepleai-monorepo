'use client';

const MOCK_COST_DATA = [
  { model: 'GPT-4 Turbo', cost: 124.8, percentage: 50, color: 'bg-blue-500' },
  { model: 'Claude 3.5 Sonnet', cost: 74.25, percentage: 30, color: 'bg-purple-500' },
  { model: 'GPT-3.5 Turbo', cost: 37.13, percentage: 15, color: 'bg-green-500' },
  { model: 'Gemini Pro', cost: 11.32, percentage: 5, color: 'bg-amber-500' },
];

export function CostBreakdownChart() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-amber-200/50 dark:border-zinc-700/50">
      <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-6">
        Cost Breakdown by Model
      </h2>
      <div className="space-y-4">
        {MOCK_COST_DATA.map((item) => (
          <div key={item.model}>
            <div className="flex items-center justify-between mb-2">
              <span className="text-sm font-medium text-gray-900 dark:text-zinc-100">
                {item.model}
              </span>
              <span className="text-sm font-semibold text-gray-900 dark:text-zinc-100">
                ${item.cost.toFixed(2)}
              </span>
            </div>
            <div className="w-full bg-gray-200 dark:bg-zinc-700 rounded-full h-6 overflow-hidden">
              <div
                className={`${item.color} h-full rounded-full transition-all duration-300 flex items-center justify-end px-2`}
                style={{ width: `${item.percentage}%` }}
              >
                <span className="text-xs font-semibold text-white">{item.percentage}%</span>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
