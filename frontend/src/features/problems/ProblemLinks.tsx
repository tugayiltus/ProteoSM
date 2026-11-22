/**
 * @input A problem number, whether the viewer may edit the links and whether new links are still allowed.
 * @output Two panels, İlgili arızalar and Düzelten değişiklikler, each with a bind dialog and a confirmed remove.
 *   A row the viewer cannot open shows its number, title and status without a link.
 * @position Problem detail page, main column.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import type { UseMutationResult } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { ActionDialog } from '../../shared/feedback/ActionDialog';
import { ConfirmDialog } from '../../shared/feedback/ConfirmDialog';
import { ErrorState } from '../../shared/feedback/States';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { changePath, ticketPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { ChangeStatusBadge } from '../changes/ChangeBadges';
import { TicketStatusBadge } from '../tickets/TicketBadges';

import {
  useLinkProblemChange,
  useLinkProblemTicket,
  useProblemChanges,
  useProblemTickets,
  useUnlinkProblemChange,
  useUnlinkProblemTicket,
  type ProblemChangeItem,
  type ProblemTicketItem,
} from './api';

const META_SEPARATOR = ' · ';

interface LinkRow {
  number: string;
  title: string;
  canOpen: boolean;
  href: string;
  description: string | null;
  status: ReactNode;
}

type LinkMutation = UseMutationResult<unknown, Error, string>;

interface PanelText {
  title: string;
  add: string;
  numberLabel: string;
  empty: string;
  removeMessage: string;
}

interface LinkPanelProps {
  text: PanelText;
  rows: readonly LinkRow[] | undefined;
  error: Error | null;
  onRetry: () => void;
  canEdit: boolean;
  canAdd: boolean;
  link: LinkMutation;
  unlink: LinkMutation;
}

function AddDialog({ text, link, onClose }: { text: PanelText; link: LinkMutation; onClose: () => void }) {
  const { t } = useTranslation('problems');
  const [number, setNumber] = useState('');
  return (
    <ActionDialog
      title={text.add}
      confirmLabel={t('links.confirmAdd')}
      isPending={link.isPending}
      isConfirmDisabled={number.trim() === ''}
      error={link.error}
      onConfirm={() => link.mutate(number.trim(), { onSuccess: onClose })}
      onClose={onClose}
    >
      <TextInput label={text.numberLabel} value={number} onChange={setNumber} />
    </ActionDialog>
  );
}

function Row({ row, canEdit, onRemove }: { row: LinkRow; canEdit: boolean; onRemove: (number: string) => void }) {
  const { t } = useTranslation('problems');
  const location = useLocation();
  const label = `${row.number}${META_SEPARATOR}${row.title}`;
  return (
    <ListItem
      label={
        row.canOpen ? (
          <StateLink href={row.href} state={linkState(location)}>
            {label}
          </StateLink>
        ) : (
          label
        )
      }
      description={row.canOpen ? row.description : t('links.notOpenable')}
      endContent={
        <HStack gap={2} vAlign="center">
          {row.status}
          {canEdit && <Button label={t('links.remove')} aria-label={recordActionLabel(t('links.remove'), row.number)} variant="ghost" onClick={() => onRemove(row.number)} />}
        </HStack>
      }
    />
  );
}

function LinkPanel({ text, rows, error, onRetry, canEdit, canAdd, link, unlink }: LinkPanelProps) {
  const { t } = useTranslation('problems');
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState<string | null>(null);
  return (
    <SectionCard
      title={text.title}
      actions={canAdd && <Button label={text.add} variant="secondary" size="sm" icon={<Icon icon={Icons.plus} />} onClick={() => setAdding(true)} />}
    >
      {error !== null && <ErrorState error={error} onRetry={onRetry} />}
      {rows?.length === 0 && <Text type="supporting">{text.empty}</Text>}
      {rows !== undefined && rows.length > 0 && (
        <List hasDividers density="compact" aria-label={text.title}>
          {rows.map((row) => (
            <Row key={row.number} row={row} canEdit={canEdit} onRemove={setRemoving} />
          ))}
        </List>
      )}
      {adding && (
        <AddDialog
          text={text}
          link={link}
          onClose={() => {
            link.reset();
            setAdding(false);
          }}
        />
      )}
      {removing !== null && (
        <ConfirmDialog
          title={t('links.remove')}
          message={text.removeMessage}
          confirmLabel={t('links.remove')}
          variant="danger"
          isPending={unlink.isPending}
          error={unlink.error}
          onConfirm={() => unlink.mutate(removing, { onSuccess: () => setRemoving(null) })}
          onCancel={() => {
            unlink.reset();
            setRemoving(null);
          }}
        />
      )}
    </SectionCard>
  );
}

const ticketRow = (item: ProblemTicketItem): LinkRow => ({
  number: item.number,
  title: item.title,
  canOpen: item.canOpen,
  href: ticketPath(item.number),
  description: item.teamName,
  status: <TicketStatusBadge status={item.status} />,
});

const changeRow = (item: ProblemChangeItem): LinkRow => ({
  number: item.number,
  title: item.title,
  canOpen: item.canOpen,
  href: changePath(item.number),
  description: null,
  status: <ChangeStatusBadge status={item.status} />,
});

/** Only owner-team members and admins edit links here; a closed problem keeps its links but takes no new ones. */
export function ProblemLinks({ number, canEdit, isClosed }: { number: string; canEdit: boolean; isClosed: boolean }) {
  const { t } = useTranslation('problems');
  const tickets = useProblemTickets(number);
  const changes = useProblemChanges(number);
  const linkTicket = useLinkProblemTicket(number);
  const unlinkTicket = useUnlinkProblemTicket(number);
  const linkChange = useLinkProblemChange(number);
  const unlinkChange = useUnlinkProblemChange(number);
  const canAdd = canEdit && !isClosed;
  return (
    <>
      <LinkPanel
        text={{
          title: t('links.incidents'),
          add: t('links.addIncident'),
          numberLabel: t('links.incidentNumber'),
          empty: t('links.noIncidents'),
          removeMessage: t('links.removeIncidentMessage'),
        }}
        rows={tickets.data?.map(ticketRow)}
        error={tickets.error}
        onRetry={() => void tickets.refetch()}
        canEdit={canEdit}
        canAdd={canAdd}
        link={linkTicket}
        unlink={unlinkTicket}
      />
      <LinkPanel
        text={{
          title: t('links.changes'),
          add: t('links.addChange'),
          numberLabel: t('links.changeNumber'),
          empty: t('links.noChanges'),
          removeMessage: t('links.removeChangeMessage'),
        }}
        rows={changes.data?.map(changeRow)}
        error={changes.error}
        onRetry={() => void changes.refetch()}
        canEdit={canEdit}
        canAdd={canAdd}
        link={linkChange}
        unlink={unlinkChange}
      />
    </>
  );
}
