import { useEffect, useId, useRef, useState } from 'react'
import type { CaseDocument, PlatformCaseDocumentEntry } from '../domain'
import { Icon } from './Icon'

type PreviewDocument = Pick<CaseDocument | PlatformCaseDocumentEntry, 'originalFileName' | 'contentType'>

export function DocumentPreviewButton({
  document,
  loadDocument,
}: {
  document: PreviewDocument
  loadDocument: () => Promise<Blob>
}) {
  const dialogRef = useRef<HTMLDialogElement | null>(null)
  const previewRequest = useRef(0)
  const titleId = useId()
  const [open, setOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return

    if (open && !dialog.open) dialog.showModal()
    else if (!open && dialog.open) dialog.close()
  }, [open])

  useEffect(() => () => {
    if (previewUrl) URL.revokeObjectURL(previewUrl)
  }, [previewUrl])

  useEffect(() => () => {
    previewRequest.current += 1
  }, [])

  async function preview() {
    const requestId = ++previewRequest.current
    setOpen(true)
    setLoading(true)
    setError(null)
    setPreviewUrl(null)

    try {
      const blob = await loadDocument()
      if (requestId !== previewRequest.current) return
      setPreviewUrl(URL.createObjectURL(blob))
    } catch (reason) {
      if (requestId !== previewRequest.current) return
      setError(reason instanceof Error
        ? reason.message
        : 'This document could not be previewed. Check your access and try again.')
    } finally {
      if (requestId === previewRequest.current) setLoading(false)
    }
  }

  function close() {
    previewRequest.current += 1
    setOpen(false)
    setLoading(false)
    setPreviewUrl(null)
    setError(null)
  }

  const contentType = document.contentType.toLowerCase().split(';')[0].trim()
  const isImage = contentType.startsWith('image/')
  const isPdf = contentType === 'application/pdf'

  return (
    <>
      <button
        aria-label={`Preview ${document.originalFileName}`}
        className="icon-button document-preview-button"
        disabled={loading}
        onClick={() => void preview()}
        title={`Preview ${document.originalFileName}`}
        type="button"
      >
        <Icon name="eye" />
      </button>
      <dialog
        aria-labelledby={titleId}
        className="document-preview-dialog"
        onCancel={(event) => {
          event.preventDefault()
          close()
        }}
        onClose={() => {
          if (open) close()
        }}
        ref={dialogRef}
      >
        <div className="document-preview-header">
          <h2 id={titleId}>{document.originalFileName}</h2>
          <button aria-label="Close document preview" className="icon-button" onClick={close} type="button">
            <Icon name="x" />
          </button>
        </div>
        <div aria-busy={loading} className="document-preview-content">
          {loading ? (
            <p aria-live="polite" className="muted">Loading preview…</p>
          ) : error ? (
            <p className="field-error" role="alert">{error}</p>
          ) : previewUrl && isImage ? (
            <img alt={document.originalFileName} className="document-preview-image" src={previewUrl} />
          ) : previewUrl && isPdf ? (
            <iframe className="document-preview-pdf" src={previewUrl} title={`PDF preview: ${document.originalFileName}`} />
          ) : previewUrl ? (
            <div className="document-preview-unavailable">
              <p className="muted">Preview is not available for this file type. Download the file to open it.</p>
              <a className="primary-button" download={document.originalFileName} href={previewUrl}>Download document</a>
            </div>
          ) : null}
        </div>
        <div className="document-preview-actions">
          <button className="secondary-button" onClick={close} type="button">Close</button>
        </div>
      </dialog>
    </>
  )
}
