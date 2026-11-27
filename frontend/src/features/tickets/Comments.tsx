/**
 * @input A ticket and its comments.
 * @output The comment thread with attachments, and the reply box (public reply or internal note).
 * @position "Yazışmalar" card on the ticket detail page.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useId, useImperativeHandle, useRef, useState, type Ref } from 'react';
import { flushSync } from 'react-dom';
import { useTranslation } from 'react-i18next';

import { FormAlert } from '../../shared/feedback/FormAlert';
import { formatDateTime } from '../../shared/lib/format';

import { useAddComment, type CommentItem, type TicketDetail, type Visibility } from './api';
import { AttachmentInput } from './AttachmentInput';
import { describeCommentError } from './commentErrors';
import { ReplyArticlePicker } from './ReplyArticlePicker';

const ATTACHMENTS_PATH = 'attachments';
const LINE_BREAK = /\r?\n/;
const VISIBILITIES: readonly Visibility[] = ['public', 'internal'];

/** What the page may ask of the reply box. */
export interface CommentBoxHandle {
  /** Shows the public reply tab and puts the cursor in the message field. A typed draft stays. */
  focusReply: () => void;
}

const attachmentUrl = (number: string, id: string) => `/api/v1/tickets/${number}/${ATTACHMENTS_PATH}/${id}`;
const isVisibility = (value: string): value is Visibility => (VISIBILITIES as readonly string[]).includes(value);

export function CommentList({ ticket, comments }: { ticket: TicketDetail; comments: readonly CommentItem[] }) {
  const { t } = useTranslation('tickets');
  if (comments.length === 0) {
    return <Text type="supporting">{t('detail.noComments')}</Text>;
  }
  return (
    <VStack gap={2}>
      {comments.map((comment) => (
        <Card key={comment.id} padding={3} variant={comment.visibility === 'internal' ? 'yellow' : 'muted'}>
          <VStack gap={1}>
            <HStack gap={2} vAlign="center" wrap="wrap">
              <Text type="label">{comment.authorName}</Text>
              <Text type="supporting">{formatDateTime(comment.createdAt)}</Text>
              {comment.visibility === 'internal' && <Token size="sm" color="yellow" label={t('detail.internal')} />}
            </HStack>
            {comment.body &&
              comment.body.split(LINE_BREAK).map((line, index) => (
                <Text key={index} wordBreak="break-word">
                  {line}
                </Text>
              ))}
            {comment.attachments.length > 0 && (
              <HStack gap={3} wrap="wrap">
                {comment.attachments.map((attachment) => (
                  // A native download link: the router must not handle a file URL.
                  <a key={attachment.id} href={attachmentUrl(ticket.number, attachment.id)} download>
                    <Text type="supporting" color="accent">
                      {attachment.originalName}
                    </Text>
                  </a>
                ))}
              </HStack>
            )}
          </VStack>
        </Card>
      ))}
    </VStack>
  );
}

/**
 * People working on the ticket also get an internal-note tab.
 * A message in flight keeps the visibility it was sent with, so the tab stays usable while it sends.
 */
export function CommentBox({ ticket, handle }: { ticket: TicketDetail; handle?: Ref<CommentBoxHandle> }) {
  const { t } = useTranslation('tickets');
  const add = useAddComment();
  const [visibility, setVisibility] = useState<Visibility>('public');
  const panelId = useId();
  // One draft per tab: a note typed as internal never turns into a public reply when the tab changes.
  const [drafts, setDrafts] = useState<Record<Visibility, string>>({ public: '', internal: '' });
  const body = drafts[visibility];
  const setBody = (update: string | ((current: string) => string)) =>
    setDrafts((current) => ({ ...current, [visibility]: typeof update === 'function' ? update(current[visibility]) : update }));
  const [files, setFiles] = useState<File[]>([]);
  const [formKey, setFormKey] = useState(0);
  const bodyRef = useRef<HTMLTextAreaElement>(null);

  // The tab change must reach the DOM before the focus call, or the focus lands on a field that is about to re-render.
  useImperativeHandle(handle, () => ({
    focusReply: () => {
      flushSync(() => setVisibility('public'));
      bodyRef.current?.focus();
    },
  }));

  const submit = () =>
    add.mutate(
      { number: ticket.number, body, visibility, files },
      {
        onSuccess: () => {
          setDrafts((current) => ({ ...current, [visibility]: '' }));
          setFiles([]);
          setFormKey((key) => key + 1);
        },
      },
    );

  return (
    // Inside the "Yazışmalar" card: the form keeps that name for screen readers and tests.
    <form
      aria-label={t('detail.comments')}
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      <VStack gap={3}>
        {ticket.canWork && (
          <TabList role="tablist" aria-label={t('comment.tabs')} value={visibility} onChange={(value) => isVisibility(value) && setVisibility(value)}>
            {VISIBILITIES.map((tab) => (
              <Tab key={tab} value={tab} label={t(`comment.${tab}`)} panelId={panelId} />
            ))}
          </TabList>
        )}
        <VStack gap={3} id={ticket.canWork ? panelId : undefined} role={ticket.canWork ? 'tabpanel' : undefined} aria-label={ticket.canWork ? t(`comment.${visibility}`) : undefined}>
        <FormAlert message={add.error ? describeCommentError(add.error) : null} />
        {/* Locked while sending: the success handler clears the box, which would drop anything typed meanwhile. */}
        <TextArea ref={bodyRef} label={t('comment.body')} value={body} isDisabled={add.isPending} onChange={setBody} />
        <ReplyArticlePicker onInsert={(text) => setBody((current) => (current.trim() === '' ? text : `${current}\n${text}`))} />
        <AttachmentInput key={formKey} label={t('comment.files')} hint={t('new.filesHint')} files={files} onChange={setFiles} />
        <HStack>
          <Button type="submit" label={t('comment.send')} variant="primary" isDisabled={body.trim() === '' && files.length === 0} isLoading={add.isPending} />
        </HStack>
        </VStack>
      </VStack>
    </form>
  );
}
