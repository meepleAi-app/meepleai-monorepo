'use client';

import { useState } from 'react';

export default function ScraperPage() {
  const [action, setAction] = useState('plays');
  const [gameId, setGameId] = useState('13');
  const [gameName, setGameName] = useState<string | null>(null);
  const [mindate, setMindate] = useState('2024-01-01');
  const [rulebook, setRulebook] = useState('rulebooks/catan.pdf');
  const [max, setMax] = useState('5');
  const [status, setStatus] = useState<string | null>(null);
  const [log, setLog] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [results, setResults] = useState<{ id: number; name: string; year?: number }[]>([]);

  const submit = async () => {
    setLoading(true);
    setStatus('Running...');
    setLog(null);
    try {
      const res = await fetch('/api/scraper', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          action,
          gameId: Number(gameId),
          mindate,
          rulebook,
          max: Number(max),
        }),
      });
      const json = await res.json();
      if (!res.ok || json.ok === false) {
        setStatus(`Error: ${json.error ?? res.statusText}`);
        setLog(json.stderr ?? '');
      } else {
        setStatus('Success');
        setLog((json.stdout ?? '') + (json.stderr ? '\n' + json.stderr : ''));
      }
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error occurred';
      setStatus(`Error: ${errorMessage}`);
    } finally {
      setLoading(false);
    }
  };

  const search = async () => {
    if (!searchTerm.trim()) return;
    setStatus('Searching...');
    try {
      const res = await fetch(`/api/bgg/search?q=${encodeURIComponent(searchTerm)}`);
      const json = await res.json();
      if (!res.ok) {
        setStatus(`Error: ${json.error ?? res.statusText}`);
        return;
      }
      setResults(json.items ?? []);
      setStatus(`Found ${json.items?.length ?? 0} results`);
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error occurred';
      setStatus(`Error: ${errorMessage}`);
    }
  };

  return (
    <div className="mx-auto max-w-3xl p-6 space-y-4">
      <h1 className="text-2xl font-semibold">Game Scraper</h1>
      <div className="rounded border p-3 space-y-2">
        <div className="text-sm font-medium">Cerca su BGG</div>
        <div className="flex gap-2">
          <input
            className="border rounded p-2 flex-1"
            placeholder="es. Catan"
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
          />
          <button
            className="bg-gray-200 px-3 rounded"
            onClick={search}
            disabled={!searchTerm.trim()}
          >
            Cerca
          </button>
        </div>
        {results.length > 0 && (
          <div className="max-h-48 overflow-auto space-y-1 text-sm">
            {results.map(r => (
              <button
                key={r.id}
                className={`w-full text-left border rounded p-2 hover:bg-gray-50 ${
                  Number(gameId) === r.id ? 'border-blue-500' : 'border-gray-200'
                }`}
                onClick={() => {
                  setGameId(String(r.id));
                  setGameName(r.name);
                }}
              >
                <div className="font-medium">{r.name}</div>
                <div className="text-xs text-gray-600">
                  ID {r.id}
                  {r.year ? ` • ${r.year}` : ''}
                </div>
              </button>
            ))}
          </div>
        )}
      </div>
      <div className="grid grid-cols-1 gap-3">
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium">Action</span>
          <select
            className="border rounded p-2"
            value={action}
            onChange={e => setAction(e.target.value)}
          >
            <option value="game">Fetch game metadata</option>
            <option value="plays">Fetch plays</option>
            <option value="qa">Generate QA from rulebook</option>
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium">Game ID (BGG)</span>
          <input
            className="border rounded p-2"
            type="number"
            value={gameId}
            onChange={e => setGameId(e.target.value)}
          />
          {gameName && <span className="text-xs text-gray-600">Selezionato: {gameName}</span>}
        </label>
        {action === 'plays' && (
          <label className="flex flex-col gap-1">
            <span className="text-sm font-medium">Min date (YYYY-MM-DD)</span>
            <input
              className="border rounded p-2"
              type="text"
              value={mindate}
              onChange={e => setMindate(e.target.value)}
            />
          </label>
        )}
        {action === 'qa' && (
          <>
            <label className="flex flex-col gap-1">
              <span className="text-sm font-medium">Rulebook path</span>
              <input
                className="border rounded p-2"
                type="text"
                value={rulebook}
                onChange={e => setRulebook(e.target.value)}
              />
            </label>
            <label className="flex flex-col gap-1">
              <span className="text-sm font-medium">Max QA</span>
              <input
                className="border rounded p-2"
                type="number"
                min={1}
                value={max}
                onChange={e => setMax(e.target.value)}
              />
            </label>
          </>
        )}
      </div>
      <button
        className="bg-blue-600 text-white px-4 py-2 rounded disabled:opacity-60"
        onClick={submit}
        disabled={loading}
      >
        {loading ? 'Running...' : 'Run scraper'}
      </button>
      {status && <div className="text-sm font-medium">{status}</div>}
      {log && (
        <pre className="bg-gray-900 text-gray-100 text-xs p-3 rounded max-h-96 overflow-auto whitespace-pre-wrap">
          {log}
        </pre>
      )}
    </div>
  );
}
