import { useCallback, useState } from 'react'
import { LogOut, Search } from 'lucide-react'
import { NavLink, Outlet } from 'react-router'
import { useAuth } from '../auth/AuthContext'
import { t } from '../i18n/es'
import { CommandPalette } from './CommandPalette'
import { sections } from './sections'
import { useShortcuts } from './useShortcuts'

export function Layout() {
  const { user, logout } = useAuth()
  const [paletteOpen, setPaletteOpen] = useState(false)
  const openPalette = useCallback(() => setPaletteOpen(true), [])
  useShortcuts(openPalette)

  return (
    <div className="flex h-full">
      <nav aria-label="Principal" className="flex w-14 shrink-0 flex-col border-r border-line bg-surface md:w-52">
        <div className="flex items-center gap-2 px-3 py-3">
          <img src="/favicon.svg" alt="" className="h-7 w-7" />
          <div className="hidden leading-tight md:block">
            <div className="text-sm font-semibold">{t.app.name}</div>
            <div className="text-[11px] text-gold">{t.app.org}</div>
          </div>
        </div>
        <button
          type="button"
          onClick={openPalette}
          className="mx-2 mb-2 flex items-center gap-2 rounded-md border border-line px-2 py-1.5 text-muted hover:text-ink"
        >
          <Search size={14} aria-hidden />
          <span className="hidden flex-1 text-left md:inline">{t.palette.navigation}…</span>
          <kbd className="hidden md:inline">Ctrl K</kbd>
        </button>
        <ul className="flex-1 space-y-0.5 overflow-auto px-2">
          {sections.map((s) => (
            <li key={s.path}>
              <NavLink
                to={s.path}
                end={s.path === '/'}
                title={s.label}
                className={({ isActive }) =>
                  `flex items-center gap-2 rounded-md px-2 py-1.5 ${
                    isActive ? 'bg-accent-soft text-ink' : 'text-muted hover:bg-surface-2 hover:text-ink'
                  }`
                }
              >
                <s.icon size={16} aria-hidden />
                <span className="hidden flex-1 md:inline">{s.label}</span>
                {s.milestone && <span className="hidden text-[10px] text-muted/70 md:inline">{s.milestone}</span>}
              </NavLink>
            </li>
          ))}
        </ul>
        <div className="border-t border-line p-2">
          <div className="hidden truncate px-1 text-xs md:block">{user?.displayName}</div>
          <div className="hidden px-1 text-[11px] text-muted md:block">{user && t.roles[user.role]}</div>
          <button
            type="button"
            onClick={logout}
            title={t.auth.logout}
            className="mt-1 flex w-full items-center gap-2 rounded-md px-1 py-1 text-muted hover:text-ink"
          >
            <LogOut size={14} aria-hidden />
            <span className="hidden md:inline">{t.auth.logout}</span>
          </button>
        </div>
      </nav>
      <main className="flex min-w-0 flex-1 flex-col">
        <Outlet />
      </main>
      <CommandPalette open={paletteOpen} onClose={() => setPaletteOpen(false)} />
    </div>
  )
}
