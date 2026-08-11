<script setup lang="ts">
const emit = defineEmits<{ close: [], detected: [value: string] }>()
const video = ref<HTMLVideoElement | null>(null)
const error = ref('')
const status = ref('Starting camera…')
let stream: MediaStream | null = null
let timer: ReturnType<typeof setTimeout> | null = null
let stopped = false

type Detector = { detect: (source: CanvasImageSource) => Promise<Array<{ rawValue?: string }>> }
type DetectorConstructor = new (options?: { formats?: string[] }) => Detector

async function start() {
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
    error.value = 'Camera scanning requires the secure HTTPS GrassiMote app.'
    return
  }

  const DetectorClass = (globalThis as typeof globalThis & { BarcodeDetector?: DetectorConstructor }).BarcodeDetector
  if (!DetectorClass) {
    error.value = 'QR detection is not available in this browser. Use Android Chrome or enter the 6-digit code.'
    return
  }

  try {
    stream = await navigator.mediaDevices.getUserMedia({
      audio: false,
      video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } }
    })
    if (!video.value || stopped) {
      stop()
      return
    }
    video.value.srcObject = stream
    await video.value.play()
    status.value = 'Point the camera at the QR in GrassiBoard Settings.'
    const detector = new DetectorClass({ formats: ['qr_code'] })
    scan(detector)
  } catch (cause) {
    const name = cause instanceof DOMException ? cause.name : ''
    error.value = name === 'NotAllowedError'
      ? 'Camera permission was denied. Allow camera access for GrassiMote and try again.'
      : 'Could not start the camera.'
  }
}

function scan(detector: Detector) {
  if (stopped) return
  timer = setTimeout(async () => {
    try {
      if (video.value && video.value.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA) {
        const results = await detector.detect(video.value)
        const value = results.find(result => result.rawValue)?.rawValue
        if (value) {
          stop()
          emit('detected', value)
          return
        }
      }
    } catch {
      // Detection can transiently fail while the camera is focusing.
    }
    scan(detector)
  }, 180)
}

function stop() {
  stopped = true
  if (timer) clearTimeout(timer)
  timer = null
  stream?.getTracks().forEach(track => track.stop())
  stream = null
}

function close() {
  stop()
  emit('close')
}

onMounted(start)
onBeforeUnmount(stop)
</script>

<template>
  <div class="scanner-backdrop" role="dialog" aria-modal="true" aria-label="Scan GrassiBoard pairing QR">
    <section class="scanner-card glass-card">
      <div class="scanner-heading">
        <div><p class="eyebrow">PAIR DEVICE</p><h2>Scan QR</h2></div>
        <button class="scanner-close" type="button" aria-label="Close scanner" @click="close">×</button>
      </div>
      <div class="scanner-viewport">
        <video ref="video" playsinline muted />
        <div class="scanner-frame" />
      </div>
      <p v-if="error" class="error-copy">{{ error }}</p>
      <p v-else class="micro-copy">{{ status }}</p>
      <button class="secondary-button" type="button" @click="close">Cancel</button>
    </section>
  </div>
</template>
