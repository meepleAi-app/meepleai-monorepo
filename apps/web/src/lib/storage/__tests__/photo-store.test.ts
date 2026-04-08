import 'fake-indexeddb/auto';
import { describe, it, expect, beforeEach } from 'vitest';

import { addPhoto, listPhotos, deletePhoto, clearAllPhotos } from '../photo-store';

const sessionId = 'sess-1';

function makeBlob(): Blob {
  return new Blob(['fake-png'], { type: 'image/png' });
}

describe('photo-store (IndexedDB)', () => {
  beforeEach(async () => {
    await clearAllPhotos();
  });

  it('addPhoto persists a photo and listPhotos returns it', async () => {
    const photo = await addPhoto(sessionId, makeBlob(), 'snap.png');

    expect(photo.id).toMatch(/^photo:sess-1:/);
    expect(photo.sessionId).toBe(sessionId);
    expect(photo.filename).toBe('snap.png');
    expect(photo.timestamp).toBeGreaterThan(0);

    const list = await listPhotos(sessionId);
    expect(list).toHaveLength(1);
    expect(list[0].id).toBe(photo.id);
  });

  it('listPhotos returns photos sorted by timestamp ascending', async () => {
    const a = await addPhoto(sessionId, makeBlob(), 'a.png');
    await new Promise(r => setTimeout(r, 5));
    const b = await addPhoto(sessionId, makeBlob(), 'b.png');

    const list = await listPhotos(sessionId);
    expect(list.map(p => p.id)).toEqual([a.id, b.id]);
  });

  it('listPhotos filters by sessionId', async () => {
    await addPhoto('sess-A', makeBlob(), 'a.png');
    await addPhoto('sess-B', makeBlob(), 'b.png');

    const listA = await listPhotos('sess-A');
    const listB = await listPhotos('sess-B');

    expect(listA).toHaveLength(1);
    expect(listB).toHaveLength(1);
    expect(listA[0].id).not.toBe(listB[0].id);
  });

  it('deletePhoto removes only the target', async () => {
    const a = await addPhoto(sessionId, makeBlob(), 'a.png');
    const b = await addPhoto(sessionId, makeBlob(), 'b.png');

    await deletePhoto(a.id);

    const list = await listPhotos(sessionId);
    expect(list).toHaveLength(1);
    expect(list[0].id).toBe(b.id);
  });

  it('returns photo blob', async () => {
    const photo = await addPhoto(sessionId, makeBlob(), 'snap.png');
    expect(photo.blob).toBeInstanceOf(Blob);
    expect(photo.blob.type).toBe('image/png');
  });

  it('listPhotos returns empty array for unknown session', async () => {
    const list = await listPhotos('nonexistent');
    expect(list).toEqual([]);
  });
});
