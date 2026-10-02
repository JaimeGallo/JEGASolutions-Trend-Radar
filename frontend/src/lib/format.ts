// Fechas siempre en hora de Colombia: es la zona en que se declaran los orígenes.
const TZ = 'America/Bogota'

const dateTime = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short', timeZone: TZ })
const dateOnly = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeZone: TZ })

export const fmtDateTime = (iso: string) => dateTime.format(new Date(iso))
export const fmtDate = (iso: string) => dateOnly.format(new Date(iso))

export function fmtRelative(iso: string) {
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000)
  if (days < 1) return 'hoy'
  if (days === 1) return 'ayer'
  if (days < 30) return `hace ${days} días`
  return fmtDate(iso)
}

/** Valor de <input type="datetime-local"> interpretado en hora de Colombia (UTC-5, sin horario de verano). */
export const localToIso = (value: string) => (value ? new Date(`${value}:00-05:00`).toISOString() : null)

export const fmtBytes = (n: number) => (n < 1024 ? `${n} B` : n < 1048576 ? `${(n / 1024).toFixed(1)} KB` : `${(n / 1048576).toFixed(1)} MB`)
