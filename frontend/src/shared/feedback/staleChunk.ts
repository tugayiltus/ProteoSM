/** Messages of the browsers when a lazy chunk is gone, usually because a deploy replaced the hashed file. */
const STALE_CHUNK_PATTERN = /Failed to fetch dynamically imported module|error loading dynamically imported module|Importing a module script failed/i;

export const isStaleChunkError = (error: unknown): boolean => error instanceof Error && STALE_CHUNK_PATTERN.test(error.message);
