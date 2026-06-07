import React, { act } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createRoot } from 'react-dom/client';

const getHealthMock = vi.fn().mockResolvedValue({ status: 'ok' });
const getVersionMock = vi.fn().mockResolvedValue({ version: 'test' });
const getInventorySummaryMock = vi.fn();
const getConsumableEntriesMock = vi.fn();
const getExpiringConsumableEntriesMock = vi.fn();
const getReplenishmentSuggestionsMock = vi.fn();
const getReplenishmentRulesMock = vi.fn();
const getShoppingListItemsMock = vi.fn();
const getDurableItemsMock = vi.fn();
const getDurableItemMock = vi.fn();
const createConsumableItemMock = vi.fn().mockResolvedValue({ id: 'item-1', name: 'Milk', kind: 'Consumable' });
const createDurableItemMock = vi.fn();
const addConsumableEntryMock = vi.fn();
const updateConsumableEntryMock = vi.fn();
const updateDurableItemMock = vi.fn();
const retireDurableItemMock = vi.fn();
const createShoppingListItemFromSuggestionMock = vi.fn();
const updateShoppingListItemStatusMock = vi.fn();
const deleteShoppingListItemMock = vi.fn();
const updateReplenishmentRuleMock = vi.fn();

const setControlValue = (control: HTMLInputElement | HTMLSelectElement, value: string) => {
  const prototype = control instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(prototype, 'value')?.set?.call(control, value);
  control.dispatchEvent(new Event(control instanceof HTMLSelectElement ? 'change' : 'input', { bubbles: true }));
};

const setCheckboxValue = (control: HTMLInputElement, checked: boolean) => {
  if (control.checked !== checked) control.click();
};

vi.mock('../../../packages/api-client/src/client', () => ({
  getHealth: getHealthMock,
  getVersion: getVersionMock,
  getInventorySummary: getInventorySummaryMock,
  getConsumableEntries: getConsumableEntriesMock,
  getExpiringConsumableEntries: getExpiringConsumableEntriesMock,
  getReplenishmentSuggestions: getReplenishmentSuggestionsMock,
  getReplenishmentRules: getReplenishmentRulesMock,
  getShoppingListItems: getShoppingListItemsMock,
  getDurableItems: getDurableItemsMock,
  getDurableItem: getDurableItemMock,
  createConsumableItem: createConsumableItemMock,
  createDurableItem: createDurableItemMock,
  addConsumableEntry: addConsumableEntryMock,
  updateConsumableEntry: updateConsumableEntryMock,
  updateDurableItem: updateDurableItemMock,
  retireDurableItem: retireDurableItemMock,
  createShoppingListItemFromSuggestion: createShoppingListItemFromSuggestionMock,
  updateShoppingListItemStatus: updateShoppingListItemStatusMock,
  deleteShoppingListItem: deleteShoppingListItemMock,
  updateReplenishmentRule: updateReplenishmentRuleMock
}));

