import { type Metadata } from 'next';
import Link from 'next/link';

import {
  FileTextIcon,
  DatabaseIcon,
  UploadIcon,
  BrainCircuitIcon,
  SettingsIcon,
  ArrowRightIcon,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';

export const metadata: Metadata = {
  title: 'Knowledge Base',
  description: 'Manage documents, vector collections, and the RAG pipeline',
};

const sections = [
  {
    title: 'Documents',
    description: 'Browse uploaded documents, view processing status, and manage the document library',
    icon: FileTextIcon,
    href: '/admin/knowledge-base/documents',
    color: 'from-amber-500 to-orange-600',
    stats: 'View library',
  },
  {
    title: 'Vector Collections',
    description: 'Manage Qdrant vector collections, view embeddings health, and run similarity searches',
    icon: DatabaseIcon,
    href: '/admin/knowledge-base/vectors',
    color: 'from-blue-500 to-indigo-600',
    stats: 'Manage vectors',
  },
  {
    title: 'Upload & Process',
    description: 'Upload new documents for processing, configure extraction settings, and monitor pipeline jobs',
    icon: UploadIcon,
    href: '/admin/knowledge-base/upload',
    color: 'from-emerald-500 to-green-600',
    stats: 'Upload files',
  },
  {
    title: 'RAG Pipeline',
    description: 'Monitor the RAG retrieval pipeline, view execution logs, and tune retrieval parameters',
    icon: BrainCircuitIcon,
    href: '/rag',
    color: 'from-purple-500 to-violet-600',
    stats: 'View dashboard',
  },
  {
    title: 'Settings',
    description: 'Configure tier strategies, embedding models, chunking parameters, and reranking options',
    icon: SettingsIcon,
    href: '/admin/rag/tier-strategy-config',
    color: 'from-slate-500 to-zinc-600',
    stats: 'Configure',
  },
];

export default function KnowledgeBasePage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Knowledge Base
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Manage documents, vector collections, and the RAG retrieval pipeline
        </p>
      </div>

      {/* Section Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {sections.map((section) => {
          const Icon = section.icon;
          return (
            <Link key={section.title} href={section.href}>
              <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60 hover:border-amber-300/60 dark:hover:border-amber-600/40 transition-all cursor-pointer group h-full">
                <CardHeader>
                  <CardTitle className="flex items-center gap-3 text-base">
                    <div
                      className={`h-10 w-10 rounded-xl bg-gradient-to-br ${section.color} flex items-center justify-center shadow-sm`}
                    >
                      <Icon className="h-5 w-5 text-white" />
                    </div>
                    <span>{section.title}</span>
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-3">
                  <p className="text-sm text-muted-foreground leading-relaxed">
                    {section.description}
                  </p>
                  <div className="flex items-center gap-1 text-sm font-medium text-amber-700 dark:text-amber-400 group-hover:gap-2 transition-all">
                    {section.stats}
                    <ArrowRightIcon className="h-3 w-3" />
                  </div>
                </CardContent>
              </Card>
            </Link>
          );
        })}
      </div>

      {/* Quick Links */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-6">
        <h2 className="font-quicksand font-semibold text-lg text-foreground mb-4">
          Quick Links
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
          <Link
            href="/admin/rag-executions"
            className="text-sm text-muted-foreground hover:text-foreground transition-colors px-3 py-2 rounded-lg hover:bg-slate-100/80 dark:hover:bg-zinc-700/60"
          >
            RAG Executions Log
          </Link>
          <Link
            href="/admin/ai-models"
            className="text-sm text-muted-foreground hover:text-foreground transition-colors px-3 py-2 rounded-lg hover:bg-slate-100/80 dark:hover:bg-zinc-700/60"
          >
            AI Models
          </Link>
          <Link
            href="/admin/prompts"
            className="text-sm text-muted-foreground hover:text-foreground transition-colors px-3 py-2 rounded-lg hover:bg-slate-100/80 dark:hover:bg-zinc-700/60"
          >
            Prompts Library
          </Link>
          <Link
            href="/admin/configuration"
            className="text-sm text-muted-foreground hover:text-foreground transition-colors px-3 py-2 rounded-lg hover:bg-slate-100/80 dark:hover:bg-zinc-700/60"
          >
            General Configuration
          </Link>
        </div>
      </div>
    </div>
  );
}
