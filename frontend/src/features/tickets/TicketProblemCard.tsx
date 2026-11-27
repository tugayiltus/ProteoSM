/**
 * @input An incident and whether the viewer is IT staff or an admin.
 * @output The incident's problem (number, status, owner team, workaround, link), or "Probleme bağla" and "Problem oluştur".
 * @position Ticket detail page, main column. Employees and requests get no card and make no request.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { ActionDialog } from '../../shared/feedback/ActionDialog';
import { ErrorState } from '../../shared/feedback/States';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { problemPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { useLinkIncidentToProblem, useTicketProblem, type TicketProblemItem } from '../problems/api';
import { ProblemStatusBadge } from '../problems/ProblemBadges';
import { ProblemFormDialog } from '../problems/ProblemFormDialog';

import type { TicketDetail } from './api';

const META_SEPARATOR = ' · ';
const INCIDENT_KIND: TicketDetail['kind'] = 'incident';

function LinkDialog({ ticketNumber, onClose }: { ticketNumber: string; onClose: () => void }) {
  const { t } = useTranslation('tickets');
  const link = useLinkIncidentToProblem(ticketNumber);
  const [number, setNumber] = useState('');
  return (
    <ActionDialog
      title={t('problem.link')}
      confirmLabel={t('problem.confirmLink')}
      isPending={link.isPending}
      isConfirmDisabled={number.trim() === ''}
      error={link.error}
      onConfirm={() => link.mutate(number.trim(), { onSuccess: onClose })}
      onClose={onClose}
    >
      <TextInput label={t('problem.number')} value={number} onChange={setNumber} />
    </ActionDialog>
  );
}

function LinkedProblem({ problem }: { problem: TicketProblemItem }) {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  return (
    <>
      <List density="compact" aria-label={t('problem.title')}>
        <ListItem
          label={
            <StateLink href={problemPath(problem.number)} state={linkState(location)}>
              {`${problem.number}${META_SEPARATOR}${problem.title}`}
            </StateLink>
          }
          description={`${t('problem.team')}: ${problem.ownerTeamName}`}
          endContent={<ProblemStatusBadge status={problem.status} />}
        />
      </List>
      <MetadataList label={{ position: 'top' }}>
        <MetadataListItem label={t('problem.workaround')}>
          {problem.workaround === null ? <Text type="supporting">{t('problem.noWorkaround')}</Text> : <Text wordBreak="break-word">{problem.workaround}</Text>}
        </MetadataListItem>
      </MetadataList>
    </>
  );
}

/** The incident's problem card (IT staff and admins only); a request never has a problem. */
export function TicketProblemCard({ ticket, isStaff }: { ticket: TicketDetail; isStaff: boolean }) {
  const { t } = useTranslation('tickets');
  const shown = isStaff && ticket.kind === INCIDENT_KIND;
  const problem = useTicketProblem(ticket.number, shown);
  const [dialog, setDialog] = useState<'link' | 'create' | null>(null);
  const close = () => setDialog(null);
  if (!shown) {
    return null;
  }

  // No problem yet: the title, the message and the two ways to add one share a row.
  const isUnlinked = problem.data === null;
  return (
    <SectionCard
      title={t('problem.title')}
      emptyMessage={isUnlinked ? t('problem.none') : undefined}
      actions={
        isUnlinked && (
          <HStack gap={2} wrap="wrap">
            <Button label={t('problem.link')} variant="secondary" size="sm" onClick={() => setDialog('link')} />
            <Button label={t('problem.create')} variant="secondary" size="sm" icon={<Icon icon={Icons.plus} />} onClick={() => setDialog('create')} />
          </HStack>
        )
      }
    >
      {problem.isError && <ErrorState error={problem.error} onRetry={() => void problem.refetch()} />}
      {problem.data != null && <LinkedProblem problem={problem.data} />}
      {dialog === 'link' && <LinkDialog ticketNumber={ticket.number} onClose={close} />}
      {dialog === 'create' && (
        <ProblemFormDialog incident={{ number: ticket.number, subject: ticket.subject, description: ticket.description }} onSaved={close} onClose={close} />
      )}
    </SectionCard>
  );
}
