import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act } from 'react-dom/test-utils';
import { createRoot } from 'react-dom/client';

const getHealthMock = vi.fn().mockResolvedValue({ status: 'ok' });
const getVersionMock = vi.fn().mockResolvedValue({ version: 'test' });
const getInventorySummaryMock = vi.fn();
const getExpiringLotsMock = vi.fn();
const getReplenishmentSuggestionsMock = vi.fn();
const createConsumableItemMock = vi.fn().mockResolvedValue({ id: 'item-1', name: 'Milk', kind: 'Consumable' });
const addInventoryLotMock = vi.fn();

vi.mock('../../../packages/api-client/src/client', () => ({
  getHealth: getHealthMock,
  getVersion: getVersionMock,
  getInventorySummary: getInventorySummaryMock,
  getExpiringLots: getExpiringLotsMock,
  getReplenishmentSuggestions: getReplenishmentSuggestionsMock,
  createConsumableItem: createConsumableItemMock,
  addInventoryLot: addInventoryLotMock
}));

vi.mock('react-dom/client', async () => {
  const actual = await vi.importActual<typeof import('react-dom/client')>('react-dom/client');
  return {
    ...actual,
    createRoot: vi.fn(() => ({ render: vi.fn() }))
  };
});

describe('pantry mvp page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = '<div id="root"></div>';
    getInventorySummaryMock.mockResolvedValue([]);
    getExpiringLotsMock.mockResolvedValue([]);
    getReplenishmentSuggestionsMock.mockResolvedValue([]);
  });

  it('shows loading then empty states', async () => {
    const { App } = await import('./main');

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<App />);
    });

    expect(container.textContent).toContain('No inventory yet.');
    expect(container.textContent).toContain('No expiring lots.');
    expect(container.textContent).toContain('No replenishment needed.');
  });

  it('renders backend-provided expiryStatus and requiredAmount values', async () => {
    getInventorySummaryMock.mockResolvedValue([
      {
        inventoryEntryId: 'entry-1',
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        totalQuantity: 1,
        unit: 'liter',
        lotCount: 1
      }
    ]);
    getExpiringLotsMock.mockResolvedValue([
      {
        lotId: 'lot-1',
        quantity: 1,
        unit: 'liter',
        expiresOn: '2026-06-01',
        expiresInDays: 1,
        expiryStatus: 'Soon'
      }
    ]);
    getReplenishmentSuggestionsMock.mockResolvedValue([
      {
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        requiredAmount: 4,
        desiredQuantity: 10,
        currentQuantity: 6,
        unit: 'liter',
        lots: []
      }
    ]);

    const { App } = await import('./main');

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<App />);
      await Promise.resolve();
    });

    expect(container.textContent).toContain('1 liter - Soon');
    expect(container.textContent).toContain('Milk: 4 liter');
  });

  it('shows basic error state', async () => {
    getInventorySummaryMock.mockRejectedValue(new Error('boom'));

    const { App } = await import('./main');

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<App />);
      await Promise.resolve();
    });

    expect(container.textContent).toContain('Error: Error: boom');
  });
});
