import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import {
  addInventoryLot,
  createConsumableItem,
  getExpiringLots,
  getHealth,
  getInventorySummary,
  getReplenishmentSuggestions,
  getVersion,
  type ExpiringLot,
  type InventorySummaryItem,
  type ReplenishmentSuggestion,
  type ItemDefinition
} from '../../../packages/api-client/src/client';

const baseUrl = 'http://localhost:5199';

export function App() {
  const [health, setHealth] = useState<string>('loading');
  const [version, setVersion] = useState<string>('loading');

  const [itemName, setItemName] = useState('');
  const [minimumDesiredAmount, setMinimumDesiredAmount] = useState('');
  const [lotItemDefinitionId, setLotItemDefinitionId] = useState('');
  const [quantity, setQuantity] = useState('');
  const [unit, setUnit] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [storageSlotId, setStorageSlotId] = useState('');

  const [summary, setSummary] = useState<InventorySummaryItem[]>([]);
  const [expiringLots, setExpiringLots] = useState<ExpiringLot[]>([]);
  const [suggestions, setSuggestions] = useState<ReplenishmentSuggestion[]>([]);
  const [createdItems, setCreatedItems] = useState<ItemDefinition[]>([]);

  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [summaryData, expiringData, suggestionData] = await Promise.all([
        getInventorySummary(baseUrl),
        getExpiringLots(baseUrl),
        getReplenishmentSuggestions(baseUrl)
      ]);
      setSummary(summaryData);
      setExpiringLots(expiringData);
      setSuggestions(suggestionData);
    } catch (e) {
      setError(String(e));
    } finally {
      setIsLoading(false);
    }
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
        minimumDesiredAmount: minimumDesiredAmount ? Number(minimumDesiredAmount) : null,
        replenishmentThreshold: minimumDesiredAmount ? Number(minimumDesiredAmount) : null
      });
      setCreatedItems((current) => [created, ...current.filter((item) => item.id !== created.id)]);
      setItemName('');
      setMinimumDesiredAmount('');
      setLotItemDefinitionId(created.id);
      await loadData();
    } catch (e) {
      setError(String(e));
    }
  };

  const onAddLot = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!lotItemDefinitionId || !quantity || !unit.trim()) return;

    try {
      await addInventoryLot(baseUrl, {
        itemDefinitionId: lotItemDefinitionId,
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

  return (
    <main style={{ fontFamily: 'system-ui', padding: 16 }}>
      <h1>PHOODAB Pantry MVP</h1>
      <p>Health: {health}</p>
      <p>Version: {version}</p>

      <h2>Create Consumable Item</h2>
      <form onSubmit={onCreateItem}>
        <input placeholder="Item name" value={itemName} onChange={(e) => setItemName(e.target.value)} />
        <input
          placeholder="Minimum desired amount (optional)"
          type="number"
          step="any"
          value={minimumDesiredAmount}
          onChange={(e) => setMinimumDesiredAmount(e.target.value)}
        />
        <button type="submit">Create Item</button>
      </form>

      <h2>Add Lot</h2>
      <form onSubmit={onAddLot}>
        <select value={lotItemDefinitionId} onChange={(e) => setLotItemDefinitionId(e.target.value)}>
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
        <button type="submit">Add Lot</button>
      </form>

      <h2>Inventory Summary</h2>
      {isLoading && <p>Loading pantry data...</p>}
      {error && <p>Error: {error}</p>}
      {!isLoading && !error && summary.length === 0 && <p>No inventory yet.</p>}
      {!isLoading && !error && summary.length > 0 && (
        <ul>
          {summary.map((item) => (
            <li key={item.inventoryEntryId}>
              {item.itemName}: {item.totalQuantity} {item.unit ?? ''} ({item.lotCount} lots)
            </li>
          ))}
        </ul>
      )}

      <h2>Expiring / Expired Lots</h2>
      {!isLoading && !error && expiringLots.length === 0 && <p>No expiring lots.</p>}
      {!isLoading && !error && expiringLots.length > 0 && (
        <ul>
          {expiringLots.map((lot) => (
            <li key={lot.lotId}>
              {lot.quantity} {lot.unit} - {lot.expiryStatus}
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
