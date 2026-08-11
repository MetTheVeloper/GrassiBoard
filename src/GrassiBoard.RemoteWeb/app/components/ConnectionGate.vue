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

function openSecure() {
  if (remote.secureAppUrl.value) window.location.href = remote.secureAppUrl.value
}

async function onDetected(value: string) {
  scannerOpen.value = false
  submitting.value = true
  try { await remote.pairFromQr(value) }
  finally { submitting.value = false }
}
</script>

<template>
  <main class="connection-gate">
    <section class="pair-card gb-surface">
      <div class="pair-hero">
        <img class="brand-icon" src="/icons/grassimote-192.png" alt="" width="68" height="68">
        <div>
          <p class="eyebrow">GRASSIMOTE</p>
          <h1>{{ remote.isSecureContext.value ? 'Pair this phone' : 'Secure setup required' }}</h1>
        </div>
      </div>

      <template v-if="remote.isSecureContext.value">
        <p class="pair-lead">Scan the QR shown in GrassiBoard Settings for the fastest setup, or enter the temporary six-digit code.</p>

        <button class="scan-button" type="button" :disabled="submitting" @click="scannerOpen = true">
          <span class="scan-button__icon"><GbIcon name="qr" :size="30" /></span>
          <span class="scan-button__copy"><strong>Scan QR</strong><small>Open camera and pair securely</small></span>
          <span class="scan-button__arrow" aria-hidden="true">›</span>
        </button>

        <div class="or-divider"><span>or use code</span></div>

        <form class="pair-form" @submit.prevent="submitCode">
          <label for="pair-code">Pairing code</label>
          <input
            id="pair-code"
            v-model="remote.manualCode.value"
            class="pair-input"
            type="text"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="6"
            placeholder="000000"
            aria-describedby="pair-code-help"
          >
          <small id="pair-code-help">The code expires automatically and can only be used for pairing.</small>
          <GbButton variant="filled" :disabled="submitting" @click="submitCode">
            {{ submitting ? 'Pairing…' : 'Pair device' }}
          </GbButton>
        </form>
      </template>

      <template v-else>
        <div class="secure-setup-copy">
          <span class="gb-empty-state__icon"><GbIcon name="wifi" :size="28" /></span>
          <div>
            <strong>Open the trusted HTTPS GrassiMote address</strong>
            <p>PWA installation and camera scanning need the secure local origin.</p>
          </div>
        </div>
        <GbButton v-if="remote.secureAppUrl.value" variant="filled" icon="output" @click="openSecure">Open secure GrassiMote</GbButton>
        <p class="micro-copy">If HTTPS is not trusted yet, scan the desktop QR with the phone camera and complete the one-time CA setup first.</p>
      </template>

      <GbButton v-if="pwa.canInstall.value" class="install-button" variant="outlined" icon="install" @click="pwa.install">Install GrassiMote</GbButton>

      <div v-if="remote.lastError.value" class="network-banner network-banner--inline" role="alert">
        <div><GbIcon name="error" :size="19" /><span>{{ remote.lastError.value }}</span></div>
      </div>

      <p class="pair-footnote"><GbIcon name="wifi" :size="16" /> Both devices must share the same private LAN. VPNs must allow local-network traffic.</p>
    </section>
  </main>

  <QrScannerModal v-if="scannerOpen" @close="scannerOpen = false" @detected="onDetected" />
</template>
