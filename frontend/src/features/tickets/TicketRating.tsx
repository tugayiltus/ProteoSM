/**
 * @input A ticket and a flag that moves the focus to the first choice.
 * @output The requester's rating form or their score with a change button; IT staff read the score and comment.
 * @position "Memnuniyet" card under the description and resolution on the ticket detail page.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { RadioList, RadioListItem } from '@astryxdesign/core/RadioList';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { VStack } from '@astryxdesign/core/VStack';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { ApiError } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { formatDateTime } from '../../shared/lib/format';
import { SectionCard } from '../../shared/page/SectionCard';
import { isItStaffOrAdmin, useSession } from '../auth/session';

import { useRateTicket, type TicketDetail, type TicketOutcome, type TicketRatingView } from './api';

const SCORES = [1, 2, 3, 4, 5] as const;
type Score = (typeof SCORES)[number];
const MAX_SCORE: Score = 5;
/** Same limit as the server (`RatingLimits.CommentMaxLength`). */
const COMMENT_MAX_LENGTH = 500;
const RATEABLE_OUTCOMES: readonly TicketOutcome[] = ['resolved', 'fulfilled'];
const SCORE_FIELD = 'score';
const COMMENT_FIELD = 'comment';
const RADIO_SELECTOR = 'input[type="radio"]';
const NO_SCORE = '';
const LINE_BREAK = /\r?\n/;

const isScore = (value: number): value is Score => (SCORES as readonly number[]).includes(value);

/** Field errors arrive as codes on "score" or "comment"; they show at their field. */
function fieldMessage(error: unknown, field: string): string | undefined {
  if (!(error instanceof ApiError)) {
    return undefined;
  }
  const code = error.fieldErrors[field]?.[0];
  return code === undefined ? undefined : describeError(new ApiError(error.status, code));
}

function ScoreText({ score }: { score: number }) {
  const { t } = useTranslation('tickets');
  const label = isScore(score) ? t(`rating.scores.${score}`) : '';
  return <Text type="label">{t('rating.scoreValue', { score, max: MAX_SCORE, label })}</Text>;
}

function RatingView({ rating }: { rating: TicketRatingView }) {
  return (
    <VStack gap={1}>
      <ScoreText score={rating.score} />
      {rating.comment?.split(LINE_BREAK).map((line, index) => (
        <Text key={index} wordBreak="break-word">
          {line}
        </Text>
      ))}
    </VStack>
  );
}

interface RatingFormProps {
  ticket: TicketDetail;
  focusOnMount: boolean;
  onDone: () => void;
  onCancel?: () => void;
}

/** Starts from the current rating, so a change edits what the requester sent before. */
function RatingForm({ ticket, focusOnMount, onDone, onCancel }: RatingFormProps) {
  const { t } = useTranslation('tickets');
  const rate = useRateTicket(ticket.number);
  const formRef = useRef<HTMLFormElement>(null);
  const [score, setScore] = useState(ticket.rating === null ? NO_SCORE : String(ticket.rating.score));
  const [comment, setComment] = useState(ticket.rating?.comment ?? '');

  // Syncs DOM focus: the email link (`?rate=1`) opens the page at the first choice.
  useEffect(() => {
    if (focusOnMount) {
      formRef.current?.querySelector<HTMLInputElement>(RADIO_SELECTOR)?.focus();
    }
  }, [focusOnMount]);

  const scoreError = fieldMessage(rate.error, SCORE_FIELD);
  const commentError = fieldMessage(rate.error, COMMENT_FIELD);
  const formError = rate.error && scoreError === undefined && commentError === undefined ? describeError(rate.error) : null;

  const submit = () => {
    const trimmed = comment.trim();
    rate.mutate({ score: Number(score), comment: trimmed === '' ? null : trimmed }, { onSuccess: onDone });
  };

  return (
    <form
      ref={formRef}
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      <VStack gap={3}>
        <FormAlert message={formError} />
        <RadioList
          label={t('rating.question')}
          value={score}
          onChange={setScore}
          isDisabled={rate.isPending}
          status={scoreError === undefined ? undefined : { type: 'error', message: scoreError }}
        >
          {SCORES.map((value) => (
            <RadioListItem key={value} value={String(value)} label={t(`rating.scores.${value}`)} />
          ))}
        </RadioList>
        <TextArea
          label={t('rating.comment')}
          value={comment}
          onChange={setComment}
          isOptional
          isDisabled={rate.isPending}
          maxLength={COMMENT_MAX_LENGTH}
          status={commentError === undefined ? undefined : { type: 'error', message: commentError }}
        />
        <HStack gap={2} wrap="wrap">
          <Button type="submit" label={t('rating.send')} variant="primary" isDisabled={score === NO_SCORE} isLoading={rate.isPending} />
          {onCancel && <Button label={t('rating.cancel')} variant="secondary" isDisabled={rate.isPending} onClick={onCancel} />}
        </HStack>
      </VStack>
    </form>
  );
}

/** The requester rates or changes the rating; the score after the window closes stays visible. */
function RequesterRating({ ticket, focusOnMount }: { ticket: TicketDetail; focusOnMount: boolean }) {
  const { t } = useTranslation('tickets');
  // The email link opens the form at once, also when a rating exists.
  const [isEditing, setIsEditing] = useState(focusOnMount);
  if (ticket.rating === null) {
    return <RatingForm ticket={ticket} focusOnMount={focusOnMount} onDone={() => setIsEditing(false)} />;
  }
  if (isEditing) {
    return <RatingForm ticket={ticket} focusOnMount={focusOnMount} onDone={() => setIsEditing(false)} onCancel={() => setIsEditing(false)} />;
  }
  return (
    <VStack gap={3}>
      <RatingView rating={ticket.rating} />
      {ticket.rateUntil && <Text type="supporting">{t('rating.changeUntil', { date: formatDateTime(ticket.rateUntil) })}</Text>}
      <HStack>
        <Button label={t('rating.change')} variant="secondary" onClick={() => setIsEditing(true)} />
      </HStack>
    </VStack>
  );
}

/** Props of {@link TicketRating}. */
export interface TicketRatingProps {
  ticket: TicketDetail;
  /** True when the page opens from the rating link; the first choice gets the focus. */
  focusOnMount: boolean;
}

/**
 * The satisfaction rating card. It renders nothing while the ticket has no rating to show,
 * so an open ticket or a not rateable outcome shows no empty card.
 *
 * @example
 * ```
 * <TicketRating ticket={ticket} focusOnMount={searchParams.get('rate') === '1'} />
 * ```
 */
export function TicketRating({ ticket, focusOnMount }: TicketRatingProps) {
  const { t } = useTranslation('tickets');
  const me = useSession();
  const isRateable = ticket.status === 'completed' && ticket.outcome !== null && RATEABLE_OUTCOMES.includes(ticket.outcome);

  let content: ReactNode = null;
  if (ticket.canRate) {
    content = <RequesterRating ticket={ticket} focusOnMount={focusOnMount} />;
  } else if (ticket.rating !== null) {
    content = <RatingView rating={ticket.rating} />;
  } else if (isRateable && isItStaffOrAdmin(me.role)) {
    content = <Text type="supporting">{t('rating.none')}</Text>;
  }
  if (content === null) {
    return null;
  }
  return <SectionCard title={t('rating.title')}>{content}</SectionCard>;
}
