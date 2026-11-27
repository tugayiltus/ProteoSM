/**
 * @input A ticket, the way back to its list, and what "Yanıtla" does.
 * @output The page title row (subject, back link, the main action) and below it the number, type, status, team, priority and the SLA date.
 * @position Top of the ticket detail page; on a phone it holds everything the first screen needs.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { Icons } from '../../shared/ui/icons';

import { useTicketAction, type TicketDetail } from './api';
import { TicketSlaLine, type SlaLoad } from './SlaDue';
import { PriorityBadge, TicketStatusBadge } from './TicketBadges';
import { mainActionOf, useTicketBusy } from './ticketBusy';

const META_SEPARATOR = ' · ';

export interface TicketHeaderProps {
  ticket: TicketDetail;
  backTo: string;
  sla: SlaLoad;
  onReply: () => void;
}

export function TicketHeader({ ticket, backTo, sla, onReply }: TicketHeaderProps) {
  const { t } = useTranslation('tickets');
  const main = mainActionOf(ticket);
  const claim = useTicketAction(ticket);
  const busy = useTicketBusy(ticket);
  const replyButton = useRef<HTMLButtonElement>(null);
  const focusReplyButton = useRef(false);

  // After a claim the claim button is gone; keyboard focus moves to its successor instead of falling to the page start.
  useEffect(() => {
    if (focusReplyButton.current && main === 'reply') {
      replyButton.current?.focus();
      focusReplyButton.current = false;
    }
  });
  return (
    <VStack gap={2}>
      <PageHeader
        icon={ROUTE_META.myTickets.icon}
        titleOverride={ticket.subject}
        actions={
          <>
            {main === 'claim' && (
              <Button
                label={t('actions.claim')}
                variant="primary"
                isDisabled={busy}
                isLoading={claim.isPending}
                onClick={() => claim.mutate({ kind: 'claim' }, { onSuccess: () => {
                    focusReplyButton.current = true;
                  } })}
              />
            )}
            {main === 'reply' && <Button ref={replyButton} label={t('actions.reply')} variant="primary" onClick={onReply} />}
            <Button label={t('detail.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />
          </>
        }
      />
      <HStack gap={2} vAlign="center" wrap="wrap">
        <Text type="supporting">{[ticket.number, t(`kinds.${ticket.kind}`), ticket.teamName].join(META_SEPARATOR)}</Text>
        <TicketStatusBadge status={ticket.status} />
        <PriorityBadge priority={ticket.priority} />
        {ticket.holdReason && <Text type="supporting">{t(`holdReasons.${ticket.holdReason}`)}</Text>}
      </HStack>
      <FormAlert message={claim.error ? describeError(claim.error) : null} />
      <TicketSlaLine load={sla} />
    </VStack>
  );
}
