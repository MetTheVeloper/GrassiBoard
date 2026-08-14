<script setup lang="ts">
import type { PhoneMicCaptureMode } from '~/composables/useRemotePhoneMicSpike'

const remote = useRemoteConnection()
const mic = useRemotePhoneMicSpike()

onMounted(() => {
  remote.initialize()
  mic.initialize()
})

const secureContext = computed(() => import.meta.client ? window.isSecureContext : false)
const engineRunning = computed(() => remote.snapshot.value?.engine.running ?? false)
const captureConnected = computed(() => mic.phase.value === 'connected')

const meterPercent = computed(() => {
  const db = Math.max(-60, Math.min(0, mic.peakDbfs.value))
  return Math.round(((db + 60) / 60) * 100)
})

const captureLabel = computed(() => {
  if (mic.phase.value === 'connected') return 'LIVE'
  if (['requesting-permission', 'negotiating'].includes(mic.phase.value)) return 'CONNECTING'
  if (mic.phase.value === 'failed') return 'FAILED'
  return 'OFF'
})

const captureTone = computed<'neutral' | 'primary' | 'success' | 'warning' | 'danger'>(() => {
  if (captureLabel.value === 'LIVE') return 'success'
  if (captureLabel.value === 'CONNECTING') return 'primary'
  if (captureLabel.value === 'FAILED') return 'danger'
  return 'neutral'
})

const phoneInProgram = computed(() => mic.routedToAudioEngine.value)
const programLabel = computed(() => {
  if (phoneInProgram.value) return 'PHONE MIC'
  if (mic.routeRequested.value) return 'BUFFERING'
  return 'WINDOWS MIC'
})

const canRoutePhone = computed(() =>
  engineRunning.value &&
  captureConnected.value &&
  !mic.routeRequested.value &&
  !phoneInProgram.value
)

function setMode(mode: PhoneMicCaptureMode) {
  mic.captureMode.value = mode
}

function startEngine() {
  if (remote.sendCommand('engine.start')) remote.vibrate([12, 18, 12])
}

function routePhoneMic() {
  mic.setRoute(true)
}

function returnToWindowsMic() {
  mic.setRoute(false)
}
</script>

