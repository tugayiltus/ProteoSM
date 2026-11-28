import { Button } from '@astryxdesign/core/Button';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AstryxProvider } from '../ui/AstryxProvider';

import { ListEmptyState } from './ListEmptyState';

const withAstryx = (ui: React.ReactNode) => render(<AstryxProvider>{ui}</AstryxProvider>);

describe('ListEmptyState', () => {
  it('shows the first-use message and action when no filter is active', () => {
    withAstryx(<ListEmptyState activeCount={0} onClear={vi.fn()} message="Görev yok." action={<Button label="Yeni görev" />} />);
    expect(screen.getByText('Görev yok.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Yeni görev' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Filtreleri temizle' })).not.toBeInTheDocument();
  });

  it('shows a no-result message and clears the filters when a filter is active', async () => {
    const onClear = vi.fn();
    withAstryx(<ListEmptyState activeCount={2} onClear={onClear} message="Görev yok." action={<Button label="Yeni görev" />} />);
    expect(screen.getByText('Bu filtrelerle kayıt bulunamadı.')).toBeInTheDocument();
    expect(screen.queryByText('Görev yok.')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Yeni görev' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Filtreleri temizle' }));
    expect(onClear).toHaveBeenCalledOnce();
  });
});
