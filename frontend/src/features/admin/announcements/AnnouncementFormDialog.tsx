/**
 * @input An announcement to edit, or null for a new one, and a close callback.
 * @output The announcement form.
 * @position `/admin/announcements`.
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { FormDateTimeInput, FormSelector, FormTextArea, FormTextInput } from '../../../shared/form/formFields';
import { fromLocalInput, toLocalInput } from '../../changes/dateTime';
import { useCloseOnConflict } from '../../changes/useCloseOnConflict';

import { AnnouncementPreview } from './AnnouncementPreview';
import { useCreateAnnouncement, useUpdateAnnouncement, type Announcement } from './api';

/** Mirror backend Announcement.TitleMaxLength / BodyMaxLength. */
const TITLE_MAX_LENGTH = 150;
const BODY_MAX_LENGTH = 1000;
const LEVELS = ['info', 'warning'] as const;

const schema = z
  .object({
    title: z.string().trim().min(1, ERROR_CODES.required).max(TITLE_MAX_LENGTH, ERROR_CODES.tooLong),
    body: z.string().trim().min(1, ERROR_CODES.required).max(BODY_MAX_LENGTH, ERROR_CODES.tooLong),
    level: z.enum(LEVELS),
    startsAt: z.string().min(1, ERROR_CODES.required),
    endsAt: z.string(),
  })
  .refine((v) => v.endsAt === '' || v.endsAt > v.startsAt, { path: ['endsAt'], message: ERROR_CODES.announcementInvalidPeriod });
type FormValues = z.infer<typeof schema>;
const FORM_FIELDS = ['title', 'body', 'level', 'startsAt', 'endsAt'] as const;
const CODE_FIELDS = { [ERROR_CODES.announcementInvalidPeriod]: 'endsAt' } as const;

export function AnnouncementFormDialog({ announcement, onClose }: { announcement: Announcement | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const create = useCreateAnnouncement();
  const update = useUpdateAnnouncement();
  const mutation = announcement === null ? create : update;
  const closeOnConflict = useCloseOnConflict(onClose);
  const [now] = useState(() => new Date().toISOString());
  const {
    control,
    handleSubmit,
    setError,
    formState: { isDirty },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      title: announcement?.title ?? '',
      body: announcement?.body ?? '',
      level: announcement?.level ?? 'info',
      startsAt: toLocalInput(announcement?.startsAt ?? now),
      endsAt: toLocalInput(announcement?.endsAt ?? null),
    },
  });
  const draft = useWatch({ control });
  const onSubmit = handleSubmit(({ startsAt, endsAt, ...rest }) => {
    const body = { ...rest, startsAt: fromLocalInput(startsAt) ?? now, endsAt: fromLocalInput(endsAt) };
    const onError = (error: unknown) => {
      closeOnConflict(error);
      applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS);
    };
    if (announcement === null) {
      create.mutate(body, { onSuccess: onClose, onError });
    } else {
      update.mutate({ id: announcement.id, version: announcement.version, ...body }, { onSuccess: onClose, onError });
    }
  });
  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;
  return (
    <FormDialog
      isDirty={isDirty}
      title={announcement === null ? t('announcements.new') : t('announcements.edit')}
      submitLabel={announcement === null ? tc('actions.create') : tc('actions.save')}
      isPending={mutation.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="title" label={t('announcements.fields.title')} autoComplete="off" isRequired />
      <FormTextArea control={control} name="body" label={t('announcements.fields.body')} isRequired />
      <FormSelector control={control} name="level" label={t('announcements.fields.level')} options={LEVELS.map((value) => ({ value, label: t(`announcements.levels.${value}`) }))} isRequired />
      <FormDateTimeInput control={control} name="startsAt" label={t('announcements.fields.startsAt')} isRequired />
      <FormDateTimeInput control={control} name="endsAt" label={t('announcements.fields.endsAt')} />
      <AnnouncementPreview draft={{ title: draft.title ?? '', body: draft.body ?? '', level: draft.level ?? 'info', startsAt: draft.startsAt ?? '', endsAt: draft.endsAt ?? '' }} />
    </FormDialog>
  );
}
