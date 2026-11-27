import { useTranslation } from 'react-i18next';

import { dayRelation, formatDateTime, formatTime } from '../../shared/lib/format';

import type { TicketSla } from './api';

const SECONDS_PER_MINUTE = 60;
const MINUTES_PER_HOUR = 60;

/** Metric name of the known-error timer of a problem. */
export const KNOWN_ERROR_METRIC = 'known_error';

/** One timer as the badge shows it: a ticket timer, or the known-error timer of a problem (its `metric` is `known_error`). */
export type SlaEntry = Omit<TicketSla, 'metric'> & { metric: TicketSla['metric'] | typeof KNOWN_ERROR_METRIC };

export type SlaState = 'running' | 'paused' | 'warned' | 'breached' | 'met' | 'metBreached' | 'cancelled';

/** A stopped timer shows its result; a running one shows how close it is to the target. */
export function slaState(sla: SlaEntry): SlaState {
  if (sla.result === 'met') {
    return 'met';
  }
  if (sla.result === 'breached') {
    return 'metBreached';
  }
  if (sla.result === 'cancelled') {
    return 'cancelled';
  }
  if (sla.isBreached || (sla.remainingSeconds ?? 0) < 0) {
    return 'breached';
  }
  if (sla.isPaused) {
    return 'paused';
  }
  return sla.isWarned ? 'warned' : 'running';
}

export function useFormatBusinessDuration() {
  const { t } = useTranslation('tickets');
  return (seconds: number) => {
    const totalMinutes = Math.floor(Math.abs(seconds) / SECONDS_PER_MINUTE);
    const hours = Math.floor(totalMinutes / MINUTES_PER_HOUR);
    const minutes = totalMinutes % MINUTES_PER_HOUR;
    return hours > 0 ? t('sla.duration.hoursMinutes', { hours, minutes }) : t('sla.duration.minutes', { minutes });
  };
}

/**
 * The timer that sets the date to watch: the running timer due first. Without a due date (paused) the first running timer
 * stands in, and when every timer has stopped the first one tells the result. An empty list gives `null`.
 */
export function currentSla<T extends SlaEntry>(sla: readonly T[]): T | null {
  const running = sla.filter((item) => item.result === null);
  const due = running.filter((item) => item.dueAt !== null).sort((a, b) => Date.parse(a.dueAt ?? '') - Date.parse(b.dueAt ?? ''));
  return due[0] ?? running[0] ?? sla[0] ?? null;
}

/** "Bugün 11:00", "Yarın 11:00" or the full date and time. */
export function useFormatDue() {
  const { t } = useTranslation('tickets');
  return (iso: string, now: Date = new Date()) => {
    const relation = dayRelation(iso, now);
    return relation === 'other' ? formatDateTime(iso) : t(`sla.${relation}`, { time: formatTime(iso) });
  };
}
