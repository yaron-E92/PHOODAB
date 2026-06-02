import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import {
  addConsumableEntry,
  createConsumableItem,
  getConsumableEntries,
  getExpiringConsumableEntries,
  getHealth,
  getInventorySummary,
  getReplenishmentSuggestions,
  getReplenishmentRules,
  getShoppingListItems,
  getVersion,
  createShoppingListItemFromSuggestion,
  updateShoppingListItemStatus,
  deleteShoppingListItem,
  updateConsumableEntry,
  type ConsumableEntry,
  updateReplenishmentRule,
  type ExpiringConsumableEntry,
  type InventorySummaryItem,
  type ReplenishmentSuggestion,
  type ReplenishmentRule,
  type ItemDefinition,
  type ShoppingListItem
} from '../../../packages/api-client/src/client';

const baseUrl = 'http://localhost:5199';

type EntryEdit = { quantity: string; unit: string; expiresOn: string; storageSlotId: string };
type EntryActionEdit = { addStock: string; addStockUnit: string; consumeStock: string; consumeStockUnit: string };

const toEntryEdit = (entry: ConsumableEntry): EntryEdit => ({
  quantity: String(entry.quantity),
  unit: entry.unit,
  expiresOn: entry.expiresOn ?? '',
  storageSlotId: entry.storageSlotId ?? ''
});

const toEntryActionEdit = (entry: ConsumableEntry): EntryActionEdit => ({
  addStock: '',
  addStockUnit: entry.unit,
  consumeStock: '',
  consumeStockUnit: entry.unit
});

const parseQuantity = (value: string) => {
  const quantity = Number(value);
  return Number.isFinite(quantity) ? quantity : null;
};

const getExpiryStyle = (expiryStatus: ConsumableEntry['expiryStatus']): React.CSSProperties => {
  if (expiryStatus === 'Expired') {
    return { background: '#fee2e2', borderLeft: '4px solid #dc2626' };
  }
  if (expiryStatus === 'Urgent') {
    return { background: '#ffedd5', borderLeft: '4px solid #ea580c' };
  }
  if (expiryStatus === 'Soon') {
    return { background: '#fef9c3', borderLeft: '4px solid #ca8a04' };
  }
  return { borderLeft: '4px solid #16a34a' };
};

const getShoppingStatus = (item: ShoppingListItem): ShoppingListItem['status'] => {
  if (item.status) return item.status;
  if (item.isPurchased) return item.isResolved ? 'Bought' : 'StockUpdateNeeded';
  return 'ShoppingList';
};

const getShoppingStatusLabel = (item: ShoppingListItem) => {
  switch (getShoppingStatus(item)) {
    case 'InCart':
      return 'In cart / buying';
    case 'Bought':
      return 'Bought';
    case 'StockUpdateNeeded':
      return 'Stock update needed';
    default:
      return 'Added to shopping list';
  }
};

