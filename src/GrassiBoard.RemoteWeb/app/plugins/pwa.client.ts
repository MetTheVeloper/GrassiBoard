export default defineNuxtPlugin(() => {
  if (!window.isSecureContext || !('serviceWorker' in navigator)) return

  // Register immediately rather than waiting for window.load. This makes it far
  // more likely that the first successful GrassiMote session is already under
  // service-worker control before the user installs/relaunches the PWA.
  void navigator.serviceWorker.register('/sw.js', { scope: '/', updateViaCache: 'none' })
    .then(async registration => {
      await registration.update().catch(() => undefined)
      await navigator.serviceWorker.ready.catch(() => undefined)
    })
    .catch(() => {
      // Remote functionality must remain usable even if PWA registration fails.
    })
})
