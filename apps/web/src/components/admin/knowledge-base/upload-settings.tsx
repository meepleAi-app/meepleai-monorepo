'use client';

import { InfoIcon, CpuIcon, LanguagesIcon, ScissorsIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';

/**
 * UploadSettings - Informational display of current processing configuration.
 *
 * These settings are configured via environment variables on the server.
 * This component displays the current values for admin reference.
 */
export function UploadSettings() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
      <div className="flex items-center gap-2 mb-6">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100">
          Processing Settings
        </h2>
        <Badge variant="outline" className="text-xs">Read-only</Badge>
      </div>

      <div className="space-y-5">
        {/* Info Banner */}
        <div className="flex items-start gap-2 p-3 bg-amber-50/50 dark:bg-amber-900/10 border border-amber-200/50 dark:border-amber-700/30 rounded-lg">
          <InfoIcon className="w-4 h-4 text-amber-600 dark:text-amber-400 mt-0.5 shrink-0" />
          <p className="text-xs text-amber-800 dark:text-amber-300">
            These settings are configured via environment variables. To modify, update the relevant
            config files and restart the services.
          </p>
        </div>

        {/* Embedding Model */}
        <div className="space-y-1.5">
          <div className="flex items-center gap-2">
            <CpuIcon className="w-4 h-4 text-slate-500" />
            <span className="text-sm font-medium text-slate-700 dark:text-zinc-300">
              Embedding Model
            </span>
          </div>
          <div className="ml-6 space-y-1">
            <p className="text-sm text-slate-900 dark:text-zinc-100 font-mono">
              intfloat/multilingual-e5-large
            </p>
            <p className="text-xs text-slate-500">1024 dimensions</p>
          </div>
        </div>

        {/* Chunking */}
        <div className="space-y-1.5">
          <div className="flex items-center gap-2">
            <ScissorsIcon className="w-4 h-4 text-slate-500" />
            <span className="text-sm font-medium text-slate-700 dark:text-zinc-300">
              Chunking
            </span>
          </div>
          <div className="ml-6 grid grid-cols-2 gap-2">
            <div>
              <p className="text-xs text-slate-500">Default Chunk Size</p>
              <p className="text-sm text-slate-900 dark:text-zinc-100">512 tokens</p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Overlap</p>
              <p className="text-sm text-slate-900 dark:text-zinc-100">20%</p>
            </div>
          </div>
        </div>

        {/* Supported Languages */}
        <div className="space-y-1.5">
          <div className="flex items-center gap-2">
            <LanguagesIcon className="w-4 h-4 text-slate-500" />
            <span className="text-sm font-medium text-slate-700 dark:text-zinc-300">
              Supported Languages
            </span>
          </div>
          <div className="ml-6 flex flex-wrap gap-1.5">
            {[
              { code: 'en', label: 'English' },
              { code: 'it', label: 'Italian' },
              { code: 'de', label: 'German' },
              { code: 'fr', label: 'French' },
              { code: 'es', label: 'Spanish' },
            ].map((lang) => (
              <Badge key={lang.code} variant="secondary" className="text-xs">
                {lang.label}
              </Badge>
            ))}
          </div>
        </div>

        {/* Processing Pipeline */}
        <div className="space-y-1.5">
          <span className="text-sm font-medium text-slate-700 dark:text-zinc-300">
            Pipeline Steps
          </span>
          <div className="ml-0 flex items-center gap-1 text-xs text-slate-500">
            <Badge variant="outline" className="text-xs">Upload</Badge>
            <span>→</span>
            <Badge variant="outline" className="text-xs">Extract</Badge>
            <span>→</span>
            <Badge variant="outline" className="text-xs">Chunk</Badge>
            <span>→</span>
            <Badge variant="outline" className="text-xs">Embed</Badge>
            <span>→</span>
            <Badge variant="outline" className="text-xs">Index</Badge>
          </div>
        </div>

        {/* Upload Limits */}
        <div className="space-y-1.5 pt-2 border-t border-slate-200/50 dark:border-zinc-700/50">
          <span className="text-sm font-medium text-slate-700 dark:text-zinc-300">
            Upload Limits
          </span>
          <div className="ml-0 grid grid-cols-2 gap-2 text-sm">
            <div>
              <p className="text-xs text-slate-500">Max File Size</p>
              <p className="text-slate-900 dark:text-zinc-100">500 MB</p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Chunked Upload</p>
              <p className="text-slate-900 dark:text-zinc-100">&gt; 10 MB</p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Accepted Formats</p>
              <p className="text-slate-900 dark:text-zinc-100">PDF</p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Concurrent Sessions</p>
              <p className="text-slate-900 dark:text-zinc-100">3 per user</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
