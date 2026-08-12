import { computed, ref, shallowRef } from 'vue'
import type { RemoteEnvelope } from '~/types/remote'

type MonitorSpikePhase = 'idle' | 'negotiating' | 'connected' | 'failed' | 'stopped'
type MonitorSpikeSource = 'monitor-mix' | 'windows-loopback' | 'soundboard-tap' | 'synthetic-sine'
type MonitorMixGainKey = 'windows' | 'soundboard' | 'media' | 'voice' | 'master'

interface MonitorSpikeAnswerPayload {
  type?: string
  sdp?: string
}

interface MonitorSpikeIcePayload {
  candidate?: string
  sdpMid?: string | null
  sdpMLineIndex?: number | null
  usernameFragment?: string | null
}

interface MonitorSpikeStatePayload {
  state?: string
  detail?: string | null
  source?: string
  codec?: string
  ice?: string
  device?: string | null
  sampleRate?: number
  channels?: number
  frameMilliseconds?: number
  encoderBitrateKbps?: number
  encoderProfile?: string
  mix?: {
    windowsGain?: number
    soundboardGain?: number
    mediaGain?: number
    voiceGain?: number
    voiceEnabled?: boolean
    voiceMode?: string
    mediaDuplicateSuppressed?: boolean
    mediaMode?: string
    masterGain?: number
  } | null
}

const DESIRED_SOURCE_KEY = 'grassiboard.monitor.desired-source'
const VOICE_HEADPHONES_HINT_KEY = 'grassiboard.monitor.voice-headphones-hint.v1'

let peer: RTCPeerConnection | null = null
let subscriptions: Array<() => void> = []
let initialized = false
let remoteDescriptionReady = false
let pendingServerIce: RTCIceCandidateInit[] = []
let resumeInFlight = false
let statsTimer: ReturnType<typeof setInterval> | null = null
let previousInboundBytes = 0
let previousInboundAt = 0
let playbackInvoker: (() => Promise<boolean>) | null = null
let autoResumeTimer: ReturnType<typeof setTimeout> | null = null
let mixUpdateTimer: ReturnType<typeof setTimeout> | null = null

// Shared singleton state: the monitor is a session-level feature, not a page-level
// widget. Keeping it outside the /monitor route lets audio survive navigation and
// ordinary installed-PWA background/foreground transitions.
const phase = ref<MonitorSpikePhase>('idle')
const detail = ref('Ready for a local WebRTC/Opus test.')
const mediaStream = shallowRef<MediaStream | null>(null)
const playbackBlocked = ref(false)
const selectedSource = ref<MonitorSpikeSource>('monitor-mix')
const activeSource = ref<MonitorSpikeSource>('monitor-mix')
const desiredSource = ref<MonitorSpikeSource | null>(null)
const monitorMuted = ref(false)
const deviceName = ref('')
const sampleRate = ref(0)
const channels = ref(0)
const frameMilliseconds = ref(20)
const peerConnectionState = ref('new')
const iceConnectionState = ref('new')
const inboundBitrateKbps = ref(0)
const jitterMs = ref(0)
const packetsLost = ref(0)
const codecName = ref('Opus')
const encoderBitrateKbps = ref(0)
const encoderProfile = ref('library-default')
const mixWindowsGainPercent = ref(90)
const mixSoundboardGainPercent = ref(70)
const mixMediaGainPercent = ref(70)
const mixVoiceGainPercent = ref(10)
const mixVoiceEnabled = ref(false)
const mixMediaDuplicateSuppressed = ref(false)
const mixMediaMode = ref<'direct' | 'windows-output'>('direct')
const mixMasterGainPercent = ref(85)
const peerAttached = ref(false)

async function waitForIceGatheringComplete(connection: RTCPeerConnection, timeoutMs = 3500) {
  if (connection.iceGatheringState === 'complete') return

  await new Promise<void>(resolve => {
    let settled = false
    let timeout: ReturnType<typeof setTimeout> | null = null

    const finish = () => {
      if (settled) return
      settled = true
      connection.removeEventListener('icegatheringstatechange', onStateChange)
      if (timeout) clearTimeout(timeout)
      resolve()
    }

    const onStateChange = () => {
      if (connection.iceGatheringState === 'complete') finish()
    }

    connection.addEventListener('icegatheringstatechange', onStateChange)
    timeout = setTimeout(finish, timeoutMs)
    onStateChange()
  })
}

function stopStats() {
  if (statsTimer) clearInterval(statsTimer)
  statsTimer = null
  previousInboundBytes = 0
  previousInboundAt = 0
  inboundBitrateKbps.value = 0
  jitterMs.value = 0
  packetsLost.value = 0
}

