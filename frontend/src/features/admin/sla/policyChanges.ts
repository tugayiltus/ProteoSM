/**
 * @input The current policy targets and pause rules, and the values typed in the form.
 * @output The targets and pause reasons that differ, so the admin sees them before saving a new version.
 * @position Used by the SLA policy page and its change summary.
 */
import type { HoldReason, PauseRuleItem, PolicyTargetItem, Priority, SlaMetric, TicketKind } from './api';

export interface CellDraft {
  minutes: number | null;
  percent: number | null;
}

export type PauseDraft = Record<HoldReason, { firstResponse: boolean; resolution: boolean }>;

export interface TargetChange {
  kind: TicketKind;
  priority: Priority;
  metric: SlaMetric;
  field: 'minutes' | 'percent';
  from: number;
  to: number;
}

export const cellKey = (kind: string, priority: string, metric: string) => `${kind}:${priority}:${metric}`;

/** Base targets only; request-type overrides carry over unchanged. Cells with no valid number are skipped. */
export function diffTargets(base: readonly PolicyTargetItem[], cells: Record<string, CellDraft>): TargetChange[] {
  return base
    .filter((target) => target.requestTypeId === null)
    .flatMap((target) => {
      const cell = cells[cellKey(target.kind, target.priority, target.metric)];
      const identity = { kind: target.kind, priority: target.priority, metric: target.metric };
      const changes: TargetChange[] = [];
      if (cell?.minutes != null && cell.minutes !== target.targetMinutes) {
        changes.push({ ...identity, field: 'minutes', from: target.targetMinutes, to: cell.minutes });
      }
      if (cell?.percent != null && cell.percent !== target.warningPercent) {
        changes.push({ ...identity, field: 'percent', from: target.warningPercent, to: cell.percent });
      }
      return changes;
    });
}

/** Hold reasons whose pause switches differ from the current rules. */
export function diffPauses(rules: readonly PauseRuleItem[], draft: PauseDraft): HoldReason[] {
  return (Object.keys(draft) as HoldReason[]).filter((reason) => {
    const rule = rules.find((r) => r.holdReason === reason);
    return (rule?.pausesFirstResponse ?? false) !== draft[reason].firstResponse || (rule?.pausesResolution ?? false) !== draft[reason].resolution;
  });
}
