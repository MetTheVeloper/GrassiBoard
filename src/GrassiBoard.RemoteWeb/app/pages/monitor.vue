<script setup lang="ts">
const remote = useRemoteConnection()
const monitor = useRemoteMonitorSpike()

const displaySource = computed(() => monitor.active.value ? monitor.activeSource.value : 'monitor-mix')

const phaseLabel = computed(() => {
  switch (monitor.phase.value) {
    case 'negotiating': return 'Connecting'
    case 'connected': return monitor.monitorMuted.value ? 'Muted' : 'Live'
    case 'failed': return 'Failed'
    default: return 'Ready'
  }
})

const phaseTone = computed<'neutral' | 'primary' | 'success' | 'warning' | 'danger'>(() => {
  if (monitor.phase.value === 'connected') return monitor.monitorMuted.value ? 'warning' : 'success'
  if (monitor.phase.value === 'negotiating') return 'primary'
  if (monitor.phase.value === 'failed') return 'danger'
  return 'neutral'
})

const qualitySummary = computed(() => {
  if (monitor.phase.value !== 'connected') return 'Connection details'
  const bitrate = monitor.inboundBitrateKbps.value ? `${monitor.inboundBitrateKbps.value} kbps` : '… kbps'
  return `${bitrate} · ${monitor.jitterMs.value} ms · ${monitor.packetsLost.value} lost`
})

type QuickGainKey = 'windows' | 'soundboard' | 'media' | 'voice' | 'master'

type QuickDragSession = {
  key: QuickGainKey
  pointerId: number
  startX: number
  startY: number
  startValue: number
  width: number
  left: number
  dragging: boolean
}

let quickDrag: QuickDragSession | null = null

function quickGainValue(key: QuickGainKey) {
  if (key === 'windows') return monitor.mixWindowsGainPercent.value
  if (key === 'soundboard') return monitor.mixSoundboardGainPercent.value
  if (key === 'media') return monitor.mixMediaGainPercent.value
  if (key === 'voice') return monitor.mixVoiceGainPercent.value
  return monitor.mixMasterGainPercent.value
}

function quickFillValue(key: QuickGainKey) {
  if (key === 'media' && monitor.mixMediaDuplicateSuppressed.value) return monitor.mixWindowsGainPercent.value
  return quickGainValue(key)
}

function quickGainStyle(key: QuickGainKey): Record<string, string> {
  return { '--level': `${Math.round(quickFillValue(key))}%` }
}

function quickGainDisabled(key: QuickGainKey) {
  return key === 'media' && monitor.mixMediaDuplicateSuppressed.value
}

function vibrateTick(previous: number, next: number) {
  if (!import.meta.client || !('vibrate' in navigator)) return
  const marks = [0, 25, 50, 75, 100]
  if (marks.some(mark => (previous < mark && next >= mark) || (previous > mark && next <= mark))) {
    navigator.vibrate(7)
  }
}

function setQuickGain(key: QuickGainKey, value: number) {
  if (quickGainDisabled(key)) return
  const previous = quickGainValue(key)
  const next = Math.max(0, Math.min(100, Math.round(value)))
  if (next === Math.round(previous)) return
  monitor.setMixGain(key, next)
  vibrateTick(previous, next)
}

function onQuickPointerDown(event: PointerEvent, key: QuickGainKey) {
  if (quickGainDisabled(key) || (event.pointerType === 'mouse' && event.button !== 0)) return
  const element = event.currentTarget as HTMLElement
  const rect = element.getBoundingClientRect()
  quickDrag = {
    key,
    pointerId: event.pointerId,
    startX: event.clientX,
    startY: event.clientY,
    startValue: quickGainValue(key),
    width: Math.max(1, rect.width),
    left: rect.left,
    dragging: false
  }
  element.setPointerCapture?.(event.pointerId)
}

function onQuickPointerMove(event: PointerEvent, key: QuickGainKey) {
  const session = quickDrag
  if (!session || session.key !== key || session.pointerId !== event.pointerId) return
  const dx = event.clientX - session.startX
  const dy = event.clientY - session.startY

  if (!session.dragging) {
    if (Math.abs(dy) > 10 && Math.abs(dy) > Math.abs(dx)) {
      (event.currentTarget as HTMLElement).releasePointerCapture?.(event.pointerId)
      quickDrag = null
      return
    }
    if (Math.abs(dx) < 6) return
    session.dragging = true
  }

  event.preventDefault()
  setQuickGain(key, session.startValue + (dx / session.width) * 100)
}

