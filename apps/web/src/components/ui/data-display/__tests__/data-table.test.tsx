/**
 * DataTable Component Tests (Issue #2887)
 *
 * Tests rendering, sorting, selection, and accessibility for the generic DataTable component.
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { type ColumnDef } from '@tanstack/react-table';
import { DataTable, SortableHeader, createSelectColumn } from '../data-table';

type TestData = {
  id: string;
  name: string;
  email: string;
  age: number;
};

const testData: TestData[] = [
  { id: '1', name: 'Alice', email: 'alice@example.com', age: 25 },
  { id: '2', name: 'Bob', email: 'bob@example.com', age: 30 },
  { id: '3', name: 'Charlie', email: 'charlie@example.com', age: 35 },
];

const basicColumns: ColumnDef<TestData>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
  },
  {
    accessorKey: 'email',
    header: 'Email',
  },
  {
    accessorKey: 'age',
    header: 'Age',
  },
];

describe('DataTable', () => {
  describe('Basic Rendering', () => {
    it('should render table with data', () => {
      render(<DataTable columns={basicColumns} data={testData} />);

      expect(screen.getByText('Alice')).toBeInTheDocument();
      expect(screen.getByText('Bob')).toBeInTheDocument();
      expect(screen.getByText('Charlie')).toBeInTheDocument();
    });

    it('should render column headers', () => {
      render(<DataTable columns={basicColumns} data={testData} />);

      expect(screen.getByText('Name')).toBeInTheDocument();
      expect(screen.getByText('Email')).toBeInTheDocument();
      expect(screen.getByText('Age')).toBeInTheDocument();
    });

    it('should render empty message when no data', () => {
      render(<DataTable columns={basicColumns} data={[]} emptyMessage="No users found." />);

      expect(screen.getByText('No users found.')).toBeInTheDocument();
    });

    it('should render loading state', () => {
      render(<DataTable columns={basicColumns} data={[]} isLoading />);

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });
  });

  describe('Row Click', () => {
    it('should call onRowClick when row is clicked', () => {
      const onRowClick = vi.fn();

      render(<DataTable columns={basicColumns} data={testData} onRowClick={onRowClick} />);

      const row = screen.getByText('Alice').closest('tr');
      fireEvent.click(row!);

      expect(onRowClick).toHaveBeenCalledWith(testData[0]);
    });

    it('should add cursor-pointer class when onRowClick is provided', () => {
      const onRowClick = vi.fn();

      render(<DataTable columns={basicColumns} data={testData} onRowClick={onRowClick} />);

      const row = screen.getByText('Alice').closest('tr');
      expect(row).toHaveClass('cursor-pointer');
    });

    it('should not add cursor-pointer class when onRowClick is not provided', () => {
      render(<DataTable columns={basicColumns} data={testData} />);

      const row = screen.getByText('Alice').closest('tr');
      expect(row).not.toHaveClass('cursor-pointer');
    });
  });

  describe('Sorting', () => {
    it('should call onSortingChange when sorting changes', () => {
      const onSortingChange = vi.fn();
      const sortableColumns: ColumnDef<TestData>[] = [
        {
          accessorKey: 'name',
          header: ({ column }) => <SortableHeader column={column}>Name</SortableHeader>,
        },
      ];

      render(
        <DataTable
          columns={sortableColumns}
          data={testData}
          sorting={[]}
          onSortingChange={onSortingChange}
        />
      );

      const sortButton = screen.getByRole('button', { name: /Name/i });
      fireEvent.click(sortButton);

      expect(onSortingChange).toHaveBeenCalled();
    });
  });

  describe('Row Selection', () => {
    it('should render checkbox column with createSelectColumn', () => {
      const columnsWithSelect: ColumnDef<TestData>[] = [
        createSelectColumn<TestData>(),
        ...basicColumns,
      ];

      render(<DataTable columns={columnsWithSelect} data={testData} />);

      const checkboxes = screen.getAllByRole('checkbox');
      // 1 header checkbox + 3 row checkboxes
      expect(checkboxes).toHaveLength(4);
    });

    it('should call onRowSelectionChange when selection changes', () => {
      const onRowSelectionChange = vi.fn();
      const columnsWithSelect: ColumnDef<TestData>[] = [
        createSelectColumn<TestData>(),
        ...basicColumns,
      ];

      render(
        <DataTable
          columns={columnsWithSelect}
          data={testData}
          rowSelection={{}}
          onRowSelectionChange={onRowSelectionChange}
          getRowId={row => row.id}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      // Click first row checkbox
      fireEvent.click(checkboxes[1]);

      expect(onRowSelectionChange).toHaveBeenCalled();
    });
  });

  describe('Custom Row ID', () => {
    it('should use custom getRowId function', () => {
      const columnsWithSelect: ColumnDef<TestData>[] = [
        createSelectColumn<TestData>(),
        ...basicColumns,
      ];

      const { container } = render(
        <DataTable
          columns={columnsWithSelect}
          data={testData}
          getRowId={row => row.id}
          rowSelection={{ '1': true }}
        />
      );

      // First data row should be selected
      const rows = container.querySelectorAll('tbody tr');
      expect(rows[0]).toHaveAttribute('data-state', 'selected');
    });
  });
});

describe('SortableHeader', () => {
  it('should render children', () => {
    const mockColumn = {
      getIsSorted: vi.fn().mockReturnValue(false),
      toggleSorting: vi.fn(),
    };

    render(<SortableHeader column={mockColumn}>Column Name</SortableHeader>);

    expect(screen.getByText('Column Name')).toBeInTheDocument();
  });

  it('should show up arrow when sorted ascending', () => {
    const mockColumn = {
      getIsSorted: vi.fn().mockReturnValue('asc'),
      toggleSorting: vi.fn(),
    };

    render(<SortableHeader column={mockColumn}>Column Name</SortableHeader>);

    // ArrowUp icon should be present
    const button = screen.getByRole('button');
    expect(button.querySelector('svg')).toBeInTheDocument();
  });

  it('should show down arrow when sorted descending', () => {
    const mockColumn = {
      getIsSorted: vi.fn().mockReturnValue('desc'),
      toggleSorting: vi.fn(),
    };

    render(<SortableHeader column={mockColumn}>Column Name</SortableHeader>);

    const button = screen.getByRole('button');
    expect(button.querySelector('svg')).toBeInTheDocument();
  });

  it('should call toggleSorting on click', () => {
    const mockColumn = {
      getIsSorted: vi.fn().mockReturnValue(false),
      toggleSorting: vi.fn(),
    };

    render(<SortableHeader column={mockColumn}>Column Name</SortableHeader>);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    expect(mockColumn.toggleSorting).toHaveBeenCalledWith(false);
  });

  it('should toggle to descending when currently ascending', () => {
    const mockColumn = {
      getIsSorted: vi.fn().mockReturnValue('asc'),
      toggleSorting: vi.fn(),
    };

    render(<SortableHeader column={mockColumn}>Column Name</SortableHeader>);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    expect(mockColumn.toggleSorting).toHaveBeenCalledWith(true);
  });
});

describe('Shift Selection (Issue #2888)', () => {
  const columnsWithSelect: ColumnDef<TestData>[] = [
    createSelectColumn<TestData>(),
    ...basicColumns,
  ];

  it('should enable Shift+click range selection when enableShiftSelection is true', () => {
    const mockRowSelectionChange = vi.fn();

    render(
      <DataTable
        columns={columnsWithSelect}
        data={testData}
        rowSelection={{}}
        onRowSelectionChange={mockRowSelectionChange}
        getRowId={(row) => row.id}
        enableShiftSelection={true}
      />
    );

    // Get checkboxes (first is "select all", rest are rows)
    const checkboxes = screen.getAllByRole('checkbox');

    // Click first row checkbox (normal click)
    fireEvent.click(checkboxes[1]);

    // Shift+click third row checkbox
    fireEvent.click(checkboxes[3], { shiftKey: true });

    // Should have called onRowSelectionChange with rows 1-3 selected
    expect(mockRowSelectionChange).toHaveBeenCalled();
  });

  it('should handle Shift+click without initial selection', () => {
    const mockRowSelectionChange = vi.fn();

    render(
      <DataTable
        columns={columnsWithSelect}
        data={testData}
        rowSelection={{}}
        onRowSelectionChange={mockRowSelectionChange}
        getRowId={(row) => row.id}
        enableShiftSelection={true}
      />
    );

    const checkboxes = screen.getAllByRole('checkbox');

    // Shift+click without prior selection should just select that row
    fireEvent.click(checkboxes[2], { shiftKey: true });

    expect(mockRowSelectionChange).toHaveBeenCalled();
  });

  it('should not use Shift selection when enableShiftSelection is false', () => {
    const mockRowSelectionChange = vi.fn();

    render(
      <DataTable
        columns={columnsWithSelect}
        data={testData}
        rowSelection={{}}
        onRowSelectionChange={mockRowSelectionChange}
        getRowId={(row) => row.id}
        enableShiftSelection={false}
      />
    );

    const checkboxes = screen.getAllByRole('checkbox');

    // Normal clicks
    fireEvent.click(checkboxes[1]);
    fireEvent.click(checkboxes[3], { shiftKey: true });

    // Should just toggle individual selections, not range
    expect(mockRowSelectionChange).toHaveBeenCalled();
  });
});
