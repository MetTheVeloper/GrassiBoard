import { computed, ref, watch } from 'vue'
import type { RemoteEnvelope } from '~/types/remote'

type PhoneMicPhase = 'idle' | 'requesting' | 'negotiating' | 'connected' | 'failed' | 'stopped'
export type PhoneMicCaptureMode = 'communication' | 'clean'

interface AnswerPayload { sdp?: string }
interface IcePayload {
  candidate?: string
  sdpMid?: string | null
  sdpMLineIndex?: number | null
  usernameFragment?: string | null
}
interface StatePayload {
  state?: string
  detail?: string | null
  codec?: string | null
  sampleRate?: number
  channels?: number
  frameMilliseconds?: number
  rtpPackets?: number
  decodedFrames?: number
  decodedSamples?: number
  decodeErrors?: number
  rmsDbfs?: number
  peakDbfs?: number
  nativeAbi?: number
  routeRequested?: boolean
  routedToAudioEngine?: boolean
  nativeRequestedSourceMode?: number
  nativeSourceMode?: number
  jitterFillFrames?: number
  jitterTargetFrames?: number
  jitterDroppedFrames?: number
  bridgeUnderruns?: number
  nativeShortWrites?: number
  driftCorrection?: string
  nativeRemoteFillFrames?: number
  nativeRemoteCapacityFrames?: number
  nativeRemotePushedFrames?: number
  nativeRemoteConsumedFrames?: number
  nativeRemoteUnderrunFrames?: number
  nativeRemoteOverrunFrames?: number
}

let peer: RTCPeerConnection | null = null
let stream: MediaStream | null = null
let initialized = false
let remoteDescriptionReady = false
let pendingServerIce: RTCIceCandidateInit[] = []
let negotiateInFlight = false
let desiredActive = false
let recoveryTimer: ReturnType<typeof setTimeout> | null = null

const phase = ref<PhoneMicPhase>('idle')
const detail = ref('Gate 2 ready. Start Phone Mic, then explicitly route it to the Audio Engine.')
const captureMode = ref<PhoneMicCaptureMode>('communication')
const permissionState = ref<'idle' | 'granted' | 'denied' | 'unavailable'>('idle')
const trackState = ref<'none' | 'live' | 'ended'>('none')
const peerConnectionState = ref('new')
const iceConnectionState = ref('new')
const codecName = ref('')
const sampleRate = ref(0)
const channels = ref(0)
const frameMilliseconds = ref(0)
const rtpPackets = ref(0)
const decodedFrames = ref(0)
const decodedSamples = ref(0)
const decodeErrors = ref(0)
const rmsDbfs = ref(-96)
const peakDbfs = ref(-96)
const nativeAbi = ref(10)
const routeRequested = ref(false)
const routedToAudioEngine = ref(false)
const nativeRequestedSourceMode = ref(0)
const nativeSourceMode = ref(0)
const jitterFillFrames = ref(0)
const jitterTargetFrames = ref(1440)
const jitterDroppedFrames = ref(0)
const bridgeUnderruns = ref(0)
const nativeShortWrites = ref(0)
const driftCorrection = ref('neutral')
const nativeRemoteFillFrames = ref(0)
const nativeRemoteCapacityFrames = ref(0)
const nativeRemotePushedFrames = ref(0)
const nativeRemoteConsumedFrames = ref(0)
const nativeRemoteUnderrunFrames = ref(0)
const nativeRemoteOverrunFrames = ref(0)
const peerAttached = ref(false)

async function waitForIce(connection: RTCPeerConnection, timeoutMs = 3500) {
  if (connection.iceGatheringState === 'complete') return
  await new Promise<void>(resolve => {
    let done = false
    let timer: ReturnType<typeof setTimeout> | null = null
    const finish = () => {
      if (done) return
      done = true
      connection.removeEventListener('icegatheringstatechange', changed)
      if (timer) clearTimeout(timer)
      resolve()
    }
    const changed = () => { if (connection.iceGatheringState === 'complete') finish() }
    connection.addEventListener('icegatheringstatechange', changed)
    timer = setTimeout(finish, timeoutMs)
    changed()
  })
}

function hasLiveTrack() {
  return Boolean(stream?.getAudioTracks().some(track => track.readyState === 'live'))
}

function clearRecoveryTimer() {
  if (!recoveryTimer) return
  clearTimeout(recoveryTimer)
  recoveryTimer = null
}

