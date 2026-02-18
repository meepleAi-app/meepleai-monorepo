'use client';

import { useState } from 'react';

import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export function UploadSettings() {
  const [chunkSize, setChunkSize] = useState(512);
  const [overlap, setOverlap] = useState(20);
  const [language, setLanguage] = useState('en');

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
      <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-6">
        Upload Settings
      </h2>

      <div className="space-y-6">
        {/* Chunk Size */}
        <div>
          <Label htmlFor="chunk-size" className="text-sm font-medium mb-2">
            Chunk Size: {chunkSize} tokens
          </Label>
          <input
            id="chunk-size"
            type="range"
            min="128"
            max="2048"
            step="128"
            value={chunkSize}
            onChange={(e) => setChunkSize(parseInt(e.target.value))}
            className="w-full h-2 bg-gray-200 dark:bg-zinc-700 rounded-lg appearance-none cursor-pointer accent-amber-500"
          />
          <div className="flex justify-between text-xs text-slate-500 dark:text-zinc-500 mt-1">
            <span>128</span>
            <span>2048</span>
          </div>
        </div>

        {/* Overlap */}
        <div>
          <Label htmlFor="overlap" className="text-sm font-medium mb-2">
            Overlap: {overlap}%
          </Label>
          <input
            id="overlap"
            type="range"
            min="0"
            max="50"
            step="5"
            value={overlap}
            onChange={(e) => setOverlap(parseInt(e.target.value))}
            className="w-full h-2 bg-gray-200 dark:bg-zinc-700 rounded-lg appearance-none cursor-pointer accent-amber-500"
          />
          <div className="flex justify-between text-xs text-slate-500 dark:text-zinc-500 mt-1">
            <span>0%</span>
            <span>50%</span>
          </div>
        </div>

        {/* Language */}
        <div>
          <Label htmlFor="language" className="text-sm font-medium mb-2">
            Language
          </Label>
          <Select value={language} onValueChange={setLanguage}>
            <SelectTrigger id="language" className="bg-white dark:bg-zinc-900">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="en">English</SelectItem>
              <SelectItem value="it">Italian</SelectItem>
              <SelectItem value="es">Spanish</SelectItem>
              <SelectItem value="fr">French</SelectItem>
              <SelectItem value="de">German</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>
    </div>
  );
}
