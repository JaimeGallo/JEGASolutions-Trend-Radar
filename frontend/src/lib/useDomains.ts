import { useEffect, useState } from 'react'
import { api, type SignalDomain } from './api'

let cache: Promise<SignalDomain[]> | null = null

/** Taxonomía de dominios (cambia muy poco: se pide una vez por sesión). */
export function useDomains() {
  const [domains, setDomains] = useState<SignalDomain[]>([])
  useEffect(() => {
    cache ??= api<SignalDomain[]>('/api/domains').catch((e) => {
      cache = null
      throw e
    })
    let alive = true
    cache.then((d) => alive && setDomains(d)).catch(() => undefined)
    return () => {
      alive = false
    }
  }, [])
  const topLevel = domains.filter((d) => d.parentId === null)
  const subdomainsOf = (code: string) => {
    const parent = topLevel.find((d) => d.code === code)
    return parent ? domains.filter((d) => d.parentId === parent.id) : []
  }
  return { domains, topLevel, subdomainsOf }
}
