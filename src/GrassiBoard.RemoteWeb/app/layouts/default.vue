<script setup lang="ts">
const remote = useRemoteConnection()
onMounted(remote.initialize)

const profileName = computed(() => remote.snapshot.value?.profileName || 'GrassiBoard')
const live = computed(() => remote.snapshot.value?.engine.running ?? false)

function mute() {
  const current = remote.snapshot.value?.microphoneMuted ?? false
  if (remote.sendCommand('mic.mute.set', { muted: !current })) remote.vibrate(current ? 10 : [18, 25, 18])
}

function stopAll() {
  if (remote.sendCommand('engine.stopAll')) remote.vibrate([35, 25, 55])
}
</script>

<template>
  <div class="app-shell">
    <template v-if="remote.paired.value">
      <header class="top-status">
        <div>
          <p class="eyebrow">{{ profileName }}</p>
          <div class="status-line">
            <span class="status-dot" :class="{ live }" />
            <strong>{{ live ? 'LIVE' : remote.snapshot.value?.engine.state || 'READY' }}</strong>
            <span class="connection-pill" :class="{ good: remote.isConnected.value }">{{ remote.connectionLabel.value }}</span>
          </div>
        </div>
        <div class="meters" aria-label="Live meters">
          <span>Mic {{ remote.snapshot.value?.meters.microphoneDb || '−∞ dBFS' }}</span>
          <span>Master {{ remote.snapshot.value?.meters.masterDb || '−∞ dBFS' }}</span>
        </div>
      </header>

      <div v-if="remote.lastError.value" class="network-banner">{{ remote.lastError.value }}</div>

      <div class="emergency-row">
        <button class="mute-button" type="button" :class="{ active: remote.snapshot.value?.microphoneMuted }" @click="mute">
          {{ remote.snapshot.value?.microphoneMuted ? 'UNMUTE MIC' : 'MUTE MIC' }}
        </button>
        <DangerHoldButton label="STOP ALL" @activate="stopAll" />
      </div>

      <main class="page-frame"><slot /></main>

      <nav class="bottom-nav" aria-label="Remote sections">
        <NuxtLink to="/">Board</NuxtLink>
        <NuxtLink to="/voice">Voice</NuxtLink>
        <NuxtLink to="/mixer">Mixer</NuxtLink>
        <NuxtLink to="/media">Media</NuxtLink>
      </nav>
    </template>
    <ConnectionGate v-else />
  </div>
</template>
