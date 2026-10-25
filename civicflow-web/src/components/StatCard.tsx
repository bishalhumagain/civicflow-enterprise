import { Icon, type IconName } from './Icon'

export function StatCard({
  label,
  value,
  detail,
  tone,
  icon,
}: {
  label: string
  value: string
  detail: string
  tone: 'blue' | 'amber' | 'purple' | 'green'
  icon: IconName
}) {
  return (
    <article className="stat-card">
      <div className={`stat-icon ${tone}`} aria-hidden="true">
        <Icon className="app-icon" name={icon} />
      </div>
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
        <small>{detail}</small>
      </div>
    </article>
  )
}
