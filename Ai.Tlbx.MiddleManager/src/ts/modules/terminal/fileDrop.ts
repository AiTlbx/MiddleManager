/**
 * File Drop Module
 *
 * Handles drag-and-drop file uploads and clipboard image paste.
 * Files are uploaded to the server and the resulting path is inserted into the terminal.
 */

import { activeSessionId } from '../../state';

// Bracketed paste mode escape sequences
const PASTE_START = '\x1b[200~';
const PASTE_END = '\x1b[201~';

// Forward declaration for sendInput
let sendInput: (sessionId: string, data: string) => void = () => {};

/**
 * Register the sendInput callback from mux channel
 */
export function registerFileDropCallbacks(callbacks: {
  sendInput?: (sessionId: string, data: string) => void;
}): void {
  if (callbacks.sendInput) sendInput = callbacks.sendInput;
}

/**
 * Upload a file to the server for the given session
 */
async function uploadFile(sessionId: string, file: File): Promise<string | null> {
  const formData = new FormData();
  formData.append('file', file);

  try {
    const response = await fetch(`/api/sessions/${sessionId}/upload`, {
      method: 'POST',
      body: formData
    });

    if (!response.ok) {
      console.error('File upload failed:', response.status);
      return null;
    }

    const result = await response.json();
    return result.path;
  } catch (error) {
    console.error('File upload error:', error);
    return null;
  }
}

/**
 * Handle file drop - upload and insert path
 */
async function handleFileDrop(files: FileList): Promise<void> {
  if (!activeSessionId || files.length === 0) return;

  const paths: string[] = [];

  for (const file of Array.from(files)) {
    const path = await uploadFile(activeSessionId, file);
    if (path) {
      paths.push(path);
    }
  }

  if (paths.length > 0) {
    // Wrap in bracketed paste sequences so TUIs recognize it as pasted content
    sendInput(activeSessionId, PASTE_START + paths.join(' ') + PASTE_END);
  }
}

/**
 * Handle clipboard image paste - convert to file and upload
 */
async function handleClipboardImage(items: DataTransferItemList): Promise<boolean> {
  if (!activeSessionId) return false;

  for (const item of Array.from(items)) {
    if (item.type.startsWith('image/')) {
      const file = item.getAsFile();
      if (file) {
        // Always use .jpg extension - TUIs are smart enough to detect actual format
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        const namedFile = new File([file], `clipboard_${timestamp}.jpg`, { type: file.type });

        const path = await uploadFile(activeSessionId, namedFile);
        if (path) {
          sendInput(activeSessionId, PASTE_START + path + PASTE_END);
          return true;
        }
      }
    }
  }

  return false;
}

/**
 * Set up drag-and-drop handlers for a terminal container
 */
export function setupFileDrop(container: HTMLElement): void {
  // Prevent default drag behaviors
  container.addEventListener('dragover', (e) => {
    e.preventDefault();
    e.stopPropagation();
    container.classList.add('drag-over');
  });

  container.addEventListener('dragleave', (e) => {
    e.preventDefault();
    e.stopPropagation();
    container.classList.remove('drag-over');
  });

  container.addEventListener('dragend', () => {
    container.classList.remove('drag-over');
  });

  // Handle drop
  container.addEventListener('drop', async (e) => {
    e.preventDefault();
    e.stopPropagation();
    container.classList.remove('drag-over');

    const files = e.dataTransfer?.files;
    if (files && files.length > 0) {
      await handleFileDrop(files);
    }
  });
}

/**
 * Handle clipboard paste - checks for images first, falls back to text
 * Used by the keyboard handler in manager.ts
 */
export async function handleClipboardPaste(sessionId: string): Promise<void> {
  // Try to read clipboard items (images)
  try {
    const items = await navigator.clipboard.read();
    for (const item of items) {
      const imageType = item.types.find(t => t.startsWith('image/'));
      if (imageType) {
        const blob = await item.getType(imageType);
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        const file = new File([blob], `clipboard_${timestamp}.jpg`, { type: imageType });
        const path = await uploadFile(sessionId, file);
        if (path) {
          sendInput(sessionId, PASTE_START + path + PASTE_END);
          return; // Image handled, don't paste text
        }
      }
    }
  } catch {
    // clipboard.read() not supported or failed, fall through to text paste
  }

  // No image found or image handling failed, paste text
  try {
    const text = await navigator.clipboard.readText();
    if (text) sendInput(sessionId, PASTE_START + text + PASTE_END);
  } catch {
    // Text paste failed
  }
}
