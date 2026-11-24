import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { adminKeys } from '../../../shared/api/adminKeys';
import { api, unwrap } from '../../../shared/api/client';
import type { SelectOption } from '../../../shared/form/formFields';
import { REFERENCE_PAGE_SIZE, useActiveUsers } from '../../../shared/reference/referenceOptions';
import { useLocations } from '../locations/api';
import { buildLocationTree, flattenLocationTree } from '../locations/locationTree';


export function useDepartmentOptions(): SelectOption[] {
  const departments = useQuery({
    queryKey: [...adminKeys.departments, 'options'],
    queryFn: () => unwrap(api.GET('/api/v1/admin/departments', { params: { query: { isActive: true, pageSize: REFERENCE_PAGE_SIZE } } })),
  });
  return useMemo(() => (departments.data?.items ?? []).map((d) => ({ value: d.id, label: d.name })), [departments.data]);
}

export function useLocationOptions(): SelectOption[] {
  const locations = useLocations();
  return useMemo(
    () =>
      flattenLocationTree(buildLocationTree(locations.data ?? []))
        .filter((node) => node.item.isActive)
        .map((node) => ({ value: node.item.id, label: node.path })),
    [locations.data],
  );
}

/** Active IT staff and admins: the only roles allowed in teams. */
export function useTeamCandidateOptions(): SelectOption[] {
  const users = useActiveUsers(undefined);
  return useMemo(
    () =>
      (users.data?.items ?? [])
        .filter((u) => u.role !== 'employee')
        .map((u) => ({ value: u.id, label: `${u.firstName} ${u.lastName} (${u.email})` })),
    [users.data],
  );
}
