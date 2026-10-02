import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { Layout } from './app/Layout'
import { sections } from './app/sections'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { LoginPage } from './features/auth/LoginPage'
import { RadarPage } from './features/dashboard/RadarPage'
import { IntegrityPage } from './features/integrity/IntegrityPage'
import { SectionPage } from './features/sections/SectionPage'

function AppRoutes() {
  const { user, loading } = useAuth()
  if (loading) return null
  if (!user) return <LoginPage />

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<RadarPage />} />
        <Route path="/integridad" element={user.role === 'Owner' ? <IntegrityPage /> : <Navigate to="/" replace />} />
        {sections
          .filter((s) => s.milestone)
          .map((s) => (
            <Route key={s.path} path={s.path} element={<SectionPage section={s} />} />
          ))}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  )
}
