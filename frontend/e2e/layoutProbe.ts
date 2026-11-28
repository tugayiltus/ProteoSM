/**
 * @input A page that finished loading.
 * @output A list of layout problems: sideways page overflow, a clipped side menu, tables wider than the viewport or with a hidden last
 * column, and buttons or links that sit outside the viewport.
 * @position Used by the UI QA sweep (`ui-qa.spec.ts`), which fails when the list is not empty.
 */
import type { Page } from '@playwright/test';

export const OVERFLOW_TOLERANCE_PX = 1;
/** Below this width the menu is a drawer, which scrolls by design. */
const PHONE_MAX_WIDTH_PX = 600;

/** Runs in the browser. Returns one line per problem. */
export async function layoutProblems(page: Page, path: string): Promise<string[]> {
  return page.evaluate(({ tolerance, isAdminArea, phoneMax }) => {
    const found: string[] = [];
    const viewport = document.documentElement.clientWidth;
    const describe = (el: Element) => {
      const label = (el.getAttribute('aria-label') ?? el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 40);
      return `${el.tagName.toLowerCase()}${el.getAttribute('role') ? `[${el.getAttribute('role')}]` : ''} "${label}"`;
    };
    const visible = (el: Element) => {
      const rect = el.getBoundingClientRect();
      return rect.width > 0 && rect.height > 0 && getComputedStyle(el).visibility !== 'hidden';
    };
    /** The right edge that really shows: the viewport narrowed by every ancestor that clips sideways. */
    const visibleRight = (el: Element) => {
      let right = viewport;
      for (let parent = el.parentElement; parent !== null; parent = parent.parentElement) {
        if (getComputedStyle(parent).overflowX !== 'visible') {
          right = Math.min(right, parent.getBoundingClientRect().right);
        }
      }
      return right;
    };
    const page = document.documentElement.scrollWidth - viewport;
    if (page > tolerance) {
      found.push(`page overflow ${page}px`);
    }
    for (const table of document.querySelectorAll('table, [role="table"], [role="grid"]')) {
      if (!visible(table)) {
        continue;
      }
      const rect = table.getBoundingClientRect();
      if (rect.right > viewport + tolerance || table.scrollWidth > viewport + tolerance) {
        found.push(`table wider than the viewport (${Math.round(Math.max(rect.right, table.scrollWidth))}px > ${viewport}px): ${describe(table)}`);
      }
      const firstRow = table.querySelector('tbody tr, [role="row"]:not(:has([role="columnheader"]))');
      const lastCell = firstRow?.lastElementChild;
      if (lastCell && visible(lastCell) && lastCell.getBoundingClientRect().right > visibleRight(lastCell) + tolerance) {
        found.push(`table hides its last column: ${describe(table)}`);
      }
    }
    for (const control of document.querySelectorAll('main a[href], main button, header button')) {
      if (visible(control) && control.getBoundingClientRect().right > visibleRight(control) + tolerance) {
        found.push(`control outside the viewport: ${describe(control)}`);
      }
    }
    // The admin menu has more entries than any desktop height shows, so only the app menu must fit without scrolling.
    if (!isAdminArea && viewport > phoneMax) {
      for (const nav of document.querySelectorAll('nav.astryx-side-nav')) {
        for (const scroller of nav.querySelectorAll('*')) {
          if (getComputedStyle(scroller).overflowY === 'auto' && scroller.scrollHeight > scroller.clientHeight + tolerance) {
            found.push(`side menu is cut off: ${scroller.scrollHeight - scroller.clientHeight}px of entries hide under the footer`);
          }
        }
      }
    }
    return found;
  }, { tolerance: OVERFLOW_TOLERANCE_PX, isAdminArea: path.startsWith('/admin'), phoneMax: PHONE_MAX_WIDTH_PX });
}
