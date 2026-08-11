export function useCoalescedCommand(type: string, delay = 55) {
  const { sendCommand } = useRemoteConnection()
  let timer: ReturnType<typeof setTimeout> | null = null
  let latest: Record<string, unknown> = {}

  function send(payload: Record<string, unknown>) {
    latest = payload
    if (timer) return
    timer = setTimeout(() => {
      timer = null
      sendCommand(type, latest)
    }, delay)
  }

  onBeforeUnmount(() => {
    if (timer) clearTimeout(timer)
  })

  return send
}
