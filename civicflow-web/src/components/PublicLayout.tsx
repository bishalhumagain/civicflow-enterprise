import { useAppConfig } from '../app-config'
import { ThemeToggle } from './ThemeToggle'

function currentYear(): number {
  return new Date().getFullYear()
}

export function Brand({ onClick }: { onClick?: () => void }) {
  const { applicationName } = useAppConfig()

  if (onClick) {
    return (
      <button className="landing-brand brand-button" onClick={onClick} type="button">
        <span aria-hidden="true" className="landing-brand-mark">C</span>
        <span className="landing-brand-name">{applicationName}</span>
      </button>
    )
  }

  return (
    <a aria-label={`${applicationName} home`} className="landing-brand" href="/">
      <span aria-hidden="true" className="landing-brand-mark">C</span>
      <span className="landing-brand-name">{applicationName}</span>
    </a>
  )
}

export function PublicHeader({ onHome }: { onHome?: () => void }) {
  return (
    <header className="landing-header">
      <Brand onClick={onHome} />
      <ThemeToggle />
    </header>
  )
}

export function PublicFooter() {
  const { applicationName } = useAppConfig()

  return (
    <footer className="landing-footer">
      <p>
        &copy; {currentYear()} {applicationName}. All rights reserved.
      </p>
    </footer>
  )
}

export function AppFooter() {
  const { applicationName } = useAppConfig()

  return (
    <footer className="app-footer">
      <p>
        &copy; {currentYear()} {applicationName}. All rights reserved.
      </p>
    </footer>
  )
}
