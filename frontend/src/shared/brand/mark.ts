/**
 * @output The ProteoSM mark: one line that turns into a "P" and closes around a single point (one record, one source).
 * @position One source for the app icon; `public/favicon.svg` repeats the shapes and a test keeps them equal.
 */
export const MARK_VIEW_BOX = '0 0 24 24';
export const MARK_LINE = 'M6.5 21.5V9.5a6.5 6.5 0 1 1 6.5 6.5h-2';
export const MARK_LINE_WIDTH = 2.75;
export const MARK_POINT = { cx: 13, cy: 9.5, r: 2.25 } as const;
