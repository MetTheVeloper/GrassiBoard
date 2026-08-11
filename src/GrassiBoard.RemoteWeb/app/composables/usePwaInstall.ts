type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed', platform: string }>
}

let installListenerAttached = false

export function usePwaInstall() {
  const promptEvent = useState<BeforeInstallPromptEvent | null>('pwa:install-prompt', () => null)
  const installed = useState<boolean>('pwa:installed', () => false)

  const isStandalone = computed(() => installed.value)
  const canInstall = computed(() => Boolean(promptEvent.value) && !installed.value)

  function initialize() {
    if (!import.meta.client || installListenerAttached) return
    installListenerAttached = true
    installed.value = window.matchMedia('(display-mode: standalone)').matches || Boolean((navigator as Navigator & { standalone?: boolean }).standalone)
    window.addEventListener('beforeinstallprompt', event => {
      event.preventDefault()
      promptEvent.value = event as BeforeInstallPromptEvent
    })
    window.addEventListener('appinstalled', () => {
      installed.value = true
      promptEvent.value = null
    })
  }

  async function install() {
    const event = promptEvent.value
    if (!event) return false
    await event.prompt()
    const choice = await event.userChoice
    if (choice.outcome === 'accepted') {
      installed.value = true
      promptEvent.value = null
      return true
    }
    return false
  }

  return { initialize, canInstall, isStandalone, install }
}
