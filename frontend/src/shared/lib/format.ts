import { TURKISH_LOCALE } from './turkish';

/** Display time zone of the institution (spec assumption; stored times are UTC). */
export const APP_TIME_ZONE = 'Europe/Istanbul';

const dateTimeFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: APP_TIME_ZONE,
});

export const formatDateTime = (iso: string): string => dateTimeFormatter.format(new Date(iso));

const UTC = 'UTC';
const MIDNIGHT_UTC = 'T00:00:00Z';
const dateFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, { dateStyle: 'medium', timeZone: UTC });

/** Formats an ISO calendar date ("2027-12-31"); read as UTC midnight so no time zone moves it to another day. */
export const formatDate = (isoDate: string): string => dateFormatter.format(new Date(`${isoDate}${MIDNIGHT_UTC}`));

const timeFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, { timeStyle: 'short', timeZone: APP_TIME_ZONE });
/** Calendar day in the display time zone, as a comparable key. */
const DAY_KEY_LENGTH = 10;
const dayKeyFormatter = new Intl.DateTimeFormat('en-CA', { dateStyle: 'short', timeZone: APP_TIME_ZONE });

export const formatTime = (iso: string): string => timeFormatter.format(new Date(iso));

/** How far a moment lies from today in the display time zone. */
export function dayRelation(iso: string, now: Date = new Date()): 'today' | 'tomorrow' | 'other' {
  const key = dayKeyFormatter.format(new Date(iso));
  if (key === dayKeyFormatter.format(now)) {
    return 'today';
  }
  // The next calendar day of the display zone, by date arithmetic on the day key (no 24-hour guess).
  const next = new Date(`${dayKeyFormatter.format(now)}${MIDNIGHT_UTC}`);
  next.setUTCDate(next.getUTCDate() + 1);
  return key === next.toISOString().slice(0, DAY_KEY_LENGTH) ? 'tomorrow' : 'other';
}
