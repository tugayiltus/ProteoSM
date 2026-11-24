/**
 * @input A release to edit, or null for a new one, a saved callback and a close callback.
 * @output The release form: title, description, owner team and owner, target window, notes.
 * @position Release list ("Yeni sürüm") and release detail ("Düzenle").
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { FormDateTimeInput, FormSelector, FormTextArea, FormTextInput } from '../../shared/form/formFields';
import { withCurrentOption } from '../../shared/reference/referenceOptions';
import { useSession } from '../auth/session';
import { fromLocalInput, toLocalInput } from '../changes/dateTime';
import { useReferenceTeams } from '../tickets/api';

import { useCreateRelease, useUpdateRelease, type ReleaseDetail } from './api';

/** Mirrors backend Release.TitleMaxLength / TextMaxLength. */
const TITLE_MAX_LENGTH = 200;
const TEXT_MAX_LENGTH = 4000;
const ADMIN_ROLE = 'admin';

const schema = z
  .object({
    title: z.string().trim().min(1, ERROR_CODES.required).max(TITLE_MAX_LENGTH, ERROR_CODES.tooLong),
    description: z.string().trim().min(1, ERROR_CODES.required).max(TEXT_MAX_LENGTH, ERROR_CODES.tooLong),
    ownerTeamId: z.string().min(1, ERROR_CODES.required),
    ownerId: z.string(),
    windowStart: z.string(),
    windowEnd: z.string(),
    notes: z.string().trim().max(TEXT_MAX_LENGTH, ERROR_CODES.tooLong),
  })
  .refine((v) => v.windowStart === '' || v.windowEnd === '' || v.windowEnd > v.windowStart, { path: ['windowEnd'], message: 'releases:form.windowOrder' });
type FormValues = z.infer<typeof schema>;
const FORM_FIELDS = ['title', 'description', 'ownerTeamId', 'ownerId', 'windowStart', 'windowEnd', 'notes'] as const;
const CODE_FIELDS = { [ERROR_CODES.releaseOwnerTeamNotAllowed]: 'ownerTeamId', [ERROR_CODES.releaseOwnerNotInTeam]: 'ownerId' } as const;
const orNull = (value: string) => (value === '' ? null : value);

interface ReleaseFormDialogProps {
  release: ReleaseDetail | null;
  onSaved: (release: ReleaseDetail) => void;
  onClose: () => void;
}

export function ReleaseFormDialog({ release, onSaved, onClose }: ReleaseFormDialogProps) {
  const { t } = useTranslation('releases');
  const { t: tc } = useTranslation();
  const me = useSession();
  const teams = useReferenceTeams(true);
  const create = useCreateRelease();
  const update = useUpdateRelease();
  const mutation = release === null ? create : update;
  // The version the user saw when opening the dialog; a background refetch must not skip the conflict check.
  const [openedVersion] = useState(release?.version ?? 0);
  const {
    control,
    handleSubmit,
    setError,
    setValue,
    formState: { isDirty },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      title: release?.title ?? '',
      description: release?.description ?? '',
      ownerTeamId: release?.ownerTeamId ?? '',
      ownerId: release?.ownerId ?? '',
      windowStart: toLocalInput(release?.windowStart ?? null),
      windowEnd: toLocalInput(release?.windowEnd ?? null),
      notes: release?.notes ?? '',
    },
  });
  const teamId = useWatch({ control, name: 'ownerTeamId' });
  const teamOptions = useMemo(
    () =>
      withCurrentOption(
        (teams.data ?? [])
          .filter((team) => me.role === ADMIN_ROLE || team.members.some((m) => m.id === me.id))
          .map((team) => ({ value: team.id, label: team.name })),
        release?.ownerTeamId,
        release?.ownerTeamName,
      ),
    [teams.data, me.role, me.id, release],
  );
  const members = teams.data?.find((team) => team.id === teamId)?.members ?? [];
  const currentOwner = teamId === release?.ownerTeamId ? release : null;
  const memberOptions = withCurrentOption(members.map((m) => ({ value: m.id, label: m.fullName })), currentOwner?.ownerId, currentOwner?.ownerName);
  const onSubmit = handleSubmit((v) => {
    const body = {
      title: v.title,
      description: v.description,
      ownerTeamId: v.ownerTeamId,
      ownerId: orNull(v.ownerId),
      windowStart: fromLocalInput(v.windowStart),
      windowEnd: fromLocalInput(v.windowEnd),
      notes: orNull(v.notes),
    };
    const callbacks = { onSuccess: onSaved, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (release === null) {
      create.mutate(body, callbacks);
    } else {
      update.mutate({ number: release.number, version: openedVersion, ...body }, callbacks);
    }
  });
  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;
  return (
    <FormDialog
      isDirty={isDirty}
      title={release === null ? t('form.new') : t('form.edit')}
      submitLabel={release === null ? tc('actions.create') : tc('actions.save')}
      isPending={mutation.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="title" label={t('columns.title')} autoComplete="off" isRequired />
      <FormTextArea control={control} name="description" label={t('form.description')} isRequired />
      <FormSelector control={control} name="ownerTeamId" label={t('columns.team')} options={teamOptions} placeholder={tc('none')} onValueChange={() => setValue('ownerId', '')} isRequired />
      <FormSelector control={control} name="ownerId" label={t('form.owner')} options={memberOptions} placeholder={tc('none')} isClearable />
      <FormDateTimeInput control={control} name="windowStart" label={t('form.windowStart')} />
      <FormDateTimeInput control={control} name="windowEnd" label={t('form.windowEnd')} />
      <FormTextArea control={control} name="notes" label={t('form.notes')} />
    </FormDialog>
  );
}
