<script setup lang="ts">
const remote = useRemoteConnection()
const submitting = ref(false)

async function submitCode() {
  submitting.value = true
  try { await remote.pairWithCode() }
  finally { submitting.value = false }
}
</script>

<template>
  <section class="pair-card glass-card">
    <div class="brand-mark">GB</div>
    <p class="eyebrow">GRASSIBOARD REMOTE</p>
    <h1>Pair this phone</h1>
    <p class="muted">Scan the QR code shown in GrassiBoard Settings, or enter its temporary 6-digit code.</p>
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
    <p v-if="remote.lastError.value" class="error-copy">{{ remote.lastError.value }}</p>
    <p class="micro-copy">Both devices must be on the same private LAN/Wi-Fi.</p>
  </section>
</template>