function onQuickPointerUp(event: PointerEvent, key: QuickGainKey) {
  const session = quickDrag
  if (!session || session.key !== key || session.pointerId !== event.pointerId) return
  const dx = event.clientX - session.startX
  const dy = event.clientY - session.startY
  if (!session.dragging && Math.hypot(dx, dy) < 8) {
    setQuickGain(key, ((event.clientX - session.left) / session.width) * 100)
  }
  ;(event.currentTarget as HTMLElement).releasePointerCapture?.(event.pointerId)
  quickDrag = null
}

function onQuickPointerCancel(event: PointerEvent, key: QuickGainKey) {
  if (quickDrag?.key === key && quickDrag.pointerId === event.pointerId) quickDrag = null
}

function onQuickKeydown(event: KeyboardEvent, key: QuickGainKey) {
  if (quickGainDisabled(key)) return
  const value = quickGainValue(key)
  if (event.key === 'ArrowLeft' || event.key === 'ArrowDown') {
    event.preventDefault()
    setQuickGain(key, value - 2)
  } else if (event.key === 'ArrowRight' || event.key === 'ArrowUp') {
    event.preventDefault()
    setQuickGain(key, value + 2)
  } else if (event.key === 'Home') {
    event.preventDefault()
    setQuickGain(key, 0)
  } else if (event.key === 'End') {
    event.preventDefault()
    setQuickGain(key, 100)
  }
}

function toggleQuickVoice() {
  if (!monitor.active.value || monitor.activeSource.value !== 'monitor-mix') return
  monitor.setMixVoiceEnabled(!monitor.mixVoiceEnabled.value)
  if (import.meta.client && 'vibrate' in navigator) navigator.vibrate(10)
}

function diagnosticLabel(source: string) {
  if (source === 'windows-loopback') return 'Windows output'
  if (source === 'soundboard-tap') return 'Soundboard tap'
  if (source === 'synthetic-sine') return 'Test tone'
  return 'Monitor mix'
}

async function startMonitor() {
  monitor.selectedSource.value = 'monitor-mix'
  await monitor.start('monitor-mix')
}

async function startDiagnostic() {
  if (monitor.selectedSource.value === 'monitor-mix') monitor.selectedSource.value = 'windows-loopback'
  await monitor.start(monitor.selectedSource.value)
}

function chooseDiagnostic(source: 'windows-loopback' | 'soundboard-tap' | 'synthetic-sine') {
  if (monitor.active.value) return
  monitor.selectedSource.value = source
}

async function playManually() {
  monitor.setMonitorMuted(false)
  const played = await monitor.requestPlayback()
  if (!played) remote.showSnackbar('Android blocked audio playback. Check media volume and tap again.', 'warning')
}

onMounted(() => monitor.initialize())
</script>