<template>
  <main class="mic-page">
    <header class="mic-hero">
      <div>
        <p class="eyebrow">GRASSIMOTE · PHONE INPUT</p>
        <h1>Mic</h1>
      </div>

      <GbStatusChip
        :label="phoneInProgram ? 'PHONE LIVE' : 'WINDOWS'"
        :tone="phoneInProgram ? 'success' : 'neutral'"
        :icon="phoneInProgram ? 'mic' : 'desktop'"
        :pulse="phoneInProgram"
      />
    </header>

    <section v-if="!secureContext" class="mic-alert mic-alert--danger">
      <GbIcon name="warning" :size="20" />
      <div>
        <strong>Trusted HTTPS required</strong>
        <span>Open the installed GrassiMote PWA from its trusted HTTPS origin.</span>
      </div>
    </section>

    <section v-if="!engineRunning" class="mic-alert mic-alert--engine">
      <GbIcon name="power" :size="21" />
      <div>
        <strong>Audio Engine is stopped</strong>
        <span>Start it before routing Phone Mic into Program.</span>
      </div>
      <GbButton variant="filled" icon="power" @click="startEngine">Start Audio Engine</GbButton>
    </section>

    <section class="mic-card mic-capture-card">
      <div class="mic-card__head">
        <div>
          <small>CAPTURE</small>
          <h2>Phone microphone</h2>
        </div>
        <GbStatusChip :label="captureLabel" :tone="captureTone" :icon="captureConnected ? 'check' : 'mic'" />
      </div>

      <div class="mic-meter" :class="{ 'mic-meter--live': captureConnected }" aria-label="Phone microphone level">
        <i :style="{ width: `${meterPercent}%` }" />
      </div>

      <div class="mic-profiles" aria-label="Capture profile">
        <button
          type="button"
          class="mic-profile"
          :class="{ 'mic-profile--active': mic.captureMode.value === 'communication' }"
          :disabled="mic.active.value"
          @click="setMode('communication')"
        >
          <GbIcon name="mic" :size="20" />
          <span>
            <strong>Communication</strong>
            <small>AEC + noise suppression</small>
          </span>
        </button>

        <button
          type="button"
          class="mic-profile"
          :class="{ 'mic-profile--active': mic.captureMode.value === 'clean' }"
          :disabled="mic.active.value"
          @click="setMode('clean')"
        >
          <GbIcon name="headphones" :size="20" />
          <span>
            <strong>Clean / headset</strong>
            <small>Browser DSP minimized</small>
          </span>
        </button>
      </div>

      <div class="mic-actions">
        <GbButton
          variant="filled"
          icon="mic"
          :disabled="!remote.isConnected.value || !secureContext || mic.active.value"
          @click="mic.start(mic.captureMode.value)"
        >
          Enable Phone Mic
        </GbButton>

        <GbButton
          variant="outlined"
          icon="stop"
          :disabled="!mic.active.value && mic.trackState.value !== 'live'"
          @click="mic.stop()"
        >
          Stop
        </GbButton>
      </div>

      <div v-if="captureConnected" class="mic-connection">
        <span>WebRTC</span>
        <strong>OPUS · {{ mic.sampleRate.value }} Hz · {{ mic.channels.value }} ch</strong>
      </div>
      <p v-else-if="mic.phase.value === 'failed'" class="mic-detail mic-detail--error">{{ mic.detail.value }}</p>
    </section>

    <section class="mic-card mic-route-card" :class="{ 'mic-route-card--phone': phoneInProgram }">
      <div class="mic-card__head">
        <div>
          <small>PROGRAM INPUT</small>
          <h2>{{ programLabel }}</h2>
        </div>
        <GbStatusChip
          :label="phoneInProgram ? 'LIVE' : mic.routeRequested.value ? 'BUFFERING' : 'SAFE'"
          :tone="phoneInProgram ? 'success' : mic.routeRequested.value ? 'warning' : 'neutral'"
          :icon="phoneInProgram ? 'output' : 'desktop'"
          :pulse="mic.routeRequested.value"
        />
      </div>

      <div class="source-switch" aria-label="Program microphone source">
        <button
          type="button"
          class="source-option"
          :class="{ 'source-option--active': !phoneInProgram && !mic.routeRequested.value }"
          :disabled="!mic.routeRequested.value && !phoneInProgram"
          @click="returnToWindowsMic"
        >
          <GbIcon name="desktop" :size="22" />
          <span>
            <strong>Windows Mic</strong>
            <small>Physical microphone</small>
          </span>
        </button>

        <button
          type="button"
          class="source-option source-option--phone"
          :class="{ 'source-option--active': phoneInProgram || mic.routeRequested.value }"
          :disabled="!canRoutePhone"
          @click="routePhoneMic"
        >
          <GbIcon name="mic" :size="22" />
          <span>
            <strong>Phone Mic</strong>
            <small>{{ mic.routeRequested.value && !phoneInProgram ? 'Buffering…' : 'Route Phone Mic' }}</small>
          </span>
        </button>
      </div>

      <div class="signal-path">
        <span>{{ phoneInProgram ? 'Phone Mic' : 'Windows Mic' }}</span>
        <i>→</i>
        <span>Pitch / Formant</span>
        <i>→</i>
        <span>Mixer</span>
        <i>→</i>
        <span>VB-CABLE</span>
      </div>

      <p v-if="phoneInProgram" class="mic-route-note mic-route-note--live">
        Phone Mic is live in Program.
      </p>
      <p v-else-if="mic.routeRequested.value" class="mic-route-note">
        Filling the realtime buffer before switching from Windows Mic.
      </p>
      <p v-else-if="!captureConnected" class="mic-route-note">
        Enable Phone Mic first. Program stays safely on Windows Mic until you explicitly switch it.
      </p>
    </section>

    <details class="mic-diagnostics">
      <summary>
        <span>
          <small>ADVANCED</small>
          <strong>Diagnostics</strong>
        </span>
        <span class="mic-diagnostics__summary">
          ABI {{ mic.nativeAbi.value }}
          · {{ mic.decodeErrors.value === 0 ? 'clean' : `${mic.decodeErrors.value} errors` }}
        </span>
      </summary>

      <div class="diag-grid">
        <div><span>Track</span><strong>{{ mic.trackState.value }}</strong></div>
        <div><span>Peer</span><strong>{{ mic.peerConnectionState.value }}</strong></div>
        <div><span>ICE</span><strong>{{ mic.iceConnectionState.value }}</strong></div>
        <div><span>Codec</span><strong>{{ mic.codecName.value || '—' }}</strong></div>
        <div><span>Format</span><strong>{{ mic.sampleRate.value || '—' }} Hz · {{ mic.channels.value || '—' }} ch</strong></div>
        <div><span>Frame</span><strong>{{ mic.frameMilliseconds.value || '—' }} ms</strong></div>
        <div><span>RTP packets</span><strong>{{ mic.rtpPackets.value }}</strong></div>
        <div><span>Decoded frames</span><strong>{{ mic.decodedFrames.value }}</strong></div>
        <div><span>Decode errors</span><strong>{{ mic.decodeErrors.value }}</strong></div>
        <div><span>Jitter</span><strong>{{ mic.jitterFillFrames.value }} / {{ mic.jitterTargetFrames.value }} fr</strong></div>
        <div><span>Drift</span><strong>{{ mic.driftCorrection.value }}</strong></div>
        <div><span>Bridge underruns</span><strong>{{ mic.bridgeUnderruns.value }}</strong></div>
        <div><span>Jitter drops</span><strong>{{ mic.jitterDroppedFrames.value }}</strong></div>
        <div><span>Native request</span><strong>{{ mic.nativeRequestedSourceMode.value === 1 ? 'PHONE' : 'WINDOWS' }}</strong></div>
        <div><span>Native source</span><strong>{{ mic.nativeSourceMode.value === 1 ? 'PHONE' : 'WINDOWS' }}</strong></div>
        <div><span>Native fill</span><strong>{{ mic.nativeRemoteFillFrames.value }} / {{ mic.nativeRemoteCapacityFrames.value }} fr</strong></div>
        <div><span>Native underrun</span><strong>{{ mic.nativeRemoteUnderrunFrames.value }}</strong></div>
        <div><span>Native overrun</span><strong>{{ mic.nativeRemoteOverrunFrames.value }}</strong></div>
        <div><span>Short writes</span><strong>{{ mic.nativeShortWrites.value }}</strong></div>
        <div><span>RMS</span><strong>{{ mic.rmsDbfs.value.toFixed(1) }} dBFS</strong></div>
        <div><span>Peak</span><strong>{{ mic.peakDbfs.value.toFixed(1) }} dBFS</strong></div>
      </div>

      <p class="diag-foot">
        v1.3 · ABI {{ mic.nativeAbi.value }} · Routed to Audio Engine:
        <strong>{{ phoneInProgram ? 'YES' : 'NO' }}</strong>
      </p>
    </details>
  </main>
