import { Icon } from './Icon'
import { useTheme } from '../theme'

export function ThemeToggle({ compact = false }: { compact?: boolean }) {
  const { theme, toggleTheme } = useTheme()
  const label = theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'

  return (
    <button
      aria-label={label}
      className={compact ? 'theme-toggle compact' : 'theme-toggle'}
      data-tooltip={compact ? label : undefined}
      onClick={toggleTheme}
      title={label}
      type="button"
    >
      <Icon name={theme === 'dark' ? 'sun' : 'moon'} />
      {!compact && <span>{theme === 'dark' ? 'Light' : 'Dark'}</span>}
    </button>
  )
}
