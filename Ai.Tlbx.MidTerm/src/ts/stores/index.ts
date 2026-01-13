/**
 * Nanostores State Management
 *
 * This module provides reactive state management using nanostores.
 * Migration from legacy state.ts will happen incrementally.
 *
 * Usage:
 *   import { $sessions, $activeSessionId } from './stores';
 *   $sessions.subscribe(sessions => renderSessionList(sessions));
 *   $sessions.set([...newSessions]);
 */

// Nanostores is available - stores will be added during migration
export {};
