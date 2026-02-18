'use client';

import { AlertTriangleIcon } from 'lucide-react';

import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

export function KBSettings() {
  return (
    <div className="space-y-6">
      {/* Embedding Model */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-4">
          Embedding Model
        </h2>
        <div className="space-y-4">
          <div>
            <Label htmlFor="embedding-model">Model</Label>
            <Select defaultValue="text-embedding-3-small">
              <SelectTrigger id="embedding-model" className="bg-white dark:bg-zinc-900">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="text-embedding-3-small">text-embedding-3-small (OpenAI)</SelectItem>
                <SelectItem value="text-embedding-3-large">text-embedding-3-large (OpenAI)</SelectItem>
                <SelectItem value="all-minilm-l6-v2">all-MiniLM-L6-v2 (Sentence Transformers)</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      {/* Vector DB */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-4">
          Vector Database
        </h2>
        <div className="space-y-4">
          <div>
            <Label htmlFor="db-host">Host</Label>
            <Input id="db-host" defaultValue="localhost:6333" className="bg-white dark:bg-zinc-900" readOnly />
          </div>
          <div>
            <Label htmlFor="db-type">Type</Label>
            <Input id="db-type" defaultValue="Qdrant" className="bg-white dark:bg-zinc-900" readOnly />
          </div>
        </div>
      </div>

      {/* Processing Defaults */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-4">
          Processing Defaults
        </h2>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <Label htmlFor="default-chunk">Chunk Size</Label>
            <Input id="default-chunk" type="number" defaultValue="512" className="bg-white dark:bg-zinc-900" />
          </div>
          <div>
            <Label htmlFor="default-overlap">Overlap %</Label>
            <Input id="default-overlap" type="number" defaultValue="20" className="bg-white dark:bg-zinc-900" />
          </div>
        </div>
      </div>

      {/* Cache Settings */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-4">
          Cache Settings
        </h2>
        <div className="space-y-4">
          <div>
            <Label htmlFor="cache-ttl">Cache TTL (seconds)</Label>
            <Input id="cache-ttl" type="number" defaultValue="3600" className="bg-white dark:bg-zinc-900" />
          </div>
        </div>
      </div>

      {/* Danger Zone */}
      <div className="bg-red-50/70 dark:bg-red-900/20 backdrop-blur-md rounded-xl p-6 border-2 border-red-200 dark:border-red-800">
        <div className="flex items-center gap-2 mb-4">
          <AlertTriangleIcon className="w-5 h-5 text-red-600 dark:text-red-400" />
          <h2 className="font-quicksand text-xl font-bold text-red-900 dark:text-red-300">
            Danger Zone
          </h2>
        </div>
        <p className="text-sm text-red-700 dark:text-red-400 mb-4">
          These actions are irreversible and may affect system performance
        </p>
        <div className="flex gap-4">
          <Button variant="destructive">Rebuild Index</Button>
          <Button variant="outline" className="border-red-300 text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-900/20">
            Clear Cache
          </Button>
        </div>
      </div>
    </div>
  );
}
