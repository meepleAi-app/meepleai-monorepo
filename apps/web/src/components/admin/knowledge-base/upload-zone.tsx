'use client';

import { UploadCloudIcon, FileTextIcon } from 'lucide-react';

export function UploadZone() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-8 border-2 border-dashed border-amber-400/40 dark:border-amber-600/40 hover:border-amber-400/60 hover:bg-amber-50/10 dark:hover:bg-amber-900/10 transition-all cursor-pointer">
      <div className="flex flex-col items-center justify-center text-center">
        <div className="w-16 h-16 bg-amber-100 dark:bg-amber-900/30 rounded-full flex items-center justify-center mb-4">
          <UploadCloudIcon className="w-8 h-8 text-amber-700 dark:text-amber-400" />
        </div>
        <h3 className="font-quicksand text-lg font-bold text-slate-900 dark:text-zinc-100 mb-2">
          Drop files here or click to browse
        </h3>
        <p className="text-sm text-slate-600 dark:text-zinc-400 mb-4">
          Supports PDF, DOCX, TXT files (max 50MB per file)
        </p>
        <div className="flex items-center gap-4 text-xs text-slate-500 dark:text-zinc-500">
          <div className="flex items-center gap-1">
            <FileTextIcon className="w-4 h-4" />
            <span>PDF</span>
          </div>
          <div className="flex items-center gap-1">
            <FileTextIcon className="w-4 h-4" />
            <span>DOCX</span>
          </div>
          <div className="flex items-center gap-1">
            <FileTextIcon className="w-4 h-4" />
            <span>TXT</span>
          </div>
        </div>
      </div>
    </div>
  );
}
