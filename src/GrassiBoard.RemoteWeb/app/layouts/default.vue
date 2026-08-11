<script setup lang="ts">
const remote = useRemoteConnection()
const pwa = usePwaInstall()
const scannerOpen = ref(false)

onMounted(() => {
  remote.initialize()
  pwa.initialize()
})

const profileName = computed(() => remote.snapshot.value?.profileName || 'GrassiBoard')
const engineRunning = computed(() => remote.snapshot.value?.engine.running ?? false)
const micMuted = computed(() => remote.snapshot.value?.microphoneMuted ?? false)
const connectionTone = computed(() => remote.isConnected.value ? 'success' : (remote.connectionState.value === 'unauthorized' ? 'warning' : 'danger'))

const navItems = [
  { to: '/', label: 'Board', icon: 'board' },
  { to: '/voice', label: 'Voice', icon: 'voice' },
  { to: '/mixer', label: 'Mixer', icon: 'mixer' },
  { to: '/media', label: 'Media', icon: 'media' }
]

function toggleMute() {
  const current = micMuted.value
  if (remote.sendCommand('mic.mute.set', { muted: !current })) remote.vibrate(current ? 10 : [18, 25, 18])
}

function toggleEngine() {
  const command = engineRunning.value ? 'engine.stop' : 'engine.start'
  if (remote.sendCommand(command)) remote.vibrate(engineRunning.value ? [16, 14, 10] : [12, 18, 12])
}

function stopAll() {
  if (remote.sendCommand('engine.stopAll')) remote.vibrate([35, 25, 55])
}

async function onDetected(value: string) {
  scannerOpen.value = false
  await remote.pairFromQr(value)
}
</script>

<template>
  <div class="app-shell">
    <template v-if="remote.paired.value && remote.isConnected.value">
      <aside class="adaptive-nav" aria-label="Remote sections">
        <div class="adaptive-nav__brand" aria-hidden="true">G</div>
        <NuxtLink v-for="item in navItems" :key="item.to" :to="item.to" class="adaptive-nav__item">
          <span class="adaptive-nav__icon"><GbIcon :name="item.icon" :size="24" /></span>
          <span>{{ item.label }}</span>
        </NuxtLink>
      </aside>

      <div class="app-content">
        <header class="session-header">
          <div class="session-header__identity">
            <p class="eyebrow">GRASSIMOTE</p>
            <div class="session-header__title-row">
              <h1 class="session-title">{{ profileName }}</h1>
              <GbStatusChip
                :label="remote.connectionLabel.value"
                :tone="connectionTone"
                :icon="remote.isConnected.value ? 'wifi' : 'wifi_off'"
                :pulse="!remote.isConnected.value"
              />
            </div>
          </div>
          <div class="session-header__telemetry" aria-label="Live meters">
            <span><small>MIC</small><strong>{{ remote.snapshot.value?.meters.microphoneDb || '−∞ dBFS' }}</strong></span>
            <span><small>MASTER</small><strong>{{ remote.snapshot.value?.meters.masterDb || '−∞ dBFS' }}</strong></span>
          </div>
        </header>

        <div class="session-health-row">
          <div class="session-health" aria-live="polite">
            <GbStatusChip
              :label="engineRunning ? 'Engine live' : (remote.snapshot.value?.engine.state || 'Engine ready')"
              :tone="engineRunning ? 'success' : 'neutral'"
              icon="power"
            />
            <GbStatusChip
              :label="micMuted ? 'Mic muted' : 'Mic live'"
              :tone="micMuted ? 'danger' : 'primary'"
              :icon="micMuted ? 'mic_off' : 'mic'"
            />
          </div>
          <DangerHoldButton compact label="Stop All" @activate="stopAll" />
        </div>

        <div v-if="pwa.canInstall.value" class="pwa-banner gb-surface gb-surface--tonal">
          <div class="pwa-banner__copy">
            <span class="gb-control-icon"><GbIcon name="install" :size="22" /></span>
            <div><strong>Install GrassiMote</strong><span>Keep the live deck one tap away on your home screen.</span></div>
          </div>
          <GbButton variant="outlined" icon="install" @click="pwa.install">Install</GbButton>
        </div>

        <div v-if="remote.lastError.value" class="network-banner" role="status">
          <div><GbIcon name="error" :size="19" /><span>{{ remote.lastError.value }}</span></div>
          <GbButton v-if="remote.isSecureContext.value" variant="text" icon="qr" @click="scannerOpen = true">Scan QR</GbButton>
        </div>

        <main class="page-frame"><slot /></main>
      </div>

      <div class="floating-session-actions" aria-label="Live session controls">
        <GbFab
          :icon="engineRunning ? 'power_off' : 'power'"
          :label="engineRunning ? 'Stop audio engine' : 'Start audio engine'"
          :variant="engineRunning ? 'surface' : 'primary'"
          :tone="engineRunning ? 'success' : 'normal'"
          @click="toggleEngine"
        />
        <GbFab
          :icon="micMuted ? 'mic' : 'mic_off'"
          :label="micMuted ? 'Unmute microphone' : 'Mute microphone'"
          variant="surface"
          :tone="micMuted ? 'danger' : 'normal'"
          @click="toggleMute"
        />
      </div>

      <GbSnackbar
        :open="Boolean(remote.snackbar.value)"
        :message="remote.snackbar.value?.message || ''"
        :tone="remote.snackbar.value?.tone || 'neutral'"
        @dismiss="remote.dismissSnackbar"
      />

      <nav class="bottom-nav" aria-label="Remote sections">
        <NuxtLink v-for="item in navItems" :key="item.to" :to="item.to" class="bottom-nav__item">
          <span class="bottom-nav__icon"><GbIcon :name="item.icon" :size="23" /></span>
          <span>{{ item.label }}</span>
        </NuxtLink>
      </nav>
    </template>

    <DisconnectedGate v-else-if="remote.paired.value" />
    <ConnectionGate v-else />
    <QrScannerModal v-if="scannerOpen" @close="scannerOpen = false" @detected="onDetected" />
  </div>
</template>
