import { useCallback, useEffect, useState } from 'react'
import { apiRequest } from '../api'
import { Icon } from './Icon'

type NotificationItem = {
  id: string
  title: string
  message: string
  type: string
  relatedEntityType: string | null
  relatedEntityId: string | null
  isRead: boolean
  createdAt: string
}

type NotificationPage = {
  items: NotificationItem[]
  page: number
  pageSize: number
  totalCount: number
  unreadCount: number
}

type UnreadCount = { count: number }

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await apiRequest(path, '', init)
  if (!response.ok) throw new Error(`Request failed (${response.status}).`)
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export function NotificationCenter({ onOpenCase }: { onOpenCase?: (caseId: string) => void }) {
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<NotificationItem[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refreshCount = useCallback(async () => {
    try {
      const result = await request<UnreadCount>('/api/notifications/unread-count')
      setUnreadCount(result.count)
    } catch {
      setError('Notifications could not be refreshed.')
    }
  }, [])

  const loadPage = useCallback(async (requestedPage: number) => {
    setLoading(true)
    setError(null)
    try {
      const result = await request<NotificationPage>(
        `/api/notifications?page=${requestedPage}&pageSize=10`,
      )
      setItems(result.items)
      setUnreadCount(result.unreadCount)
      setTotalCount(result.totalCount)
      setPage(requestedPage)
    } catch {
      setError('Notifications could not be loaded. Try again.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    const initial = window.setTimeout(() => void refreshCount(), 0)
    const timer = window.setInterval(() => void refreshCount(), 60_000)
    return () => {
      window.clearTimeout(initial)
      window.clearInterval(timer)
    }
  }, [refreshCount])

  useEffect(() => {
    if (!open) return
    const timer = window.setTimeout(() => void loadPage(page), 0)
    return () => window.clearTimeout(timer)
  }, [open, loadPage, page])

  const markRead = async (id: string) => {
    try {
      await request(`/api/notifications/${id}/read`, { method: 'PATCH' })
      setItems((current) =>
        current.map((item) => item.id === id ? { ...item, isRead: true } : item),
      )
      setUnreadCount((count) => Math.max(0, count - 1))
    } catch {
      setError('This notification could not be marked as read.')
    }
  }

  const markAllRead = async () => {
    try {
      await request('/api/notifications/read', { method: 'POST' })
      setItems((current) => current.map((item) => ({ ...item, isRead: true })))
      setUnreadCount(0)
    } catch {
      setError('Notifications could not be marked as read.')
    }
  }

  return (
    <div className="notification-center">
      <button
        aria-expanded={open}
        aria-label={`Notifications${unreadCount ? `, ${unreadCount} unread` : ''}`}
        className="auth-button notification-toggle"
        data-tooltip="Open notifications"
        onClick={() => setOpen((value) => !value)}
        title="Open notifications"
        type="button"
      >
        <Icon className="app-icon" name="bell" />
        Notifications {unreadCount > 0 && <span className="notification-count">{unreadCount}</span>}
      </button>
      {open && (
        <section aria-label="Notifications" className="notification-panel">
          <header>
            <strong>Notifications</strong>
            <button
              className="document-link"
              disabled={unreadCount === 0}
              onClick={() => void markAllRead()}
              type="button"
            >
              Mark all read
            </button>
          </header>
          {error && (
            <div className="auth-error" role="alert">
              {error}
              <button className="document-link" onClick={() => void loadPage(page)} type="button">
                Retry
              </button>
            </div>
          )}
          {loading ? (
            <p className="muted empty-state">Loading notifications…</p>
          ) : items.length === 0 ? (
            <p className="muted empty-state">You’re all caught up.</p>
          ) : (
            <ul className="notification-list">
              {items.map((item) => (
                <li className={item.isRead ? 'notification-read' : 'notification-unread'} key={item.id}>
                  <div>
                    <strong>{item.title}</strong>
                    <p>{item.message}</p>
                    <small>{new Date(item.createdAt).toLocaleString()}</small>
                  </div>
                  {!item.isRead && (
                    <button
                      aria-label={`Mark ${item.title} as read`}
                      className="document-link"
                      onClick={() => void markRead(item.id)}
                      type="button"
                    >
                      Mark read
                    </button>
                  )}
                  {item.relatedEntityType === 'Case' && item.relatedEntityId && onOpenCase && (
                    <button
                      className="document-link"
                      onClick={() => {
                        const caseId = item.relatedEntityId
                        if (!caseId) return
                        if (!item.isRead) void markRead(item.id)
                        onOpenCase(caseId)
                      }}
                      type="button"
                    >
                      Open case
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
          <footer className="notification-pagination">
            <span>Page {page} · {totalCount} total</span>
            <span>
              <button disabled={page <= 1 || loading} onClick={() => setPage((value) => value - 1)} type="button">
                Previous
              </button>
              <button
                disabled={page * 10 >= totalCount || loading}
                onClick={() => setPage((value) => value + 1)}
                type="button"
              >
                Next
              </button>
            </span>
          </footer>
        </section>
      )}
    </div>
  )
}
