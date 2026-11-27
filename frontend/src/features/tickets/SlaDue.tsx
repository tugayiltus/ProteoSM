/**
 * @input The SLA timers of a ticket, or the load state of the ticket that holds them.
 * @output The date to watch: a list cell (state and due time) and a header line ("Çözüm SLA'sı risk altında · Son tarih Bugün 11:00").
 * @position Ticket and queue lists, ticket detail header.
 */
import { HStack } from '@astryxdesign/core/HStack';
import { StatusDot } from '@astryxdesign/core/StatusDot';
import { Text } from '@astryxdesign/core/Text';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import { currentSla, slaState, useFormatDue, type SlaEntry, type SlaState } from './slaState';

type DotVariant = ComponentProps<typeof StatusDot>['variant'];

const STATE_DOT: Record<SlaState, DotVariant> = {
  running: 'accent',
  paused: 'neutral',
  warned: 'warning',
  breached: 'error',
  met: 'success',
  metBreached: 'error',
  cancelled: 'neutral',
};

const LINE_SEPARATOR = ' · ';

/** The SLA data is part of the ticket, so it loads and fails with the ticket. The backend gives no reason for a missing SLA. */
export type SlaLoad = { status: 'loading' } | { status: 'error' } | { status: 'ready'; sla: readonly SlaEntry[] };

/** List cell: the state dot and the due time of the timer to watch. `data-sla-state` lets tests read the state. */
export function TicketDueCell({ sla }: { sla: readonly SlaEntry[] }) {
  const { t } = useTranslation('tickets');
  const formatDue = useFormatDue();
  const current = currentSla(sla);
  if (current === null) {
    return <Text type="supporting">{t('sla.none')}</Text>;
  }
  const state = slaState(current);
  const stateText = t(`sla.headerStates.${state}`);
  return (
    <HStack gap={1} vAlign="center" wrap="wrap" data-sla-state={state}>
      <StatusDot variant={STATE_DOT[state]} label={stateText} />
      <Text>{current.dueAt !== null && current.result === null ? formatDue(current.dueAt) : stateText}</Text>
    </HStack>
  );
}

/** Header line: what the timer is doing and, while it runs, when it ends. */
export function TicketSlaLine({ load }: { load: SlaLoad }) {
  const { t } = useTranslation('tickets');
  const formatDue = useFormatDue();
  if (load.status === 'loading') {
    return <Text type="supporting">{t('sla.loading')}</Text>;
  }
  if (load.status === 'error') {
    return <Text type="supporting">{t('sla.error')}</Text>;
  }
  const current = currentSla(load.sla);
  if (current === null) {
    return <Text type="supporting">{t('sla.none')}</Text>;
  }
  const state = slaState(current);
  const metric = t(`sla.headerMetrics.${current.metric as 'resolution'}`);
  const parts = [`${metric} ${t(`sla.headerStates.${state}`)}`];
  if (current.dueAt !== null && current.result === null) {
    parts.push(t('sla.dueLine', { date: formatDue(current.dueAt) }));
  }
  return (
    <HStack gap={1} vAlign="center" wrap="wrap" data-sla-state={state}>
      <StatusDot variant={STATE_DOT[state]} label={t(`sla.headerStates.${state}`)} />
      <Text type="supporting">{parts.join(LINE_SEPARATOR)}</Text>
    </HStack>
  );
}
