/**
 * @input The view in use: my work, unassigned tickets, SLA risk, or one of the other queues.
 * @output Three counted views as links (Bana atanan, Atanmamış, SLA riski) and a "Diğer kuyruklar" toggle that opens the other queue cards.
 * @position Under the title of İş listem and of its queue pages.
 */
import { Badge } from '@astryxdesign/core/Badge';
import { Button } from '@astryxdesign/core/Button';
import { Grid } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ROUTES } from '../../shared/routes';
import { ChangeQueueCard } from '../changes/ChangeQueueCard';
import { ProblemQueueCard } from '../problems/ProblemQueueCard';
import type { TaskQueue } from '../tasks/api';
import { useWorkItems } from '../tasks/api';
import { TaskQueueCard } from '../tasks/TaskQueueCard';
import { OTHER_TICKET_QUEUES, useTickets } from '../tickets/api';

import { QueueCard } from './QueueCard';
import { queuePath } from './queuePath';

/** `assigned` is the combined list of tickets, tasks and changes; the other two are ticket queues. */
export type WorkView = 'assigned' | 'unassigned' | 'sla_risk' | 'other';

const COUNT_PAGE_SIZE = 1;
const FIRST_PAGE = 1;
const PENDING_COUNT = '…';
const OTHERS_PANEL_ID = 'work-other-queues';
/** Task queues shown as cards beside the ticket queues (ADR-0031). */
const TASK_CARD_QUEUES: readonly TaskQueue[] = ['mine', 'team', 'overdue'];
/** Queue cards reflow from one column on phones to one row on wide screens. */
const QUEUE_COLUMNS = { minWidth: 200 } as const;

interface CountState {
  data: { totalCount: number } | undefined;
  isError: boolean;
}

interface ViewLinkProps {
  to: string;
  label: string;
  count: CountState;
  isCurrent: boolean;
}

function ViewLink({ to, label, count, isCurrent }: ViewLinkProps) {
  const { t } = useTranslation('tickets');
  const text = count.data ? String(count.data.totalCount) : count.isError ? t('workspace.countUnknown') : PENDING_COUNT;
  return <Button href={to} label={label} variant={isCurrent ? 'primary' : 'secondary'} aria-current={isCurrent ? 'page' : undefined} endContent={<Badge label={text} />} />;
}

export function WorkViewBar({ view }: { view: WorkView }) {
  const { t } = useTranslation('tickets');
  const [othersOpen, setOthersOpen] = useState(view === 'other');
  const mine = useWorkItems({ queue: 'assigned_to_me', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  const unassigned = useTickets({ queue: 'unassigned', status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  const slaRisk = useTickets({ queue: 'sla_risk', status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  return (
    <VStack gap={3}>
      <HStack gap={2} vAlign="center" wrap="wrap" role="group" aria-label={t('workspace.viewsLabel')}>
        <ViewLink to={ROUTES.workspace} label={t('workspace.assigned_to_me')} count={mine} isCurrent={view === 'assigned'} />
        <ViewLink to={queuePath('unassigned')} label={t('workspace.unassigned')} count={unassigned} isCurrent={view === 'unassigned'} />
        <ViewLink to={queuePath('sla_risk')} label={t('workspace.sla_risk')} count={slaRisk} isCurrent={view === 'sla_risk'} />
        <Button
          label={t('workspace.others')}
          variant={view === 'other' ? 'primary' : 'secondary'}
          aria-expanded={othersOpen}
          // The panel exists only while open; aria-controls must not point to a missing element.
          aria-controls={othersOpen ? OTHERS_PANEL_ID : undefined}
          onClick={() => setOthersOpen((open) => !open)}
        />
      </HStack>
      {othersOpen && (
        <Grid id={OTHERS_PANEL_ID} columns={QUEUE_COLUMNS} gap={3} role="region" aria-label={t('workspace.othersPanel')}>
          {OTHER_TICKET_QUEUES.map((queue) => (
            <QueueCard key={queue} queue={queue} />
          ))}
          {TASK_CARD_QUEUES.map((queue) => (
            <TaskQueueCard key={queue} queue={queue} />
          ))}
          <ChangeQueueCard queue="mine" />
          <ChangeQueueCard queue="this_week" />
          <ProblemQueueCard />
        </Grid>
      )}
    </VStack>
  );
}
