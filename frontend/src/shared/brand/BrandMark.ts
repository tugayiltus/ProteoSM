/**
 * @output The mark as an icon in the current text color, so light and dark themes need no second file.
 * @position `Icons.brand`; render it through `Icon`.
 */
import { createLucideIcon } from 'lucide-react';

import { MARK_LINE, MARK_LINE_WIDTH, MARK_POINT } from './mark';

export const BrandMark = createLucideIcon('proteone', [
  ['path', { d: MARK_LINE, strokeWidth: MARK_LINE_WIDTH, key: 'line' }],
  ['circle', { ...MARK_POINT, fill: 'currentColor', stroke: 'none', key: 'point' }],
]);
