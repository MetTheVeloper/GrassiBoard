export default defineNuxtPlugin(() => {
  if (!window.isSecureContext || !('serviceWorker' in navigator)) return
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js', { scope: '/' })
  }, { once: true })
})
