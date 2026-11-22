/**
 * @input Nothing.
 * @output A card link to the open problems of my teams (Ekibimin problemleri), with the problem count.
 * @position IT workspace (İş listem), beside the ticket, task and change queue cards.
 */
import { useTranslation } from 'react-i18next';

import { ActionCard } from '../../shared/page/ActionCard';

import { problemQueuePath, TEAM_OPEN_QUEUE, useProblems } from './api';

const QUEUE = TEAM_OPEN_QUEUE;
const COUNT_PAGE_SIZE = 1;
const FIRST_PAGE = 1;
const PENDING_COUNT = '…';

/** The count is the list endpoint's total for a one-row page; it is part of the link name. */
export function ProblemQueueCard() {
  const { t } = useTranslation('problems');
  const count = useProblems({ queue: QUEUE, status: undefined, impact: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  const title = t('list.teamCard');
  const countText = count.data ? t('list.cardCount', { count: count.data.totalCount }) : PENDING_COUNT;
  return <ActionCard to={problemQueuePath(QUEUE)} title={title} description={countText} label={`${title} ${countText}`} icon="problems" iconColor="accent" />;
}