<template>
  <section class="page-section monitor-page">
    <div class="monitor-page__heading">
      <div>
        <p class="eyebrow">REMOTE AUDIO</p>
        <h2>Monitor</h2>
      </div>
      <GbStatusChip :label="phaseLabel" :tone="phaseTone" :icon="monitor.phase.value === 'connected' ? 'headphones' : 'wifi'" :pulse="monitor.phase.value === 'negotiating'" />
    </div>

    <GbEmptyState
      v-if="remote.serverInfo.value && !monitor.available.value"
      icon="headphones"
      title="Remote Monitor is unavailable"
      message="This build does not include the Remote Monitor audio module."
    />

    <template v-else>
      <section class="gb-surface monitor-console">
        <div class="monitor-console__hero">
          <span class="monitor-console__icon"><GbIcon name="headphones" :size="28" /></span>
          <div class="monitor-console__identity">
            <strong>Remote Monitor</strong>
            <span v-if="monitor.active.value && displaySource !== 'monitor-mix'">Diagnostic · {{ diagnosticLabel(displaySource) }}</span>
            <span v-else-if="monitor.phase.value === 'connected'">Windows + Board + Media{{ monitor.mixVoiceEnabled.value ? ' + Voice' : '' }}</span>
            <span v-else-if="monitor.phase.value === 'negotiating'">Connecting to this phone…</span>
            <span v-else>Ready when you are</span>
          </div>
        </div>

        <div class="monitor-quick-grid" aria-label="Quick Remote Monitor controls">
          <div
            class="monitor-quick-tile monitor-quick-tile--gain"
            :style="quickGainStyle('windows')"
            role="slider"
            tabindex="0"
            aria-label="Windows monitor level"
            title="Tap or drag horizontally"
            aria-valuemin="0"
            aria-valuemax="100"
            :aria-valuenow="Math.round(monitor.mixWindowsGainPercent.value)"
            @pointerdown="event => onQuickPointerDown(event, 'windows')"
            @pointermove="event => onQuickPointerMove(event, 'windows')"
            @pointerup="event => onQuickPointerUp(event, 'windows')"
            @pointercancel="event => onQuickPointerCancel(event, 'windows')"
            @keydown="event => onQuickKeydown(event, 'windows')"
          >
            <span>Windows</span>
            <strong>{{ Math.round(monitor.mixWindowsGainPercent.value) }}%</strong>
          </div>

          <div
            class="monitor-quick-tile monitor-quick-tile--gain"
            :style="quickGainStyle('soundboard')"
            role="slider"
            tabindex="0"
            aria-label="Soundboard monitor level"
            title="Tap or drag horizontally"
            aria-valuemin="0"
            aria-valuemax="100"
            :aria-valuenow="Math.round(monitor.mixSoundboardGainPercent.value)"
            @pointerdown="event => onQuickPointerDown(event, 'soundboard')"
            @pointermove="event => onQuickPointerMove(event, 'soundboard')"
            @pointerup="event => onQuickPointerUp(event, 'soundboard')"
            @pointercancel="event => onQuickPointerCancel(event, 'soundboard')"
            @keydown="event => onQuickKeydown(event, 'soundboard')"
          >
            <span>Board</span>
            <strong>{{ Math.round(monitor.mixSoundboardGainPercent.value) }}%</strong>
          </div>

          <div
            class="monitor-quick-tile monitor-quick-tile--gain"
            :class="{ 'monitor-quick-tile--linked': monitor.mixMediaDuplicateSuppressed.value }"
            :style="quickGainStyle('media')"
            role="slider"
            :tabindex="monitor.mixMediaDuplicateSuppressed.value ? -1 : 0"
            aria-label="Media monitor level"
            title="Tap or drag horizontally"
            aria-valuemin="0"
            aria-valuemax="100"
            :aria-valuenow="Math.round(monitor.mixMediaGainPercent.value)"
            :aria-disabled="monitor.mixMediaDuplicateSuppressed.value"
            @pointerdown="event => onQuickPointerDown(event, 'media')"
            @pointermove="event => onQuickPointerMove(event, 'media')"
            @pointerup="event => onQuickPointerUp(event, 'media')"
            @pointercancel="event => onQuickPointerCancel(event, 'media')"
            @keydown="event => onQuickKeydown(event, 'media')"
          >
            <span>Media</span>
            <strong v-if="!monitor.mixMediaDuplicateSuppressed.value">{{ Math.round(monitor.mixMediaGainPercent.value) }}%</strong>
            <strong v-else class="monitor-quick-tile__linked-value"><GbIcon name="link" :size="15" />Via Windows</strong>
          </div>

          <button
            class="monitor-quick-tile monitor-quick-tile--voice-toggle"
            :class="{ 'monitor-quick-tile--active': monitor.mixVoiceEnabled.value }"
            type="button"
            :disabled="!monitor.active.value || monitor.activeSource.value !== 'monitor-mix'"
            :aria-pressed="monitor.mixVoiceEnabled.value"
            @click="toggleQuickVoice"
          >
            <span>Voice</span>
            <strong>{{ monitor.mixVoiceEnabled.value ? 'On' : 'Off' }}</strong>
          </button>

          <div
            class="monitor-quick-tile monitor-quick-tile--gain"
            :class="{ 'monitor-quick-tile--sleeping': !monitor.mixVoiceEnabled.value }"
            :style="quickGainStyle('voice')"
            role="slider"
            tabindex="0"
            aria-label="My Voice monitor level"
            title="Tap or drag horizontally"
            aria-valuemin="0"
            aria-valuemax="100"
            :aria-valuenow="Math.round(monitor.mixVoiceGainPercent.value)"
            @pointerdown="event => onQuickPointerDown(event, 'voice')"
            @pointermove="event => onQuickPointerMove(event, 'voice')"
            @pointerup="event => onQuickPointerUp(event, 'voice')"
            @pointercancel="event => onQuickPointerCancel(event, 'voice')"
            @keydown="event => onQuickKeydown(event, 'voice')"
          >
            <span>Voice Lv</span>
            <strong>{{ Math.round(monitor.mixVoiceGainPercent.value) }}%</strong>
          </div>

          <div
            class="monitor-quick-tile monitor-quick-tile--gain monitor-quick-tile--master"
            :style="quickGainStyle('master')"
            role="slider"
            tabindex="0"
            aria-label="Monitor master level"
            title="Tap or drag horizontally"
            aria-valuemin="0"
            aria-valuemax="100"
            :aria-valuenow="Math.round(monitor.mixMasterGainPercent.value)"
            @pointerdown="event => onQuickPointerDown(event, 'master')"
            @pointermove="event => onQuickPointerMove(event, 'master')"
            @pointerup="event => onQuickPointerUp(event, 'master')"
            @pointercancel="event => onQuickPointerCancel(event, 'master')"
            @keydown="event => onQuickKeydown(event, 'master')"
          >
            <span>Master</span>
            <strong>{{ Math.round(monitor.mixMasterGainPercent.value) }}%</strong>
          </div>
        </div>

        <div v-if="monitor.phase.value === 'failed'" class="monitor-inline-error" aria-live="polite">
          <GbIcon name="error" :size="20" />
          <span>{{ monitor.detail.value || 'Remote Monitor could not connect.' }}</span>
        </div>

        <div class="monitor-console__actions">
          <GbButton
            v-if="!monitor.active.value"
            variant="filled"
            icon="headphones"
            :disabled="!remote.isConnected.value || !monitor.available.value"
            @click="startMonitor"
          >Start monitor</GbButton>
          <GbButton v-else variant="outlined" icon="stop" @click="monitor.stop()">Stop monitor</GbButton>

          <button
            v-if="monitor.active.value"
            class="monitor-icon-action"
            :class="{ 'monitor-icon-action--active': monitor.monitorMuted.value }"
            type="button"
            :aria-label="monitor.monitorMuted.value ? 'Unmute monitor audio' : 'Mute monitor audio'"
            :title="monitor.monitorMuted.value ? 'Unmute monitor audio' : 'Mute monitor audio'"
            @click="monitor.toggleMonitorMuted()"
          >
            <GbIcon :name="monitor.monitorMuted.value ? 'volume_up' : 'volume_off'" :size="22" />
          </button>

          <GbButton v-if="monitor.playbackBlocked.value" variant="tonal" icon="play" @click="playManually">Play audio</GbButton>
        </div>
      </section>

      <details class="gb-surface monitor-fold">
        <summary>
          <span class="monitor-fold__lead">
            <span class="monitor-fold__icon"><GbIcon name="tune" :size="21" /></span>
            <span>
              <strong>Monitor levels</strong>
              <small>Phone-only mix</small>
            </span>
          </span>
          <span class="monitor-fold__value">Master {{ Math.round(monitor.mixMasterGainPercent.value) }}%</span>
          <GbIcon class="monitor-fold__chevron" name="expand_more" :size="22" />
        </summary>

        <div class="monitor-fold__body monitor-levels">
          <div class="monitor-level-row">
            <GbSlider
              label="Windows / Space"
              :model-value="monitor.mixWindowsGainPercent.value"
              :min="0"
              :max="100"
              :step="1"
              :show-scale="false"
              :value-text="`${Math.round(monitor.mixWindowsGainPercent.value)}%`"
              @input="value => monitor.setMixGain('windows', Number(value))"
            />
          </div>

          <div class="monitor-level-row">
            <GbSlider
              label="Soundboard"
              :model-value="monitor.mixSoundboardGainPercent.value"
              :min="0"
              :max="100"
              :step="1"
              :show-scale="false"
              :value-text="`${Math.round(monitor.mixSoundboardGainPercent.value)}%`"
              @input="value => monitor.setMixGain('soundboard', Number(value))"
            />
          </div>

          <div class="monitor-level-row">
            <div class="monitor-level-row__status">
              <span>Media</span>
              <span v-if="monitor.mixMediaDuplicateSuppressed.value" class="monitor-inline-pill">Via Windows</span>
            </div>
            <GbSlider
              label="Media"
              :model-value="monitor.mixMediaGainPercent.value"
              :min="0"
              :max="100"
              :step="1"
              :show-scale="false"
              :value-text="`${Math.round(monitor.mixMediaGainPercent.value)}%`"
              @input="value => monitor.setMixGain('media', Number(value))"
            />
            <small v-if="monitor.mixMediaDuplicateSuppressed.value" class="monitor-level-note">Direct Media is bypassed to prevent doubling; this value is kept for direct mode.</small>
          </div>

          <div class="monitor-level-row monitor-level-row--voice">
            <div class="monitor-voice-head">
              <div>
                <strong>My Voice</strong>
                <small>{{ monitor.mixVoiceEnabled.value ? 'On' : 'Off' }}</small>
              </div>
              <GbButton
                :variant="monitor.mixVoiceEnabled.value ? 'tonal' : 'outlined'"
                :icon="monitor.mixVoiceEnabled.value ? 'mic' : 'mic_off'"
                :disabled="!monitor.active.value || monitor.activeSource.value !== 'monitor-mix'"
                @click="monitor.setMixVoiceEnabled(!monitor.mixVoiceEnabled.value)"
              >{{ monitor.mixVoiceEnabled.value ? 'Disable' : 'Enable' }}</GbButton>
            </div>
            <GbSlider
              label="My Voice level"
              :model-value="monitor.mixVoiceGainPercent.value"
              :min="0"
              :max="100"
              :step="1"
              :show-scale="false"
              :value-text="`${Math.round(monitor.mixVoiceGainPercent.value)}%`"
              @input="value => monitor.setMixGain('voice', Number(value))"
            />
          </div>

          <div class="monitor-level-row monitor-level-row--master">
            <GbSlider
              label="Monitor Master"
              :model-value="monitor.mixMasterGainPercent.value"
              :min="0"
              :max="100"
              :step="1"
              :show-scale="false"
              :value-text="`${Math.round(monitor.mixMasterGainPercent.value)}%`"
              @input="value => monitor.setMixGain('master', Number(value))"
            />
          </div>
        </div>
      </details>

      <details class="gb-surface monitor-fold monitor-fold--details">
        <summary>
          <span class="monitor-fold__lead">
            <span class="monitor-fold__icon"><GbIcon name="network_check" :size="21" /></span>
            <span>
              <strong>Connection details</strong>
              <small>{{ monitor.phase.value === 'connected' ? 'Healthy receiver stats' : 'WebRTC diagnostics' }}</small>
            </span>
          </span>
          <span class="monitor-fold__value monitor-fold__value--quality">{{ qualitySummary }}</span>
          <GbIcon class="monitor-fold__chevron" name="expand_more" :size="22" />
        </summary>

        <div class="monitor-fold__body">
          <div class="monitor-quality" aria-label="Remote Monitor receive quality">
            <div><span>Codec</span><strong>{{ monitor.codecName.value || 'Opus' }}</strong></div>
            <div><span>Receive</span><strong>{{ monitor.inboundBitrateKbps.value ? `${monitor.inboundBitrateKbps.value} kbps` : '—' }}</strong></div>
            <div><span>Jitter</span><strong>{{ monitor.jitterMs.value }} ms</strong></div>
            <div><span>Lost</span><strong>{{ monitor.packetsLost.value }}</strong></div>
          </div>

          <div class="monitor-connection-line">
            <span>Peer <strong>{{ monitor.peerConnectionState.value }}</strong></span>
            <span>ICE <strong>{{ monitor.iceConnectionState.value }}</strong></span>
            <span>Track <strong>{{ monitor.mediaStream.value ? 'received' : 'waiting' }}</strong></span>
          </div>

          <details class="monitor-advanced">
            <summary>
              <span><GbIcon name="science" :size="18" /> Advanced diagnostics</span>
              <GbIcon class="monitor-fold__chevron" name="expand_more" :size="20" />
            </summary>

            <div class="monitor-advanced__body">
              <div class="monitor-tech-grid">
                <div><span>Windows device</span><strong>{{ monitor.deviceName.value || 'Default output' }}</strong></div>
                <div><span>Capture format</span><strong>{{ monitor.sampleRate.value ? `${monitor.sampleRate.value / 1000} kHz · ${monitor.channels.value} ch · ${monitor.frameMilliseconds.value} ms` : '48 kHz · 2 ch · 20 ms' }}</strong></div>
                <div><span>Opus target</span><strong>{{ monitor.encoderBitrateKbps.value ? `${monitor.encoderBitrateKbps.value} kbps` : '128 kbps' }} · VBR</strong></div>
                <div><span>ICE</span><strong>Same-LAN host only</strong></div>
              </div>

              <div class="monitor-diagnostic-source">
                <div>
                  <strong>Isolated source test</strong>
                  <small>Only needed when debugging the monitor bus.</small>
                </div>
                <div class="monitor-diagnostic-source__buttons">
                  <GbButton
                    :variant="monitor.selectedSource.value === 'windows-loopback' ? 'tonal' : 'outlined'"
                    icon="desktop_windows"
                    :disabled="monitor.active.value"
                    @click="chooseDiagnostic('windows-loopback')"
                  >Windows</GbButton>
                  <GbButton
                    :variant="monitor.selectedSource.value === 'soundboard-tap' ? 'tonal' : 'outlined'"
                    icon="grid_view"
                    :disabled="monitor.active.value"
                    @click="chooseDiagnostic('soundboard-tap')"
                  >Soundboard</GbButton>
                  <GbButton
                    :variant="monitor.selectedSource.value === 'synthetic-sine' ? 'tonal' : 'outlined'"
                    icon="graphic_eq"
                    :disabled="monitor.active.value"
                    @click="chooseDiagnostic('synthetic-sine')"
                  >Test tone</GbButton>
                </div>
                <GbButton
                  v-if="!monitor.active.value"
                  variant="outlined"
                  icon="play"
                  :disabled="!remote.isConnected.value || !monitor.available.value"
                  @click="startDiagnostic"
                >Start {{ diagnosticLabel(monitor.selectedSource.value === 'monitor-mix' ? 'windows-loopback' : monitor.selectedSource.value) }}</GbButton>
                <small v-else>Stop the current monitor session before switching diagnostic sources.</small>
              </div>

              <p v-if="monitor.detail.value" class="monitor-detail-text">{{ monitor.detail.value }}</p>
              <span class="monitor-build-note">Local v1.2 audio build</span>
            </div>
          </details>
        </div>
      </details>
    </template>
  </section>
