import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { AppConfigProvider } from './components/AppConfig'
import { ToastProvider } from './components/Toast'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppConfigProvider>
      <ToastProvider>
        <App />
      </ToastProvider>
    </AppConfigProvider>
  </StrictMode>,
)
