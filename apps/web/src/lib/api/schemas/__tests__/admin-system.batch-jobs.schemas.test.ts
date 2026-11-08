/**
 * admin-system batch jobs — BE-aligned contract tests (#3853)
 *
 * `GET /api/v1/admin/operations/batch-jobs` restituisce `BatchJobListDto`, i cui
 * elementi sono costruiti posizionalmente da `GetAllBatchJobsQueryHandler`:
 *
 *   new BatchJobDto(job.Id, job.Type.ToString(), job.Status.ToString(), job.Progress,
 *                   job.StartedAt, job.CompletedAt, job.DurationSeconds,
 *                   job.ResultSummary, job.ErrorMessage, job.CreatedAt)
 *
 * Lo schema pretendeva `parameters`, `results` e `duration` — le prime due inesistenti
 * nel contratto, la terza chiamata `durationSeconds`. Essendo `.nullable()` e non
 * `.optional()`, una chiave assente fa fallire la validazione e il client scarta una
 * risposta valida.
 *
 * Il difetto e' sopravvissuto alla correzione di #3870 (che ha aggiunto `Page`/`PageSize`
 * al CONTENITORE) perche' il solo chiamante chiede `?status=queued&pageSize=1` e legge
 * `total`: con la coda vuota `jobs` e' `[]`, e lo schema dell'elemento non viene mai
 * esercitato. Da qui la scelta di questi test: il caso che conta e' la lista NON vuota.
 */
import { describe, it, expect } from 'vitest';

import {
  BatchJobDtoSchema,
  BatchJobListSchema,
  CreateBatchJobResponseSchema,
} from '../admin/admin-system.schemas';

/**
 * Un job come il backend lo serializza davvero: solo i dieci campi del record,
 * camelCase, enum come stringa (JsonStringEnumConverter e' registrato in Program.cs),
 * DateTime UTC con suffisso Z (l'entita' assegna sempre `DateTime.UtcNow`).
 */
const backendJob = {
  id: '0f8fad5b-d9cb-469f-a165-70867728950e',
  type: 'BggSync',
  status: 'Running',
  progress: 42,
  startedAt: '2026-08-30T10:15:00Z',
  completedAt: null,
  durationSeconds: null,
  resultSummary: null,
  errorMessage: null,
  createdAt: '2026-08-30T10:00:00Z',
};

describe('BatchJobDtoSchema — allineato al record C# (#3853)', () => {
  it('accetta un job come il backend lo serializza', () => {
    const parsed = BatchJobDtoSchema.parse(backendJob);

    expect(parsed.durationSeconds).toBeNull();
    expect(parsed.resultSummary).toBeNull();
    expect(parsed.type).toBe('BggSync');
  });

  it('accetta un job completato, con durata e riepilogo valorizzati', () => {
    const parsed = BatchJobDtoSchema.parse({
      ...backendJob,
      status: 'Completed',
      progress: 100,
      completedAt: '2026-08-30T10:20:00Z',
      durationSeconds: 300,
      resultSummary: '128 giochi sincronizzati',
    });

    expect(parsed.durationSeconds).toBe(300);
    expect(parsed.resultSummary).toBe('128 giochi sincronizzati');
  });

  it('accetta VectorReembedding, il sesto valore di JobType', () => {
    // JobType ne dichiara sei; lo schema ne elencava cinque, quindi un job di questo
    // tipo faceva fallire la validazione dell'intera pagina di risultati.
    expect(() =>
      BatchJobDtoSchema.parse({ ...backendJob, type: 'VectorReembedding' })
    ).not.toThrow();
  });

  it('rifiuta i campi che il contratto non ha mai avuto', () => {
    // Guard sulla direzione opposta: se qualcuno rimettesse `duration` obbligatorio,
    // il caso valido tornerebbe a fallire. Qui il payload backend NON li porta.
    expect(backendJob).not.toHaveProperty('duration');
    expect(backendJob).not.toHaveProperty('parameters');
    expect(backendJob).not.toHaveProperty('results');
  });
});

describe('BatchJobListSchema — la lista non vuota e il caso che conta (#3853)', () => {
  it('valida una pagina con almeno un job', () => {
    const parsed = BatchJobListSchema.parse({
      jobs: [backendJob],
      total: 1,
      page: 1,
      pageSize: 20,
    });

    expect(parsed.jobs).toHaveLength(1);
    expect(parsed.jobs[0].id).toBe(backendJob.id);
  });

  it('valida anche la pagina vuota — che e il caso che nascondeva il difetto', () => {
    const parsed = BatchJobListSchema.parse({ jobs: [], total: 0, page: 1, pageSize: 1 });
    expect(parsed.jobs).toHaveLength(0);
  });
});

describe('CreateBatchJobResponseSchema — jobId, non id (#3853)', () => {
  it('accetta la risposta del backend', () => {
    // `Results.Created(..., new CreateBatchJobResponse(jobId))` serializza `jobId`.
    const parsed = CreateBatchJobResponseSchema.parse({
      jobId: '0f8fad5b-d9cb-469f-a165-70867728950e',
    });

    expect(parsed.jobId).toBe('0f8fad5b-d9cb-469f-a165-70867728950e');
  });

  it('rifiuta la forma `id`, che nessun endpoint restituisce', () => {
    expect(() =>
      CreateBatchJobResponseSchema.parse({ id: '0f8fad5b-d9cb-469f-a165-70867728950e' })
    ).toThrow();
  });
});
