/**
 * data-table.test.tsx — rete di sicurezza per la migrazione react-table 8 -> 9.
 *
 * `data-table.tsx` e' il wrapper condiviso (lo usano entity-table-view,
 * entity-list-view, ResourcesTab, BuilderTable) e fino a #3893 non aveva un
 * test proprio: era raggiunto solo per transitivita' da due dei quattro
 * call-site. Migrare la API dei generics guardando solo `tsc` significherebbe
 * fidarsi di un typecheck per un cambio di comportamento.
 *
 * Questi test asseriscono sul DOM renderizzato — ordinamento, visibilita'
 * colonne, selezione, stati vuoti — e devono restare verdi PRIMA e DOPO la
 * migrazione. `@tanstack/react-table` non e' mockato da nessuna parte, quindi
 * qui gira la libreria vera.
 *
 * Il caso sulla checkbox "seleziona tutto" e' quello che il typecheck non
 * vedrebbe: in v9 `getIsSomePageRowsSelected()` significa "almeno una, anche
 * quando sono tutte", mentre in v8 escludeva il caso "tutte".
 *
 * Refs: https://github.com/meepleAi-app/meepleai-monorepo/issues/3893
 */

import { describe, it, expect, vi } from 'vitest';

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { DataTable, SortableHeader, createSelectColumn, type ColumnDef } from '../data-table';

interface Riga {
  id: string;
  nome: string;
  punti: number;
}

const righe: Riga[] = [
  { id: 'b', nome: 'Brass', punti: 20 },
  { id: 'a', nome: 'Ark Nova', punti: 30 },
  { id: 'c', nome: 'Catan', punti: 10 },
];

const colonne: ColumnDef<Riga, unknown>[] = [
  { accessorKey: 'nome', header: 'Nome' },
  { accessorKey: 'punti', header: 'Punti' },
];

const colonneOrdinabili: ColumnDef<Riga, unknown>[] = [
  {
    accessorKey: 'nome',
    header: ({ column }) => <SortableHeader column={column}>Nome</SortableHeader>,
  },
  { accessorKey: 'punti', header: 'Punti' },
];

/** Testi della prima cella di ogni riga del corpo, nell'ordine renderizzato. */
function nomiRenderizzati(): string[] {
  const righeDom = screen.getAllByRole('row').slice(1); // salta l'header
  return righeDom.map(r => within(r).getAllByRole('cell')[0].textContent?.trim() ?? '');
}

describe('DataTable (#3893)', () => {
  it('renderizza intestazioni e righe', () => {
    render(<DataTable columns={colonne} data={righe} />);

    expect(screen.getByRole('columnheader', { name: 'Nome' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Punti' })).toBeInTheDocument();
    expect(nomiRenderizzati()).toEqual(['Brass', 'Ark Nova', 'Catan']);
  });

  it('mostra lo stato di caricamento invece delle righe', () => {
    render(<DataTable columns={colonne} data={righe} isLoading />);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
    expect(screen.queryByText('Brass')).not.toBeInTheDocument();
  });

  it('mostra il messaggio di vuoto quando non ci sono dati', () => {
    render(<DataTable columns={colonne} data={[]} emptyMessage="Nessun gioco" />);

    expect(screen.getByText('Nessun gioco')).toBeInTheDocument();
  });

  it('ordina le righe al click sull intestazione ordinabile', async () => {
    const user = userEvent.setup();
    render(<DataTable columns={colonneOrdinabili} data={righe} />);

    expect(nomiRenderizzati()).toEqual(['Brass', 'Ark Nova', 'Catan']);

    await user.click(screen.getByRole('button', { name: /Nome/ }));
    expect(nomiRenderizzati()).toEqual(['Ark Nova', 'Brass', 'Catan']);

    await user.click(screen.getByRole('button', { name: /Nome/ }));
    expect(nomiRenderizzati()).toEqual(['Catan', 'Brass', 'Ark Nova']);
  });

  it('nasconde le colonne marcate non visibili', () => {
    render(<DataTable columns={colonne} data={righe} columnVisibility={{ punti: false }} />);

    expect(screen.getByRole('columnheader', { name: 'Nome' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Punti' })).not.toBeInTheDocument();
    expect(screen.queryByText('20')).not.toBeInTheDocument();
  });

  it('propaga il click sulla riga con l oggetto originale', async () => {
    const user = userEvent.setup();
    const onRowClick = vi.fn();
    render(<DataTable columns={colonne} data={righe} onRowClick={onRowClick} />);

    await user.click(screen.getByText('Catan'));

    expect(onRowClick).toHaveBeenCalledTimes(1);
    expect(onRowClick).toHaveBeenCalledWith(righe[2]);
  });

  describe('selezione', () => {
    const conSelezione: ColumnDef<Riga, unknown>[] = [
      createSelectColumn<Riga>() as ColumnDef<Riga, unknown>,
      ...colonne,
    ];

    it('seleziona una riga e riflette lo stato sulla checkbox', async () => {
      const user = userEvent.setup();
      render(<DataTable columns={conSelezione} data={righe} getRowId={r => r.id} />);

      const checkboxRighe = screen.getAllByLabelText('Select row');
      expect(checkboxRighe[0]).toHaveAttribute('data-state', 'unchecked');

      await user.click(checkboxRighe[0]);
      expect(screen.getAllByLabelText('Select row')[0]).toHaveAttribute('data-state', 'checked');
    });

    // Il caso che il typecheck non vede: in v9 getIsSomePageRowsSelected()
    // include "tutte selezionate", quindi l'ordine dei rami nel `||` di
    // createSelectColumn e' load-bearing.
    it('la checkbox globale passa da vuota a indeterminata a piena', async () => {
      const user = userEvent.setup();
      render(<DataTable columns={conSelezione} data={righe} getRowId={r => r.id} />);

      const tutte = () => screen.getByLabelText('Select all');
      expect(tutte()).toHaveAttribute('data-state', 'unchecked');

      await user.click(screen.getAllByLabelText('Select row')[0]);
      expect(tutte()).toHaveAttribute('data-state', 'indeterminate');

      await user.click(screen.getAllByLabelText('Select row')[1]);
      await user.click(screen.getAllByLabelText('Select row')[2]);
      expect(tutte()).toHaveAttribute('data-state', 'checked');
    });

    it('la checkbox globale seleziona e deseleziona tutte le righe', async () => {
      const user = userEvent.setup();
      render(<DataTable columns={conSelezione} data={righe} getRowId={r => r.id} />);

      await user.click(screen.getByLabelText('Select all'));
      for (const c of screen.getAllByLabelText('Select row')) {
        expect(c).toHaveAttribute('data-state', 'checked');
      }

      await user.click(screen.getByLabelText('Select all'));
      for (const c of screen.getAllByLabelText('Select row')) {
        expect(c).toHaveAttribute('data-state', 'unchecked');
      }
    });
  });
});
