import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act } from 'react-dom/test-utils';
import { createRoot } from 'react-dom/client';

const getHealthMock = vi.fn().mockResolvedValue({ status: 'ok' });
const getVersionMock = vi.fn().mockResolvedValue({ version: 'test' });
const getInventorySummaryMock = vi.fn();
const getExpiringConsumableEntriesMock = vi.fn();
const getReplenishmentSuggestionsMock = vi.fn();
const getReplenishmentRulesMock = vi.fn();
const getShoppingListItemsMock = vi.fn();
const createConsumableItemMock = vi.fn().mockResolvedValue({ id: 'item-1', name: 'Milk', kind: 'Consumable' });
const addConsumableEntryMock = vi.fn();
const createShoppingListItemFromSuggestionMock = vi.fn();
const updateShoppingListItemStatusMock = vi.fn();
const updateReplenishmentRuleMock = vi.fn();

vi.mock('../../../packages/api-client/src/client', () => ({
  getHealth: getHealthMock,
  getVersion: getVersionMock,
  getInventorySummary: getInventorySummaryMock,
  getExpiringConsumableEntries: getExpiringConsumableEntriesMock,
  getReplenishmentSuggestions: getReplenishmentSuggestionsMock,
  getReplenishmentRules: getReplenishmentRulesMock,
  getShoppingListItems: getShoppingListItemsMock,
  createConsumableItem: createConsumableItemMock,
  addConsumableEntry: addConsumableEntryMock,
  createShoppingListItemFromSuggestion: createShoppingListItemFromSuggestionMock,
  updateShoppingListItemStatus: updateShoppingListItemStatusMock,
  updateReplenishmentRule: updateReplenishmentRuleMock
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
    getExpiringConsumableEntriesMock.mockResolvedValue([]);
    getReplenishmentSuggestionsMock.mockResolvedValue([]);
    getReplenishmentRulesMock.mockResolvedValue([]);
    getShoppingListItemsMock.mockResolvedValue([]);
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
    expect(container.textContent).toContain('No expiring entries.');
    expect(container.textContent).toContain('No replenishment needed.');
  });

  it('renders backend-provided expiryStatus and requiredAmount values', async () => {
    getInventorySummaryMock.mockResolvedValue([
      {
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        totalQuantity: 1,
        unit: 'liter',
        entryCount: 1
      }
    ]);
    getExpiringConsumableEntriesMock.mockResolvedValue([
      {
        entryId: 'entry-1',
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
        entries: []
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
