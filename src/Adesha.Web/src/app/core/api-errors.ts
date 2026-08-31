/** Status 0 is a browser-level failure; 502/503/504 come from a proxy in front of a stopped API. */
export const UNREACHABLE_STATUSES = new Set([0, 502, 503, 504]);
