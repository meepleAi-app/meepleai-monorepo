/**
 * Unit test per la mappa dei contesti e il generatore di inventario.
 *
 * L'inventario è il tracker dell'audit: ogni riga è un'unità di copertura, e
 * l'ordinamento deve essere deterministico perché il diff del CSV resti leggibile.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { describe, expect, it } from 'vitest';

import { buildInventory, toCsv } from '../build-inventory';
import { contextForEndpoint, contextForRoute } from '../context-map';
import type { EndpointEntry, RouteEntry } from '../types';

const route = (route: string): RouteEntry => ({
  route,
  group: 'x',
  dynamicSegments: [],
  file: 'f',
});
const endpoint = (over: Partial<EndpointEntry> = {}): EndpointEntry => ({
  method: 'GET',
  path: '/api/v1/games',
  auth: 'anonymous',
  tags: [],
  file: 'Routing/GameEndpoints.cs',
  line: 1,
  ...over,
});

describe('contextForRoute', () => {
  it('mappa i prefissi noti sui bounded context', () => {
    expect(contextForRoute('/library')).toBe('UserLibrary');
    expect(contextForRoute('/chat/[threadId]')).toBe('KnowledgeBase');
    expect(contextForRoute('/shared-games')).toBe('SharedGameCatalog');
    expect(contextForRoute('/notifications')).toBe('UserNotifications');
  });

  it('preferisce il prefisso più lungo quando due combaciano', () => {
    // /library → UserLibrary, ma la sezione KB di un gioco è DocumentProcessing.
    expect(contextForRoute('/library/[gameId]/kb')).toBe('DocumentProcessing');
  });

  it('distingue le sezioni admin fra loro invece di appiattirle su Administration', () => {
    expect(contextForRoute('/admin/users')).toBe('Administration');
    expect(contextForRoute('/admin/rag-quality')).toBe('KbQuality');
    expect(contextForRoute('/admin/database-sync')).toBe('DatabaseSync');
    expect(contextForRoute('/admin/shared-games')).toBe('SharedGameCatalog');
  });

  it('usa Unmapped per i prefissi sconosciuti, invece di indovinare', () => {
    expect(contextForRoute('/qualcosa-di-nuovo')).toBe('Unmapped');
  });

  // Administration era un contenitore di scarto: tutto ciò che sta sotto /admin e
  // non ha una regola propria vi finiva per fallback, gonfiandola a 520 righe e
  // lasciando a zero i contesti a cui quelle righe appartengono davvero.
  it.each([
    ['/admin/entity-links', 'EntityRelationships'],
    ['/admin/audit-log', 'SecurityAudit'],
    ['/admin/kb', 'KnowledgeBase'],
    ['/admin/mechanic-extractor', 'KnowledgeBase'],
    ['/admin/prompts', 'KnowledgeBase'],
    ['/admin/rag-executions', 'KbQuality'],
    ['/admin/pdfs', 'DocumentProcessing'],
    ['/admin/bgg-queue', 'SharedGameCatalog'],
    ['/admin/wikidata', 'SharedGameCatalog'],
    ['/admin/feature-flags', 'SystemConfiguration'],
    ['/admin/alert-rules', 'SystemConfiguration'],
    ['/admin/financial-ledger', 'BusinessSimulations'],
    ['/admin/email-templates', 'UserNotifications'],
  ])('attribuisce %s al contesto %s, non ad Administration', (route, expected) => {
    expect(contextForRoute(route)).toBe(expected);
  });

  it('lascia ad Administration ciò che è davvero amministrativo', () => {
    expect(contextForRoute('/admin/users')).toBe('Administration');
    expect(contextForRoute('/admin/monitor/logs')).toBe('Administration');
    expect(contextForRoute('/admin/impersonation')).toBe('Administration');
  });
});

describe('contextForEndpoint', () => {
  it('preferisce la cartella del file di routing quando presente', () => {
    expect(contextForEndpoint(endpoint({ file: 'Routing/SessionTracking/X.cs' }))).toBe(
      'SessionTracking'
    );
  });

  it('ricade sul path quando il file è nella root di Routing', () => {
    expect(contextForEndpoint(endpoint({ file: 'Routing/GameEndpoints.cs' }))).toBe(
      'GameManagement'
    );
  });
});

describe('buildInventory', () => {
  it('genera due righe per rotta non-admin e una per rotta admin', () => {
    const rows = buildInventory([route('/library'), route('/admin/users')], []);
    expect(rows.filter(r => r.path === '/library').map(r => r.ruolo)).toEqual(['admin', 'user']);
    expect(rows.filter(r => r.path === '/admin/users').map(r => r.ruolo)).toEqual(['admin']);
  });

  it('assegna L2 alle mutazioni e L1 alle letture', () => {
    const rows = buildInventory([], [endpoint({ method: 'POST' }), endpoint({ method: 'GET' })]);
    expect(rows.map(r => r.livello).sort()).toEqual(['L1', 'L2']);
  });

  it('parte da stato non coperto', () => {
    expect(buildInventory([route('/library')], [])[0].stato).toBe('⬜ non coperto');
  });

  it('annota gli endpoint la cui autorizzazione non è stata dedotta', () => {
    const rows = buildInventory([], [endpoint({ auth: 'unknown' })]);
    expect(rows[0].note).toContain('auth non dedotta');
  });

  it('produce id stabili e univoci', () => {
    const rows = buildInventory([route('/library'), route('/games')], [endpoint()]);
    expect(new Set(rows.map(r => r.id)).size).toBe(rows.length);
    expect(buildInventory([route('/library')], [])[0].id).toBe(
      buildInventory([route('/library')], [])[0].id
    );
  });
});

describe('toCsv', () => {
  it('protegge le virgole nelle note', () => {
    const rows = buildInventory([route('/library')], []);
    rows[0].note = 'nota, con virgola';
    expect(toCsv(rows)).toContain('"nota, con virgola"');
  });

  it("emette l'intestazione attesa", () => {
    expect(toCsv([]).trim()).toBe('id,tipo,path,metodo,contesto,ruolo,livello,stato,evidenza,note');
  });
});
