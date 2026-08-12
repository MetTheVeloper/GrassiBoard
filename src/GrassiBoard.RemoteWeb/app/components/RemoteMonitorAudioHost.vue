<script setup lang="ts">
const remote = useRemoteConnection()
const monitor = useRemoteMonitorSpike()
const audioElement = ref<HTMLAudioElement | null>(null)
let resumeTimer: ReturnType<typeof setTimeout> | null = null

function setMediaSessionState() {
  if (!import.meta.client || !('mediaSession' in navigator)) return
  try {
    // Local monitor mute is not a transport pause. Keep the OS-visible media
    // session in the playing state while the WebRTC receiver is alive so an
    // Android task/background transition is less likely to be interpreted as
    // an explicit request to stop the monitor.
    navigator.mediaSession.playbackState = monitor.active.value ? 'playing' : 'none'
  } catch { }
}

async function ensurePlayback() {
  const element = audioElement.value
  const stream = monitor.mediaStream.value
  if (!element || !stream) return false
  if (element.srcObject !== stream) element.srcObject = stream
  element.muted = monitor.monitorMuted.value
  try {
    await element.play()
    monitor.markPlaybackBlocked(false)
    setMediaSessionState()
    return true
  } catch {
    monitor.markPlaybackBlocked(true)
    setMediaSessionState()
    return false
  }
}

function queueResume(delay = 150, force = false) {
  if (resumeTimer) clearTimeout(resumeTimer)
  resumeTimer = setTimeout(() => {
    resumeTimer = null
    if (monitor.mediaStream.value) void ensurePlayback()
    if (!monitor.active.value || force) void monitor.resumeIfDesired(force)
  }, delay)
}

function configureMediaSession() {
  if (!import.meta.client || !('mediaSession' in navigator)) return
  try {
    navigator.mediaSession.metadata = new MediaMetadata({
      title: 'GrassiMote Remote Monitor',
      artist: 'GrassiBoard',
      album: monitor.activeSource.value === 'monitor-mix' ? 'Windows + Soundboard + Media + My Voice' : monitor.activeSource.value === 'windows-loopback' ? 'Windows output' : monitor.activeSource.value === 'soundboard-tap' ? 'Soundboard tap' : 'WebRTC test tone',
      artwork: [
        { src: '/icons/grassimote-192.png', sizes: '192x192', type: 'image/png' },
        { src: '/icons/grassimote-512.png', sizes: '512x512', type: 'image/png' }
      ]
    })
  } catch { }

  const bind = (action: MediaSessionAction, handler: MediaSessionActionHandler | null) => {
    try { navigator.mediaSession.setActionHandler(action, handler) } catch { }
  }

  bind('play', async () => {
    monitor.setMonitorMuted(false)
    await ensurePlayback()
  })
  bind('pause', () => {
    // System pause maps to local listening mute only. It must never tear down
    // the Windows/WebRTC session; explicit Stop monitor remains the sole stop.
    monitor.setMonitorMuted(true)
    if (audioElement.value) audioElement.value.muted = true
    setMediaSessionState()
  })
  bind('stop', () => {
    // Some Android/Chrome task transitions can surface a MediaSession stop.
    // Treat it like a local mute instead of monitor.stop(), otherwise minimizing
    // the installed PWA can clear desired-source and kill the session.
    monitor.setMonitorMuted(true)
    if (audioElement.value) audioElement.value.muted = true
    setMediaSessionState()
  })
}

function onVisibilityChange() {
  if (document.visibilityState === 'visible') queueResume(80)
  // Hidden is intentionally a no-op: keep media/WebRTC alive if Android allows it.
}

function onLifecycleResume() {
  queueResume(80)
}

function onPageShow() {
  queueResume(80)
}

function onWindowFocus() {
  queueResume(80)
}

function onFreeze() {
  // Chrome may freeze a background PWA. Do not clear monitor intent or signal
  // Windows to stop; foreground resume will re-use or rebuild the local peer.
}

watch(() => monitor.mediaStream.value, () => { void ensurePlayback() })
watch(() => monitor.monitorMuted.value, muted => {
  if (audioElement.value) audioElement.value.muted = muted
  if (!muted) void ensurePlayback()
  setMediaSessionState()
})
watch(() => monitor.activeSource.value, () => configureMediaSession())
watch(() => remote.isConnected.value, connected => {
  if (connected) queueResume(180)
})
watch(() => monitor.phase.value, () => setMediaSessionState())

onMounted(() => {
  monitor.initialize()
  monitor.registerPlaybackInvoker(ensurePlayback)
  configureMediaSession()
  setMediaSessionState()
  document.addEventListener('visibilitychange', onVisibilityChange)
  document.addEventListener('resume', onLifecycleResume as EventListener)
  document.addEventListener('freeze', onFreeze as EventListener)
  window.addEventListener('pageshow', onPageShow)
  window.addEventListener('focus', onWindowFocus)
  queueResume(250)
})

onBeforeUnmount(() => {
  document.removeEventListener('visibilitychange', onVisibilityChange)
  document.removeEventListener('resume', onLifecycleResume as EventListener)
  document.removeEventListener('freeze', onFreeze as EventListener)
  window.removeEventListener('pageshow', onPageShow)
  window.removeEventListener('focus', onWindowFocus)
  if (resumeTimer) clearTimeout(resumeTimer)
  monitor.registerPlaybackInvoker(null)
  // Do not stop here. Normal route/PWA lifecycle changes are not an explicit
  // user request to stop Remote Monitor.
})
</script>

<template>
  <audio
    ref="audioElement"
    class="monitor-audio monitor-audio--hidden"
    autoplay
    playsinline
    preload="auto"
    aria-label="GrassiMote Remote Monitor audio"
  />
</template>
