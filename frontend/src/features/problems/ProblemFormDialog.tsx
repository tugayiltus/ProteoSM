/**
 * @input An optional problem to edit, a saved callback and a close callback.
 * @output A dialog that opens a problem (title, symptom, impact, owner team and owner) or edits one (adds root cause and workaround).
 * @position Problem list ("Yeni problem") and the problem detail actions ("Düzenle").
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { FormSelector, FormTextArea, FormTextInput } from '../../shared/form/formFields';
import { useSession } from '../auth/session';
import { useCloseOnConflict } from '../changes/useCloseOnConflict';
import { useReferenceTeams } from '../tickets/api';

import { LEVELS, useCreateProblem, useEditProblem, type Level, type ProblemDetail } from './api';

/** Mirror backend Problem.TitleMaxLength / TextMaxLength. */
const TITLE_MAX_LENGTH = 200;
const TEXT_MAX_LENGTH = 4000;
const DEFAULT_IMPACT: Level = 'medium';
const ADMIN_ROLE = 'admin';

const text = z.string().trim().max(TEXT_MAX_LENGTH, ERROR_CODES.tooLong);
const baseSchema = z.object({
  title: z.string().trim().min(1, ERROR_CODES.required).max(TITLE_MAX_LENGTH, ERROR_CODES.tooLong),
  description: text.min(1, ERROR_CODES.required),
  impact: z.string().min(1, ERROR_CODES.required),
  rootCause: text,
  workaround: text,
  ownerTeamId: z.string(),
  ownerId: z.string(),
});
const createSchema = baseSchema.extend({ ownerTeamId: z.string().min(1, ERROR_CODES.required) });
type Values = z.infer<typeof baseSchema>;

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());

function initialValues(problem: ProblemDetail | undefined): Values {
  return {
    title: problem?.title ?? '',
    description: problem?.description ?? '',
    impact: problem?.impact ?? DEFAULT_IMPACT,
    rootCause: problem?.rootCause ?? '',
    workaround: problem?.workaround ?? '',
    ownerTeamId: '',
    ownerId: '',
  };
}

interface ProblemFormDialogProps {
  problem?: ProblemDetail;
  onSaved: (problem: ProblemDetail) => void;
  onClose: () => void;
}

export function ProblemFormDialog({ problem, onSaved, onClose }: ProblemFormDialogProps) {
  const { t } = useTranslation('problems');
  const me = useSession();
  const teams = useReferenceTeams(problem === undefined);
  const create = useCreateProblem();
  // The version the user saw when opening the dialog; a background refetch must not silently skip the conflict check.
  const [openedVersion] = useState(problem?.version ?? 0);
  const edit = useEditProblem(problem?.number ?? '', openedVersion);
  const closeOnConflict = useCloseOnConflict(onClose);
  const { control, setValue, handleSubmit, formState } = useForm<Values>({
    resolver: zodResolver(problem === undefined ? createSchema : baseSchema),
    defaultValues: initialValues(problem),
  });
  const teamId = useWatch({ control, name: 'ownerTeamId' });
  // The owner team must be one of the creator's teams, so the creator can always work the new problem; admins pick any team.
  const teamOptions = useMemo(
    () =>
      (teams.data ?? [])
        .filter((team) => me.role === ADMIN_ROLE || team.members.some((m) => m.id === me.id))
        .map((team) => ({ value: team.id, label: team.name })),
    [teams.data, me.role, me.id],
  );
  const memberOptions = useMemo(
    () => (teams.data?.find((team) => team.id === teamId)?.members ?? []).map((m) => ({ value: m.id, label: m.fullName })),
    [teams.data, teamId],
  );
  const mutation = problem === undefined ? create : edit;
  const onSubmit = handleSubmit((v) => {
    if (problem === undefined) {
      create.mutate(
        {
          title: v.title.trim(),
          description: v.description.trim(),
          impact: v.impact as Level,
          ownerTeamId: v.ownerTeamId,
          ownerId: orNull(v.ownerId),
          incidentNumber: null,
        },
        { onSuccess: onSaved },
      );
      return;
    }

    edit.mutate(
      { title: v.title.trim(), description: v.description.trim(), impact: v.impact as Level, rootCause: orNull(v.rootCause), workaround: orNull(v.workaround) },
      { onSuccess: onSaved, onError: closeOnConflict },
    );
  });
  return (
    <FormDialog
      title={problem === undefined ? t('form.createTitle') : `${t('form.editTitle')} · ${problem.number}`}
      submitLabel={problem === undefined ? t('form.submitCreate') : t('form.submitEdit')}
      isPending={mutation.isPending}
      alert={mutation.error ? describeError(mutation.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
      isDirty={formState.isDirty}
    >
      <FormTextInput control={control} name="title" label={t('form.problemTitle')} autoComplete="off" />
      <FormTextArea control={control} name="description" label={t('form.description')} />
      <FormSelector control={control} name="impact" label={t('form.impact')} options={LEVELS.map((value) => ({ value, label: t(`levels.${value}`) }))} />
      {problem === undefined ? (
        <>
          <FormSelector
            control={control}
            name="ownerTeamId"
            label={t('form.team')}
            placeholder={t('form.select')}
            options={teamOptions}
            onValueChange={(next) => {
              const members = teams.data?.find((team) => team.id === next)?.members ?? [];
              setValue('ownerId', members.some((m) => m.id === me.id) ? me.id : '');
            }}
          />
          <FormSelector control={control} name="ownerId" label={t('form.owner')} placeholder={t('form.nobody')} options={memberOptions} isClearable />
        </>
      ) : (
        <>
          <FormTextArea control={control} name="rootCause" label={t('form.rootCause')} />
          <FormTextArea control={control} name="workaround" label={t('form.workaround')} />
        </>
      )}
    </FormDialog>
  );
}