async function sampleReceiverStats() {
  if (!peer || peer.connectionState !== 'connected') return
  try {
    const report = await peer.getStats()
    let inbound: any = null
    const codecs = new Map<string, any>()
    report.forEach((entry: any) => {
      if (entry.type === 'codec') codecs.set(entry.id, entry)
      if (entry.type === 'inbound-rtp' && (entry.kind === 'audio' || entry.mediaType === 'audio') && !entry.isRemote) inbound = entry
    })
    if (!inbound) return

    const now = performance.now()
    const bytes = Number(inbound.bytesReceived || 0)
    if (previousInboundAt > 0 && now > previousInboundAt && bytes >= previousInboundBytes) {
      inboundBitrateKbps.value = Math.round(((bytes - previousInboundBytes) * 8) / (now - previousInboundAt))
    }
    previousInboundBytes = bytes
    previousInboundAt = now
    jitterMs.value = Math.round(Number(inbound.jitter || 0) * 1000 * 10) / 10
    packetsLost.value = Math.max(0, Number(inbound.packetsLost || 0))

    const codec = inbound.codecId ? codecs.get(inbound.codecId) : null
    if (codec?.mimeType) codecName.value = String(codec.mimeType).replace(/^audio\//i, '')
  } catch {
    // Receiver statistics are diagnostics only; never disturb the media session.
  }
}

function startStats() {
  stopStats()
  void sampleReceiverStats()
  statsTimer = setInterval(() => { void sampleReceiverStats() }, 2000)
}

function saveDesiredSource(source: MonitorSpikeSource | null) {
  desiredSource.value = source
  if (!import.meta.client) return
  if (source) localStorage.setItem(DESIRED_SOURCE_KEY, source)
  else localStorage.removeItem(DESIRED_SOURCE_KEY)
}

export function useRemoteMonitorSpike() {
  const remote = useRemoteConnection()
  const available = computed(() => Boolean(remote.serverInfo.value?.remoteMonitorSpikeAvailable))
  const active = computed(() => peerAttached.value && (phase.value === 'negotiating' || phase.value === 'connected'))
  const wantsActive = computed(() => desiredSource.value !== null)

  function resetPeer() {
    peerAttached.value = false
    if (mixUpdateTimer) { clearTimeout(mixUpdateTimer); mixUpdateTimer = null }
    stopStats()
    if (peer) {
      peer.ontrack = null
      peer.onicecandidate = null
      peer.onconnectionstatechange = null
      peer.oniceconnectionstatechange = null
      try { peer.close() } catch { }
    }
    peer = null
    remoteDescriptionReady = false
    pendingServerIce = []
    mediaStream.value = null
    playbackBlocked.value = false
    peerConnectionState.value = 'closed'
    iceConnectionState.value = 'closed'
  }

  async function addServerIce(candidate: RTCIceCandidateInit) {
    if (!peer) return
    if (!remoteDescriptionReady) {
      pendingServerIce.push(candidate)
      return
    }
    try {
      await peer.addIceCandidate(candidate)
    } catch (error) {
      phase.value = 'failed'
      detail.value = `Could not apply Windows ICE candidate: ${error instanceof Error ? error.message : String(error)}`
    }
  }

  async function flushServerIce() {
    if (!peer || !remoteDescriptionReady) return
    const queued = pendingServerIce.splice(0)
    for (const candidate of queued) await addServerIce(candidate)
  }

  async function onAnswer(message: RemoteEnvelope<MonitorSpikeAnswerPayload>) {
    if (!peer || !message.payload?.sdp) return
    try {
      await peer.setRemoteDescription({ type: 'answer', sdp: message.payload.sdp })
      remoteDescriptionReady = true
      detail.value = 'SDP answer received; completing ICE/DTLS on the LAN.'
      await flushServerIce()
    } catch (error) {
      phase.value = 'failed'
      detail.value = `Browser rejected the WebRTC answer: ${error instanceof Error ? error.message : String(error)}`
      resetPeer()
    }
  }

  async function onServerIce(message: RemoteEnvelope<MonitorSpikeIcePayload>) {
    const payload = message.payload
    if (!payload?.candidate) return
    await addServerIce({
      candidate: payload.candidate,
      sdpMid: payload.sdpMid ?? undefined,
      sdpMLineIndex: payload.sdpMLineIndex ?? undefined,
      usernameFragment: payload.usernameFragment ?? undefined
    })
  }

  function onServerState(message: RemoteEnvelope<MonitorSpikeStatePayload>) {
    const payload = message.payload || {}
    const serverState = String(payload.state || '').toLowerCase()
    if (payload.detail) detail.value = payload.detail
    if (payload.source === 'monitor-mix' || payload.source === 'windows-loopback' || payload.source === 'soundboard-tap' || payload.source === 'synthetic-sine') activeSource.value = payload.source
    if (payload.device) deviceName.value = payload.device
    if (Number.isFinite(payload.sampleRate)) sampleRate.value = Number(payload.sampleRate)
    if (Number.isFinite(payload.channels)) channels.value = Number(payload.channels)
    if (Number.isFinite(payload.frameMilliseconds)) frameMilliseconds.value = Number(payload.frameMilliseconds)
    if (Number.isFinite(payload.encoderBitrateKbps)) encoderBitrateKbps.value = Number(payload.encoderBitrateKbps)
    if (payload.encoderProfile) encoderProfile.value = String(payload.encoderProfile)
    if (payload.mix) {
      if (Number.isFinite(payload.mix.windowsGain)) mixWindowsGainPercent.value = Math.round(Number(payload.mix.windowsGain) * 100)
      if (Number.isFinite(payload.mix.soundboardGain)) mixSoundboardGainPercent.value = Math.round(Number(payload.mix.soundboardGain) * 100)
      if (Number.isFinite(payload.mix.mediaGain)) mixMediaGainPercent.value = Math.round(Number(payload.mix.mediaGain) * 100)
      if (Number.isFinite(payload.mix.voiceGain)) mixVoiceGainPercent.value = Math.round(Number(payload.mix.voiceGain) * 100)
      if (typeof payload.mix.voiceEnabled === 'boolean') mixVoiceEnabled.value = payload.mix.voiceEnabled
      if (typeof payload.mix.mediaDuplicateSuppressed === 'boolean') mixMediaDuplicateSuppressed.value = payload.mix.mediaDuplicateSuppressed
      if (payload.mix.mediaMode === 'windows-output' || payload.mix.mediaMode === 'direct') mixMediaMode.value = payload.mix.mediaMode
      if (Number.isFinite(payload.mix.masterGain)) mixMasterGainPercent.value = Math.round(Number(payload.mix.masterGain) * 100)
    }

    if (serverState === 'connected') {
      phase.value = 'connected'
      // A preserved Windows session can outlive an Android page/peer. If the
      // browser peer was discarded while backgrounded, do not mistake the
      // server's connected state for a usable local media session.
      if (!peerAttached.value && desiredSource.value) scheduleAutoResume(120)
    }
    else if (serverState === 'failed') {
      phase.value = 'failed'
      if (desiredSource.value) scheduleAutoResume(450)
    }
    else if (serverState === 'closed') phase.value = 'stopped'
    else if (serverState && phase.value !== 'connected') phase.value = 'negotiating'
  }

  function initialize() {
    if (!import.meta.client || initialized) return
    initialized = true
    const stored = localStorage.getItem(DESIRED_SOURCE_KEY)
    if (stored === 'monitor-mix' || stored === 'windows-loopback' || stored === 'soundboard-tap' || stored === 'synthetic-sine') {
      desiredSource.value = stored
      selectedSource.value = stored
    }
    subscriptions = [
      remote.subscribeMessage('monitor.spike.answer', onAnswer),
      remote.subscribeMessage('monitor.spike.ice', onServerIce),
      remote.subscribeMessage('monitor.spike.state', onServerState)
    ]
    void remote.refreshInfo()
  }

  async function start(source: MonitorSpikeSource = selectedSource.value, rememberIntent = true) {
    if (!import.meta.client) return false
    initialize()
    if (!remote.isConnected.value) {
      remote.showSnackbar('Connect GrassiMote before starting Remote Monitor.', 'warning')
      return false
    }
    if (!available.value) {
      remote.showSnackbar('This local build does not include the v1.2 WebRTC spike.', 'warning')
      return false
    }

    if (rememberIntent) saveDesiredSource(source)
    resetPeer()
    selectedSource.value = source
    activeSource.value = source
    deviceName.value = ''
    sampleRate.value = 0
    channels.value = 0
    frameMilliseconds.value = 20
    encoderBitrateKbps.value = 0
    encoderProfile.value = source === 'synthetic-sine' ? 'library-default' : 'hq-audio-vbr'
    phase.value = 'negotiating'
    detail.value = source === 'monitor-mix'
      ? 'Preparing the independent Windows + Soundboard + Media Remote Monitor Mix with opt-in My Voice…'
      : source === 'windows-loopback'
        ? 'Preparing a receive-only peer for Windows output capture…'
        : source === 'soundboard-tap'
          ? 'Preparing the ABI-9 native Soundboard source tap…'
          : 'Creating a receive-only browser peer and an Opus offer…'
    playbackBlocked.value = false

    try {
      const nextPeer = new RTCPeerConnection({ iceServers: [] })
      peer = nextPeer
      peerAttached.value = true
      peerConnectionState.value = nextPeer.connectionState
      iceConnectionState.value = nextPeer.iceConnectionState
      let gatheredLocalCandidates = 0

      nextPeer.addTransceiver('audio', { direction: 'recvonly' })
      nextPeer.ontrack = event => {
        const stream = event.streams[0] || new MediaStream([event.track])
        mediaStream.value = stream
        detail.value = 'Audio track received from Windows; waiting for stable playback.'
      }
      nextPeer.onicecandidate = event => {
        if (!event.candidate) return
        gatheredLocalCandidates += 1
        detail.value = `Gathering same-LAN ICE candidate${gatheredLocalCandidates === 1 ? '' : 's'}…`
      }
      nextPeer.onconnectionstatechange = () => {
        peerConnectionState.value = nextPeer.connectionState
        if (nextPeer.connectionState === 'connected') {
          phase.value = 'connected'
          detail.value = source === 'monitor-mix'
            ? 'WebRTC connected. Windows output, Soundboard, and Media are live; My Voice remains opt-in for this phone.'
            : source === 'windows-loopback'
              ? 'WebRTC connected. Windows output is streaming to this device.'
              : source === 'soundboard-tap'
                ? 'WebRTC connected. Trigger a Sound Pad while the Windows engine is running.'
                : 'WebRTC connected. The synthetic Opus tone should be audible.'
          startStats()
        } else if (nextPeer.connectionState === 'failed' || nextPeer.connectionState === 'closed') {
          phase.value = 'failed'
          detail.value = 'WebRTC peer connection ended. GrassiMote will rebuild it automatically when possible.'
          peerAttached.value = false
          stopStats()
          mediaStream.value = null
          if (desiredSource.value) scheduleAutoResume(500)
        }
      }
      nextPeer.oniceconnectionstatechange = () => {
        iceConnectionState.value = nextPeer.iceConnectionState
        if (nextPeer.iceConnectionState === 'failed') {
          phase.value = 'failed'
          detail.value = 'ICE failed. GrassiMote will retry this same-LAN monitor when the app becomes active again.'
          if (desiredSource.value) scheduleAutoResume(500)
        }
      }

      const offer = await nextPeer.createOffer()
      await nextPeer.setLocalDescription(offer)
      detail.value = 'Gathering the phone LAN candidate before sending the offer…'
      await waitForIceGatheringComplete(nextPeer)

      const sdp = nextPeer.localDescription?.sdp || offer.sdp || ''
      if (!sdp) throw new Error('Browser did not generate SDP.')
      if (!/a=candidate:/i.test(sdp)) {
        throw new Error('Android did not expose a same-LAN ICE candidate for this WebRTC test.')
      }

      detail.value = 'Sending the complete Opus offer to Windows…'
      const offerPayload = source === 'monitor-mix'
        ? {
            sdp,
            source,
            windowsGain: mixWindowsGainPercent.value / 100,
            soundboardGain: mixSoundboardGainPercent.value / 100,
            mediaGain: mixMediaGainPercent.value / 100,
            voiceGain: mixVoiceGainPercent.value / 100,
            voiceEnabled: mixVoiceEnabled.value,
            masterGain: mixMasterGainPercent.value / 100
          }
        : { sdp, source }
      if (!remote.sendCommand('monitor.spike.offer', offerPayload)) throw new Error('Could not send the WebRTC offer over Remote WSS.')
      return true
    } catch (error) {
      phase.value = 'failed'
      detail.value = error instanceof Error ? error.message : String(error)
      resetPeer()
      return false
    }
  }

  function localPeerIsUsable() {
    if (!peer || !peerAttached.value) return false
    if (peer.connectionState === 'new' || peer.connectionState === 'connecting') return true
    if (peer.connectionState !== 'connected') return false
    const stream = mediaStream.value
    return Boolean(stream && stream.getAudioTracks().some(track => track.readyState === 'live'))
  }

  function scheduleAutoResume(delay = 250) {
    if (!import.meta.client || !desiredSource.value) return
    if (autoResumeTimer) clearTimeout(autoResumeTimer)
    autoResumeTimer = setTimeout(() => {
      autoResumeTimer = null
      void resumeIfDesired()
    }, delay)
  }

  async function resumeIfDesired(force = false) {
    if (!import.meta.client || resumeInFlight || !desiredSource.value) return false
    if (!remote.isConnected.value || !available.value) return false
    if (!force && localPeerIsUsable() && active.value) return false
    resumeInFlight = true
    try {
      return await start(desiredSource.value, false)
    } finally {
      resumeInFlight = false
    }
  }

  function sendMixSettings() {
    if (!remote.isConnected.value || !active.value || activeSource.value !== 'monitor-mix') return
    remote.sendCommand('monitor.spike.mix.set', {
      windowsGain: mixWindowsGainPercent.value / 100,
      soundboardGain: mixSoundboardGainPercent.value / 100,
      mediaGain: mixMediaGainPercent.value / 100,
      voiceGain: mixVoiceGainPercent.value / 100,
      voiceEnabled: mixVoiceEnabled.value,
      masterGain: mixMasterGainPercent.value / 100
    })
  }

  function scheduleMixSettings() {
    if (mixUpdateTimer) clearTimeout(mixUpdateTimer)
    mixUpdateTimer = setTimeout(() => {
      mixUpdateTimer = null
      sendMixSettings()
    }, 45)
  }

  function setMixGain(kind: MonitorMixGainKey, value: number) {
    const normalized = Math.max(0, Math.min(100, Number.isFinite(Number(value)) ? Number(value) : 0))
    if (kind === 'windows') mixWindowsGainPercent.value = normalized
    else if (kind === 'soundboard') mixSoundboardGainPercent.value = normalized
    else if (kind === 'media') mixMediaGainPercent.value = normalized
    else if (kind === 'voice') mixVoiceGainPercent.value = normalized
    else mixMasterGainPercent.value = normalized
    scheduleMixSettings()
  }

  function setMixVoiceEnabled(enabled: boolean) {
    const next = Boolean(enabled)
    mixVoiceEnabled.value = next
    if (mixUpdateTimer) { clearTimeout(mixUpdateTimer); mixUpdateTimer = null }
    sendMixSettings()

    if (next && import.meta.client && !localStorage.getItem(VOICE_HEADPHONES_HINT_KEY)) {
      localStorage.setItem(VOICE_HEADPHONES_HINT_KEY, '1')
      remote.showSnackbar('Headphones recommended for My Voice to prevent feedback.', 'warning')
    }
  }

  function markPlaybackBlocked(blocked: boolean) {
    playbackBlocked.value = blocked
  }

  function setMonitorMuted(muted: boolean) {
    monitorMuted.value = muted
  }

  function toggleMonitorMuted() {
    monitorMuted.value = !monitorMuted.value
  }

  function registerPlaybackInvoker(invoker: (() => Promise<boolean>) | null) {
    playbackInvoker = invoker
  }

  async function requestPlayback() {
    if (!playbackInvoker) return false
    return await playbackInvoker()
  }

  function stop(sendSignal = true, clearIntent = true) {
    if (autoResumeTimer) { clearTimeout(autoResumeTimer); autoResumeTimer = null }
    if (clearIntent) {
      saveDesiredSource(null)
      // My Voice is safety-sensitive self-monitoring. An explicit stop resets
      // the next manual session to OFF; automatic recovery never calls this
      // clear-intent path, so a live session can still recover transparently.
      mixVoiceEnabled.value = false
    }
    if (sendSignal && remote.isConnected.value && active.value) remote.sendCommand('monitor.spike.stop')
    resetPeer()
    phase.value = 'stopped'
    detail.value = 'Remote Monitor stopped.'
  }

  function dispose() {
    stop(true, true)
    for (const unsubscribe of subscriptions.splice(0)) unsubscribe()
    initialized = false
    playbackInvoker = null
  }

  return {
    phase,
    detail,
    mediaStream,
    playbackBlocked,
    selectedSource,
    activeSource,
    desiredSource,
    monitorMuted,
    deviceName,
    sampleRate,
    channels,
    frameMilliseconds,
    peerConnectionState,
    iceConnectionState,
    inboundBitrateKbps,
    jitterMs,
    packetsLost,
    codecName,
    encoderBitrateKbps,
    encoderProfile,
    mixWindowsGainPercent,
    mixSoundboardGainPercent,
    mixMediaGainPercent,
    mixVoiceGainPercent,
    mixVoiceEnabled,
    mixMediaDuplicateSuppressed,
    mixMediaMode,
    mixMasterGainPercent,
    peerAttached,
    available,
    active,
    wantsActive,
    initialize,
    start,
    resumeIfDesired,
    scheduleAutoResume,
    stop,
    dispose,
    setMixGain,
    setMixVoiceEnabled,
    markPlaybackBlocked,
    setMonitorMuted,
    toggleMonitorMuted,
    registerPlaybackInvoker,
    requestPlayback
  }
}
