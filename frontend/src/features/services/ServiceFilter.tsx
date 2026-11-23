/**
 * @input The `serviceId` filter value from the URL and its setter.
 * @output A "Hizmet" select over every service, also inactive ones. The toolbar's "Temizle" clears it.
 * @position List toolbars of the staff ticket list, the change list and the problem list (IT staff and admins).
 */
import { Selector } from '@astryxdesign/core/Selector';
import { useTranslation } from 'react-i18next';

import { useServiceFilterOptions } from './api';

export function ServiceFilter({ value, onChange }: { value: string; onChange: (serviceId: string) => void }) {
  const { t } = useTranslation('services');
  const options = useServiceFilterOptions();
  return <Selector label={t('filter.label')} placeholder={t('filter.all')} options={options} value={value} onChange={onChange} />;
}
