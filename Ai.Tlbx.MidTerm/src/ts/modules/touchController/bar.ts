/**
 * Touch Controller Bar
 *
 * Manages the visibility and lifecycle of the touch controller bar.
 */

import { CSS_CLASSES, SELECTORS } from './constants';
import { initModifiers, clearModifiers } from './modifiers';
import { initEvents, teardownEvents } from './events';
import {
  shouldShowTouchController,
  setupPointerDetection,
  teardownPointerDetection,
} from './detection';

let controllerElement: HTMLElement | null = null;
let isInitialized = false;

/**
 * Initialize the touch controller bar
 */
export function initTouchController(): void {
  if (isInitialized) return;

  controllerElement = document.querySelector<HTMLElement>(SELECTORS.controller);
  if (!controllerElement) {
    return;
  }

  initModifiers(controllerElement);
  initEvents(controllerElement);

  setupPointerDetection(handlePointerChange);

  updateVisibility();

  window.addEventListener('resize', handleResize);

  isInitialized = true;
}

/**
 * Destroy the touch controller
 */
export function destroyTouchController(): void {
  if (!isInitialized) return;

  teardownEvents();
  teardownPointerDetection();
  window.removeEventListener('resize', handleResize);

  if (controllerElement) {
    controllerElement.classList.remove(CSS_CLASSES.visible);
  }
  document.body.classList.remove(CSS_CLASSES.touchMode);

  controllerElement = null;
  isInitialized = false;
}

/**
 * Show the touch controller
 */
export function showTouchController(): void {
  if (!controllerElement) return;
  controllerElement.classList.add(CSS_CLASSES.visible);
  document.body.classList.add(CSS_CLASSES.touchMode);
}

/**
 * Hide the touch controller
 */
export function hideTouchController(): void {
  if (!controllerElement) return;
  controllerElement.classList.remove(CSS_CLASSES.visible);
  document.body.classList.remove(CSS_CLASSES.touchMode);
  clearModifiers();
}

/**
 * Update visibility based on current device state
 */
export function updateVisibility(): void {
  if (shouldShowTouchController()) {
    showTouchController();
  } else {
    hideTouchController();
  }
}

function handlePointerChange(hasPrecisePointer: boolean): void {
  if (hasPrecisePointer) {
    hideTouchController();
  } else {
    updateVisibility();
  }
}

function handleResize(): void {
  updateVisibility();
}
