'use client';

import dynamic from 'next/dynamic';

// Re-export props interface from actual component for type safety
// (Avoids TypeScript strict mode issues with dynamic component inference)
export type { PromptEditorProps } from './PromptEditor';

// Lazy load Monaco Editor to reduce initial bundle size (~800KB savings)
// Issue #994: Frontend Build Optimization (BGAI-054)
const LazyPromptEditor = dynamic(() => import('./PromptEditor'), {
  ssr: false,
  loading: () => (
    <div className="border border-gray-300 rounded-md overflow-hidden">
      <div className="flex items-center justify-center h-[400px] bg-gray-50">
        <div className="text-gray-500 animate-pulse">Loading editor...</div>
      </div>
    </div>
  ),
});

export default LazyPromptEditor;