</template>

<style scoped>
.mic-page {
  width: min(720px, 100%);
  margin: auto;
  padding: 10px 2px 24px;
  display: grid;
  gap: 12px;
}

.mic-hero {
  min-height: 82px;
  padding: 8px 4px 2px;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 12px;
}
.mic-hero h1 {
  margin: 3px 0 0;
  font-size: clamp(2.4rem, 11vw, 3.8rem);
  line-height: .9;
  letter-spacing: -.065em;
}

.mic-alert,
.mic-card,
.mic-diagnostics {
  border: 1px solid var(--gb-outline-variant);
  border-radius: var(--gb-radius-lg);
  background: var(--gb-surface-container-low);
}
.mic-alert {
  padding: 12px 13px;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
}
.mic-alert > div { display: grid; gap: 2px; }
.mic-alert strong { font-size: .83rem; }
.mic-alert span { color: var(--gb-on-surface-variant); font-size: .68rem; line-height: 1.35; }
.mic-alert--danger { border-color: rgba(255, 113, 135, .35); }
.mic-alert--engine { border-color: rgba(255, 208, 109, .22); }

.mic-card {
  padding: 14px;
  display: grid;
  gap: 12px;
}
.mic-card__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}
.mic-card__head small {
  color: #7895ac;
  font-size: .61rem;
  font-weight: 850;
  letter-spacing: .13em;
}
.mic-card__head h2 {
  margin: 3px 0 0;
  font-size: 1.08rem;
}

.mic-meter {
  height: 7px;
  overflow: hidden;
  border-radius: 999px;
  background: var(--gb-surface-container-highest);
}
.mic-meter i {
  display: block;
  width: 0;
  height: 100%;
  border-radius: inherit;
  background: var(--gb-primary);
  transition: width 90ms linear;
}
.mic-meter--live i { background: var(--gb-success); }