describe('pantry mvp page', () => {
  beforeEach(() => {
    globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    vi.clearAllMocks();
    window.history.replaceState({}, '', '/');
    document.body.innerHTML = '';
    getInventorySummaryMock.mockResolvedValue([]);
    getConsumableEntriesMock.mockResolvedValue([]);
    getExpiringConsumableEntriesMock.mockResolvedValue([]);
    getReplenishmentSuggestionsMock.mockResolvedValue([]);
    getReplenishmentRulesMock.mockResolvedValue([]);
    getShoppingListItemsMock.mockResolvedValue([]);
    getDurableItemsMock.mockResolvedValue([]);
    getDurableItemMock.mockResolvedValue({
      id: 'durable-1',
      itemDefinitionId: 'definition-1',
      displayName: 'Laptop',
      description: 'Work computer',
      itemType: 'Electronics',
      brandManufacturer: 'Contoso',
      model: 'Pro 13',
      serialNumber: 'SN-1',
      purchaseDate: '2026-01-01',
      purchaseValue: 1200,
      warrantyEndsOn: '2027-01-01',
      status: 'Active',
      currentLocation: 'Office',
      notes: 'Assigned to desk',
      storageSlotId: null
    });
    createDurableItemMock.mockResolvedValue({
      id: 'durable-new',
      itemDefinitionId: 'definition-new',
      displayName: 'Camera',
      description: null,
      itemType: 'Electronics',
      brandManufacturer: null,
      model: null,
      serialNumber: null,
      purchaseDate: null,
      purchaseValue: null,
      warrantyEndsOn: null,
      status: 'Active',
      currentLocation: 'Hall closet',
      notes: null,
      storageSlotId: null
    });
    updateDurableItemMock.mockResolvedValue({
      id: 'durable-1',
      itemDefinitionId: 'definition-1',
      displayName: 'Laptop',
      description: 'Work computer',
      itemType: 'Electronics',
      brandManufacturer: 'Contoso',
      model: 'Pro 13',
      serialNumber: 'SN-1',
      purchaseDate: '2026-01-01',
      purchaseValue: 1200,
      warrantyEndsOn: '2027-01-01',
      status: 'Stored',
      currentLocation: 'Office shelf',
      notes: 'Assigned to desk',
      storageSlotId: null
    });
    retireDurableItemMock.mockResolvedValue({
      id: 'durable-1',
      itemDefinitionId: 'definition-1',
      displayName: 'Laptop',
      description: 'Work computer',
      itemType: 'Electronics',
      brandManufacturer: 'Contoso',
      model: 'Pro 13',
      serialNumber: 'SN-1',
      purchaseDate: '2026-01-01',
      purchaseValue: 1200,
      warrantyEndsOn: '2027-01-01',
      status: 'Retired',
      currentLocation: 'Office',
      notes: 'Assigned to desk',
      storageSlotId: null
    });
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
    expect(container.textContent).toContain('No durable items.');
    expect(container.textContent).toContain('No expiring entries.');
    expect(container.textContent).toContain('No replenishment needed.');
  });

  it('creates durable items from the durable item form', async () => {
    const { App } = await import('./main');

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<App />);
      await Promise.resolve();
    });

    const nameInput = container.querySelector('input[aria-label="Durable item name"]') as HTMLInputElement;
    const typeInput = container.querySelector('input[aria-label="Durable item type"]') as HTMLInputElement;
    const locationInput = container.querySelector('input[aria-label="Durable item location"]') as HTMLInputElement;
    const warrantyInput = container.querySelector('input[aria-label="Durable item warranty end"]') as HTMLInputElement;

    await act(async () => {
      setControlValue(nameInput, 'Camera');
      setControlValue(typeInput, 'Electronics');
      setControlValue(locationInput, 'Hall closet');
      setControlValue(warrantyInput, '2027-03-01');
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Create Durable Item')!.click();
      await Promise.resolve();
    });

    expect(createDurableItemMock).toHaveBeenCalledWith('http://localhost:5199', {
      displayName: 'Camera',
      description: null,
      itemType: 'Electronics',
      brandManufacturer: null,
      model: null,
      serialNumber: null,
      purchaseDate: null,
      purchaseValue: null,
      warrantyEndsOn: '2027-03-01',
      status: 'Active',
      currentLocation: 'Hall closet',
      notes: null,
      storageSlotId: null
    });
  });

  it('shows durable item list, opens details, edits, and retires items', async () => {
    getDurableItemsMock.mockResolvedValue([
      {
        id: 'durable-1',
        itemDefinitionId: 'definition-1',
        displayName: 'Laptop',
        description: 'Work computer',
        itemType: 'Electronics',
        brandManufacturer: 'Contoso',
        model: 'Pro 13',
        serialNumber: 'SN-1',
        purchaseDate: '2026-01-01',
        purchaseValue: 1200,
        warrantyEndsOn: '2027-01-01',
        status: 'Active',
        currentLocation: 'Office',
        notes: 'Assigned to desk',
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

    expect(container.textContent).toContain('Laptop: Electronics [Active]');
    expect(container.textContent).toContain('Location: Office | Warranty through 2027-01-01');

    await act(async () => {
      container.querySelector<HTMLButtonElement>('button[aria-label="Open durable item Laptop"]')!.click();
      await Promise.resolve();
    });

    expect(getDurableItemMock).toHaveBeenCalledWith('http://localhost:5199', 'durable-1');
    expect(window.location.pathname).toBe('/items/durable/durable-1');
    expect(container.textContent).toContain('Durable');
    expect(container.textContent).toContain('Brand / manufacturer: Contoso');
    expect(container.textContent).toContain('Serial number: SN-1');
    expect(container.textContent).toContain('Notes: Assigned to desk');
    expect(container.textContent).not.toContain('Consume Stock');

    await act(async () => {
      container.querySelector<HTMLButtonElement>('button[aria-label="Edit durable item Laptop"]')!.click();
    });

    const statusSelect = container.querySelector('select[aria-label="Durable item status"]') as HTMLSelectElement;
    const locationInput = container.querySelector('input[aria-label="Durable item location"]') as HTMLInputElement;

    await act(async () => {
      setControlValue(statusSelect, 'Stored');
      setControlValue(locationInput, 'Office shelf');
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Save Durable Item')!.click();
      await Promise.resolve();
    });

    expect(updateDurableItemMock).toHaveBeenCalledWith('http://localhost:5199', 'durable-1', expect.objectContaining({
      displayName: 'Laptop',
      currentLocation: 'Office shelf',
      status: 'Stored',
      warrantyEndsOn: '2027-01-01'
    }));

    await act(async () => {
      container.querySelector<HTMLButtonElement>('button[aria-label="Retire durable item Laptop"]')!.click();
      await Promise.resolve();
    });

    expect(retireDurableItemMock).toHaveBeenCalledWith('http://localhost:5199', 'durable-1', { notes: 'Assigned to desk' });
  });

  it('opens a consumable detail route with lots, replenishment, shopping state, and consumable actions', async () => {
    getInventorySummaryMock.mockResolvedValue([
      {
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        totalQuantity: 2,
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
        quantity: 2,
        unit: 'liter',
        expiresOn: '2026-06-10',
        expiresInDays: 3,
        expiryStatus: 'Soon',
        storageSlotId: 'fridge-slot'
      }
    ]);
    getReplenishmentRulesMock.mockResolvedValue([
      {
        id: 'rule-1',
        itemDefinitionId: 'item-1',
        desiredAmount: 4,
        desiredUnit: 'liter',
        expiryWarningDays: 3,
        isDisabled: false
      }
    ]);
    getReplenishmentSuggestionsMock.mockResolvedValue([
      {
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        requiredAmount: 2,
        desiredQuantity: 4,
        currentQuantity: 2,
        usableCurrentQuantity: 2,
        deficitAmount: 2,
        expiringSoonAmount: 1,
        suggestedPurchaseAmount: 3,
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
      container.querySelector<HTMLButtonElement>('button[aria-label="Open consumable item Milk"]')!.click();
    });

    expect(window.location.pathname).toBe('/items/consumable/item-1');
    expect(container.textContent).toContain('Consumable');
    expect(container.textContent).toContain('Current Amount');
    expect(container.textContent).toContain('2 liter');
    expect(container.textContent).toContain('Lot entry-1');
    expect(container.textContent).toContain('Expiry: 2026-06-10 (Soon)');
    expect(container.textContent).toContain('Location: fridge-slot');
    expect(container.textContent).toContain('Suggested purchase: 3 liter');
    expect(container.textContent).toContain('Save Rule');
    expect(container.textContent).toContain('Consume Stock');
    expect(container.textContent).not.toContain('Edit Durable Item');
    expect(container.textContent).not.toContain('Retire Durable Item');

    const consumeAmount = container.querySelector<HTMLInputElement>('input[aria-label="Consume stock amount for Milk lot entry-1"]')!;
    await act(async () => {
      setControlValue(consumeAmount, '0.5');
    });
    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Consume Stock')!.click();
      await Promise.resolve();
    });

    expect(updateConsumableEntryMock).toHaveBeenCalledWith('http://localhost:5199', 'entry-1', {
      quantity: 1.5,
      unit: 'liter',
      expiresOn: '2026-06-10',
      storageSlotId: 'fridge-slot'
    });
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
    expect(container.textContent).not.toContain('Current: 7 liter; Required: 5 liter; Suggested: 5 liter; Rule source: replenishment target');
    expect(container.textContent).not.toContain('(+2 about to expire)');
    const breakdown = 'Breakdown: current 7 liter; required 5 liter; suggested 5 liter; rule source replenishment target; desired 10 liter; usable 7 liter; deficit 3 liter; expiring soon 2 liter';
    const breakdownTrigger = container.querySelector(`[aria-label="${breakdown}"]`);
    expect(breakdownTrigger?.textContent).toContain('Breakdown');
    expect(breakdownTrigger?.getAttribute('title')).toBe(breakdown);
    expect(container.textContent).toContain('Milk');
    expect(container.textContent).toContain('Lot entry-1');
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

  it('moves shopping items into cart and then bought with stock follow-up visible', async () => {
    getShoppingListItemsMock.mockResolvedValue([
      {
        id: 'shopping-1',
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        quantity: 5,
        unit: 'liter',
        isResolved: false,
        isPurchased: false,
        status: 'ShoppingList',
        stockUpdateNeeded: false,
        nextInventoryAction: null,
        sourceDeficitAmount: 3,
        sourceExpiringSoonAmount: 2,
        sourceSuggestedPurchaseAmount: 5
      },
      {
        id: 'shopping-2',
        itemDefinitionId: 'item-2',
        itemName: 'Eggs',
        quantity: 1,
        unit: 'dozen',
        isResolved: false,
        isPurchased: false,
        status: 'InCart',
        stockUpdateNeeded: false,
        nextInventoryAction: null,
        sourceDeficitAmount: 1,
        sourceExpiringSoonAmount: 0,
        sourceSuggestedPurchaseAmount: 1
      },
      {
        id: 'shopping-3',
        itemDefinitionId: 'item-3',
        itemName: 'Rice',
        quantity: 2,
        unit: 'kg',
        isResolved: true,
        isPurchased: true,
        status: 'Bought',
        stockUpdateNeeded: false,
        nextInventoryAction: null,
        sourceDeficitAmount: 2,
        sourceExpiringSoonAmount: 0,
        sourceSuggestedPurchaseAmount: 2
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

    expect(container.textContent).toContain('Milk: 5 liter [Added to shopping list]');
    expect(container.textContent).toContain('Eggs: 1 dozen [In cart / buying]');
    expect(container.textContent).toContain('Rice: 2 kg [Bought]');
    expect(container.textContent).not.toContain('Stock update needed: Add stock details for quantity, lot, expiry, and location.');

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Add to Cart')!.click();
      await Promise.resolve();
    });

    expect(updateShoppingListItemStatusMock).toHaveBeenCalledWith('http://localhost:5199', 'shopping-1', { status: 'InCart' });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Mark Bought')!.click();
      await Promise.resolve();
    });

    expect(updateShoppingListItemStatusMock).toHaveBeenCalledWith('http://localhost:5199', 'shopping-2', { status: 'Bought' });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Remove Bought Item')!.click();
      await Promise.resolve();
    });

    expect(deleteShoppingListItemMock).toHaveBeenCalledWith('http://localhost:5199', 'shopping-3');
  });

  it('labels replenishment rule fields and saves clarified values', async () => {
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
    getReplenishmentRulesMock.mockResolvedValue([
      {
        id: 'rule-1',
        itemDefinitionId: 'item-1',
        desiredAmount: 2,
        desiredUnit: 'liter',
        expiryWarningDays: 3,
        isDisabled: false
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

    expect(container.textContent).toContain('Target amount');
    expect(container.textContent).toContain('Expiry warning days');
    expect(container.textContent).not.toContain('Amount to keep stocked before replenishment is suggested.');
    expect(container.textContent).not.toContain('Treat entries expiring within this many days as warning items.');

    const targetAmountInput = container.querySelector('input[aria-label="Target amount for Milk"]') as HTMLInputElement;
    const targetUnitInput = container.querySelector('input[aria-label="Target unit for Milk"]') as HTMLInputElement;
    const expiryWarningInput = container.querySelector('input[aria-label="Expiry warning days for Milk"]') as HTMLInputElement;
    const disabledInput = container.querySelector('input[aria-label="Disable replenishment rule for Milk"]') as HTMLInputElement;

    expect(targetAmountInput.title).toBe('Amount to keep stocked before replenishment is suggested.');
    expect(targetAmountInput.getAttribute('aria-description')).toBe('Amount to keep stocked before replenishment is suggested.');
    expect(expiryWarningInput.title).toBe('Treat entries expiring within this many days as warning items.');
    expect(expiryWarningInput.getAttribute('aria-description')).toBe('Treat entries expiring within this many days as warning items.');

    await act(async () => {
      setControlValue(targetAmountInput, '4');
      setControlValue(targetUnitInput, 'carton');
      setControlValue(expiryWarningInput, '5');
      setCheckboxValue(disabledInput, true);
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.getAttribute('aria-label') === 'Save replenishment rule for Milk')!.click();
      await Promise.resolve();
    });

    expect(updateReplenishmentRuleMock).toHaveBeenCalledWith('http://localhost:5199', 'rule-1', {
      desiredAmount: 4,
      desiredUnit: 'carton',
      expiryWarningDays: 5,
      isDisabled: true
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
      setControlValue(quantityInput, '2');
      setControlValue(unitInput, 'carton');
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Adjust Quantity / Expiry / Location')!.click();
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

  it('offers lot actions for adding, consuming, depleting, editing expiry, and moving location', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    getConsumableEntriesMock.mockResolvedValue([
      {
        entryId: 'entry-1',
        itemDefinitionId: 'item-1',
        itemName: 'Milk',
        quantity: 5,
        unit: 'liter',
        expiresOn: '2026-06-10',
        expiresInDays: 8,
        expiryStatus: 'Safe',
        storageSlotId: 'slot-1'
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

    expect(container.textContent).toContain('Lot entry-1');
    expect(container.textContent).toContain('Expiry: 2026-06-10 (Safe)');
    expect(container.textContent).toContain('Location: slot-1');
    expect(container.textContent).toContain('Add stock');
    expect(container.textContent).toContain('Consume stock');
    expect(container.textContent).toContain('Add stock unit');
    expect(container.textContent).toContain('Consume stock unit');
    expect(container.textContent).toContain('Mark Lot Depleted');
    expect(container.textContent).toContain('Undo Unsaved Changes');

    const addStockInput = container.querySelector('input[aria-label="Add stock amount for Milk lot entry-1"]') as HTMLInputElement;
    const addStockUnitInput = container.querySelector('input[aria-label="Add stock unit for Milk lot entry-1"]') as HTMLInputElement;
    expect(addStockUnitInput.value).toBe('liter');
    await act(async () => {
      setControlValue(addStockInput, '2');
      setControlValue(addStockUnitInput, 'liter');
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Add Stock')!.click();
      await Promise.resolve();
    });

    expect(updateConsumableEntryMock).toHaveBeenLastCalledWith('http://localhost:5199', 'entry-1', {
      quantity: 7,
      unit: 'liter',
      expiresOn: '2026-06-10',
      storageSlotId: 'slot-1'
    });

    const consumeStockInput = container.querySelector('input[aria-label="Consume stock amount for Milk lot entry-1"]') as HTMLInputElement;
    const consumeStockUnitInput = container.querySelector('input[aria-label="Consume stock unit for Milk lot entry-1"]') as HTMLInputElement;
    expect(consumeStockUnitInput.value).toBe('liter');
    await act(async () => {
      setControlValue(consumeStockInput, '3');
      setControlValue(consumeStockUnitInput, 'liter');
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Consume Stock')!.click();
      await Promise.resolve();
    });

    expect(updateConsumableEntryMock).toHaveBeenLastCalledWith('http://localhost:5199', 'entry-1', {
      quantity: 2,
      unit: 'liter',
      expiresOn: '2026-06-10',
      storageSlotId: 'slot-1'
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Mark Lot Depleted')!.click();
      await Promise.resolve();
    });

    expect(window.confirm).toHaveBeenCalledWith('Mark Milk lot entry-1 as depleted?');
    expect(updateConsumableEntryMock).toHaveBeenLastCalledWith('http://localhost:5199', 'entry-1', {
      quantity: 0,
      unit: 'liter',
      expiresOn: '2026-06-10',
      storageSlotId: 'slot-1'
    });
  });

  it('rejects invalid consumable quantity operations before sending updates', async () => {
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

    const quantityInput = container.querySelector('input[aria-label="Entry quantity for Milk"]') as HTMLInputElement;
    await act(async () => {
      setControlValue(quantityInput, '-1');
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Adjust Quantity / Expiry / Location')!.click();
      await Promise.resolve();
    });

    expect(container.textContent).toContain('Quantity must be zero or greater.');
    expect(updateConsumableEntryMock).not.toHaveBeenCalled();

    const consumeStockInput = container.querySelector('input[aria-label="Consume stock amount for Milk lot entry-1"]') as HTMLInputElement;
    const consumeStockUnitInput = container.querySelector('input[aria-label="Consume stock unit for Milk lot entry-1"]') as HTMLInputElement;
    await act(async () => {
      setControlValue(consumeStockInput, '2');
      setControlValue(consumeStockUnitInput, 'carton');
    });

    await act(async () => {
      Array.from(container.querySelectorAll('button')).find((button) => button.textContent === 'Consume Stock')!.click();
      await Promise.resolve();
    });

    expect(container.textContent).toContain('Consume stock unit must match the lot unit liter.');
    expect(updateConsumableEntryMock).not.toHaveBeenCalled();
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
