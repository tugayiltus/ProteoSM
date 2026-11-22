/**
 * @input A problem, the action kind (resolve, cancel, transfer or owner) and a close callback.
 * @output A dialog with the form that action needs; it sends the version the person saw.
 * @position Opened from the problem work actions.
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormDialog } from '../../shared/feedback/FormDialog';
import { FormSelector, FormTextArea, FormTextInput } from '../../shared/form/formFields';
import { useCloseOnConflict } from '../changes/useCloseOnConflict';
import { useReferenceTeams } from '../tickets/api';

import { useProblemAction, type ProblemAction, type ProblemDetail } from './api';

export type ProblemDialogKind = 'resolve' | 'cancel' | 'transfer' | 'owner';

/** Mirror backend Problem.TextMaxLength / ReasonMaxLength. */
const TEXT_MAX_LENGTH = 4000;
const REASON_MAX_LENGTH = 500;

const reason = z.string().trim().min(1, ERROR_CODES.required).max(REASON_MAX_LENGTH, ERROR_CODES.tooLong);
const resolveSchema = z.object({ resolution: z.string().trim().min(1, ERROR_CODES.required).max(TEXT_MAX_LENGTH, ERROR_CODES.tooLong) });
const cancelSchema = z.object({ reason });
const transferSchema = z.object({ teamId: z.string().min(1, ERROR_CODES.required), reason });
const ownerSchema = z.object({ ownerId: z.string() });

interface FormProps {
  problem: ProblemDetail;
  onClose: () => void;
}

/** The version the dialog opened with, and a send that closes the dialog on success or on a stale-version conflict. */
function useDialogAction({ problem, onClose }: FormProps) {
  const [openedVersion] = useState(problem.version);
  const action = useProblemAction({ number: problem.number, version: openedVersion });
  const closeOnConflict = useCloseOnConflict(onClose);
  const send = (body: ProblemAction) => action.mutate(body, { onSuccess: onClose, onError: closeOnConflict });
  return { action, send };
}

function ResolveForm({ problem, onClose }: FormProps) {
  const { t } = useTranslation('problems');
  const { t: tc } = useTranslation();
  const { action, send } = useDialogAction({ problem, onClose });
  const { control, handleSubmit } = useForm<z.infer<typeof resolveSchema>>({ resolver: zodResolver(resolveSchema), defaultValues: { resolution: '' } });
  const onSubmit = handleSubmit((v) => send({ kind: 'resolve', resolution: v.resolution.trim() }));
  return (
    <FormDialog
      title={`${t('actions.resolve')} · ${problem.number}`}
      submitLabel={tc('actions.confirm')}
      isPending={action.isPending}
      alert={action.error ? describeError(action.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextArea control={control} name="resolution" label={t('fields.resolution')} />
    </FormDialog>
  );
}

function CancelForm({ problem, onClose }: FormProps) {
  const { t } = useTranslation('problems');
  const { t: tc } = useTranslation();
  const { action, send } = useDialogAction({ problem, onClose });
  const { control, handleSubmit } = useForm<z.infer<typeof cancelSchema>>({ resolver: zodResolver(cancelSchema), defaultValues: { reason: '' } });
  const onSubmit = handleSubmit((v) => send({ kind: 'cancel', reason: v.reason.trim() }));
  return (
    <FormDialog
      title={`${t('actions.cancel')} · ${problem.number}`}
      submitLabel={tc('actions.confirm')}
      isPending={action.isPending}
      alert={action.error ? describeError(action.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="reason" label={t('fields.reason')} autoComplete="off" />
    </FormDialog>
  );
}

function TransferForm({ problem, onClose }: FormProps) {
  const { t } = useTranslation('problems');
  const { t: tc } = useTranslation();
  const teams = useReferenceTeams(true);
  const { action, send } = useDialogAction({ problem, onClose });
  const { control, handleSubmit } = useForm<z.infer<typeof transferSchema>>({ resolver: zodResolver(transferSchema), defaultValues: { teamId: '', reason: '' } });
  const options = useMemo(
    () => (teams.data ?? []).filter((team) => team.id !== problem.ownerTeamId).map((team) => ({ value: team.id, label: team.name })),
    [teams.data, problem.ownerTeamId],
  );
  const onSubmit = handleSubmit((v) => send({ kind: 'transfer', teamId: v.teamId, reason: v.reason.trim() }));
  return (
    <FormDialog
      title={`${t('actions.transfer')} · ${problem.number}`}
      submitLabel={tc('actions.confirm')}
      isPending={action.isPending}
      alert={action.error ? describeError(action.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormSelector control={control} name="teamId" label={t('fields.team')} placeholder={t('fields.select')} options={options} />
      <FormTextInput control={control} name="reason" label={t('fields.reason')} autoComplete="off" />
    </FormDialog>
  );
}

function OwnerForm({ problem, onClose }: FormProps) {
  const { t } = useTranslation('problems');
  const { t: tc } = useTranslation();
  const teams = useReferenceTeams(true);
  const { action, send } = useDialogAction({ problem, onClose });
  const { control, handleSubmit } = useForm<z.infer<typeof ownerSchema>>({ resolver: zodResolver(ownerSchema), defaultValues: { ownerId: problem.ownerId ?? '' } });
  const options = useMemo(
    () => (teams.data?.find((team) => team.id === problem.ownerTeamId)?.members ?? []).map((m) => ({ value: m.id, label: m.fullName })),
    [teams.data, problem.ownerTeamId],
  );
  const onSubmit = handleSubmit((v) => send({ kind: 'owner', ownerId: v.ownerId === '' ? null : v.ownerId }));
  return (
    <FormDialog
      title={`${t('actions.owner')} · ${problem.number}`}
      submitLabel={tc('actions.confirm')}
      isPending={action.isPending}
      alert={action.error ? describeError(action.error) : null}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormSelector control={control} name="ownerId" label={t('fields.owner')} placeholder={t('fields.nobody')} options={options} isClearable />
    </FormDialog>
  );
}

export function ProblemActionDialog({ problem, kind, onClose }: { problem: ProblemDetail; kind: ProblemDialogKind; onClose: () => void }) {
  switch (kind) {
    case 'resolve':
      return <ResolveForm problem={problem} onClose={onClose} />;
    case 'cancel':
      return <CancelForm problem={problem} onClose={onClose} />;
    case 'transfer':
      return <TransferForm problem={problem} onClose={onClose} />;
    case 'owner':
      return <OwnerForm problem={problem} onClose={onClose} />;
  }
}
