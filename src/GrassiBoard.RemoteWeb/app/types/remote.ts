export type ConnectionState = 'idle' | 'pairing' | 'connecting' | 'authenticating' | 'connected' | 'unauthorized' | 'disconnected'

export interface RemotePad {
  id: string
  title: string
  ready: boolean
  hasError: boolean
  playing: boolean
  loop: boolean
}

export interface RemotePreset {
  id: string
  name: string
}

export interface RemoteStateSnapshot {
  revision: number
  profileName: string
  engine: {
    state: string
    running: boolean
    nativeReady: boolean
    busy: boolean
    status: string
  }
  voice: {
    enabled: boolean
    pitch: number
    finePitch: number
    formant: number
    preserveVocalCharacter: boolean
  }
  mixer: {
    micGain: number
    soundboardGain: number
    masterGain: number
  }
  media: {
    hasMedia: boolean
    displayName: string
    playing: boolean
    position: number
    duration: number
    volume: number
    monitorEnabled: boolean
    sendEnabled: boolean
    hasError: boolean
  }
  meters: {
    microphone: number
    soundboard: number
    master: number
    microphoneDb: string
    soundboardDb: string
    masterDb: string
  }
  microphoneMuted: boolean
  pads: RemotePad[]
  presets: RemotePreset[]
}

export interface RemoteEnvelope<T = unknown> {
  protocolVersion: number
  type: string
  messageId?: string
  revision?: number
  payload: T
}

export interface PairResponse {
  clientId: string
  clientToken: string
  deviceName: string
}
