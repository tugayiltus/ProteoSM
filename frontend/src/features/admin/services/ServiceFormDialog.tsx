/**
 * @input The service to edit (loaded by id), or null to create one, and a close callback.
 * @output A form dialog with the name, the owner team and the description.
 * @position Opened from the admin service list.
 */
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { FormDialog } from '../../../shared/feedback/FormDialog';
import { ErrorState, LoadingState } from '../../../shared/feedback/States';
import { FormSelector, FormTextArea, FormTextInput } from '../../../shared/form/formFields';
import { withCurrentOption } from '../../../shared/reference/referenceOptions';
import { useCreateService, useOwnerTeamOptions, useServiceDetail, useUpdateService, type ServiceDetail } from '../../services/serviceApi';

/** Mirrors backend Service.NameMaxLength and Service.DescriptionMaxLength. */
const SERVICE_NAME_MAX_LENGTH = 100;
const SERVICE_DESCRIPTION_MAX_LENGTH = 1000;

const serviceSchema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(SERVICE_NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  ownerTeamId: z.string().min(1, ERROR_CODES.required),
  description: z.string().trim().max(SERVICE_DESCRIPTION_MAX_LENGTH, ERROR_CODES.tooLong),
});
type ServiceFormValues = z.infer<typeof serviceSchema>;

const FORM_FIELDS = ['name', 'ownerTeamId', 'description'] as const;
const CODE_FIELDS = { [ERROR_CODES.serviceNameTaken]: 'name', [ERROR_CODES.serviceOwnerTeamInactive]: 'ownerTeamId' } as const;

function ServiceForm({ service, onClose }: { service: ServiceDetail | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const create = useCreateService();
  const update = useUpdateService();
  const mutation = service === null ? create : update;
  const teamOptions = withCurrentOption(useOwnerTeamOptions(), service?.ownerTeamId, service?.ownerTeamName);
  const {
    control,
    handleSubmit,
    setError,
    formState: { isDirty },
  } = useForm<ServiceFormValues>({
    resolver: zodResolver(serviceSchema),
    defaultValues: { name: service?.name ?? '', ownerTeamId: service?.ownerTeamId ?? '', description: service?.description ?? '' },
  });

  const onSubmit = handleSubmit(({ name, ownerTeamId, description }) => {
    const body = { name, ownerTeamId, description: description === '' ? null : description };
    const callbacks = { onSuccess: onClose, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (service === null) {
      create.mutate(body, callbacks);
    } else {
      update.mutate({ id: service.id, version: service.version, ...body }, callbacks);
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;

  return (
    <FormDialog
      isDirty={isDirty}
      title={service === null ? t('services.new') : t('services.edit')}
      submitLabel={service === null ? tc('actions.create') : tc('actions.save')}
      isPending={mutation.isPending}
      alert={alert}
      onSubmit={(event) => void onSubmit(event)}
      onClose={onClose}
    >
      <FormTextInput control={control} name="name" label={t('services.fields.name')} autoComplete="off" isRequired />
      <FormSelector control={control} name="ownerTeamId" label={t('services.fields.ownerTeam')} options={teamOptions} placeholder={tc('none')} isRequired />
      <FormTextArea control={control} name="description" label={t('services.fields.description')} />
    </FormDialog>
  );
}

/** Loads the current version and description first; the list row has neither. */
function EditServiceForm({ serviceId, onClose }: { serviceId: string; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const detail = useServiceDetail(serviceId);
  if (detail.data) {
    return <ServiceForm service={detail.data} onClose={onClose} />;
  }
  return (
    <FormDialog title={t('services.edit')} submitLabel={tc('actions.save')} isPending alert={null} onSubmit={(event) => event.preventDefault()} onClose={onClose}>
      {detail.isError ? <ErrorState error={detail.error} onRetry={() => void detail.refetch()} /> : <LoadingState />}
    </FormDialog>
  );
}

export function ServiceFormDialog({ serviceId, onClose }: { serviceId: string | null; onClose: () => void }) {
  return serviceId === null ? <ServiceForm service={null} onClose={onClose} /> : <EditServiceForm serviceId={serviceId} onClose={onClose} />;
}
