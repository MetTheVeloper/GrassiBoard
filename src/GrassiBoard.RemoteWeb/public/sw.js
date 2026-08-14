const CACHE_NAME = 'grassimote-shell-v22'
const SHELL = ['/', '/index.html', '/offline.html', '/manifest.webmanifest', '/icons/grassimote-192.png', '/icons/grassimote-512.png']

async function cacheGeneratedShell(cache) {
  try {
    const response = await fetch('/index.html', { cache: 'no-store' })
    if (!response.ok) return
    const html = await response.clone().text()
    await cache.put('/index.html', response)

    const assets = Array.from(html.matchAll(/(?:src|href)=["']([^"']+)["']/g))
      .map(match => match[1])
      .filter(value => value.startsWith('/_nuxt/'))

    await Promise.allSettled(assets.map(asset => cache.add(asset)))
  } catch {
    // The static offline page remains available even if generated assets
    // cannot be precached during this install attempt.
  }
}

self.addEventListener('install', event => {
  event.waitUntil((async () => {
    const cache = await caches.open(CACHE_NAME)
    await cache.addAll(SHELL)
    await cacheGeneratedShell(cache)
    await self.skipWaiting()
  })())
})

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))))
      .then(() => self.clients.claim())
  )
})

self.addEventListener('fetch', event => {
  const request = event.request
  if (request.method !== 'GET') return
  const url = new URL(request.url)
  if (url.origin !== self.location.origin || url.pathname.startsWith('/api/') || url.pathname === '/ws' || url.pathname === '/onboard') return

  if (request.mode === 'navigate') {
    event.respondWith((async () => {
      try {
        const response = await fetch(request)
        const copy = response.clone()
        caches.open(CACHE_NAME).then(cache => cache.put(request, copy))
        return response
      } catch {
        return (await caches.match('/offline.html')) || (await caches.match('/index.html'))
      }
    })())
    return
  }

  event.respondWith(
    caches.match(request).then(cached => cached || fetch(request).then(response => {
      if (response.ok && response.type === 'basic') {
        const copy = response.clone()
        caches.open(CACHE_NAME).then(cache => cache.put(request, copy))
      }
      return response
    }))
  )
})
