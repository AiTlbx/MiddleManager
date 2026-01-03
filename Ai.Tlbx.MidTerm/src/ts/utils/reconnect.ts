/**
 * Reconnection Utilities
 *
 * Exponential backoff reconnection logic for WebSocket connections.
 */

/**
 * Schedule a reconnection with exponential backoff.
 * Returns the timer ID for cancellation.
 */
export function scheduleReconnect(
  currentDelay: number,
  maxDelay: number,
  connect: () => void,
  setDelay: (delay: number) => void,
  setTimer: (timer: number | undefined) => void,
  existingTimer: number | undefined
): void {
  if (existingTimer !== undefined) {
    clearTimeout(existingTimer);
  }

  const timer = window.setTimeout(() => {
    setDelay(Math.min(currentDelay * 1.5, maxDelay));
    connect();
  }, currentDelay);

  setTimer(timer);
}
