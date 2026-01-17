/**
 * Process Module
 *
 * Exports process monitoring functionality.
 */

export {
  registerProcessStateCallback,
  getProcessState,
  handleProcessEvent,
  handleForegroundChange,
  clearProcessState,
  initializeFromSession,
  getRacingLogText,
  getFullRacingLog,
  isRacingLogVisible,
  getForegroundInfo,
} from './processMonitor';
