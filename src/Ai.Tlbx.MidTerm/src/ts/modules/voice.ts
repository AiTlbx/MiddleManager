/**
 * Voice Module
 *
 * Handles WebSocket connection to MidTerm.Voice server
 * and bridges audio capture/playback.
 */

import { createLogger } from './logging';
import {
  setVoiceStatus,
  setMicActive,
  setToggleEnabled,
  setToggleRecording,
} from './sidebar/voiceSection';

const log = createLogger('voice');
const VOICE_SERVER_PORT = 2010;

let ws: WebSocket | null = null;
let isSessionActive = false;
let voiceServerAvailable = false;

/**
 * Check if MidTerm.Voice server is available
 */
export async function checkVoiceServerHealth(): Promise<boolean> {
  try {
    const host = window.location.hostname;
    const url = `http://${host}:${VOICE_SERVER_PORT}/api/health`;

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 2000);

    const response = await fetch(url, { signal: controller.signal });
    clearTimeout(timeoutId);

    if (response.ok) {
      const data = await response.json();
      voiceServerAvailable = data.status === 'ok';
      log.info(() => `Voice server available: v${data.version}`);
      return voiceServerAvailable;
    }
  } catch {
    // Server not available - this is expected if not running
    log.info(() => 'Voice server not available');
  }
  voiceServerAvailable = false;
  return false;
}

/**
 * Get current voice server availability status
 */
export function isVoiceServerAvailable(): boolean {
  return voiceServerAvailable;
}

/**
 * Request microphone permission and initialize audio
 */
export async function requestMicrophonePermission(): Promise<boolean> {
  try {
    log.info(() => 'Requesting microphone permission');

    if (!window.initAudioWithUserInteraction) {
      log.error(() => 'Audio API not available');
      setVoiceStatus('Audio API not available');
      return false;
    }

    const result = await window.initAudioWithUserInteraction();
    if (!result) {
      setVoiceStatus('Audio init failed');
      return false;
    }

    if (window.requestMicrophonePermissionAndGetDevices) {
      await window.requestMicrophonePermissionAndGetDevices();
    }

    log.info(() => 'Microphone permission granted');
    setVoiceStatus('Ready');
    return true;
  } catch (error) {
    log.error(() => `Microphone permission error: ${error}`);
    setVoiceStatus('Mic permission denied');
    return false;
  }
}

/**
 * Start a voice session - connect to MidTerm.Voice and begin recording
 */
export async function startVoiceSession(): Promise<void> {
  if (isSessionActive) {
    log.warn(() => 'Voice session already active');
    return;
  }

  try {
    const host = window.location.hostname;
    const wsUrl = `ws://${host}:${VOICE_SERVER_PORT}/voice`;

    log.info(() => `Connecting to voice server: ${wsUrl}`);
    setVoiceStatus('Connecting...');

    ws = new WebSocket(wsUrl);

    ws.onopen = async () => {
      log.info(() => 'Voice WebSocket connected');
      setVoiceStatus('Connected');

      // Send start message
      ws?.send(JSON.stringify({ type: 'start' }));

      // Start recording
      if (window.startRecording) {
        const success = await window.startRecording(
          (base64Audio: string) => {
            if (ws && ws.readyState === WebSocket.OPEN) {
              // Convert base64 to ArrayBuffer and send
              const bytes = base64ToArrayBuffer(base64Audio);
              ws.send(bytes);
            }
          },
          500,
          null,
          24000,
        );

        if (success) {
          isSessionActive = true;
          setVoiceStatus('Listening...');
          setToggleRecording(true);
        } else {
          setVoiceStatus('Recording failed');
          ws?.close();
        }
      }
    };

    ws.onmessage = async (event: MessageEvent) => {
      if (event.data instanceof Blob) {
        // Audio data from server
        const arrayBuffer = await event.data.arrayBuffer();
        const base64 = arrayBufferToBase64(arrayBuffer);

        if (window.playAudio) {
          await window.playAudio(base64, 24000);
        }
      } else if (typeof event.data === 'string') {
        // JSON message
        try {
          const msg = JSON.parse(event.data);
          handleVoiceMessage(msg);
        } catch {
          log.warn(() => `Invalid JSON from voice server: ${event.data}`);
        }
      }
    };

    ws.onclose = () => {
      log.info(() => 'Voice WebSocket closed');
      isSessionActive = false;
      setVoiceStatus('Disconnected');
      setToggleRecording(false);
    };

    ws.onerror = (error) => {
      log.error(() => `Voice WebSocket error: ${error}`);
      setVoiceStatus('Connection error');
    };
  } catch (error) {
    log.error(() => `Failed to start voice session: ${error}`);
    setVoiceStatus('Connection failed');
  }
}

/**
 * Stop the voice session
 */
export async function stopVoiceSession(): Promise<void> {
  if (!isSessionActive) {
    return;
  }

  log.info(() => 'Stopping voice session');

  // Stop recording
  if (window.stopRecording) {
    await window.stopRecording();
  }

  // Stop playback
  if (window.stopAudioPlayback) {
    await window.stopAudioPlayback();
  }

  // Send stop message and close WebSocket
  if (ws && ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify({ type: 'stop' }));
    ws.close();
  }

  isSessionActive = false;
  setVoiceStatus('Ready');
  setToggleRecording(false);
}

/**
 * Handle messages from the voice server
 */
function handleVoiceMessage(msg: { type: string; status?: string }): void {
  switch (msg.type) {
    case 'status':
      if (msg.status) {
        setVoiceStatus(msg.status);
      }
      break;
    case 'speaking':
      setVoiceStatus('Speaking...');
      break;
    case 'listening':
      setVoiceStatus('Listening...');
      break;
    case 'error':
      setVoiceStatus('Server error');
      break;
    default:
      log.info(() => `Unknown voice message type: ${msg.type}`);
  }
}

/**
 * Convert base64 string to ArrayBuffer
 */
function base64ToArrayBuffer(base64: string): ArrayBuffer {
  const binaryString = atob(base64);
  const bytes = new Uint8Array(binaryString.length);
  for (let i = 0; i < binaryString.length; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  return bytes.buffer;
}

/**
 * Convert ArrayBuffer to base64 string
 */
function arrayBufferToBase64(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = '';
  for (let i = 0; i < bytes.byteLength; i++) {
    binary += String.fromCharCode(bytes[i]!);
  }
  return btoa(binary);
}

/**
 * Bind voice button event handlers
 */
export function bindVoiceEvents(): void {
  const micBtn = document.getElementById('btn-voice-mic');
  const toggleBtn = document.getElementById('btn-voice-toggle');

  if (micBtn) {
    micBtn.addEventListener('click', async () => {
      const success = await requestMicrophonePermission();
      if (success) {
        setMicActive(true);
        setToggleEnabled(true);
      }
    });
  }

  if (toggleBtn) {
    toggleBtn.addEventListener('click', async () => {
      if (isSessionActive) {
        await stopVoiceSession();
      } else {
        await startVoiceSession();
      }
    });
  }

  // Set up error callback
  if (window.setOnError) {
    window.setOnError((error: string) => {
      log.error(() => `Audio error: ${error}`);
      setVoiceStatus('Error');
    });
  }
}
