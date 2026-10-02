import { Command } from 'cmdk'
import { Plus, SunMoon } from 'lucide-react'
import { useNavigate } from 'react-router'
import { t } from '../i18n/es'
import { toggleTheme } from '../lib/theme'
import { sections } from './sections'

export function CommandPalette({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  if (!open) return null

  const run = (fn: () => void) => {
    fn()
    onClose()
  }

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center bg-black/50 px-4 pt-[15vh]" onMouseDown={onClose}>
      <Command
        label={t.palette.placeholder}
        className="w-full max-w-lg overflow-hidden rounded-xl border border-line bg-surface shadow-2xl"
        onMouseDown={(e) => e.stopPropagation()}
        onKeyDown={(e) => e.key === 'Escape' && onClose()}
      >
        <Command.Input
          autoFocus
          placeholder={t.palette.placeholder}
          className="w-full border-b border-line bg-transparent px-4 py-3 text-ink outline-none placeholder:text-muted"
        />
        <Command.List className="max-h-80 overflow-auto p-2">
          <Command.Empty className="px-3 py-6 text-center text-muted">{t.palette.empty}</Command.Empty>
          <Command.Group heading={t.palette.navigation} className="text-[11px] text-muted [&_[cmdk-group-heading]]:px-2 [&_[cmdk-group-heading]]:py-1">
            {sections.map((s) => (
              <Command.Item
                key={s.path}
                value={s.label}
                onSelect={() => run(() => navigate(s.path))}
                className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-2 text-sm text-ink data-[selected=true]:bg-accent-soft"
              >
                <s.icon size={15} aria-hidden />
                <span className="flex-1">{s.label}</span>
                <kbd>G {s.key.toUpperCase()}</kbd>
              </Command.Item>
            ))}
          </Command.Group>
          <Command.Group heading={t.palette.actions} className="text-[11px] text-muted [&_[cmdk-group-heading]]:px-2 [&_[cmdk-group-heading]]:py-1">
            <Command.Item disabled className="flex items-center gap-2 rounded-md px-2 py-2 text-sm text-muted opacity-60">
              <Plus size={15} aria-hidden />
              {t.palette.newSignal}
            </Command.Item>
            <Command.Item
              onSelect={() => run(toggleTheme)}
              className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-2 text-sm text-ink data-[selected=true]:bg-accent-soft"
            >
              <SunMoon size={15} aria-hidden />
              {t.palette.toggleTheme}
            </Command.Item>
          </Command.Group>
        </Command.List>
      </Command>
    </div>
  )
}
