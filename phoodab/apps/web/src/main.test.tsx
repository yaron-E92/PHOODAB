import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act } from 'react-dom/test-utils';
import { createRoot } from 'react-dom/client';

const getHealthMock = vi.fn().mockResolvedValue({ status: 'ok' });
const getVersionMock = vi.fn().mockResolvedValue({ version: 'test' });
const getInventorySummaryMock = vi.fn();
const getConsumableEntriesMock = vi.fn();
const getExpiringConsumableEntriesMock = vi.fn();
const getReplenishmentSuggestionsMock = vi.fn();
const getReplenishmentRulesMock = vi.fn();
const getShoppingListItemsMock = vi.fn();
const createConsumableItemMock = vi.fn().mockResolvedValue({ id: 'item-1', name: 'Milk', kind: 'Consumable' });
const addConsumableEntryMock = vi.fn();
const updateConsumableEntryMock = vi.fn();
const createShoppingListItemFromSuggestionMock = vi.fn();
const updateShoppingListItemStatusMock = vi.fn();
const updateReplenishmentRuleMock = vi.fn();

vi.mock('../../../packages/api-client/src/client', () => ({
  getHealth: getHealthMock,
  getVersion: getVersionMock,
  getInventorySummary: getInventorySummaryMock,
  getConsumableEntries: getConsumableEntriesMock,
  getExpiringConsumableEntries: getExpiringConsumableEntriesMock,
  getReplenishmentSuggestions: getReplenishmentSuggestionsMock,
  getReplenishmentRules: getReplenishmentRulesMock,
  getShoppingListItems: getShoppingListItemsMock,
  createConsumableItem: createConsumableItemMock,
  addConsumableEntry: addConsumableEntryMock,
  updateConsumableEntry: updateConsumableEntryMock,
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
    getConsumableEntriesMock.mockResolvedValue([]);
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

  it('renders backend-provided expiryStatus and replenishment breakdown values', async () => {
    getInventorySummaryMock.mockResolvedValue([
      {
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        totalQuantity: 1,
        unit: 'liter',
        entryCount: 1,
        hasMixedUnits: false,
        mixedUnitWarning: null
      }
    ]);
    getConsumableEntriesMock.mockResolvedValue([
      {
        entryId: 'entry-1',
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        quantity: 1,
        unit: 'liter',
        expiresOn: '2026-06-01',
        expiresInDays: -1,
        expiryStatus: 'Expired',
        storageSlotId: 'slot-1'
      }
    ]);
    getExpiringConsumableEntriesMock.mockResolvedValue([
      {
        entryId: 'entry-1',
        itemName: 'Milk',
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
        requiredAmount: 5,
        desiredQuantity: 10,
        currentQuantity: 7,
        usableCurrentQuantity: 7,
        deficitAmount: 3,
        expiringSoonAmount: 2,
        suggestedPurchaseAmount: 5,
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

    expect(container.textContent).toContain('Milk - 1 liter - Soon');
    expect(container.textContent).toContain('Milk: 5 liter (2 about to expire)');
    expect(container.textContent).not.toContain('(+2 about to expire)');
    const breakdown = 'Breakdown: desired 10 liter; usable 7 liter; deficit 3 liter; expiring soon 2 liter';
    const breakdownTrigger = container.querySelector(`[aria-label="${breakdown}"]`);
    expect(breakdownTrigger?.textContent).toContain('Breakdown');
    expect(breakdownTrigger?.getAttribute('title')).toBe(breakdown);
    expect(container.textContent).toContain('Milk');
    expect(container.textContent).toContain('Expired');
    expect(container.textContent).toContain('slot-1');
  });

  it('uses backend suggested purchase amount and source context when adding a suggestion to shopping list', async () => {
    getReplenishmentSuggestionsMock.mockResolvedValue([
      {
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        requiredAmount: 5,
        desiredQuantity: 10,
        currentQuantity: 7,
        usableCurrentQuantity: 7,
        deficitAmount: 3,
        expiringSoonAmount: 2,
        suggestedPurchaseAmount: 5,
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

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Add to Shopping List')!.click();
      await Promise.resolve();
    });

    expect(createShoppingListItemFromSuggestionMock).toHaveBeenCalledWith('http://localhost:5199', {
      itemDefinitionId: 'item-1',
      quantity: 5,
      unit: 'liter',
      deficitAmount: 3,
      expiringSoonAmount: 2,
      suggestedPurchaseAmount: 5
    });
  });

  it('updates consumable entries and refreshes pantry data', async () => {
    getConsumableEntriesMock.mockResolvedValue([
      {
        entryId: 'entry-1',
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        quantity: 1,
        unit: 'liter',
        expiresOn: null,
        expiresInDays: null,
        expiryStatus: 'Unknown',
        storageSlotId: null
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

    const inputs = Array.from(container.querySelectorAll('input'));
    const quantityInput = inputs.find((input) => input.getAttribute('aria-label') === 'Entry quantity for Milk')!;
    const unitInput = inputs.find((input) => input.getAttribute('aria-label') === 'Entry unit for Milk')!;

    await act(async () => {
      quantityInput.value = '2';
      quantityInput.dispatchEvent(new Event('input', { bubbles: true }));
      unitInput.value = 'carton';
      unitInput.dispatchEvent(new Event('input', { bubbles: true }));
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Save Entry')!.click();
      await Promise.resolve();
    });

    expect(updateConsumableEntryMock).toHaveBeenCalledWith('http://localhost:5199', 'entry-1', {
      quantity: 2,
      unit: 'carton',
      expiresOn: null,
      storageSlotId: null
    });
    expect(getInventorySummaryMock).toHaveBeenCalledTimes(2);
  });

  it('shows mixed-unit inventory summaries as warnings', async () => {
    getInventorySummaryMock.mockResolvedValue([
      {
        itemDefinitionId: 'item-1',
        itemName: 'Rice',
        totalQuantity: null,
        unit: null,
        entryCount: 2,
        hasMixedUnits: true,
        mixedUnitWarning: 'Mixed units cannot be totaled safely.'
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

    expect(container.textContent).toContain('Rice: mixed units (2 entries)');
    expect(container.textContent).toContain('Mixed units cannot be totaled safely.');
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
