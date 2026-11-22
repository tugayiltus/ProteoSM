/**
 * @input A problem.
 * @output The problem history with notes, newest last, and a note box for people who work on the problem.
 * @position Problem detail page, main column.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';

import { useAddProblemNote, useProblemTimeline, type ProblemDetail, type ProblemTimelineItem } from './api';

const META_SEPARATOR = ' · ';
/** Event data keys that carry text worth showing: a note, a resolution, a reason or the linked record. */
const TEXT_KEYS = ['text', 'resolution', 'reason', 'ticketNumber', 'changeNumber'] as const;

function textOf(event: ProblemTimelineItem): string | null {
  if (typeof event.data !== 'object' || event.data === null) {
    return null;
  }

  const data = event.data as Record<string, unknown>;
  for (const key of TEXT_KEYS) {
    const value = data[key];
    if (typeof value === 'string' && value !== '') {
      return value;
    }
  }

  return null;
}

function TimelineList({ events }: { events: readonly ProblemTimelineItem[] }) {
  const { t } = useTranslation('problems');
  if (events.length === 0) {
    return <EmptyState message={t('timeline.empty')} />;
  }

  return (
    <List listStyle="decimal" density="compact" aria-label={t('timeline.title')}>
      {events.map((event) => {
        const text = textOf(event);
        const meta = [formatDateTime(event.occurredAt), event.actorName].filter(Boolean).join(META_SEPARATOR);
        return (
          <ListItem
            key={event.id}
            label={t(`timeline.events.${event.kind}`)}
            description={
              text === null ? (
                meta
              ) : (
                <VStack gap={1}>
                  <Text wordBreak="break-word">{text}</Text>
                  <Text type="supporting">{meta}</Text>
                </VStack>
              )
            }
          />
        );
      })}
    </List>
  );
}

function NoteBox({ number }: { number: string }) {
  const { t } = useTranslation('problems');
  const add = useAddProblemNote(number);
  const [text, setText] = useState('');
  return (
    <form
      aria-label={t('timeline.noteForm')}
      onSubmit={(event) => {
        event.preventDefault();
        add.mutate(text.trim(), { onSuccess: () => setText('') });
      }}
    >
      <VStack gap={3}>
        <FormAlert message={add.error ? describeError(add.error) : null} />
        {/* Locked while sending: the success handler clears the box, which would drop anything typed meanwhile. */}
        <TextArea label={t('timeline.note')} value={text} isDisabled={add.isPending} onChange={setText} />
        <HStack>
          <Button type="submit" label={t('timeline.send')} variant="secondary" isDisabled={text.trim() === ''} isLoading={add.isPending} />
        </HStack>
      </VStack>
    </form>
  );
}

/** Every IT staff member reads the history; only people who can work on the problem add notes, also after it is closed. */
export function ProblemTimeline({ problem }: { problem: ProblemDetail }) {
  const timeline = useProblemTimeline(problem.number);
  return (
    <VStack gap={4}>
      {timeline.isPending && <LoadingState />}
      {timeline.isError && <ErrorState error={timeline.error} onRetry={() => void timeline.refetch()} />}
      {timeline.data && <TimelineList events={timeline.data} />}
      {problem.canWork && <NoteBox number={problem.number} />}
    </VStack>
  );
}
