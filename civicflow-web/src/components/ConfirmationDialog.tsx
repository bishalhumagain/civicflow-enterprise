import { useEffect, useId, useRef } from 'react'

export function ConfirmationDialog({
  open,
  title,
  description,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  destructive = false,
  loading = false,
  onCancel,
  onConfirm,
}: {
  open: boolean
  title: string
  description: string
  confirmLabel?: string
  cancelLabel?: string
  destructive?: boolean
  loading?: boolean
  onCancel: () => void
  onConfirm: () => void | Promise<void>
}) {
  const dialogRef = useRef<HTMLDialogElement | null>(null)
  const titleId = useId()
  const descriptionId = useId()

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return

    if (open && !dialog.open) dialog.showModal()
    else if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
      className="confirmation-dialog"
      onCancel={(event) => {
        event.preventDefault()
        if (!loading) onCancel()
      }}
      onClose={() => {
        if (open && !loading) onCancel()
      }}
      ref={dialogRef}
    >
      <div className="confirmation-dialog-content">
        <h2 id={titleId}>{title}</h2>
        <p id={descriptionId}>{description}</p>
        <div className="confirmation-dialog-actions">
          <button
            autoFocus
            className="secondary-button"
            disabled={loading}
            onClick={onCancel}
            type="button"
          >
            {cancelLabel}
          </button>
          <button
            className={destructive ? 'danger-button' : 'primary-button'}
            disabled={loading}
            onClick={() => void onConfirm()}
            type="button"
          >
            {loading ? 'Working…' : confirmLabel}
          </button>
        </div>
      </div>
    </dialog>
  )
}
