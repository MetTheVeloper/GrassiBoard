<script setup lang="ts">
const remote = useRemoteConnection()
const pwa = usePwaInstall()
const submitting = ref(false)
const scannerOpen = ref(false)

onMounted(() => {
  pwa.initialize()
  void remote.refreshInfo()
})

async function submitCode() {
  submitting.value = true
  try { await remote.pairWithCode() }
  finally { submitting.value = false }
}

async function onDetected(value: string) {
  scannerOpen.value = false
  submitting.value = true
  try { await remote.pairFromQr(value) }
  finally { submitting.value = false }
}
</script>

<template>
  <section class="pair-card glass-card">
    <img class="brand-icon" src="/icons/grassimote-192.png" alt="" width="64" height="64">
    <p class="eyebrow">GRASSIMOTE</p>
    <h1>{{ remote.isSecureContext.value ? 'Pair this phone' : 'Secure setup required' }}</h1>

    <template v-if="remote.isSecureContext.value">
      <p class="muted">Scan the QR shown in GrassiBoard Settings, or enter its temporary 6-digit code.</p>
      <button class="scan-button" type="button" :disabled="submitting" @click="scannerOpen = true">
        <span class="scan-glyph">⌗</span>
        <span><strong>SCAN QR</strong><small>Open camera and pair</small></span>
      </button>
      <div class="or-divider"><span>or</span></div>
      <form class="pair-form" @submit.prevent="submitCode">
        <input
          v-model="remote.manualCode.value"
          class="pair-input"
          type="text"
          inputmode="numeric"
          autocomplete="one-time-code"
          maxlength="6"
          placeholder="000000"
          aria-label="Pairing code"
        >
        <button class="primary-button" type="submit" :disabled="submitting">
          {{ submitting ? 'Pairing…' : 'Pair device' }}
        </button>
      </form>
    </template>

    <template v-else>
      <p class="muted">PWA installation and camera scanning require the trusted HTTPS GrassiMote origin.</p>
      <a v-if="remote.secureAppUrl.value" class="primary-button link-button" :href="remote.secureAppUrl.value">Open secure GrassiMote</a>
      <p class="micro-copy">If HTTPS is not trusted yet, scan the desktop QR with the phone camera and complete the one-time CA setup first.</p>
    </template>

    <button v-if="pwa.canInstall.value" class="secondary-button install-button" type="button" @click="pwa.install">
      Install GrassiMote
    </button>
    <p v-if="remote.lastError.value" class="error-copy">{{ remote.lastError.value }}</p>
    <p class="micro-copy">Both devices must be on the same private LAN/Wi-Fi. VPNs must allow local-network traffic.</p>
  </section>

  <QrScannerModal v-if="scannerOpen" @close="scannerOpen = false" @detected="onDetected" />
</template>
