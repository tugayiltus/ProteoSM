/**
 * @input A source to edit, or null for a new one, a callback with a new secret and a close callback.
 * @output The source form.
 * @position `/admin/monitoring`.
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { FormSelector, FormSwitch, FormTextInput } from '../../../shared/form/formFields';
import { withCurrentOption } from '../../../shared/reference/referenceOptions';
import { useCloseOnConflict } from '../../changes/useCloseOnConflict';
import { useActiveServiceOptions, useOwnerTeamOptions } from '../../services/serviceApi';
import { useCategories } from '../catalog/api';
import { useTeamCandidateOptions } from '../shared/referenceOptions';

import { useCreateMonitoringSource, useUpdateMonitoringSource, type MonitoringSource } from './api';

/** Mirrors backend MonitoringLimits.NameMaxLength. */
const NAME_MAX_LENGTH = 100;
const LEVELS = ['low', 'medium', 'high'] as const;

const schema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  teamId: z.string().min(1, ERROR_CODES.required),
  requesterId: z.string().min(1, ERROR_CODES.required),
  serviceId: z.string(),
  categoryId: z.string(),
  impact: z.enum(LEVELS),
  urgency: z.enum(LEVELS),
  isActive: z.boolean(),
});
type FormValues = z.infer<typeof schema>;
const FORM_FIELDS = ['name', 'teamId', 'requesterId'] as const;
const CODE_FIELDS = { [ERROR_CODES.monitoringNameTaken]: 'name', [ERROR_CODES.monitoringRequesterInvalid]: 'requesterId' } as const;
const orNull = (value: string) => (value === '' ? null : value);

interface MonitoringSourceFormDialogProps {
  source: MonitoringSource | null;
  onSecret: (name: string, secret: string) => void;
  onClose: () => void;
}

export function MonitoringSourceFormDialog({ source, onSecret, onClose }: MonitoringSourceFormDialogProps) {
  const { t } = useTranslation('admin');
  const { t: tt } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const create = useCreateMonitoringSource();
  const update = useUpdateMonitoringSource();
  const mutation = source === null ? create : update;
  const closeOnConflict = useCloseOnConflict(onClose);
  const teams = withCurrentOption(useOwnerTeamOptions(), source?.teamId, source?.teamName);
  const requesters = withCurrentOption(useTeamCandidateOptions(), source?.requesterId, source?.requesterName);
  const services = withCurrentOption(useActiveServiceOptions(true), source?.serviceId, source?.serviceName);
  const categories = useCategories();
  const categoryOptions = useMemo(
    () =>
      withCurrentOption(
        (categories.data ?? []).filter((c) => c.isActive && c.scope !== 'request').map((c) => ({ value: c.id, label: c.name })),
        source?.categoryId,
        source?.categoryName,
      ),
    [categories.data, source],
  );
  const levels = LEVELS.map((value) => ({ value, label: tt(`levels.${value}`) }));
  const {
    control,
    handleSubmit,
    setError,
    formState: { isDirty },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: source?.name ?? '',
      teamId: source?.teamId ?? '',
      requesterId: source?.requesterId ?? '',
      serviceId: source?.serviceId ?? '',
      categoryId: source?.categoryId ?? '',
      impact: source?.impact ?? 'medium',
      urgency: source?.urgency ?? 'medium',
      isActive: source?.isActive ?? true,
    },
  });
  const onSubmit = handleSubmit(({ serviceId, categoryId, ...rest }) => {
    const body = { ...rest, serviceId: orNull(serviceId), categoryId: orNull(categoryId) };
    const onError = (error: unknown) => {
      closeOnConflict(error);
      applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS);
    };
    if (source === null) {
      create.mutate(body, { onSuccess: (result) => onSecret(result.source.name, result.secret), onError });
    } else {
      update.mutate({ id: source.id, version: source.version, ...body }, { onSuccess: onClose, onError });
    }
  });
  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;
  return (
    <FormDialog
      isDirty={isDirty}
      title={source === null ? t('monitoring.new') : t('monitoring.edit')}
      submitLabel={source === null ? tc('actions.create') : tc('actions.save')}
      isPending={mutation.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="name" label={t('monitoring.fields.name')} autoComplete="off" isRequired />
      <FormSelector control={control} name="teamId" label={t('monitoring.fields.team')} options={teams} placeholder={tc('none')} isRequired />
      <FormSelector control={control} name="requesterId" label={t('monitoring.fields.requester')} options={requesters} placeholder={tc('none')} isRequired />
      <FormSelector control={control} name="impact" label={t('monitoring.fields.impact')} options={levels} isRequired />
      <FormSelector control={control} name="urgency" label={t('monitoring.fields.urgency')} options={levels} isRequired />
      <FormSelector control={control} name="serviceId" label={t('monitoring.fields.service')} options={services} placeholder={tc('none')} isClearable />
      <FormSelector control={control} name="categoryId" label={t('monitoring.fields.category')} options={categoryOptions} placeholder={tc('none')} isClearable />
      <FormSwitch control={control} name="isActive" label={t('monitoring.fields.active')} />
    </FormDialog>
  );
}
