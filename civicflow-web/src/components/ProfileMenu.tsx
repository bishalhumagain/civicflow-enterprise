import { useEffect, useRef, useState } from 'react'
import { Icon } from './Icon'
import { useTheme, type Theme } from '../theme'

export type ProfileMenuProps = {
  fullName: string
  email: string
  onOpenProfile: () => void
  onSignOut: () => void
}

function initialsFor(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return 'CF'

  return parts
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

export function ProfileMenu({
  fullName,
  email,
  onOpenProfile,
  onSignOut,
}: ProfileMenuProps) {
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const { theme, setTheme } = useTheme()

  useEffect(() => {
    if (!open) return

    function handlePointerDown(event: PointerEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setOpen(false)
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [open])

  function chooseTheme(nextTheme: Theme) {
    setTheme(nextTheme)
    setOpen(false)
  }

  function openProfile() {
    onOpenProfile()
    setOpen(false)
  }

  function signOut() {
    onSignOut()
    setOpen(false)
  }

  return (
    <div className="profile-menu" ref={containerRef}>
      <button
        aria-expanded={open}
        aria-haspopup="menu"
        className="profile-menu-trigger"
        onClick={() => setOpen((value) => !value)}
        type="button"
      >
        <span aria-hidden="true" className="avatar">{initialsFor(fullName)}</span>
        <span className="profile-menu-copy">
          <strong>{fullName}</strong>
          <small>{email}</small>
        </span>
        <Icon className="profile-menu-caret" name="chevron-down" />
      </button>

      {open && (
        <div aria-label="Account menu" className="profile-menu-popover" role="menu">
          <div className="profile-menu-header">
            <span aria-hidden="true" className="avatar">{initialsFor(fullName)}</span>
            <span>
              <strong>{fullName}</strong>
              <small>{email}</small>
            </span>
          </div>

          <div className="profile-menu-section">
            <button className="profile-menu-item" onClick={openProfile} role="menuitem" type="button">
              <Icon className="app-icon" name="profile" />
              <span>My profile</span>
            </button>
          </div>

          <div className="profile-menu-section">
            <p className="profile-menu-label">Appearance</p>
            <div aria-label="Theme" className="profile-theme-switch" role="group">
              <button
                aria-pressed={theme === 'light'}
                className="profile-theme-option"
                onClick={() => chooseTheme('light')}
                type="button"
              >
                <Icon className="app-icon" name="sun" />
                <span>Light</span>
              </button>
              <button
                aria-pressed={theme === 'dark'}
                className="profile-theme-option"
                onClick={() => chooseTheme('dark')}
                type="button"
              >
                <Icon className="app-icon" name="moon" />
                <span>Dark</span>
              </button>
            </div>
          </div>

          <div className="profile-menu-section">
            <button className="profile-menu-item profile-menu-signout" onClick={signOut} role="menuitem" type="button">
              <Icon className="app-icon" name="logout" />
              <span>Sign out</span>
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
