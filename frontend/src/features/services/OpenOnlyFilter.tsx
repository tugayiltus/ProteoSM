/**
 * @input The `open` filter value from the URL and its setter.
 * @output A "Yalnız açık kayıtlar" checkbox; on, the list shows the records the service page counts as open.
 * @position List toolbars of the staff ticket list, the change list and the problem list.
 */
import { CheckboxInput } from '@astryxdesign/core/CheckboxInput';
import { useTranslation } from 'react-i18next';

import { OPEN_FILTER_ON } from '../../shared/routes';

export function OpenOnlyFilter({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const { t } = useTranslation('services');
  return <CheckboxInput label={t('filter.openOnly')} value={value === OPEN_FILTER_ON} onChange={(checked) => onChange(checked ? OPEN_FILTER_ON : '')} />;
}
