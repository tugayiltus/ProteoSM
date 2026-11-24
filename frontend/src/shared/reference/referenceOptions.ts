import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { adminKeys } from '../api/adminKeys';
import { api, unwrap } from '../api/client';
import type { components } from '../api/schema';
import type { SelectOption } from '../form/formFields';

type UserRole = components['schemas']['UserRole'];

/** Reference lists are small; one page of the API maximum (PageRequest.MaxPageSize) covers them. */
export const REFERENCE_PAGE_SIZE = 100;

/** Keeps the record's current (maybe now inactive) reference selectable so editing does not silently clear it. */
export function withCurrentOption(options: SelectOption[], id: string | null | undefined, label: string | null | undefined): SelectOption[] {
  if (!id || options.some((option) => option.value === id)) {
    return options;
  }
  return [...options, { value: id, label: label ?? id }];
}

/** Active users, of one role or all. */
export function useActiveUsers(role: UserRole | undefined) {
  return useQuery({
    queryKey: [...adminKeys.users, 'options', role ?? 'all'],
    queryFn: () =>
      unwrap(api.GET('/api/v1/admin/users', { params: { query: { isActive: true, role, pageSize: REFERENCE_PAGE_SIZE } } })),
  });
}

/** Active admins: department approvers must be admins (ADR-0005). */
export function useAdminOptions(): SelectOption[] {
  const admins = useActiveUsers('admin');
  return useMemo(() => (admins.data?.items ?? []).map((u) => ({ value: u.id, label: `${u.firstName} ${u.lastName}` })), [admins.data]);
}
