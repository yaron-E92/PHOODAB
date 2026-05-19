import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act } from 'react-dom/test-utils';
import { createRoot } from 'react-dom/client';

const getHealthMock = vi.fn().mockResolvedValue({ status: 'ok' });
const getVersionMock = vi.fn().mockResolvedValue({ version: 'test' });
const getReplenishmentSuggestionsMock = vi.fn();

vi.mock('../../../packages/api-client/src/client', () => ({
  getHealth: getHealthMock,
  getVersion: getVersionMock,
  getReplenishmentSuggestions: getReplenishmentSuggestionsMock
}));

vi.mock('react-dom/client', async () => {
  const actual = await vi.importActual<typeof import('react-dom/client')>('react-dom/client');
  return {
    ...actual,
    createRoot: vi.fn(() => ({ render: vi.fn() }))
  };
});

describe('replenishment rendering contract', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = '<div id="root"></div>';
  });

  it('renders requiredAmount from API payload flow without frontend recalculation', async () => {
    getReplenishmentSuggestionsMock.mockResolvedValue([
      {
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        requiredAmount: 4,
        desiredQuantity: 10,
        currentQuantity: 9,
        unit: 'liter'
      }
    ]);

    const { App } = await import('./main');

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<App />);
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(getReplenishmentSuggestionsMock).toHaveBeenCalledWith('http://localhost:5199');
    expect(container.textContent).toContain('Milk: 4 liter');
    expect(container.textContent).not.toContain('Milk: 1 liter');
  });
});
