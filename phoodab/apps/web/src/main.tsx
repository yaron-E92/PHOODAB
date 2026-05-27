import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import {
  addConsumableEntry,
  createConsumableItem,
  getExpiringConsumableEntries,
  getHealth,
  getInventorySummary,
  getReplenishmentSuggestions,
  getReplenishmentRules,
  getShoppingListItems,
  getVersion,
  createShoppingListItemFromSuggestion,
  updateShoppingListItemStatus,
  updateReplenishmentRule,
  type ExpiringConsumableEntry,
  type InventorySummaryItem,
  type ReplenishmentSuggestion,
  type ReplenishmentRule,
  type ItemDefinition,
  type ShoppingListItem
} from '../../../packages/api-client/src/client';

const baseUrl = 'http://localhost:5199';

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
      const [summaryData, expiringData, suggestionData, shoppingData, rulesData] = await Promise.all([
        getInventorySummary(baseUrl),
        getExpiringConsumableEntries(baseUrl),
        getReplenishmentSuggestions(baseUrl),
        getShoppingListItems(baseUrl),
        getReplenishmentRules(baseUrl)
      ]);
      setSummary(summaryData);
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
      quantity: suggestion.requiredAmount,
      unit: suggestion.unit
    });
    await loadData();
  };

  const onMarkPurchased = async (item: ShoppingListItem) => {
    await updateShoppingListItemStatus(baseUrl, item.id, { isResolved: true, isPurchased: true });
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
      await addConsumableEntry(baseUrl, {
        itemDefinitionId: entryItemDefinitionId,
        quantity: Number(quantity),
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
              {item.itemName}: {item.totalQuantity} {item.unit ?? ''} ({item.entryCount} entries)
            </li>
          ))}
        </ul>
      )}

      <h2>Expiring / Expired Entries</h2>
      {!isLoading && !error && expiringEntries.length === 0 && <p>No expiring entries.</p>}
      {!isLoading && !error && expiringEntries.length > 0 && (
        <ul>
          {expiringEntries.map((entry) => (
            <li key={entry.entryId}>
              {entry.quantity} {entry.unit} - {entry.expiryStatus}
            </li>
          ))}
        </ul>
      )}

      <h2>Replenishment Suggestions</h2>
      {!isLoading && !error && suggestions.length === 0 && <p>No replenishment needed.</p>}
      {!isLoading && !error && suggestions.length > 0 && (
        <ul>
          {suggestions.map((suggestion) => (
            <li key={suggestion.itemDefinitionId}>
              {suggestion.itemName}: {suggestion.requiredAmount} {suggestion.unit}
              <button style={{ marginLeft: 8 }} onClick={() => onCreateFromSuggestion(suggestion)}>
                Add to Shopping List
              </button>
            </li>
          ))}
        </ul>
      )}

      <h2>Replenishment Rules</h2>
      {!isLoading && !error && rules.length === 0 && <p>No rules.</p>}
      {!isLoading && !error && rules.length > 0 && (
        <ul>
          {rules.map((rule) => (
            <li key={rule.id}>
              {summary.find((x) => x.itemDefinitionId === rule.itemDefinitionId)?.itemName ?? rule.itemDefinitionId}:{' '}
              <input type="number" step="any" value={rule.desiredAmount} onChange={(e) => setRules((cur) => cur.map((r) => (r.id === rule.id ? { ...r, desiredAmount: Number(e.target.value) } : r)))} />
              <input value={rule.desiredUnit} onChange={(e) => setRules((cur) => cur.map((r) => (r.id === rule.id ? { ...r, desiredUnit: e.target.value } : r)))} />
              <input type="number" min={0} value={rule.expiryWarningDays} onChange={(e) => setRules((cur) => cur.map((r) => (r.id === rule.id ? { ...r, expiryWarningDays: Number(e.target.value) } : r)))} />
              <label>
                Disabled
                <input type="checkbox" checked={rule.isDisabled} onChange={(e) => setRules((cur) => cur.map((r) => (r.id === rule.id ? { ...r, isDisabled: e.target.checked } : r)))} />
              </label>
              <button style={{ marginLeft: 8 }} onClick={() => onSaveRule(rule)}>Save</button>
            </li>
          ))}
        </ul>
      )}

      <h2>Shopping List</h2>
      {!isLoading && !error && shoppingListItems.length === 0 && <p>No shopping items.</p>}
      {!isLoading && !error && shoppingListItems.length > 0 && (
        <ul>
          {shoppingListItems.map((item) => (
            <li key={item.id}>
              {item.itemName}: {item.quantity} {item.unit} [{item.isPurchased ? 'Purchased' : item.isResolved ? 'Resolved' : 'Open'}]
              {!item.isPurchased && (
                <button style={{ marginLeft: 8 }} onClick={() => onMarkPurchased(item)}>
                  Mark Purchased
                </button>
              )}
            </li>
          ))}
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