</template>

<style scoped>
.monitor-page {
  display: grid;
  gap: 14px;
}

.monitor-page__heading {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
}

.monitor-page__heading h2 {
  margin: 2px 0 0;
}

.monitor-console {
  display: grid;
  gap: 16px;
  padding: 18px;
}

.monitor-console__hero {
  display: flex;
  align-items: center;
  gap: 12px;
}

.monitor-console__icon,
.monitor-fold__icon {
  display: grid;
  flex: 0 0 auto;
  place-items: center;
  color: var(--gb-primary);
  background: var(--gb-primary-container);
}

.monitor-console__icon {
  width: 48px;
  height: 48px;
  border-radius: 18px;
}

.monitor-console__identity {
  display: grid;
  gap: 3px;
  min-width: 0;
}

.monitor-console__identity strong {
  color: var(--gb-on-surface);
  font-size: 1.05rem;
}

.monitor-console__identity span {
  overflow: hidden;
  color: var(--gb-on-surface-variant);
  font-size: 0.86rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.monitor-quick-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
}

.monitor-quick-tile {
  --level: 0%;
  position: relative;
  isolation: isolate;
  display: grid;
  min-width: 0;
  min-height: 76px;
  padding: 12px 13px;
  align-content: space-between;
  overflow: hidden;
  border: 0;
  border-radius: 18px;
  color: var(--gb-on-surface);
  background: var(--gb-surface-container);
  text-align: left;
  -webkit-tap-highlight-color: transparent;
}

