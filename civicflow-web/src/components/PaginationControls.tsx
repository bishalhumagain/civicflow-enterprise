import type { PagedResponse } from '../domain'

export function PaginationControls<T>({
  result,
  loading,
  onPageChange,
  onPageSizeChange,
}: {
  result: Pick<PagedResponse<T>, 'page' | 'pageSize' | 'totalCount' | 'totalPages'>
  loading: boolean
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}) {
  const start = result.totalCount === 0 ? 0 : (result.page - 1) * result.pageSize + 1
  const end = Math.min(result.page * result.pageSize, result.totalCount)

  return (
    <div className="pagination">
      <span>{start}–{end} of {result.totalCount}</span>
      <label>
        Rows per page{' '}
        <select
          aria-label="Rows per page"
          disabled={loading}
          onChange={(event) => onPageSizeChange(Number(event.target.value))}
          value={result.pageSize}
        >
          {[10, 25, 50, 100].map((size) => <option key={size} value={size}>{size}</option>)}
        </select>
      </label>
      <span>Page {result.page} of {Math.max(1, result.totalPages)}</span>
      <span>
        <button disabled={result.page <= 1 || loading} onClick={() => onPageChange(result.page - 1)} type="button">Previous</button>
        <button disabled={result.page >= result.totalPages || loading} onClick={() => onPageChange(result.page + 1)} type="button">Next</button>
      </span>
    </div>
  )
}
