/**
 * @input Nothing; reads the focused element and the page title.
 * @output Moves focus to the page title when focus fell to the page body.
 * @position Called after a control that held focus left the page, for example a closed dialog.
 */
/** Moves focus to the page title when focus has fallen to the page body, for example after the control that held it left the page. */
export function focusMainHeadingIfLost(): void {
  const active = document.activeElement;
  if (active === null || active === document.body) {
    document.querySelector<HTMLElement>('h1')?.focus();
  }
}
