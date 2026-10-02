const KEY = 'tr-theme'

export function applyStoredTheme() {
  try {
    const stored = localStorage.getItem(KEY)
    if (stored === 'light' || stored === 'dark') document.documentElement.dataset.theme = stored
  } catch {
    // Sin almacenamiento: se usa el tema oscuro por defecto.
  }
}

export function toggleTheme() {
  const current = document.documentElement.dataset.theme ?? 'dark'
  const next = current === 'light' ? 'dark' : 'light'
  document.documentElement.dataset.theme = next
  try {
    localStorage.setItem(KEY, next)
  } catch {
    // Ignorar: el cambio aplica solo a esta sesión.
  }
}
