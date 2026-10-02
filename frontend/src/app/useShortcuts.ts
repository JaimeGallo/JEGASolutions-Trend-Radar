import { useEffect } from 'react'
import { useNavigate } from 'react-router'
import { sections } from './sections'

function isTyping(target: EventTarget | null) {
  const el = target as HTMLElement | null
  return !!el && (el.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName))
}

/** Ctrl/Cmd+K abre la paleta; "G" seguido de una letra navega a una sección. */
export function useShortcuts(openPalette: () => void) {
  const navigate = useNavigate()

  useEffect(() => {
    let awaitingSection = false
    let timer: number | undefined

    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        openPalette()
        return
      }
      if (e.metaKey || e.ctrlKey || e.altKey || isTyping(e.target)) return

      const key = e.key.toLowerCase()
      if (awaitingSection) {
        awaitingSection = false
        window.clearTimeout(timer)
        const section = sections.find((s) => s.key === key)
        if (section) {
          e.preventDefault()
          navigate(section.path)
        }
        return
      }
      if (key === 'g') {
        awaitingSection = true
        timer = window.setTimeout(() => (awaitingSection = false), 1200)
      }
    }

    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('keydown', onKey)
      window.clearTimeout(timer)
    }
  }, [navigate, openPalette])
}
