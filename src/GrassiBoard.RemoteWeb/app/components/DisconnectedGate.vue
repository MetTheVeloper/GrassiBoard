<script setup lang="ts">
const remote = useRemoteConnection()
const submitting = ref(false)
const scannerOpen = ref(false)

const activelyConnecting = computed(() =>
  ['connecting', 'authenticating', 'pairing'].includes(remote.connectionState.value) && !remote.lastError.value
)

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

function retry() {
  remote.connect(true)
}
</script>

<template>
  <main class="recovery-gate">
    <section class="recovery-card gb-surface">
      <div class="recovery-hero" :class="{ 'recovery-hero--connecting': activelyConnecting }">
        <span class="recovery-hero__icon" aria-hidden="true">
          <GbIcon :name="activelyConnecting ? 'refresh' : 'desktop'" :size="34" />
        </span>
        <div>
          <p class="eyebrow">GRASSIMOTE</p>
          <h1>{{ activelyConnecting ? 'Connecting to GrassiBoard…' : 'Open GrassiBoard on Windows' }}</h1>
        </div>
      </div>

      <p class="recovery-lead">
        {{ activelyConnecting
          ? 'Looking for your paired GrassiBoard on the local network.'
          : 'To use the Remote, start GrassiBoard on your PC. Then open Settings → Remote Control and scan the QR code, or enter the temporary six-digit pairing code.'
        }}
      </p>

      <div v-if="!activelyConnecting" class="recovery-actions">
        <button
          v-if="remote.isSecureContext.value"
          class="scan-button scan-button--recovery"
          type="button"
          :disabled="submitting"
          @click="scannerOpen = true"
        >
          <span class="scan-button__icon"><GbIcon name="qr" :size="30" /></span>
          <span class="scan-button__copy"><strong>Scan QR</strong><small>Recommended if the PC address changed</small></span>
          <span class="scan-button__arrow" aria-hidden="true">›</span>
        </button>

        <div class="or-divider"><span>or enter code</span></div>

        <form class="pair-form recovery-code-form" @submit.prevent="submitCode">
          <label for="recovery-pair-code"><GbIcon name="key" :size="18" /> Pairing code</label>
          <input
            id="recovery-pair-code"
            v-model="remote.manualCode.value"
            class="pair-input"
            type="text"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="6"
            placeholder="000000"
          >
          <GbButton variant="filled" :disabled="submitting" @click="submitCode">
            {{ submitting ? 'Pairing…' : 'Pair device' }}
          </GbButton>
        </form>
      </div>

      <div v-if="remote.lastError.value && !activelyConnecting" class="recovery-note" role="status">
        <GbIcon name="wifi_off" :size="19" />
        <span>{{ remote.lastError.value }}</span>
      </div>

      <GbButton variant="text" icon="refresh" :disabled="submitting" @click="retry">
        {{ activelyConnecting ? 'Try again now' : 'Retry connection' }}
      </GbButton>

      <p class="recovery-footnote">Keep the phone and PC on the same private LAN. If the LAN IP changed, scan the new QR shown by GrassiBoard.</p>
    </section>
  </main>

  <QrScannerModal v-if="scannerOpen" @close="scannerOpen = false" @detected="onDetected" />
</template>
