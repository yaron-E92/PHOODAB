import type { paths } from './generated';

type HealthResponse = paths['/health']['get']['responses']['200']['content']['application/json'];
type VersionResponse = paths['/version']['get']['responses']['200']['content']['application/json'];
type ReplenishmentRulesResponse = paths['/api/replenishment/rules']['get']['responses']['200']['content']['application/json'];
type UpdateReplenishmentRuleRequest = paths['/api/replenishment/rules/{ruleId}']['patch']['requestBody']['content']['application/json'];
type CreateConsumableEntryRequest = paths['/api/consumable-entries']['post']['requestBody']['content']['application/json'];
type UpdateConsumableEntryRequest = paths['/api/consumable-entries/{entryId}']['patch']['requestBody']['content']['application/json'];
type DurableItemsResponse = paths['/api/durable-entries']['get']['responses']['200']['content']['application/json'];
type CreateDurableEntryRequest = paths['/api/durable-entries']['post']['requestBody']['content']['application/json'];
type UpdateDurableEntryRequest = paths['/api/durable-entries/{entryId}']['patch']['requestBody']['content']['application/json'];
type RetireDurableEntryRequest = paths['/api/durable-entries/{entryId}/retire']['patch']['requestBody']['content']['application/json'];

export type ItemDefinition = {
  id: string;
  name: string;
  kind: string;
};

export type InventorySummaryItem = {
  itemDefinitionId: string;
  itemName: string;
  totalQuantity: number | null;
  unit: string | null;
  entryCount: number;
  hasMixedUnits: boolean;
  mixedUnitWarning: string | null;
};

export type ConsumableEntry = {
  entryId: string;
  itemDefinitionId: string;
  itemName: string;
  quantity: number;
  unit: string;
  expiresOn: string | null;
  expiresInDays: number | null;
  expiryStatus: 'Unknown' | 'Expired' | 'Urgent' | 'Soon' | 'Safe';
  storageSlotId: string | null;
};

export type ExpiringConsumableEntry = ConsumableEntry;

export type DurableItemStatus = 'Active' | 'NeedsRepair' | 'LoanedOut' | 'Stored' | 'Retired' | 'Lost';
export type DurableItem = DurableItemsResponse[number] & {
  status: DurableItemStatus;
};

export type ReplenishmentSuggestion = {
  itemDefinitionId: string;
  itemName: string;
  currentQuantity: number;
  usableCurrentQuantity: number;
  desiredQuantity: number;
  deficitAmount: number;
  expiringSoonAmount: number;
  suggestedPurchaseAmount: number;
  requiredAmount: number;
  unit: string;
  entries: {
    entryId: string;
    quantity: number;
    unit: string;
    expiresOn: string | null;
    expiresInDays: number | null;
    expiryStatus: 'Unknown' | 'Expired' | 'Urgent' | 'Soon' | 'Safe';
  }[];
};

export type ReplenishmentRule = ReplenishmentRulesResponse[number];

export type ShoppingListItem = {
  id: string;
  itemDefinitionId: string;
  itemName: string;
  quantity: number;
  unit: string;
  isResolved: boolean;
  isPurchased: boolean;
  status: 'ShoppingList' | 'InCart' | 'Bought' | 'StockUpdateNeeded';
  stockUpdateNeeded: boolean;
  nextInventoryAction: string | null;
  sourceDeficitAmount: number | null;
  sourceExpiringSoonAmount: number | null;
  sourceSuggestedPurchaseAmount: number | null;
};

export async function getHealth(baseUrl: string): Promise<HealthResponse> {
  const response = await fetch(`${baseUrl}/health`);
  if (!response.ok) throw new Error(`Health failed: ${response.status}`);
  return response.json();
}

export async function getVersion(baseUrl: string): Promise<VersionResponse> {
  const response = await fetch(`${baseUrl}/version`);
  if (!response.ok) throw new Error(`Version failed: ${response.status}`);
  return response.json();
}

export async function createConsumableItem(
  baseUrl: string,
  payload: {
    name: string;
    desiredAmount?: number | null;
    desiredUnit?: string | null;
  }
): Promise<ItemDefinition> {
  const response = await fetch(`${baseUrl}/api/item-definitions`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: payload.name,
      kind: 1,
      desiredAmount: payload.desiredAmount ?? null,
      desiredUnit: payload.desiredUnit ?? null
    })
  });

  if (!response.ok) throw new Error(`Create item failed: ${response.status}`);

  return response.json();
}

export async function addConsumableEntry(
  baseUrl: string,
  payload: CreateConsumableEntryRequest
): Promise<void> {
  const response = await fetch(`${baseUrl}/api/consumable-entries`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) throw new Error(`Add consumable entry failed: ${response.status}`);
}

export async function createDurableItem(
  baseUrl: string,
  payload: CreateDurableEntryRequest
): Promise<DurableItem> {
  const response = await fetch(`${baseUrl}/api/durable-entries`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) throw new Error(`Create durable item failed: ${response.status}`);

  return response.json();
}

