import { describe, expect, it } from 'vitest';

import adminHtml from '../../../admin/index.html?raw';
import indexHtml from '../../../index.html?raw';
import favicon from '../../../public/favicon.svg?raw';
import common from '../i18n/locales/tr/common.json';

import { MARK_LINE, MARK_LINE_WIDTH, MARK_POINT, MARK_VIEW_BOX } from './mark';

describe('brand', () => {
  it('draws the favicon from the same shapes as the app mark', () => {
    expect(favicon).toContain(`viewBox="${MARK_VIEW_BOX}"`);
    expect(favicon).toContain(`d="${MARK_LINE}" stroke-width="${MARK_LINE_WIDTH}"`);
    expect(favicon).toContain(`cx="${MARK_POINT.cx}" cy="${MARK_POINT.cy}" r="${MARK_POINT.r}"`);
  });

  it('names the product in the title of both entries', () => {
    expect(common.appName).toBe('ProteoSM');
    expect(indexHtml).toContain(`<title>${common.appName}</title>`);
    expect(adminHtml).toContain(`<title>${common.appName} Yönetim</title>`);
  });

  it('links the same icons from both entries', () => {
    for (const icon of ['/favicon.svg', '/favicon-32.png', '/apple-touch-icon.png']) {
      expect(indexHtml).toContain(`href="${icon}"`);
      expect(adminHtml).toContain(`href="${icon}"`);
    }
  });
});
