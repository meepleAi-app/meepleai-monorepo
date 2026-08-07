/**
 * Cover R2-strict E2E (issue #3498).
 *
 * Sostituisce il fixme mockato di `cover-l4.spec.ts` (`src.match(/\.webp/)`, un falso-verde) con
 * un'asserzione di CARICAMENTO REALE contro backend + MinIO veri (STORAGE_PROVIDER=s3, host di
 * presign pubblico localhost:9000 via #3535). Prova l'invariante «la cover è servita da R2/MinIO,
 * mai dall'host di input» E che l'immagine si carichi davvero.
 *
 * Perché l'assert sulla forma dell'URL non basta (la false-green trap dell'issue): MinIO presigna
 * verso il proprio endpoint, quindi un host sbagliato dà 404 ma un match `/\.webp/` passa lo
 * stesso. Qui si verificano tre cose indipendenti: la risposta HTTP è 200, il browser ha decodificato
 * pixel (`naturalWidth > 0`), e l'host è quello di MinIO.
 *
 * Gira SOLO nel job dedicato e2e-cover-r2-strict (backend reale + `--profile storage`).
 *
 * Fixture: `tests/fixtures/cover-r2-strict.sql` (gioco + entry di libreria) e l'oggetto WebP
 * caricato dal workflow a `covers/{gameId}/cover.webp` nel bucket meepleai-uploads.
 */
import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';

/** Titolo del gioco seeded da tests/fixtures/cover-r2-strict.sql. */
const SEEDED_GAME_TITLE = process.env.E2E_COVER_GAME_TITLE ?? 'Catan R2 Strict';
const MINIO_PRESIGN_HOST = 'localhost:9000';

test.describe('cover R2-strict (real backend + MinIO)', () => {
  test('the library cover loads from MinIO and never from the input host', async ({
    page,
    request,
  }) => {
    // /library è una rotta autenticata: senza sessione il proxy redirige a /login e il test
    // fallirebbe con "gioco non trovato" invece che sull'invariante che deve verificare.
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);

    // Registra le risposte servite da MinIO PRIMA di navigare: serve a distinguere «l'immagine non
    // si è caricata» da «si è caricata ma da un host sbagliato».
    const minioResponses: { url: string; status: number }[] = [];
    page.on('response', res => {
      const url = res.url();
      if (url.includes(MINIO_PRESIGN_HOST)) {
        minioResponses.push({ url, status: res.status() });
      }
    });

    await page.goto('/library', { waitUntil: 'domcontentloaded' });

    // Selettore strutturale, non un xpath `ancestor::` — quello prendeva il primo div antenato del
    // testo, che può essere il wrapper del solo titolo e non contenere la cover.
    const card = page
      .locator('[data-slot="library-grid-card"]')
      .filter({ hasText: SEEDED_GAME_TITLE })
      .first();
    await expect(card).toBeVisible({ timeout: 30_000 });

    // Diagnostica prima dell'assert: quando `imageUrl` non arriva, MeepleCard rende un
    // <div data-slot="cover-emoji-band"> al posto di <img> (parts/Cover.tsx). Senza questo
    // controllo il test fallirebbe con «img non visibile» — il sintomo — nascondendo la causa
    // vera, che è a monte: la cover non è stata risolta dal backend.
    const placeholder = card.locator('[data-slot="cover-emoji-band"]');
    if ((await placeholder.count()) > 0) {
      throw new Error(
        'La card mostra il placeholder emoji invece della cover: il backend non ha risolto una ' +
          'coverUrl. Cause tipiche: oggetto MinIO assente alla chiave presignata (il resolver fa ' +
          'HEAD prima di firmare e cade sul placeholder), credenziali di presign errate, o chiave ' +
          'seeded col suffisso sbagliato. Vedi lo step "Verify the API resolves the cover from MinIO".'
      );
    }

    const img = card.locator('img').first();
    await expect(img).toBeVisible({ timeout: 10_000 });

    // 1. Caricamento reale: il browser ha decodificato un'immagine non vuota, il che è vero solo se
    //    l'URL presigned era davvero raggiungibile e ha servito byte.
    await expect
      .poll(async () => img.evaluate((el: HTMLImageElement) => el.naturalWidth), {
        timeout: 15_000,
      })
      .toBeGreaterThan(0);

    // 2. L'host risolto è MinIO (l'host R2/allow-list), mai quello di input.
    const src = await img.getAttribute('src');
    expect(src).toBeTruthy();
    expect(new URL(src!).host).toBe(MINIO_PRESIGN_HOST);

    // 3. MinIO ha risposto 200 almeno una volta: un 403 (presign firmato con credenziali vuote) o
    //    un 404 (oggetto alla chiave sbagliata) sarebbero altrimenti invisibili se il browser
    //    mostrasse un'immagine di fallback.
    expect(minioResponses.length).toBeGreaterThan(0);
    expect(minioResponses.every(r => r.status === 200)).toBe(true);
  });
});
