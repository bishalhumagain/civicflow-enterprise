import type { ReactNode } from 'react'
import { PublicFooter, PublicHeader } from './PublicLayout'

export type AuthPageProps = {
  title: string
  description?: ReactNode
  eyebrow?: string
  children: ReactNode
  footer?: ReactNode
  width?: 'narrow' | 'wide'
}

export function AuthPage({
  title,
  description,
  eyebrow,
  children,
  footer,
  width = 'narrow',
}: AuthPageProps) {
  return (
    <div className="landing-page">
      <PublicHeader />
      <main className="landing-main auth-main">
        <section
          aria-labelledby="auth-title"
          className={`auth-shell${width === 'wide' ? ' auth-shell-wide' : ''}`}
        >
          <header className="auth-heading">
            {eyebrow ? <p className="eyebrow">{eyebrow}</p> : null}
            <h1 id="auth-title">{title}</h1>
            {description ? <p className="muted">{description}</p> : null}
          </header>
          {children}
          {footer ? <div className="auth-footer">{footer}</div> : null}
        </section>
      </main>
      <PublicFooter />
    </div>
  )
}
