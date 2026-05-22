import type { paths } from './generated';

type HealthResponse = paths['/health']['get']['responses']['200']['content']['application/json'];
type VersionResponse = paths['/version']['get']['responses']['200']['content']['application/json'];

export type ItemDefinition = {
  id: string;
  name: string;
  kind: string;
};

export type InventoryEntry = {
  id: string;
  itemDefinitionId: string;
  storageSlotId: string | null;
};

export type InventorySummaryItem = {
  inventoryEntryId: string;
  itemDefinitionId: string;
  itemName: string;
  totalQuantity: number;
  unit: string | null;
  lotCount: number;
};

export type ExpiringLot = {
  lotId: string;
  quantity: number;
  unit: string;
  expiresOn: string | null;
  expiresInDays: number | null;
  expiryStatus: 'Unknown' | 'Expired' | 'Urgent' | 'Soon' | 'Safe';
};

export type ReplenishmentSuggestion = {
  itemDefinitionId: string;
  itemName: string;
  currentQuantity: number;
  desiredQuantity: number;
  requiredAmount: number;
  unit: string;
  lots: {
    lotId: string;
    quantity: number;
    unit: string;
    expiresOn: string | null;
    expiresInDays: number | null;
    expiryStatus: 'Unknown' | 'Expired' | 'Urgent' | 'Soon' | 'Safe';
  }[];
};

export type ShoppingListItem = {
  id: string;
  itemDefinitionId: string;
  itemName: string;
  quantity: number;
  unit: string;
  isResolved: boolean;
  isPurchased: boolean;
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
    minimumDesiredAmount?: number | null;
    replenishmentThreshold?: number | null;
  }
): Promise<ItemDefinition> {
  const response = await fetch(`${baseUrl}/api/item-definitions`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: payload.name,
      kind: 1,
      minimumDesiredAmount: payload.minimumDesiredAmount ?? null,
      replenishmentThreshold: payload.replenishmentThreshold ?? null
    })
  });

  if (!response.ok) throw new Error(`Create item failed: ${response.status}`);

  return response.json();
}

export async function createInventoryEntry(baseUrl: string, itemDefinitionId: string, storageSlotId: string | null): Promise<InventoryEntry> {
  const response = await fetch(`${baseUrl}/api/inventory-entries`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ itemDefinitionId, storageSlotId })
  });

  if (!response.ok) throw new Error(`Create entry failed: ${response.status}`);

  return response.json();
}

export async function addInventoryLot(
  baseUrl: string,
  payload: {
    itemDefinitionId: string;
    quantity: number;
    unit: string;
    expiresOn: string | null;
    storageSlotId: string | null;
  }
): Promise<void> {
  const entry = await createInventoryEntry(baseUrl, payload.itemDefinitionId, payload.storageSlotId);

  const response = await fetch(`${baseUrl}/api/inventory-lots`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      inventoryEntryId: entry.id,
      quantity: payload.quantity,
      unit: payload.unit,
      expiresOn: payload.expiresOn,
      storageSlotId: payload.storageSlotId
    })
  });

  if (!response.ok) throw new Error(`Add lot failed: ${response.status}`);
}

export async function getInventorySummary(baseUrl: string): Promise<InventorySummaryItem[]> {
  const response = await fetch(`${baseUrl}/api/inventory/summary`);
  if (!response.ok) throw new Error(`Inventory summary failed: ${response.status}`);
  return response.json();
}

export async function getExpiringLots(baseUrl: string): Promise<ExpiringLot[]> {
  const response = await fetch(`${baseUrl}/api/inventory/expiring`);
  if (!response.ok) throw new Error(`Expiring lots failed: ${response.status}`);
  return response.json();
}

export async function getReplenishmentSuggestions(baseUrl: string): Promise<ReplenishmentSuggestion[]> {
  const response = await fetch(`${baseUrl}/api/replenishment/suggestions`);
  if (!response.ok) throw new Error(`Suggestions failed: ${response.status}`);
  return response.json();
}

export async function createShoppingListItemFromSuggestion(baseUrl: string, payload: { itemDefinitionId: string; quantity: number; unit: string }): Promise<ShoppingListItem> {
  const response = await fetch(`${baseUrl}/api/shopping-list-items/from-suggestion`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) throw new Error(`Create shopping list item failed: ${response.status}`);
  return response.json();
}

export async function updateShoppingListItemStatus(baseUrl: string, shoppingListItemId: string, payload: { isResolved?: boolean; isPurchased?: boolean }): Promise<ShoppingListItem> {
  const response = await fetch(`${baseUrl}/api/shopping-list-items/${shoppingListItemId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) throw new Error(`Update shopping list item failed: ${response.status}`);
  return response.json();
}

export async function getShoppingListItems(baseUrl: string): Promise<ShoppingListItem[]> {
  const response = await fetch(`${baseUrl}/api/shopping-list-items`);
  if (!response.ok) throw new Error(`Shopping list items failed: ${response.status}`);
  return response.json();
}
