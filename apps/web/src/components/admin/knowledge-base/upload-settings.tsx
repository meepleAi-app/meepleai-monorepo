/* eslint-disable local/no-hardcoded-color-utility -- admin KB chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13b admin scope (see token-bridge-map.md for --admin-* decision deferred to DS-15). */
'use client';

import { InfoIcon, CpuIcon, LanguagesIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { useEmbeddingServiceInfo } from '@/hooks/queries/useEmbeddingService';
import { usePdfUploadLimits } from '@/hooks/queries/usePdfUploadLimits';
import { formatSizeLimit } from '@/lib/format/file-size';

import { MAX_SINGLE_UPLOAD_SIZE } from './upload-thresholds';

const NOT_AVAILABLE = 'Not available';
const LOADING = 'Loading…';

const LANGUAGE_NAMES = new Intl.DisplayNames(['en'], { type: 'language' });

function languageName(code: string): string {
  try {
    return LANGUAGE_NAMES.of(code) ?? code;
  } catch {
    return code;
  }
}

/**
 * UploadSettings - the processing configuration, as the API and the embedding service report it.
 *
 * #4446: every value here used to be written in this file, and several were wrong: another model
 * and dimension than the ones the service ran, a size limit five times the API's, one chunk size
 * while the chunking strategy is chosen per document. Each value now comes from its source, and a
 * value its source does not give says so instead of showing a number.
 */
export function UploadSettings() {
  const embedding = useEmbeddingServiceInfo();
  const { data: uploadLimits, isLoading: limitsLoading } = usePdfUploadLimits();

  const info = embedding.data;
  const missingLimit = limitsLoading ? LOADING : NOT_AVAILABLE;

  let model: string;
  if (embedding.isLoading) {
    model = LOADING;
  } else if (!info) {
    model = `${NOT_AVAILABLE}: the embedding service info could not be read`;
  } else {
    model = info.model ?? `${NOT_AVAILABLE}: embedding service ${info.status}`;
  }

  return (
    <div className="bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-border/50 dark:border-zinc-700/50">
      <div className="flex items-center gap-2 mb-6">
        <h2 className="font-quicksand text-xl font-bold text-foreground dark:text-zinc-100">
          Processing Settings
        </h2>
        <Badge variant="outline" className="text-xs">
          Read-only
        </Badge>
      </div>

      <div className="space-y-5">
        {/* Info Banner */}
        <div className="flex items-start gap-2 p-3 bg-amber-50/50 dark:bg-amber-900/10 border border-amber-200/50 dark:border-amber-700/30 rounded-lg">
          <InfoIcon className="w-4 h-4 text-amber-600 dark:text-amber-400 mt-0.5 shrink-0" />
          <p className="text-xs text-amber-800 dark:text-amber-300">
            These values are read from the API and the embedding service. To change one, update that
            service&apos;s configuration and restart it.
          </p>
        </div>

        {/* Embedding Model */}
        <div className="space-y-1.5">
          <div className="flex items-center gap-2">
            <CpuIcon className="w-4 h-4 text-muted-foreground" />
            <span className="text-sm font-medium text-foreground dark:text-zinc-300">
              Embedding Model
            </span>
          </div>
          <div className="ml-6 space-y-1">
            <p className="text-sm text-foreground dark:text-zinc-100 font-mono">{model}</p>
            {info && <p className="text-xs text-muted-foreground">{info.dimension} dimensions</p>}
          </div>
        </div>

        {/* Supported Languages */}
        <div className="space-y-1.5">
          <div className="flex items-center gap-2">
            <LanguagesIcon className="w-4 h-4 text-muted-foreground" />
            <span className="text-sm font-medium text-foreground dark:text-zinc-300">
              Supported Languages
            </span>
          </div>
          <div className="ml-6 flex flex-wrap gap-1.5">
            {info?.supportedLanguages.length ? (
              info.supportedLanguages.map(code => (
                <Badge key={code} variant="secondary" className="text-xs">
                  {languageName(code)}
                </Badge>
              ))
            ) : (
              <p className="text-sm text-muted-foreground">
                {embedding.isLoading ? LOADING : NOT_AVAILABLE}
              </p>
            )}
          </div>
        </div>

        {/* Processing Pipeline */}
        <div className="space-y-1.5">
          <span className="text-sm font-medium text-foreground dark:text-zinc-300">
            Pipeline Steps
          </span>
          <div className="ml-0 flex items-center gap-1 text-xs text-muted-foreground">
            <Badge variant="outline" className="text-xs">
              Upload
            </Badge>
            <span>→</span>
            <Badge variant="outline" className="text-xs">
              Extract
            </Badge>
            <span>→</span>
            <Badge variant="outline" className="text-xs">
              Chunk
            </Badge>
            <span>→</span>
            <Badge variant="outline" className="text-xs">
              Embed
            </Badge>
            <span>→</span>
            <Badge variant="outline" className="text-xs">
              Index
            </Badge>
          </div>
        </div>

        {/* Upload Limits */}
        <div className="space-y-1.5 pt-2 border-t border-border/50 dark:border-zinc-700/50">
          <span className="text-sm font-medium text-foreground dark:text-zinc-300">
            Upload Limits
          </span>
          <div className="ml-0 grid grid-cols-2 gap-2 text-sm">
            <div>
              <p className="text-xs text-muted-foreground">Max File Size</p>
              <p className="text-foreground dark:text-zinc-100">
                {uploadLimits ? formatSizeLimit(uploadLimits.maxFileSizeBytes) : missingLimit}
              </p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Chunked Upload</p>
              <p className="text-foreground dark:text-zinc-100">
                {`> ${formatSizeLimit(MAX_SINGLE_UPLOAD_SIZE)}`}
              </p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Accepted Formats</p>
              <p className="text-foreground dark:text-zinc-100">PDF</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Concurrent Sessions</p>
              <p className="text-foreground dark:text-zinc-100">
                {uploadLimits
                  ? `${uploadLimits.maxConcurrentChunkedUploads} per user`
                  : missingLimit}
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