.monitor-quick-tile--gain {
  cursor: ew-resize;
  touch-action: pan-y;
}

.monitor-quick-tile--gain::before {
  position: absolute;
  z-index: -1;
  inset: 0 auto 0 0;
  width: var(--level);
  content: '';
  background: var(--gb-primary-container);
  opacity: 0.55;
  pointer-events: none;
  transition: width 70ms linear, opacity 140ms ease;
}

.monitor-quick-tile > span {
  position: relative;
  z-index: 1;
  overflow: hidden;
  color: var(--gb-on-surface-variant);
  font-size: 0.72rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.monitor-quick-tile > strong {
  position: relative;
  z-index: 1;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  min-width: 0;
  color: var(--gb-on-surface);
  font-size: 1rem;
  font-weight: 600;
  line-height: 1.15;
}

.monitor-quick-tile:focus-visible {
  outline: 2px solid var(--gb-primary);
  outline-offset: 2px;
}

.monitor-quick-tile--voice-toggle {
  cursor: pointer;
  appearance: none;
}

.monitor-quick-tile--voice-toggle::before {
  position: absolute;
  z-index: -1;
  inset: 0;
  content: '';
  background: var(--gb-primary-container);
  opacity: 0;
  transition: opacity 140ms ease;
}

.monitor-quick-tile--voice-toggle.monitor-quick-tile--active::before {
  opacity: 0.72;
}

.monitor-quick-tile--voice-toggle:disabled {
  cursor: default;
  opacity: 0.55;
}

.monitor-quick-tile--sleeping::before {
  opacity: 0.26;
}

.monitor-quick-tile--master {
  background: var(--gb-surface-container-high);
}

.monitor-quick-tile--master::before {
  opacity: 0.72;
}

.monitor-quick-tile--linked {
  cursor: default;
}

.monitor-quick-tile--linked::before {
  opacity: 0.35;
}

.monitor-quick-tile__linked-value {
  font-size: 0.78rem !important;
  white-space: nowrap;
}

.monitor-console__actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.monitor-icon-action {
  display: grid;
  width: 48px;
  height: 48px;
  padding: 0;
  place-items: center;
  border: 1px solid var(--gb-outline-variant);
  border-radius: 18px;
  color: var(--gb-on-surface);
  background: var(--gb-surface-container);
  cursor: pointer;
}

.monitor-icon-action--active {
  color: var(--gb-on-error-container);
  background: var(--gb-error-container);
}

.monitor-inline-error {
  display: flex;
  align-items: flex-start;
  gap: 9px;
  padding: 11px 12px;
  border-radius: 16px;
  color: var(--gb-on-error-container);
  background: var(--gb-error-container);
  font-size: 0.86rem;
}

.monitor-fold {
  overflow: hidden;
  padding: 0;
}

.monitor-fold > summary,
.monitor-advanced > summary {
  display: flex;
  align-items: center;
  gap: 12px;
  cursor: pointer;
  list-style: none;
  user-select: none;
}

.monitor-fold > summary {
  min-height: 66px;
  padding: 12px 16px;
}

.monitor-fold > summary::-webkit-details-marker,
.monitor-advanced > summary::-webkit-details-marker {
  display: none;
}

.monitor-fold__lead {
  display: flex;
  align-items: center;
  gap: 11px;
  min-width: 0;
}

.monitor-fold__lead > span:last-child {
  display: grid;
  gap: 2px;
}

.monitor-fold__lead strong,
.monitor-advanced strong {
  color: var(--gb-on-surface);
}

.monitor-fold__lead small,
.monitor-advanced small {
  color: var(--gb-on-surface-variant);
  font-size: 0.76rem;
}

.monitor-fold__icon {
  width: 38px;
  height: 38px;
  border-radius: 14px;
}

.monitor-fold__value {
  margin-left: auto;
  color: var(--gb-on-surface-variant);
  font-size: 0.78rem;
  white-space: nowrap;
}

.monitor-fold__value--quality {
  overflow: hidden;
  max-width: 45%;
  text-overflow: ellipsis;
}

.monitor-fold__chevron {
  flex: 0 0 auto;
  transition: transform 160ms ease;
}

.monitor-fold[open] > summary .monitor-fold__chevron,
.monitor-advanced[open] > summary .monitor-fold__chevron {
  transform: rotate(180deg);
}

.monitor-fold__body {
  display: grid;
  gap: 12px;
  padding: 2px 14px 14px;
}

.monitor-levels {
  gap: 9px;
}

.monitor-level-row {
  padding: 11px 12px;
  border-radius: 16px;
  background: var(--gb-surface-container);
}

.monitor-level-row--master {
  background: var(--gb-surface-container-high);
}

.monitor-level-row__status {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 4px;
  color: var(--gb-on-surface);
  font-size: 0.82rem;
}

.monitor-inline-pill {
  padding: 4px 8px;
  border-radius: 999px;
  color: var(--gb-on-primary-container);
  background: var(--gb-primary-container);
  font-size: 0.7rem;
}

.monitor-level-note {
  display: block;
  margin-top: 6px;
  color: var(--gb-on-surface-variant);
  line-height: 1.35;
}

.monitor-voice-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 8px;
}

