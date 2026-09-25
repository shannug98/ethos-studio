/**
 * ETHOS DANCE STUDIO - FEATURE FLAGS CONFIGURATION
 * 
 * Centralized feature flags for controlling page availability,
 * portal gating, and phased feature rollouts.
 * 
 * In future iterations, these flags can be dynamically synced with
 * backend tenant configuration or an Admin CMS settings endpoint.
 */

export const FEATURE_FLAGS = {
  // Coming Soon landing pages for classes and private events
  CLASSES_COMING_SOON: true,
  EVENTS_COMING_SOON: true,

  // Student and Trainer member portal access
  ENABLE_MEMBER_PORTALS: false,
};
