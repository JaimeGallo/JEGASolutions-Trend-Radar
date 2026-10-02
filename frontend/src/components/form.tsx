import type { ComponentProps, ReactNode } from 'react'

const inputClass =
  'w-full rounded-md border border-line bg-surface-2 px-2.5 py-1.5 text-ink outline-none placeholder:text-muted/70 focus:border-accent disabled:opacity-60'

export function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <label className="block space-y-1">
      <span className="text-xs text-muted">{label}</span>
      {children}
      {hint && <span className="block text-[11px] text-muted/80">{hint}</span>}
    </label>
  )
}

export const TextInput = (props: ComponentProps<'input'>) => <input {...props} className={inputClass} />

export const TextArea = (props: ComponentProps<'textarea'>) => (
  <textarea {...props} className={`${inputClass} resize-y`} />
)

export function Select({
  options,
  ...props
}: ComponentProps<'select'> & { options: { value: string; label: string }[] }) {
  return (
    <select {...props} className={inputClass}>
      {options.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  )
}

export function FormError({ message }: { message: string | null }) {
  return message ? (
    <p role="alert" className="rounded-md border border-danger/40 px-3 py-2 text-danger">
      {message}
    </p>
  ) : null
}

export function Button({
  variant = 'secondary',
  ...props
}: ComponentProps<'button'> & { variant?: 'primary' | 'secondary' | 'ghost' }) {
  const styles = {
    primary: 'bg-accent text-white hover:opacity-90',
    secondary: 'border border-line text-ink hover:border-accent',
    ghost: 'text-muted hover:text-ink',
  }[variant]
  return (
    <button
      type="button"
      {...props}
      className={`inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm disabled:opacity-60 ${styles} ${props.className ?? ''}`}
    />
  )
}