export function App() {
  const [health, setHealth] = useState<string>('loading');
  const [version, setVersion] = useState<string>('loading');

  const [itemName, setItemName] = useState('');
  const [desiredAmount, setDesiredAmount] = useState('');
  const [desiredUnit, setDesiredUnit] = useState('');
  const [entryItemDefinitionId, setEntryItemDefinitionId] = useState('');
  const [quantity, setQuantity] = useState('');
  const [unit, setUnit] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [storageSlotId, setStorageSlotId] = useState('');

  const [summary, setSummary] = useState<InventorySummaryItem[]>([]);
  const [consumableEntries, setConsumableEntries] = useState<ConsumableEntry[]>([]);
  const [entryEdits, setEntryEdits] = useState<Record<string, EntryEdit>>({});
  const [entryActionEdits, setEntryActionEdits] = useState<Record<string, EntryActionEdit>>({});
  const [entryErrors, setEntryErrors] = useState<Record<string, string>>({});
  const [expiringEntries, setExpiringEntries] = useState<ExpiringConsumableEntry[]>([]);
  const [suggestions, setSuggestions] = useState<ReplenishmentSuggestion[]>([]);
  const [createdItems, setCreatedItems] = useState<ItemDefinition[]>([]);
  const [shoppingListItems, setShoppingListItems] = useState<ShoppingListItem[]>([]);
  const [rules, setRules] = useState<ReplenishmentRule[]>([]);

  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [summaryData, entriesData, expiringData, suggestionData, shoppingData, rulesData] = await Promise.all([
        getInventorySummary(baseUrl),
        getConsumableEntries(baseUrl),
        getExpiringConsumableEntries(baseUrl),
        getReplenishmentSuggestions(baseUrl),
        getShoppingListItems(baseUrl),
        getReplenishmentRules(baseUrl)
      ]);
      setSummary(summaryData);
      setConsumableEntries(entriesData);
      setEntryEdits(Object.fromEntries(entriesData.map((entry) => [entry.entryId, toEntryEdit(entry)])));
      setEntryActionEdits(Object.fromEntries(entriesData.map((entry) => [entry.entryId, toEntryActionEdit(entry)])));
      setEntryErrors({});
      setExpiringEntries(expiringData);
      setSuggestions(suggestionData);
      setShoppingListItems(shoppingData);
      setRules(rulesData);
    } catch (e) {
      setError(String(e));
    } finally {
      setIsLoading(false);
    }
  };

  const onCreateFromSuggestion = async (suggestion: ReplenishmentSuggestion) => {
    await createShoppingListItemFromSuggestion(baseUrl, {
      itemDefinitionId: suggestion.itemDefinitionId,
      quantity: suggestion.suggestedPurchaseAmount,
      unit: suggestion.unit,
      deficitAmount: suggestion.deficitAmount,
      expiringSoonAmount: suggestion.expiringSoonAmount,
      suggestedPurchaseAmount: suggestion.suggestedPurchaseAmount
    });
    await loadData();
  };

  const onMoveToCart = async (item: ShoppingListItem) => {
    await updateShoppingListItemStatus(baseUrl, item.id, { status: 'InCart' });
    await loadData();
  };

  const onMarkBought = async (item: ShoppingListItem) => {
    await updateShoppingListItemStatus(baseUrl, item.id, { status: 'Bought' });
    await loadData();
  };

  const onDeleteShoppingItem = async (item: ShoppingListItem) => {
    await deleteShoppingListItem(baseUrl, item.id);
    await loadData();
  };

  useEffect(() => {
    getHealth(baseUrl).then((r) => setHealth(r.status)).catch((e) => setHealth(String(e)));
    getVersion(baseUrl).then((r) => setVersion(r.version)).catch((e) => setVersion(String(e)));
    loadData();
  }, []);

  const onCreateItem = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!itemName.trim()) return;

    try {
      const created = await createConsumableItem(baseUrl, {
        name: itemName.trim(),
        desiredAmount: desiredAmount ? Number(desiredAmount) : null,
        desiredUnit: desiredUnit.trim() || null
      });
      setCreatedItems((current) => [created, ...current.filter((item) => item.id !== created.id)]);
      setItemName('');
      setDesiredAmount('');
      setDesiredUnit('');
      setEntryItemDefinitionId(created.id);
      await loadData();
    } catch (e) {
      setError(String(e));
    }
  };

  const onAddConsumableEntry = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!entryItemDefinitionId || !quantity || !unit.trim()) return;

    try {
      const parsedQuantity = parseQuantity(quantity);
      if (parsedQuantity === null || parsedQuantity < 0) {
        setError('Quantity must be zero or greater.');
        return;
      }

      await addConsumableEntry(baseUrl, {
        itemDefinitionId: entryItemDefinitionId,
        quantity: parsedQuantity,
        unit: unit.trim(),
        expiresOn: expiryDate || null,
        storageSlotId: storageSlotId || null
      });
      setQuantity('');
      setUnit('');
      setExpiryDate('');
      setStorageSlotId('');
      await loadData();
    } catch (e) {
      setError(String(e));
    }
  };

  const onSaveRule = async (rule: ReplenishmentRule) => {
    await updateReplenishmentRule(baseUrl, rule.id, {
      desiredAmount: rule.desiredAmount,
      desiredUnit: rule.desiredUnit,
      expiryWarningDays: rule.expiryWarningDays,
      isDisabled: rule.isDisabled
    });
    await loadData();
  };

  const setEntryEdit = (entryId: string, patch: Partial<EntryEdit>) => {
    setEntryEdits((current) => ({
      ...current,
      [entryId]: {
        ...current[entryId],
        ...patch
      }
    }));
  };

  const setEntryActionEdit = (entryId: string, patch: Partial<EntryActionEdit>) => {
    setEntryActionEdits((current) => ({
      ...current,
      [entryId]: {
        ...(current[entryId] ?? { addStock: '', addStockUnit: '', consumeStock: '', consumeStockUnit: '' }),
        ...patch
      }
    }));
  };

  const clearEntryError = (entryId: string) => {
    setEntryErrors((current) => {
      const rest = { ...current };
      delete rest[entryId];
      return rest;
    });
  };

  const setEntryError = (entryId: string, message: string) => {
    setEntryErrors((current) => ({ ...current, [entryId]: message }));
  };

  const updateEntryQuantity = async (entry: ConsumableEntry, nextQuantity: number) => {
    await updateConsumableEntry(baseUrl, entry.entryId, {
      quantity: nextQuantity,
      unit: entry.unit,
      expiresOn: entry.expiresOn,
      storageSlotId: entry.storageSlotId
    });
    await loadData();
  };

  const onSaveConsumableEntry = async (entry: ConsumableEntry) => {
    const edit = entryEdits[entry.entryId];
    if (!edit || !edit.quantity || !edit.unit.trim()) {
      setEntryError(entry.entryId, 'Quantity and unit are required.');
      return;
    }

    const parsedQuantity = parseQuantity(edit.quantity);
    if (parsedQuantity === null || parsedQuantity < 0) {
      setEntryError(entry.entryId, 'Quantity must be zero or greater.');
      return;
    }

    clearEntryError(entry.entryId);

    await updateConsumableEntry(baseUrl, entry.entryId, {
      quantity: parsedQuantity,
      unit: edit.unit.trim(),
      expiresOn: edit.expiresOn || null,
      storageSlotId: edit.storageSlotId || null
    });
    await loadData();
  };

  const onAddStock = async (entry: ConsumableEntry) => {
    const actionEdit = entryActionEdits[entry.entryId] ?? toEntryActionEdit(entry);
    const amount = parseQuantity(actionEdit.addStock);
    if (!actionEdit.addStockUnit.trim() || actionEdit.addStockUnit.trim().toLowerCase() !== entry.unit.toLowerCase()) {
      setEntryError(entry.entryId, `Add stock unit must match the lot unit ${entry.unit}.`);
      return;
    }
    if (amount === null || amount <= 0) {
      setEntryError(entry.entryId, 'Add stock amount must be greater than zero.');
      return;
    }

    clearEntryError(entry.entryId);
    await updateEntryQuantity(entry, entry.quantity + amount);
  };

  const onConsumeStock = async (entry: ConsumableEntry) => {
    const actionEdit = entryActionEdits[entry.entryId] ?? toEntryActionEdit(entry);
    const amount = parseQuantity(actionEdit.consumeStock);
    if (!actionEdit.consumeStockUnit.trim() || actionEdit.consumeStockUnit.trim().toLowerCase() !== entry.unit.toLowerCase()) {
      setEntryError(entry.entryId, `Consume stock unit must match the lot unit ${entry.unit}.`);
      return;
    }
    if (amount === null || amount <= 0) {
      setEntryError(entry.entryId, 'Consume amount must be greater than zero.');
      return;
    }
    if (amount > entry.quantity) {
      setEntryError(entry.entryId, 'Consume amount cannot exceed the lot quantity.');
      return;
    }

    clearEntryError(entry.entryId);
    await updateEntryQuantity(entry, entry.quantity - amount);
  };

  const onMarkLotDepleted = async (entry: ConsumableEntry) => {
    if (!window.confirm(`Mark ${entry.itemName} lot ${entry.entryId} as depleted?`)) return;

    clearEntryError(entry.entryId);
    await updateEntryQuantity(entry, 0);
  };

  const onResetEntryEdit = (entry: ConsumableEntry) => {
    setEntryEdit(entry.entryId, toEntryEdit(entry));
    setEntryActionEdit(entry.entryId, toEntryActionEdit(entry));
    clearEntryError(entry.entryId);
  };

  return (
    <main style={{ fontFamily: 'system-ui', padding: 16 }}>
      <h1>PHOODAB Pantry MVP</h1>
      <p>Health: {health}</p>
      <p>Version: {version}</p>

      <h2>Create Consumable Item</h2>
      <form onSubmit={onCreateItem}>
        <input placeholder="Item name" value={itemName} onChange={(e) => setItemName(e.target.value)} />
        <input
          placeholder="Desired amount (optional)"
          type="number"
          step="any"
          value={desiredAmount}
          onChange={(e) => setDesiredAmount(e.target.value)}
        />
        <input placeholder="Desired unit (optional)" value={desiredUnit} onChange={(e) => setDesiredUnit(e.target.value)} />
        <button type="submit">Create Item</button>
      </form>

      <h2>Add Consumable Entry</h2>
      <form onSubmit={onAddConsumableEntry}>
        <select value={entryItemDefinitionId} onChange={(e) => setEntryItemDefinitionId(e.target.value)}>
          <option value="">Select item</option>
          {createdItems.map((item) => (
            <option key={item.id} value={item.id}>
              {item.name}
            </option>
          ))}
          {summary
            .filter((item) => !createdItems.some((createdItem) => createdItem.id === item.itemDefinitionId))
            .map((item) => (
              <option key={item.itemDefinitionId} value={item.itemDefinitionId}>
                {item.itemName}
              </option>
            ))}
        </select>
        <input placeholder="Quantity" type="number" step="any" value={quantity} onChange={(e) => setQuantity(e.target.value)} />
        <input placeholder="Unit" value={unit} onChange={(e) => setUnit(e.target.value)} />
        <input type="date" value={expiryDate} onChange={(e) => setExpiryDate(e.target.value)} />
        <input placeholder="Storage slot ID (optional)" value={storageSlotId} onChange={(e) => setStorageSlotId(e.target.value)} />
        <button type="submit">Add Entry</button>
      </form>

      <h2>Inventory Summary</h2>
      {isLoading && <p>Loading pantry data...</p>}
      {error && <p>Error: {error}</p>}
      {!isLoading && !error && summary.length === 0 && <p>No inventory yet.</p>}
      {!isLoading && !error && summary.length > 0 && (
        <ul>
          {summary.map((item) => (
            <li key={item.itemDefinitionId}>
              {item.hasMixedUnits
                ? `${item.itemName}: mixed units (${item.entryCount} entries)`
                : `${item.itemName}: ${item.totalQuantity} ${item.unit ?? ''} (${item.entryCount} entries)`}
              {item.mixedUnitWarning && <strong style={{ color: '#9a3412', marginLeft: 8 }}>{item.mixedUnitWarning}</strong>}
            </li>
          ))}
        </ul>
      )}

      <h2>Consumable Entry Audit</h2>
      {!isLoading && !error && consumableEntries.length === 0 && <p>No consumable entries.</p>}
      {!isLoading && !error && consumableEntries.length > 0 && (
        <ul>
          {consumableEntries.map((entry) => {
            const edit = entryEdits[entry.entryId] ?? toEntryEdit(entry);
            const actionEdit = entryActionEdits[entry.entryId] ?? toEntryActionEdit(entry);
            const expiryStyle = getExpiryStyle(entry.expiryStatus);
            const lotLocation = entry.storageSlotId || 'No location set';
            return (
              <li
                key={entry.entryId}
                style={{
                  ...expiryStyle,
                  marginBottom: 8,
                  padding: 8
                }}
              >
                <strong>{entry.itemName}</strong>
                <div>Lot {entry.entryId}</div>
                <div>
                  Quantity: {entry.quantity} {entry.unit} | Expiry: {entry.expiresOn ?? 'No expiry'} ({entry.expiryStatus}) | Location: {lotLocation}
                </div>
                {entryErrors[entry.entryId] && <p role="alert">{entryErrors[entry.entryId]}</p>}
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginTop: 8 }}>
                  <label>
                    Add stock
                    <input
                      aria-label={`Add stock amount for ${entry.itemName} lot ${entry.entryId}`}
                      type="number"
                      step="any"
                      min={0}
                      value={actionEdit.addStock}
                      onChange={(e) => setEntryActionEdit(entry.entryId, { addStock: e.target.value })}
                    />
                  </label>
                  <label>
                    Add stock unit
                    <input
                      aria-label={`Add stock unit for ${entry.itemName} lot ${entry.entryId}`}
                      value={actionEdit.addStockUnit}
                      onChange={(e) => setEntryActionEdit(entry.entryId, { addStockUnit: e.target.value })}
                    />
                  </label>
                  <button onClick={() => onAddStock(entry)}>Add Stock</button>
                  <label>
                    Consume stock
                    <input
                      aria-label={`Consume stock amount for ${entry.itemName} lot ${entry.entryId}`}
                      type="number"
                      step="any"
                      min={0}
                      value={actionEdit.consumeStock}
                      onChange={(e) => setEntryActionEdit(entry.entryId, { consumeStock: e.target.value })}
                    />
                  </label>
                  <label>
                    Consume stock unit
                    <input
                      aria-label={`Consume stock unit for ${entry.itemName} lot ${entry.entryId}`}
                      value={actionEdit.consumeStockUnit}
                      onChange={(e) => setEntryActionEdit(entry.entryId, { consumeStockUnit: e.target.value })}
                    />
                  </label>
                  <button onClick={() => onConsumeStock(entry)}>Consume Stock</button>
                  <button onClick={() => onMarkLotDepleted(entry)}>Mark Lot Depleted</button>
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginTop: 8 }}>
                  <input
                    aria-label={`Entry quantity for ${entry.itemName}`}
                    type="number"
                    step="any"
                    min={0}
                    value={edit.quantity}
                    onChange={(e) => setEntryEdit(entry.entryId, { quantity: e.target.value })}
                  />
                  <input
                    aria-label={`Entry unit for ${entry.itemName}`}
                    value={edit.unit}
                    onChange={(e) => setEntryEdit(entry.entryId, { unit: e.target.value })}
                  />
                  <input
                    aria-label={`Entry expiry for ${entry.itemName}`}
                    type="date"
                    value={edit.expiresOn}
                    onChange={(e) => setEntryEdit(entry.entryId, { expiresOn: e.target.value })}
                  />
                  <input
                    aria-label={`Entry storage slot for ${entry.itemName}`}
                    placeholder="Storage slot ID (optional)"
                    value={edit.storageSlotId}
                    onChange={(e) => setEntryEdit(entry.entryId, { storageSlotId: e.target.value })}
                  />
                  <button onClick={() => onSaveConsumableEntry(entry)}>Adjust Quantity / Expiry / Location</button>
                  <button onClick={() => onResetEntryEdit(entry)}>Undo Unsaved Changes</button>
                </div>
              </li>
            );
          })}
        </ul>
      )}

      <h2>Expiring / Expired Entries</h2>
      {!isLoading && !error && expiringEntries.length === 0 && <p>No expiring entries.</p>}
      {!isLoading && !error && expiringEntries.length > 0 && (
        <ul>
          {expiringEntries.map((entry) => (
            <li key={entry.entryId}>
              {entry.itemName} - {entry.quantity} {entry.unit} - {entry.expiryStatus}
            </li>
          ))}
        </ul>
      )}

      <h2>Replenishment Suggestions</h2>
      {!isLoading && !error && suggestions.length === 0 && <p>No replenishment needed.</p>}
      {!isLoading && !error && suggestions.length > 0 && (
        <ul>
          {suggestions.map((suggestion) => {
            const breakdown = `Breakdown: current ${suggestion.usableCurrentQuantity} ${suggestion.unit}; required ${suggestion.requiredAmount} ${suggestion.unit}; suggested ${suggestion.suggestedPurchaseAmount} ${suggestion.unit}; rule source replenishment target; desired ${suggestion.desiredQuantity} ${suggestion.unit}; usable ${suggestion.usableCurrentQuantity} ${suggestion.unit}; deficit ${suggestion.deficitAmount} ${suggestion.unit}; expiring soon ${suggestion.expiringSoonAmount} ${suggestion.unit}`;
            const activeShoppingItem = shoppingListItems.find((item) => item.itemDefinitionId === suggestion.itemDefinitionId && !item.isPurchased);
            return (
              <li key={suggestion.itemDefinitionId}>
                {suggestion.itemName}: {suggestion.suggestedPurchaseAmount} {suggestion.unit}
                {suggestion.expiringSoonAmount > 0 && <> ({suggestion.expiringSoonAmount} about to expire)</>}
                <span
                  aria-label={breakdown}
                  tabIndex={0}
                  title={breakdown}
                  style={{ marginLeft: 8, textDecoration: 'underline dotted', cursor: 'help' }}
                >
                  Breakdown
                </span>
                {activeShoppingItem ? (
                  <span style={{ marginLeft: 8 }}>[{getShoppingStatusLabel(activeShoppingItem)}]</span>
                ) : (
                  <button style={{ marginLeft: 8 }} onClick={() => onCreateFromSuggestion(suggestion)}>
                    Add to Shopping List
                  </button>
                )}
              </li>
            );
          })}
        </ul>
      )}

      <h2>Replenishment Rules</h2>
      {!isLoading && !error && rules.length === 0 && <p>No rules.</p>}
      {!isLoading && !error && rules.length > 0 && (
        <ul>
          {rules.map((rule) => {
            const itemName = summary.find((x) => x.itemDefinitionId === rule.itemDefinitionId)?.itemName ?? rule.itemDefinitionId;
            return (
              <li key={rule.id} style={{ marginBottom: 12 }}>
                <strong>{itemName}</strong>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'end', marginTop: 4 }}>
                  <label>
                    Target amount
                    <input
                      aria-label={`Target amount for ${itemName}`}
                      aria-description="Amount to keep stocked before replenishment is suggested."
                      title="Amount to keep stocked before replenishment is suggested."
                      type="number"
                      step="any"
                      value={rule.desiredAmount}
                      onChange={(e) => setRules((cur) => cur.map((r) => (r.id === rule.id ? { ...r, desiredAmount: Number(e.target.value) } : r)))}
                    />
                  </label>
                  <label>
                    Target unit
                    <input
                      aria-label={`Target unit for ${itemName}`}
                      value={rule.desiredUnit}
                      onChange={(e) => setRules((cur) => cur.map((r) => (r.id === rule.id ? { ...r, desiredUnit: e.target.value } : r)))}
                    />
                  </label>
                  <label>
                    Expiry warning days
                    <input
                      aria-label={`Expiry warning days for ${itemName}`}
                      aria-description="Treat entries expiring within this many days as warning items."
                      title="Treat entries expiring within this many days as warning items."
                      type="number"
                      min={0}
                      value={rule.expiryWarningDays}
                      onChange={(e) => setRules((cur) => cur.map((r) => (r.id === rule.id ? { ...r, expiryWarningDays: Number(e.target.value) } : r)))}
                    />
                  </label>
                  <label>
                    Disabled
                    <input
                      aria-label={`Disable replenishment rule for ${itemName}`}
                      type="checkbox"
                      checked={rule.isDisabled}
                      onChange={(e) => setRules((cur) => cur.map((r) => (r.id === rule.id ? { ...r, isDisabled: e.target.checked } : r)))}
                    />
                  </label>
                  <button aria-label={`Save replenishment rule for ${itemName}`} onClick={() => onSaveRule(rule)}>Save</button>
                </div>
              </li>
            );
          })}
        </ul>
      )}

      <h2>Shopping List</h2>
      {!isLoading && !error && shoppingListItems.length === 0 && <p>No shopping items.</p>}
      {!isLoading && !error && shoppingListItems.length > 0 && (
        <ul>
          {shoppingListItems.map((item) => {
            const status = getShoppingStatus(item);
            const stockAction = item.nextInventoryAction ?? (item.stockUpdateNeeded ? 'Add stock details for quantity, lot, expiry, and location.' : null);
            return (
              <li key={item.id}>
                {item.itemName}: {item.quantity} {item.unit} [{getShoppingStatusLabel(item)}]
                {status === 'ShoppingList' && (
                  <button style={{ marginLeft: 8 }} onClick={() => onMoveToCart(item)}>
                    Add to Cart
                  </button>
                )}
                {status === 'InCart' && (
                  <button style={{ marginLeft: 8 }} onClick={() => onMarkBought(item)}>
                    Mark Bought
                  </button>
                )}
                {(status === 'Bought' || status === 'StockUpdateNeeded') && (
                  <button style={{ marginLeft: 8 }} onClick={() => onDeleteShoppingItem(item)}>
                    Remove Bought Item
                  </button>
                )}
                {(item.stockUpdateNeeded || status === 'StockUpdateNeeded') && stockAction && (
                  <div>Stock update needed: {stockAction}</div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </main>
  );
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