.mic-profiles,
.source-switch {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 7px;
}
.mic-profile,
.source-option {
  min-width: 0;
  min-height: 68px;
  padding: 10px 11px;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: 9px;
  text-align: left;
  border: 1px solid var(--gb-outline-variant);
  border-radius: 15px;
  background: rgba(255,255,255,.018);
  color: var(--gb-on-surface);
  touch-action: manipulation;
  transition:
    transform var(--gb-motion-instant) var(--gb-ease-standard),
    border-color var(--gb-motion-short) var(--gb-ease-standard),
    background var(--gb-motion-short) var(--gb-ease-standard);
}
.mic-profile:active,
.source-option:active { transform: scale(.98); }
.mic-profile:disabled,
.source-option:disabled { opacity: .52; }
.mic-profile span,
.source-option span { min-width: 0; display: grid; gap: 2px; }
.mic-profile strong,
.source-option strong { font-size: .79rem; }
.mic-profile small,
.source-option small {
  overflow: hidden;
  color: var(--gb-on-surface-variant);
  font-size: .62rem;
  white-space: nowrap;
  text-overflow: ellipsis;
}
.mic-profile--active {
  color: var(--gb-primary);
  border-color: rgba(126, 203, 255, .42);
  background: var(--gb-primary-container);
}
.mic-profile--active small { color: #a9c9df; }

.mic-actions {
  display: grid;
  grid-template-columns: minmax(0, 1.45fr) minmax(0, .75fr);
  gap: 7px;
}
.mic-actions :deep(md-filled-button),
.mic-actions :deep(md-outlined-button) { width: 100%; }

.mic-connection {
  min-height: 32px;
  padding: 7px 9px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  border-radius: 10px;
  background: rgba(255,255,255,.022);
}
.mic-connection span {
  color: var(--gb-on-surface-variant);
  font-size: .61rem;
  text-transform: uppercase;
}
.mic-connection strong { font-size: .68rem; font-variant-numeric: tabular-nums; }
.mic-detail {
  margin: 0;
  color: var(--gb-on-surface-variant);
  font-size: .68rem;
  line-height: 1.4;
}
.mic-detail--error { color: var(--gb-error); }

.mic-route-card--phone {
  border-color: rgba(104, 225, 174, .34);
  background: linear-gradient(145deg, #12332e, #0d252b);
}
.source-option--active {
  border-color: rgba(126, 203, 255, .4);
  background: var(--gb-primary-container);
}
.source-option--phone.source-option--active {
  color: #bdf9dd;
  border-color: rgba(104, 225, 174, .4);
  background: var(--gb-success-container);
}
.source-option--phone.source-option--active small { color: #9bd8be; }

.signal-path {
  min-height: 34px;
  padding: 7px 9px;
  display: flex;
  align-items: center;
  gap: 6px;
  overflow-x: auto;
  border-radius: 10px;
  background: rgba(255,255,255,.022);
  scrollbar-width: none;
}
.signal-path::-webkit-scrollbar { display: none; }
.signal-path span {
  flex: 0 0 auto;
  color: var(--gb-on-surface-variant);
  font-size: .61rem;
  font-weight: 700;
}
.signal-path i {
  color: #587188;
  font-style: normal;
  font-size: .7rem;
}

.mic-route-note {
  margin: 0;
  color: var(--gb-on-surface-variant);
  font-size: .68rem;
  line-height: 1.4;
}
.mic-route-note--live { color: #9bd8be; }

.mic-diagnostics { overflow: hidden; }
.mic-diagnostics summary {
  min-height: 58px;
  padding: 12px 14px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  cursor: pointer;
  list-style: none;
}
.mic-diagnostics summary::-webkit-details-marker { display: none; }
.mic-diagnostics summary > span:first-child { display: grid; gap: 2px; }
.mic-diagnostics summary small {
  color: #7895ac;
  font-size: .59rem;
  font-weight: 850;
  letter-spacing: .12em;
}
.mic-diagnostics summary strong { font-size: .82rem; }
.mic-diagnostics__summary {
  color: var(--gb-on-surface-variant);
  font-size: .64rem;
  font-variant-numeric: tabular-nums;
}
.mic-diagnostics[open] summary { border-bottom: 1px solid var(--gb-outline-variant); }

.diag-grid {
  margin: 12px;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1px;
  overflow: hidden;
  border-radius: 12px;
  background: var(--gb-outline-variant);
}
.diag-grid div {
  min-width: 0;
  padding: 9px 10px;
  display: grid;
  gap: 2px;
  background: var(--gb-surface-container);
}
.diag-grid span {
  color: var(--gb-on-surface-variant);
  font-size: .58rem;
  text-transform: uppercase;
}
.diag-grid strong {
  overflow-wrap: anywhere;
  font-size: .72rem;
  font-variant-numeric: tabular-nums;
}
.diag-foot {
  margin: 0;
  padding: 0 12px 12px;
  color: var(--gb-on-surface-variant);
  font-size: .66rem;
  line-height: 1.4;
}

@media (max-width: 480px) {
  .mic-alert {
    grid-template-columns: auto minmax(0, 1fr);
  }
  .mic-alert :deep(md-filled-button) {
    grid-column: 1 / -1;
    width: 100%;
  }
}
</style>
