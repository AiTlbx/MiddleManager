/**
 * Badges Module
 *
 * Manages global status badges (connection, paste indicator).
 * Per-terminal badges (scaled view) remain in their respective modules.
 */

import { $connectionStatus } from '../../stores';

let connectionBadge: HTMLElement | null = null;
let pasteBadge: HTMLElement | null = null;

/**
 * Initialize all global badges. Call once during bootstrap.
 */
export function initBadges(): void {
  connectionBadge = document.getElementById('connection-status');
  pasteBadge = document.getElementById('paste-indicator');

  $connectionStatus.subscribe((status) => {
    if (!connectionBadge) return;

    const text =
      status === 'connected'
        ? ''
        : status === 'disconnected'
          ? 'Server disconnected'
          : 'Reconnecting...';

    connectionBadge.className = `status-badge connection-status ${status}`;
    connectionBadge.textContent = text;
  });
}

/**
 * Show the paste indicator badge.
 */
export function showPasteIndicator(): void {
  if (pasteBadge) {
    pasteBadge.textContent = 'Pasting...';
    pasteBadge.classList.add('active');
  }
}

/**
 * Hide the paste indicator badge.
 */
export function hidePasteIndicator(): void {
  if (pasteBadge) {
    pasteBadge.classList.remove('active');
  }
}
