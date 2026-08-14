import type { ConnectionState, PairResponse, RemoteEnvelope, RemoteStateSnapshot } from '~/types/remote'

interface RemoteSnackbar {
  id: number
  message: string
  tone: 'neutral' | 'success' | 'warning' | 'danger'
}

interface RemoteInfo {
  protocolVersion: number
  name: string
  pairingOpen: boolean
  secureOrigin?: string
  onboardingOrigin?: string
  stableHost?: string
  mdnsAvailable?: boolean
  remoteMonitorSpikeAvailable?: boolean
  remotePhoneMicSpikeAvailable?: boolean
}

type RemoteMessageHandler = (message: RemoteEnvelope<any>) => void | Promise<void>

const protocolVersion = 1
let socket: WebSocket | null = null
let reconnectTimer: ReturnType<typeof setTimeout> | null = null
let reconnectAttempt = 0
let initialized = false
let authTimer: ReturnType<typeof setTimeout> | null = null
let snackbarTimer: ReturnType<typeof setTimeout> | null = null
const messageHandlers = new Map<string, Set<RemoteMessageHandler>>()

function subscribeMessage(type: string, handler: RemoteMessageHandler) {
  let handlers = messageHandlers.get(type)
  if (!handlers) {
    handlers = new Set<RemoteMessageHandler>()
    messageHandlers.set(type, handlers)
  }
  handlers.add(handler)

  return () => {
    const current = messageHandlers.get(type)
    if (!current) return
    current.delete(handler)
    if (current.size === 0) messageHandlers.delete(type)
  }
}

function dispatchSubscribedMessage(message: RemoteEnvelope<any>) {
  const handlers = messageHandlers.get(message.type)
  if (!handlers?.size) return
  for (const handler of [...handlers]) {
    try {
      Promise.resolve(handler(message)).catch(() => { /* Isolate feature listeners from the core Remote socket. */ })
    } catch { /* Isolate feature listeners from the core Remote socket. */ }
  }
}

function normalizeOrigin(value: string) {
  return value.replace(/\/+$/, '')
}