.monitor-voice-head > div {
  display: grid;
  gap: 2px;
}

.monitor-voice-head small {
  color: var(--gb-on-surface-variant);
}

.monitor-quality {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.monitor-quality > div,
.monitor-tech-grid > div {
  display: grid;
  gap: 3px;
  padding: 10px 11px;
  border-radius: 14px;
  background: var(--gb-surface-container);
}

.monitor-quality span,
.monitor-tech-grid span {
  color: var(--gb-on-surface-variant);
  font-size: 0.72rem;
}

.monitor-quality strong,
.monitor-tech-grid strong {
  overflow-wrap: anywhere;
  color: var(--gb-on-surface);
  font-size: 0.9rem;
}

.monitor-connection-line {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
}

.monitor-connection-line span {
  padding: 6px 9px;
  border: 1px solid var(--gb-outline-variant);
  border-radius: 999px;
  color: var(--gb-on-surface-variant);
  font-size: 0.72rem;
}

.monitor-advanced {
  margin-top: 2px;
  border-top: 1px solid var(--gb-outline-variant);
}

.monitor-advanced > summary {
  min-height: 48px;
  padding: 8px 2px 0;
}

.monitor-advanced > summary > span {
  display: flex;
  align-items: center;
  gap: 8px;
}

.monitor-advanced > summary .monitor-fold__chevron {
  margin-left: auto;
}

.monitor-advanced__body {
  display: grid;
  gap: 12px;
  padding-top: 10px;
}

.monitor-tech-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.monitor-diagnostic-source {
  display: grid;
  gap: 10px;
  padding: 12px;
  border-radius: 16px;
  background: var(--gb-surface-container-low);
}

.monitor-diagnostic-source > div:first-child {
  display: grid;
  gap: 2px;
}

.monitor-diagnostic-source__buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.monitor-detail-text {
  margin: 0;
  color: var(--gb-on-surface-variant);
  font-size: 0.78rem;
  line-height: 1.45;
}

.monitor-build-note {
  justify-self: start;
  color: var(--gb-on-surface-variant);
  font-size: 0.7rem;
}

@media (min-width: 760px) {
  .monitor-console,
  .monitor-fold {
    max-width: 760px;
    width: 100%;
    justify-self: center;
  }

  .monitor-levels {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .monitor-level-row--master {
    grid-column: 1 / -1;
  }
}

@media (max-width: 430px) {
  .monitor-page__heading {
    align-items: center;
  }

  .monitor-quick-tile {
    min-height: 72px;
    padding: 11px 12px;
  }

  .monitor-fold__value--quality {
    display: none;
  }

  .monitor-tech-grid {
    grid-template-columns: 1fr;
  }
}

@media (prefers-reduced-motion: reduce) {
  .monitor-fold__chevron,
  .monitor-quick-tile--gain::before,
  .monitor-quick-tile--voice-toggle::before {
    transition: none;
  }
}
</style>
