import { useEffect, useState, type ReactNode } from 'react';
import { api } from '../lib/api';
import { Pagination } from '../components/CoreComponents';
import type { PagedResult } from '../types/api';

export type AdminColumn<T> = {
  key: string;
  label: string;
  sortable?: boolean;
  render?: (item: T) => ReactNode;
};

type AdminDataTableProps<T> = {
  columns: AdminColumn<T>[];
  endpoint: string;
  initialSort?: string;
  initialOrder?: 'asc' | 'desc';
  searchPlaceholder?: string;
  /** Ẩn ô search `q` — dùng khi trang tự lọc bằng extraParams (vd. audit logs). */
  hideSearch?: boolean;
  /** Query params bổ sung (actorId, action, from, to, …); giá trị rỗng/undefined bị bỏ. */
  extraParams?: Record<string, string | number | undefined | null>;
  rowActions?: (item: T) => ReactNode;
  /** Increment to force the table to refetch after a mutation. */
  refreshKey?: number;
};

export function AdminDataTable<T extends { id: number }>({
  columns,
  endpoint,
  initialSort = 'id',
  initialOrder = 'desc',
  searchPlaceholder = 'Search…',
  hideSearch = false,
  extraParams,
  rowActions,
  refreshKey = 0,
}: AdminDataTableProps<T>) {
  const [query, setQuery] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [sort, setSort] = useState(initialSort);
  const [order, setOrder] = useState<'asc' | 'desc'>(initialOrder);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const requestKey = JSON.stringify([endpoint, page, pageSize, query, sort, order, refreshKey, extraParams]);
  const [request, setRequest] = useState<{
    key: string;
    result: PagedResult<T> | null;
    error: string;
  } | null>(null);
  const loading = request?.key !== requestKey;
  const result = request?.key === requestKey ? request.result : null;
  const error = request?.key === requestKey ? request.error : '';

  useEffect(() => {
    const controller = new AbortController();
    const params: Record<string, string | number> = {
      page,
      pageSize,
      order,
    };
    if (!hideSearch && query) params.q = query;
    if (sort) params.sort = sort;
    if (extraParams) {
      for (const [key, value] of Object.entries(extraParams)) {
        if (value === undefined || value === null || value === '') continue;
        params[key] = value;
      }
    }
    api
      .get<PagedResult<T>>(endpoint, { params, signal: controller.signal })
      .then(({ data }) => setRequest({ key: requestKey, result: data, error: '' }))
      .catch((err) => {
        if (!controller.signal.aborted) {
          setRequest({
            key: requestKey,
            result: null,
            error: typeof err === 'object' && err !== null && 'response' in err
              ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'Failed to load.')
              : 'Failed to load.',
          });
        }
      });
    return () => controller.abort();
  }, [endpoint, page, pageSize, query, sort, order, refreshKey, requestKey, hideSearch, extraParams]);

  const toggleSort = (col: AdminColumn<T>) => {
    if (!col.sortable) return;
    if (sort === col.key) {
      setOrder((prev) => (prev === 'asc' ? 'desc' : 'asc'));
    } else {
      setSort(col.key);
      setOrder('asc');
    }
    setPage(1);
  };

  return (
    <div className="admin-data-table">
      <div className="admin-table-controls">
        {hideSearch ? <div /> : (
          <form
            className="search-bar"
            role="search"
            onSubmit={(event) => {
              event.preventDefault();
              setQuery(searchInput.trim());
              setPage(1);
            }}
          >
            <label className="visually-hidden" htmlFor="admin-search">Search</label>
            <input
              id="admin-search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder={searchPlaceholder}
            />
            <button type="submit">Search</button>
          </form>
        )}
        <label className="control-field">
          <span>Show</span>
          <select
            value={pageSize}
            onChange={(event) => {
              setPageSize(Number(event.target.value));
              setPage(1);
            }}
          >
            <option value={10}>10</option>
            <option value={20}>20</option>
            <option value={50}>50</option>
          </select>
        </label>
      </div>

      {loading ? <p role="status">Loading…</p> : null}
      {error ? <p className="field-error" role="alert">{error}</p> : null}

      {!loading && result ? (
        <>
          <table className="admin-table">
            <thead>
              <tr>
                {columns.map((col) => (
                  <th
                    key={col.key}
                    className={col.sortable ? 'sortable' : ''}
                    onClick={() => toggleSort(col)}
                  >
                    {col.label}
                    {col.sortable && sort === col.key ? (
                      <span className="sort-indicator">{order === 'asc' ? ' ▲' : ' ▼'}</span>
                    ) : null}
                  </th>
                ))}
                {rowActions ? <th className="actions-col">Actions</th> : null}
              </tr>
            </thead>
            <tbody>
              {result.items.length ? (
                result.items.map((item) => (
                  <tr key={item.id}>
                    {columns.map((col) => (
                      <td key={col.key}>{col.render ? col.render(item) : String((item as unknown as Record<string, unknown>)[col.key] ?? '')}</td>
                    ))}
                    {rowActions ? <td className="actions-col">{rowActions(item)}</td> : null}
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={columns.length + (rowActions ? 1 : 0)} className="empty-table">
                    No items.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
          <Pagination page={result.page} totalPages={result.totalPages} onPageChange={setPage} />
        </>
      ) : null}
    </div>
  );
}