export async function getDurableItems(baseUrl: string): Promise<DurableItem[]> {
  const response = await fetch(`${baseUrl}/api/durable-entries`);
  if (!response.ok) throw new Error(`Durable items failed: ${response.status}`);
  return response.json();
}

export async function getDurableItem(baseUrl: string, entryId: string): Promise<DurableItem> {
  const response = await fetch(`${baseUrl}/api/durable-entries/${entryId}`);
  if (!response.ok) throw new Error(`Durable item failed: ${response.status}`);
  return response.json();
}

export async function updateDurableItem(
  baseUrl: string,
  entryId: string,
  payload: UpdateDurableEntryRequest
): Promise<DurableItem> {
  const response = await fetch(`${baseUrl}/api/durable-entries/${entryId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) throw new Error(`Update durable item failed: ${response.status}`);

  return response.json();
}

export async function retireDurableItem(
  baseUrl: string,
  entryId: string,
  payload: RetireDurableEntryRequest
): Promise<DurableItem> {
  const response = await fetch(`${baseUrl}/api/durable-entries/${entryId}/retire`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) throw new Error(`Retire durable item failed: ${response.status}`);

  return response.json();
}

export async function getInventorySummary(baseUrl: string): Promise<InventorySummaryItem[]> {
  const response = await fetch(`${baseUrl}/api/inventory/summary`);
  if (!response.ok) throw new Error(`Inventory summary failed: ${response.status}`);
  return response.json();
}

export async function getConsumableEntries(baseUrl: string): Promise<ConsumableEntry[]> {
  const response = await fetch(`${baseUrl}/api/consumable-entries`);
  if (!response.ok) throw new Error(`Consumable entries failed: ${response.status}`);
  return response.json();
}

export async function updateConsumableEntry(
  baseUrl: string,
  entryId: string,
  payload: UpdateConsumableEntryRequest
): Promise<ConsumableEntry> {
  const response = await fetch(`${baseUrl}/api/consumable-entries/${entryId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!response.ok) throw new Error(`Update consumable entry failed: ${response.status}`);
  return response.json();
}

export async function getExpiringConsumableEntries(baseUrl: string): Promise<ExpiringConsumableEntry[]> {
  const response = await fetch(`${baseUrl}/api/consumable-entries/expiring`);
  if (!response.ok) throw new Error(`Expiring consumable entries failed: ${response.status}`);
  return response.json();
}

export async function getReplenishmentSuggestions(baseUrl: string): Promise<ReplenishmentSuggestion[]> {
  const response = await fetch(`${baseUrl}/api/replenishment/suggestions`);
  if (!response.ok) throw new Error(`Suggestions failed: ${response.status}`);
  return response.json();
}

export async function getReplenishmentRules(baseUrl: string): Promise<ReplenishmentRule[]> {
  const response = await fetch(`${baseUrl}/api/replenishment/rules`);
  if (!response.ok) throw new Error(`Rules failed: ${response.status}`);
  return response.json();
}

export async function updateReplenishmentRule(
  baseUrl: string,
  ruleId: string,
  payload: UpdateReplenishmentRuleRequest
): Promise<ReplenishmentRule> {
  const response = await fetch(`${baseUrl}/api/replenishment/rules/${ruleId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!response.ok) throw new Error(`Update rule failed: ${response.status}`);
  return response.json();
}

export async function createShoppingListItemFromSuggestion(baseUrl: string, payload: { itemDefinitionId: string; quantity: number; unit: string; deficitAmount?: number | null; expiringSoonAmount?: number | null; suggestedPurchaseAmount?: number | null }): Promise<ShoppingListItem> {
  const response = await fetch(`${baseUrl}/api/shopping-list-items/from-suggestion`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) throw new Error(`Create shopping list item failed: ${response.status}`);
  return response.json();
}

export async function updateShoppingListItemStatus(baseUrl: string, shoppingListItemId: string, payload: { isResolved?: boolean; isPurchased?: boolean; status?: ShoppingListItem['status'] }): Promise<ShoppingListItem> {
  const response = await fetch(`${baseUrl}/api/shopping-list-items/${shoppingListItemId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) throw new Error(`Update shopping list item failed: ${response.status}`);
  return response.json();
}

export async function deleteShoppingListItem(baseUrl: string, shoppingListItemId: string): Promise<void> {
  const response = await fetch(`${baseUrl}/api/shopping-list-items/${shoppingListItemId}`, {
    method: 'DELETE'
  });

  if (!response.ok) throw new Error(`Delete shopping list item failed: ${response.status}`);
}

export async function getShoppingListItems(baseUrl: string): Promise<ShoppingListItem[]> {
  const response = await fetch(`${baseUrl}/api/shopping-list-items`);
  if (!response.ok) throw new Error(`Shopping list items failed: ${response.status}`);
  return response.json();
}