export function useRemotePhoneMicSpike() {
  const remote = useRemoteConnection()
  const available = computed(() => Boolean(remote.serverInfo.value?.remotePhoneMicSpikeAvailable))
  const active = computed(() => peerAttached.value && ['negotiating', 'connected'].includes(phase.value))

  function scheduleRecovery(delayMs = 500) {
    if (!import.meta.client || !desiredActive || !hasLiveTrack()) return
    if (document.visibilityState !== 'visible') return

    clearRecoveryTimer()
    recoveryTimer = setTimeout(() => {
      recoveryTimer = null
      void recoverIfPossible()
    }, delayMs)
  }

  function resetPeer(stopTracks: boolean) {
    peerAttached.value = false
    if (peer) {
      peer.onicecandidate = null
      peer.onconnectionstatechange = null
      peer.oniceconnectionstatechange = null
      try { peer.close() } catch { }
    }
    peer = null
    remoteDescriptionReady = false
    pendingServerIce = []
    peerConnectionState.value = 'closed'
    iceConnectionState.value = 'closed'

    if (stopTracks && stream) {
      for (const track of stream.getTracks()) {
        try { track.stop() } catch { }
      }
      stream = null
      trackState.value = 'ended'
    }
  }

  async function addServerIce(candidate: RTCIceCandidateInit) {
    if (!peer) return
    if (!remoteDescriptionReady) {
      pendingServerIce.push(candidate)
      return
    }
    try { await peer.addIceCandidate(candidate) }
    catch (error) {
      phase.value = 'failed'
      detail.value = `Could not apply Windows ICE: ${error instanceof Error ? error.message : String(error)}`
    }
  }

  async function onAnswer(message: RemoteEnvelope<AnswerPayload>) {
    if (!peer || !message.payload?.sdp) return
    try {
      await peer.setRemoteDescription({ type: 'answer', sdp: message.payload.sdp })
      remoteDescriptionReady = true
      detail.value = 'Windows answer received; completing ICE/DTLS.'
      for (const candidate of pendingServerIce.splice(0)) await addServerIce(candidate)
    } catch (error) {
      phase.value = 'failed'
      detail.value = `Browser rejected Windows answer: ${error instanceof Error ? error.message : String(error)}`
      resetPeer(false)
    }
  }

  async function onIce(message: RemoteEnvelope<IcePayload>) {
    const p = message.payload
    if (!p?.candidate) return
    await addServerIce({
      candidate: p.candidate,
      sdpMid: p.sdpMid ?? undefined,
      sdpMLineIndex: p.sdpMLineIndex ?? undefined,
      usernameFragment: p.usernameFragment ?? undefined
    })
  }

  function onState(message: RemoteEnvelope<StatePayload>) {
    const p = message.payload || {}
    const state = String(p.state || '').toLowerCase()
    if (p.detail) detail.value = p.detail
    if (p.codec) codecName.value = String(p.codec).toUpperCase()
    if (Number.isFinite(p.sampleRate)) sampleRate.value = Number(p.sampleRate)
    if (Number.isFinite(p.channels)) channels.value = Number(p.channels)
    if (Number.isFinite(p.frameMilliseconds)) frameMilliseconds.value = Number(p.frameMilliseconds)
    if (Number.isFinite(p.rtpPackets)) rtpPackets.value = Number(p.rtpPackets)
    if (Number.isFinite(p.decodedFrames)) decodedFrames.value = Number(p.decodedFrames)
    if (Number.isFinite(p.decodedSamples)) decodedSamples.value = Number(p.decodedSamples)
    if (Number.isFinite(p.decodeErrors)) decodeErrors.value = Number(p.decodeErrors)
    if (Number.isFinite(p.rmsDbfs)) rmsDbfs.value = Number(p.rmsDbfs)
    if (Number.isFinite(p.peakDbfs)) peakDbfs.value = Number(p.peakDbfs)
    if (Number.isFinite(p.nativeAbi)) nativeAbi.value = Number(p.nativeAbi)
    if (typeof p.routeRequested === 'boolean') routeRequested.value = p.routeRequested
    if (typeof p.routedToAudioEngine === 'boolean') routedToAudioEngine.value = p.routedToAudioEngine
    if (Number.isFinite(p.nativeRequestedSourceMode)) nativeRequestedSourceMode.value = Number(p.nativeRequestedSourceMode)
    if (Number.isFinite(p.nativeSourceMode)) nativeSourceMode.value = Number(p.nativeSourceMode)
    if (Number.isFinite(p.jitterFillFrames)) jitterFillFrames.value = Number(p.jitterFillFrames)
    if (Number.isFinite(p.jitterTargetFrames)) jitterTargetFrames.value = Number(p.jitterTargetFrames)
    if (Number.isFinite(p.jitterDroppedFrames)) jitterDroppedFrames.value = Number(p.jitterDroppedFrames)
    if (Number.isFinite(p.bridgeUnderruns)) bridgeUnderruns.value = Number(p.bridgeUnderruns)
    if (Number.isFinite(p.nativeShortWrites)) nativeShortWrites.value = Number(p.nativeShortWrites)
    if (p.driftCorrection) driftCorrection.value = String(p.driftCorrection)
    if (Number.isFinite(p.nativeRemoteFillFrames)) nativeRemoteFillFrames.value = Number(p.nativeRemoteFillFrames)
    if (Number.isFinite(p.nativeRemoteCapacityFrames)) nativeRemoteCapacityFrames.value = Number(p.nativeRemoteCapacityFrames)
    if (Number.isFinite(p.nativeRemotePushedFrames)) nativeRemotePushedFrames.value = Number(p.nativeRemotePushedFrames)
    if (Number.isFinite(p.nativeRemoteConsumedFrames)) nativeRemoteConsumedFrames.value = Number(p.nativeRemoteConsumedFrames)
    if (Number.isFinite(p.nativeRemoteUnderrunFrames)) nativeRemoteUnderrunFrames.value = Number(p.nativeRemoteUnderrunFrames)
    if (Number.isFinite(p.nativeRemoteOverrunFrames)) nativeRemoteOverrunFrames.value = Number(p.nativeRemoteOverrunFrames)

    if (state === 'connected') phase.value = 'connected'
    else if (state === 'failed') phase.value = 'failed'
    else if (state === 'closed') phase.value = 'stopped'
    else if (state && phase.value !== 'connected') phase.value = 'negotiating'
  }

  async function negotiateExistingStream() {
    if (!import.meta.client || !stream || !hasLiveTrack() || negotiateInFlight || !remote.isConnected.value)
      return false

    negotiateInFlight = true
    resetPeer(false)
    phase.value = 'negotiating'
    detail.value = 'Creating send-only same-LAN microphone peer…'

    try {
      const pc = new RTCPeerConnection({ iceServers: [] })
      peer = pc
      peerAttached.value = true
      peerConnectionState.value = pc.connectionState
      iceConnectionState.value = pc.iceConnectionState

      for (const track of stream.getAudioTracks()) pc.addTrack(track, stream)

      pc.onicecandidate = event => {
        if (event.candidate) detail.value = 'Gathering Android LAN ICE candidate…'
      }
      pc.onconnectionstatechange = () => {
        if (peer !== pc) return
        peerConnectionState.value = pc.connectionState
        if (pc.connectionState === 'connected') {
          clearRecoveryTimer()
          phase.value = 'connected'
          detail.value = 'WebRTC connected. Windows should count/decode microphone RTP now.'
        } else if (['failed', 'disconnected', 'closed'].includes(pc.connectionState)) {
          peerAttached.value = false
          if (desiredActive && hasLiveTrack()) {
            phase.value = 'failed'
            detail.value = 'WebRTC peer ended; live mic track is preserved and recovery is scheduled.'
            scheduleRecovery(pc.connectionState === 'disconnected' ? 1200 : 350)
          }
        }
      }
      pc.oniceconnectionstatechange = () => {
        if (peer !== pc) return
        iceConnectionState.value = pc.iceConnectionState
        if (pc.iceConnectionState === 'failed' || pc.iceConnectionState === 'disconnected') {
          phase.value = 'failed'
          detail.value = 'ICE disconnected; live mic track is preserved and recovery is scheduled.'
          scheduleRecovery(pc.iceConnectionState === 'disconnected' ? 1200 : 350)
        }
      }

      const offer = await pc.createOffer()
      await pc.setLocalDescription(offer)
      await waitForIce(pc)
      const sdp = pc.localDescription?.sdp || offer.sdp
      if (!sdp) throw new Error('Browser produced no SDP offer.')

      if (!remote.sendCommand('mic.spike.offer', { sdp, captureMode: captureMode.value }))
        throw new Error('Authenticated WSS is not ready.')

      detail.value = 'Microphone offer sent; waiting for Windows answer.'
      return true
    } catch (error) {
      phase.value = 'failed'
      detail.value = `Phone Mic negotiation failed: ${error instanceof Error ? error.message : String(error)}`
      resetPeer(false)
      return false
    } finally {
      negotiateInFlight = false
    }
  }

  async function start(mode: PhoneMicCaptureMode = captureMode.value) {
    if (!import.meta.client) return false
    initialize()

    if (!window.isSecureContext) {
      permissionState.value = 'unavailable'
      phase.value = 'failed'
      detail.value = 'Phone microphone capture requires trusted GrassiMote HTTPS.'
      return false
    }
    if (!navigator.mediaDevices?.getUserMedia) {
      permissionState.value = 'unavailable'
      phase.value = 'failed'
      detail.value = 'This browser does not expose getUserMedia.'
      return false
    }
    if (!remote.isConnected.value) {
      remote.showSnackbar('Connect paired GrassiMote before enabling Phone Mic.', 'warning')
      return false
    }

    await remote.refreshInfo()
    if (!available.value) {
      remote.showSnackbar('Windows build does not expose v1.3 Phone Mic Gate 2.', 'warning')
      return false
    }

    captureMode.value = mode
    desiredActive = true
    clearRecoveryTimer()
    resetPeer(true)
    phase.value = 'requesting'
    detail.value = 'Requesting Android microphone permission…'

    const audio: MediaTrackConstraints = mode === 'communication'
      ? { echoCancellation: true, noiseSuppression: true, autoGainControl: false, channelCount: 1 }
      : { echoCancellation: false, noiseSuppression: false, autoGainControl: false, channelCount: 1 }

    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio, video: false })
      permissionState.value = 'granted'
      trackState.value = hasLiveTrack() ? 'live' : 'ended'

      for (const track of stream.getAudioTracks()) {
        track.onended = () => {
          trackState.value = 'ended'
          desiredActive = false
          routeRequested.value = false
          routedToAudioEngine.value = false
          nativeRequestedSourceMode.value = 0
          nativeSourceMode.value = 0
          if (remote.isConnected.value) remote.sendCommand('mic.spike.stop')
          phase.value = 'stopped'
          detail.value = 'Android ended the mic track. Windows Mic is restored; tap Enable Phone Mic to request it again.'
          resetPeer(false)
        }
      }

      return await negotiateExistingStream()
    } catch (error: any) {
      desiredActive = false
      permissionState.value = error?.name === 'NotAllowedError' ? 'denied' : 'unavailable'
      phase.value = 'failed'
      detail.value = error?.name === 'NotAllowedError'
        ? 'Microphone permission denied. Control and Monitor remain available.'
        : `Could not open Android microphone: ${error instanceof Error ? error.message : String(error)}`
      resetPeer(true)
      return false
    }
  }

  function setRoute(enabled: boolean) {
    if (!remote.isConnected.value) return false
    if (enabled && (phase.value !== 'connected' || !desiredActive || !hasLiveTrack())) return false
    return remote.sendCommand('mic.spike.route.set', { enabled })
  }

  async function stop() {
    desiredActive = false
    clearRecoveryTimer()
    routeRequested.value = false
    routedToAudioEngine.value = false
    nativeRequestedSourceMode.value = 0
    nativeSourceMode.value = 0
    if (remote.isConnected.value) remote.sendCommand('mic.spike.stop')
    resetPeer(true)
    phase.value = 'stopped'
    detail.value = 'Phone microphone stopped.'
  }

  async function recoverIfPossible() {
    if (!desiredActive || !hasLiveTrack() || negotiateInFlight || !remote.isConnected.value) return
    if (peer && ['connected', 'connecting'].includes(peer.connectionState)) return
    await negotiateExistingStream()
  }

  function initialize() {
    if (!import.meta.client || initialized) return
    initialized = true
    remote.subscribeMessage('mic.spike.answer', onAnswer)
    remote.subscribeMessage('mic.spike.ice', onIce)
    remote.subscribeMessage('mic.spike.state', onState)
    void remote.refreshInfo()

    watch(() => remote.isConnected.value, connected => {
      if (connected) scheduleRecovery(150)
    })
    document.addEventListener('visibilitychange', () => {
      // Android browsers can report the old peer as connected for a short moment
      // immediately after foregrounding, then transition it to disconnected/failed.
      // Delay recovery so that transition is observed before deciding whether to renegotiate.
      if (document.visibilityState === 'visible') scheduleRecovery(450)
    })
    window.addEventListener('pageshow', () => { scheduleRecovery(450) })
    window.addEventListener('focus', () => { scheduleRecovery(550) })
    window.addEventListener('online', () => { scheduleRecovery(350) })
  }

  return {
    phase, detail, captureMode, permissionState, trackState,
    peerConnectionState, iceConnectionState, codecName, sampleRate, channels,
    frameMilliseconds, rtpPackets, decodedFrames, decodedSamples, decodeErrors,
    rmsDbfs, peakDbfs, nativeAbi, routeRequested, routedToAudioEngine, nativeRequestedSourceMode, nativeSourceMode,
    jitterFillFrames, jitterTargetFrames, jitterDroppedFrames, bridgeUnderruns,
    nativeShortWrites, driftCorrection, nativeRemoteFillFrames, nativeRemoteCapacityFrames,
    nativeRemotePushedFrames, nativeRemoteConsumedFrames,
    nativeRemoteUnderrunFrames, nativeRemoteOverrunFrames,
    available, active, initialize, start, stop, setRoute, recoverIfPossible
  }
}
