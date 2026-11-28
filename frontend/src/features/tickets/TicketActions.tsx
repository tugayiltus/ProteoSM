/**
 * @input A ticket.
 * @output The work actions the ticket allows: direct buttons and buttons that open a dialog.
 * @position Ticket detail page, first in the side column.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { focusMainHeadingIfLost } from '../../shared/navigation/PageHeader';
import { SectionCard } from '../../shared/page/SectionCard';

import { useTicketAction, type TicketDetail } from './api';
import { TicketActionDialog, type DialogActionKind } from './TicketActionDialog';
import { useTicketBusy } from './ticketBusy';

type DirectKind = 'claim' | 'start' | 'resume';

const isClosed = (t: TicketDetail) => t.status === 'completed' || t.status === 'cancelled';

/** Mirrors the domain state machine so only allowed actions show (the server still decides). */
function availableActions(t: TicketDetail): { direct: DirectKind[]; dialogs: DialogActionKind[] } {
  if (!t.canWork || isClosed(t)) {
    return { direct: [], dialogs: [] };
  }
  const direct: DirectKind[] = [];
  if (t.assigneeId === null) {
    direct.push('claim');
  }
  if (t.status === 'open') {
    direct.push('start');
  }
  if (t.status === 'on_hold' && t.holdReason !== 'manager_approval') {
    direct.push('resume');
  }
  const dialogs: DialogActionKind[] = [];
  if (t.status === 'open' || t.status === 'in_progress') {
    dialogs.push('hold', 'complete');
  }
  dialogs.push('priority', 'transfer');
  if (t.kind === 'request' && t.approval?.status === 'approved') {
    dialogs.push('invalidate');
  }
  dialogs.push('cancel');
  return { direct, dialogs };
}

/** `hideClaim`: the page header already shows "Üzerime al" as the main action. */
export function TicketActions({ ticket, hideClaim = false }: { ticket: TicketDetail; hideClaim?: boolean }) {
  const { t } = useTranslation('tickets');
  const direct = useTicketAction(ticket);
  const [dialog, setDialog] = useState<DialogActionKind | null>(null);
  const available = availableActions(ticket);
  const actions = hideClaim ? { ...available, direct: available.direct.filter((kind) => kind !== 'claim') } : available;
  const refreshing = useTicketBusy(ticket);

  const hasActions = actions.direct.length > 0 || actions.dialogs.length > 0;
  const wasOpen = useRef(false);
  const hadActions = useRef(hasActions);

  // Synchronizes keyboard focus with the DOM: a closed dialog or a vanished action list must not leave focus on the page body.
  useEffect(() => {
    const dialogClosed = wasOpen.current && dialog === null;
    const actionsGone = hadActions.current && !hasActions;
    wasOpen.current = dialog !== null;
    hadActions.current = hasActions;
    if (dialogClosed || actionsGone) {
      requestAnimationFrame(focusMainHeadingIfLost);
    }
  }, [dialog, hasActions]);

  if (!hasActions) {
    return null;
  }

  return (
    <SectionCard title={t('actions.title')}>
      <FormAlert message={direct.error ? describeError(direct.error) : null} />
      <HStack gap={2} wrap="wrap">
        {/* One primary action per group: the first direct step; the rest are secondary. */}
        {actions.direct.map((kind, index) => (
          <Button
            key={kind}
            label={t(`actions.${kind}`)}
            variant={index === 0 ? 'primary' : 'secondary'}
            isDisabled={refreshing}
            isLoading={direct.isPending && direct.variables?.kind === kind}
            onClick={() => direct.mutate({ kind })}
          />
        ))}
        {actions.dialogs.map((kind) => (
          <Button
            key={kind}
            label={t(`actions.${kind}`)}
            variant={kind === 'cancel' ? 'destructive' : 'secondary'}
            isDisabled={refreshing}
            onClick={() => {
              direct.reset();
              setDialog(kind);
            }}
          />
        ))}
      </HStack>
      {dialog !== null && <TicketActionDialog key={dialog} ticket={ticket} kind={dialog} onClose={() => setDialog(null)} />}
    </SectionCard>
  );
}