function createMessageId() {
  if (import.meta.client && typeof globalThis.crypto?.randomUUID === 'function') {
    return globalThis.crypto.randomUUID()
  }

  if (import.meta.client && globalThis.crypto?.getRandomValues) {
    const bytes = globalThis.crypto.getRandomValues(new Uint8Array(16))
    return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('')
  }

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`
}

function defaultDeviceName() {
  if (!import.meta.client) return 'Browser'
  const ua = navigator.userAgent
  if (/Android/i.test(ua)) return 'Android Phone'
  if (/iPhone|iPad/i.test(ua)) return 'iPhone/iPad'
  return 'Browser'
}

export function useRemoteConnection() {
  const config = useRuntimeConfig()
  const route = useRoute()
  const router = useRouter()
  const snapshot = useState<RemoteStateSnapshot | null>('remote:snapshot', () => null)
  const connectionState = useState<ConnectionState>('remote:connection', () => 'idle')
  const lastError = useState<string>('remote:error', () => '')
  const paired = useState<boolean>('remote:paired', () => false)
  const pendingAcks = useState<Record<string, string>>('remote:acks', () => ({}))
  const manualCode = useState<string>('remote:manual-code', () => '')
  const serverInfo = useState<RemoteInfo | null>('remote:server-info', () => null)
  const snackbar = useState<RemoteSnackbar | null>('remote:snackbar', () => null)

  const remoteOrigin = computed(() => {
    const configured = String(config.public.remoteOrigin || '').trim()
    if (configured) return normalizeOrigin(configured)
    if (import.meta.client) return normalizeOrigin(window.location.origin)
    return ''
  })

  const isConnected = computed(() => connectionState.value === 'connected')
  const isSecureContext = computed(() => import.meta.client ? window.isSecureContext : false)
  const secureAppUrl = computed(() => {
    const origin = serverInfo.value?.secureOrigin?.replace(/\/+$/, '') || ''
    if (!origin) return ''
    const pairSecret = typeof route.query.pair === 'string' ? route.query.pair : ''
    return pairSecret ? `${origin}/?pair=${encodeURIComponent(pairSecret)}` : `${origin}/`
  })
  const connectionLabel = computed(() => {
    switch (connectionState.value) {
      case 'connected': return 'Connected'
      case 'pairing': return 'Pairing…'
      case 'connecting': return 'Connecting…'
      case 'authenticating': return 'Authenticating…'
      case 'unauthorized': return 'Pairing required'
      case 'disconnected': return 'Reconnecting…'
      default: return 'Ready'
    }
  })

  function apiUrl(path: string) {
    return `${remoteOrigin.value}${path}`
  }

  function wsUrl() {
    const url = new URL(apiUrl('/ws'))
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:'
    return url.toString()
  }


  async function refreshInfo() {
    if (!remoteOrigin.value) return null
    try {
      const info = await $fetch<RemoteInfo>(apiUrl('/api/remote/info'))
      serverInfo.value = info
      return info
    } catch {
      return null
    }
  }

  function getToken() {
    return import.meta.client ? localStorage.getItem('grassiboard.remote.token') ?? '' : ''
  }

  function saveToken(token: string) {
    if (!import.meta.client) return
    localStorage.setItem('grassiboard.remote.token', token)
    paired.value = true
  }

  function forgetToken() {
    if (import.meta.client) localStorage.removeItem('grassiboard.remote.token')
    paired.value = false
    snapshot.value = null
  }

  function vibrate(pattern: number | number[] = 14) {
    if (import.meta.client && 'vibrate' in navigator) navigator.vibrate(pattern)
  }

  function dismissSnackbar() {
    if (snackbarTimer) clearTimeout(snackbarTimer)
    snackbarTimer = null
    snackbar.value = null
  }

  function showSnackbar(
    message: string,
    tone: RemoteSnackbar['tone'] = 'neutral',
    duration = 2800
  ) {
    if (!message) return
    if (snackbarTimer) clearTimeout(snackbarTimer)
    snackbar.value = { id: Date.now(), message, tone }
    snackbarTimer = setTimeout(() => {
      snackbarTimer = null
      snackbar.value = null
    }, Math.max(1600, duration))
  }

  async function pair(input: { secret?: string, code?: string, deviceName?: string }) {
    if (!remoteOrigin.value) return false
    connectionState.value = 'pairing'
    lastError.value = ''
    try {
      const response = await $fetch<PairResponse>(apiUrl('/api/remote/pair'), {
        method: 'POST',
        body: {
          secret: input.secret || undefined,
          code: input.code || undefined,
          deviceName: input.deviceName || defaultDeviceName()
        }
      })
      saveToken(response.clientToken)
      if (typeof route.query.pair === 'string') {
        const { pair: _pair, ...query } = route.query
        await router.replace({ query })
      }
      vibrate([18, 20, 18])
      connect(true)
      return true
    } catch (error) {
      connectionState.value = 'unauthorized'
      lastError.value = 'The pairing code is invalid, expired, or pairing is locked.'
      return false
    }
  }


  async function pairFromQr(rawValue: string) {
    lastError.value = ''
    let secret = ''
    let parsed: URL | null = null
    try {
      parsed = new URL(rawValue.trim())
      secret = parsed.searchParams.get('pair') || ''
    } catch {
      const match = rawValue.match(/[?&]pair=([^&\s]+)/i)
      if (match?.[1]) secret = decodeURIComponent(match[1])
    }
    if (!secret) {
      lastError.value = 'This QR is not a GrassiBoard pairing code.'
      return false
    }

    // A previously installed IP-scoped PWA may be running from an old LAN IP.
    // If the newly scanned desktop QR points at another host, move to that PC's
    // onboarding URL instead of trying to pair against the dead/current origin.
    if (import.meta.client && parsed?.hostname && parsed.hostname !== window.location.hostname) {
      closeSocket()
      forgetToken()
      window.location.href = parsed.toString()
      return true
    }

    closeSocket()
    forgetToken()
    return pair({ secret })
  }

  async function pairWithCode() {
    const code = manualCode.value.replace(/\D/g, '').slice(0, 6)
    if (code.length !== 6) {
      lastError.value = 'Enter the 6-digit pairing code shown on the PC.'
      return false
    }
    return pair({ code })
  }

  function clearTimers() {
    if (reconnectTimer) clearTimeout(reconnectTimer)
    if (authTimer) clearTimeout(authTimer)
    reconnectTimer = null
    authTimer = null
  }

  function closeSocket() {
    clearTimers()
    if (socket) {
      const current = socket
      socket = null
      current.onclose = null
      current.onerror = null
      current.onmessage = null
      current.close()
    }
  }

  function scheduleReconnect() {
    if (!import.meta.client || !getToken() || reconnectTimer) return
    const delay = Math.min(12_000, 500 * (2 ** Math.min(reconnectAttempt, 5)))
    reconnectAttempt += 1
    reconnectTimer = setTimeout(() => {
      reconnectTimer = null
      connect(true)
    }, delay)
  }

  function handleMessage(event: MessageEvent<string>) {
    let message: RemoteEnvelope<any>
    try {
      message = JSON.parse(event.data) as RemoteEnvelope<any>
    } catch {
      return
    }
    if (message.protocolVersion !== protocolVersion) {
      lastError.value = `Remote protocol ${protocolVersion} is required.`
      closeSocket()
      return
    }

    dispatchSubscribedMessage(message)

    if (message.type === 'connection.hello') {
      const token = getToken()
      if (!token || !socket) {
        connectionState.value = 'unauthorized'
        return
      }
      connectionState.value = 'authenticating'
      socket.send(JSON.stringify({
        protocolVersion,
        type: 'connection.auth',
        messageId: createMessageId(),
        payload: { token }
      }))
      authTimer = setTimeout(() => {
        lastError.value = 'Authentication timed out.'
        closeSocket()
        scheduleReconnect()
      }, 10_000)
      return
    }

    if (message.type === 'state.snapshot') {
      if (authTimer) clearTimeout(authTimer)
      authTimer = null
      snapshot.value = message.payload as RemoteStateSnapshot
      connectionState.value = 'connected'
      lastError.value = ''
      paired.value = true
      reconnectAttempt = 0
      return
    }

    if (message.type === 'ack') {
      if (message.payload?.command === 'connection.auth') {
        connectionState.value = 'authenticating'
      }
      if (message.messageId) {
        const next = { ...pendingAcks.value }
        delete next[message.messageId]
        pendingAcks.value = next
      }
      return
    }

    if (message.type === 'error') {
      const code = String(message.payload?.code || 'remote_error')
      const errorMessage = String(message.payload?.message || 'Remote command failed.')
      if (message.messageId) {
        const next = { ...pendingAcks.value }
        delete next[message.messageId]
        pendingAcks.value = next
      }

      if (code === 'unauthorized') {
        lastError.value = errorMessage
        forgetToken()
        connectionState.value = 'unauthorized'
        closeSocket()
        return
      }

      // Command-level failures are transient interaction feedback, not network
      // state. Keep the control surface alive and surface them as an M3-style
      // snackbar instead of a persistent inline error banner.
      showSnackbar(errorMessage, code === 'engine_not_running' ? 'warning' : 'danger')
    }
  }

  function connect(force = false) {
    if (!import.meta.client) return
    const token = getToken()
    paired.value = Boolean(token)
    if (!token) {
      connectionState.value = 'unauthorized'
      return
    }
    if (!force && socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)) return
    closeSocket()
    connectionState.value = 'connecting'
    lastError.value = ''
    try {
      const next = new WebSocket(wsUrl())
      socket = next
      next.onmessage = handleMessage
      next.onerror = () => {
        lastError.value = 'Could not reach GrassiBoard on the local network.'
      }
      next.onclose = () => {
        if (socket === next) socket = null
        if (getToken()) {
          connectionState.value = 'disconnected'
          scheduleReconnect()
        } else {
          connectionState.value = 'unauthorized'
        }
      }
    } catch {
      connectionState.value = 'disconnected'
      lastError.value = 'The Remote address is invalid or unavailable.'
      scheduleReconnect()
    }
  }

  function sendCommand(type: string, payload: Record<string, unknown> = {}) {
    if (!socket || socket.readyState !== WebSocket.OPEN || connectionState.value !== 'connected') {
      lastError.value = 'Remote is not connected. The command was not queued.'
      showSnackbar(lastError.value, 'warning')
      return false
    }
    const messageId = createMessageId()
    pendingAcks.value = { ...pendingAcks.value, [messageId]: type }
    socket.send(JSON.stringify({ protocolVersion, type, messageId, payload }))
    return true
  }

  function disconnect() {
    closeSocket()
    connectionState.value = paired.value ? 'disconnected' : 'unauthorized'
  }

  function initialize() {
    if (!import.meta.client || initialized) return
    initialized = true
    paired.value = Boolean(getToken())
    void refreshInfo()
    const pairSecret = typeof route.query.pair === 'string' ? route.query.pair : ''
    if (pairSecret && !getToken()) {
      void pair({ secret: pairSecret })
    } else {
      connect()
    }

    window.addEventListener('online', () => {
      void refreshInfo()
      connect(true)
    })
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        void refreshInfo()
        if (getToken()) connect(true)
      }
    })
  }

  return {
    snapshot,
    connectionState,
    connectionLabel,
    isConnected,
    lastError,
    snackbar,
    paired,
    pendingAcks,
    manualCode,
    serverInfo,
    remoteOrigin,
    secureAppUrl,
    isSecureContext,
    initialize,
    connect,
    disconnect,
    pair,
    pairWithCode,
    pairFromQr,
    refreshInfo,
    sendCommand,
    subscribeMessage,
    showSnackbar,
    dismissSnackbar,
    vibrate,
    forgetToken
  }
}
