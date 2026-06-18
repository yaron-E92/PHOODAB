import React, { useEffect, useRef, useState } from 'react';
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
  searchPhoodab,
  getVersion,
  createShoppingListItemFromSuggestion,
  createDurableItem,
  updateShoppingListItemStatus,
  deleteShoppingListItem,
  getDurableItem,
  getDurableItems,
  getLocationDetail,
  getLocationTree,
  createLocation,
  updateLocation,
  retireDurableItem,
  updateConsumableEntry,
  updateDurableItem,
  type ConsumableEntry,
  updateReplenishmentRule,
  type DurableItem,
  type DurableItemStatus,
  type ExpiringConsumableEntry,
  type InventorySummaryItem,
  type ReplenishmentSuggestion,
  type ReplenishmentRule,
  type ItemDefinition,
  type ShoppingListItem,
  type GlobalSearchResult,
  type Location,
  type LocationDetail,
  type LocationTreeNode,
  type LocationType
} from '../../../packages/api-client/src/client';

const baseUrl = 'http://localhost:5199';

type EntryEdit = { quantity: string; unit: string; expiresOn: string; storageSlotId: string };
type EntryActionEdit = { addStock: string; addStockUnit: string; consumeStock: string; consumeStockUnit: string };
type ItemRoute =
  | { kind: 'inventory' }
  | { kind: 'consumable'; itemDefinitionId: string }
  | { kind: 'durable'; entryId: string }
  | { kind: 'locations' }
  | { kind: 'location'; locationId: string };
type DurableItemForm = {
  displayName: string;
  description: string;
  itemType: string;
  brandManufacturer: string;
  model: string;
  serialNumber: string;
  purchaseDate: string;
  purchaseValue: string;
  warrantyEndsOn: string;
  status: DurableItemStatus;
  currentLocation: string;
  notes: string;
  storageSlotId: string;
};

const durableStatuses: DurableItemStatus[] = ['Active', 'NeedsRepair', 'LoanedOut', 'Stored', 'Retired', 'Lost'];

const getItemRoute = (pathname: string): ItemRoute => {
  if (/^\/locations\/?$/.test(pathname)) return { kind: 'locations' };
  const locationMatch = pathname.match(/^\/locations\/([^/]+)\/?$/);
  if (locationMatch) return { kind: 'location', locationId: decodeURIComponent(locationMatch[1]) };

  const match = pathname.match(/^\/items\/(consumable|durable)\/([^/]+)\/?$/);
  if (!match) return { kind: 'inventory' };

  return match[1] === 'consumable'
    ? { kind: 'consumable', itemDefinitionId: decodeURIComponent(match[2]) }
    : { kind: 'durable', entryId: decodeURIComponent(match[2]) };
};

const flattenLocationTree = (nodes: LocationTreeNode[]): Location[] =>
  nodes.flatMap((node) => [node.location, ...flattenLocationTree(node.children)]);

const findLocationAncestorIds = (nodes: LocationTreeNode[], targetId: string, ancestors: string[] = []): string[] | null => {
  for (const node of nodes) {
    if (node.location.id === targetId) return ancestors;
    const childMatch = findLocationAncestorIds(node.children, targetId, [...ancestors, node.location.id]);
    if (childMatch) return childMatch;
  }

  return null;
};

const findLocationPath = (nodes: LocationTreeNode[], targetId: string, path: Location[] = []): Location[] | null => {
  for (const node of nodes) {
    const nextPath = [...path, node.location];
    if (node.location.id === targetId) return nextPath;
    const childMatch = findLocationPath(node.children, targetId, nextPath);
    if (childMatch) return childMatch;
  }

  return null;
};

const formatLocationPath = (nodes: LocationTreeNode[], id: string | null | undefined) => {
  if (!id) return 'No location set';
  return findLocationPath(nodes, id)?.map((location) => location.name).join(' › ') ?? id;
};

const LocationName = ({ nodes, locations, id }: { nodes: LocationTreeNode[]; locations: Location[]; id: string | null | undefined }) => {
  if (!id) return <>No location set</>;
  const locationPath = findLocationPath(nodes, id);
  return <>{locationPath?.map((location) => location.name).join(' › ') ?? locations.find((location) => location.id === id)?.name ?? id}</>;
};

const LocationTreeList = ({
  nodes,
  onOpen,
  onEdit
}: {
  nodes: LocationTreeNode[];
  onOpen: (locationId: string) => void;
  onEdit: (location: Location) => void;
}) => (
  <ul>
    {nodes.map((node) => (
      <li key={node.location.id}>
        <button onClick={() => onOpen(node.location.id)} aria-label={`Open location ${node.location.name}`}>
          {node.location.name} ({node.location.type})
        </button>
        <button type="button" onClick={() => onEdit(node.location)} aria-label={`Edit location ${node.location.name}`}>
          Edit
        </button>
        {node.children.length > 0 && <LocationTreeList nodes={node.children} onOpen={onOpen} onEdit={onEdit} />}
      </li>
    ))}
  </ul>
);

const StorageSlotTreePicker = ({
  label,
  nodes,
  selectedId,
  onSelect
}: {
  label: string;
  nodes: LocationTreeNode[];
  selectedId: string;
  onSelect: (storageSlotId: string) => void;
}) => {
  const [expandedIds, setExpandedIds] = useState<Set<string>>(() => new Set(selectedId ? findLocationAncestorIds(nodes, selectedId) ?? [] : []));

  useEffect(() => {
    if (!selectedId) return;
    const ancestorIds = findLocationAncestorIds(nodes, selectedId);
    if (!ancestorIds) return;
    setExpandedIds((current) => new Set([...current, ...ancestorIds]));
  }, [nodes, selectedId]);

  const toggle = (locationId: string) => {
    setExpandedIds((current) => {
      const next = new Set(current);
      if (next.has(locationId)) {
        next.delete(locationId);
      } else {
        next.add(locationId);
      }
      return next;
    });
  };

  const renderNodes = (treeNodes: LocationTreeNode[], depth = 0): React.ReactNode => (
    <ul style={{ listStyle: 'none', margin: 0, paddingLeft: depth === 0 ? 0 : 16 }}>
      {treeNodes.map((node) => {
        const isExpanded = expandedIds.has(node.location.id);
        const isStorageSlot = node.location.type === 'StorageSlot';
        return (
          <li key={node.location.id} style={{ marginTop: 4 }}>
            {isStorageSlot ? (
              <button
                type="button"
                aria-label={`Select storage slot ${node.location.name}`}
                aria-pressed={selectedId === node.location.id}
                onClick={() => onSelect(node.location.id)}
              >
                {selectedId === node.location.id ? 'Selected: ' : ''}{node.location.name}
              </button>
            ) : (
              <button
                type="button"
                aria-label={`Toggle location ${node.location.name}`}
                aria-expanded={isExpanded}
                onClick={() => toggle(node.location.id)}
              >
                {isExpanded ? '[-]' : '[+]'} {node.location.name} ({node.location.type})
              </button>
            )}
            {!isStorageSlot && isExpanded && renderNodes(node.children, depth + 1)}
          </li>
        );
      })}
    </ul>
  );

  const selectedLocationText = formatLocationPath(nodes, selectedId);

  return (
    <fieldset style={{ border: '1px solid #ddd', padding: 8 }}>
      <legend>{label}</legend>
      <div>Selected: {selectedId ? selectedLocationText : 'No storage slot selected'}</div>
      <button type="button" onClick={() => onSelect('')}>No storage slot selected</button>
      {nodes.length === 0 ? <p>No storage slots yet.</p> : renderNodes(nodes)}
    </fieldset>
  );
};

const emptyDurableItemForm: DurableItemForm = {
  displayName: '',
  description: '',
  itemType: '',
  brandManufacturer: '',
  model: '',
  serialNumber: '',
  purchaseDate: '',
  purchaseValue: '',
  warrantyEndsOn: '',
  status: 'Active',
  currentLocation: '',
  notes: '',
  storageSlotId: ''
};

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

const toNullableString = (value: string) => {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
};

const getDurableDisplayName = (item: DurableItem) => item.displayName?.trim() || 'Unnamed durable item';

const getWarrantyIndicator = (item: DurableItem) => {
  if (!item.warrantyEndsOn) return 'No warranty recorded';
  return `Warranty through ${item.warrantyEndsOn}`;
};

const toDurableItemForm = (item: DurableItem): DurableItemForm => ({
  displayName: item.displayName ?? '',
  description: item.description ?? '',
  itemType: item.itemType ?? '',
  brandManufacturer: item.brandManufacturer ?? '',
  model: item.model ?? '',
  serialNumber: item.serialNumber ?? '',
  purchaseDate: item.purchaseDate ?? '',
  purchaseValue: item.purchaseValue == null ? '' : String(item.purchaseValue),
  warrantyEndsOn: item.warrantyEndsOn ?? '',
  status: item.status,
  currentLocation: item.currentLocation ?? '',
  notes: item.notes ?? '',
  storageSlotId: item.storageSlotId ?? ''
});

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
  const [route, setRoute] = useState<ItemRoute>(() => getItemRoute(window.location.pathname));

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
  const [durableItems, setDurableItems] = useState<DurableItem[]>([]);
  const [durableForm, setDurableForm] = useState<DurableItemForm>(emptyDurableItemForm);
  const [editingDurableItemId, setEditingDurableItemId] = useState<string | null>(null);
  const [selectedDurableItem, setSelectedDurableItem] = useState<DurableItem | null>(null);
  const [durableDetailLoading, setDurableDetailLoading] = useState(false);
  const [locationTree, setLocationTree] = useState<LocationTreeNode[]>([]);
  const [selectedLocation, setSelectedLocation] = useState<LocationDetail | null>(null);
  const [locationDetailLoading, setLocationDetailLoading] = useState(false);
  const [locationName, setLocationName] = useState('');
  const [locationType, setLocationType] = useState<LocationType>('House');
  const [locationParentId, setLocationParentId] = useState('');
  const [locationDescription, setLocationDescription] = useState('');
  const [locationSortOrder, setLocationSortOrder] = useState('');
  const [editingLocationId, setEditingLocationId] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<GlobalSearchResult[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);

  const [itemTypeFilter, setItemTypeFilter] = useState<'All' | 'Consumable' | 'Durable'>('All');
  const [locationFilter, setLocationFilter] = useState('All');
  const [durableStatusFilter, setDurableStatusFilter] = useState<'All' | DurableItemStatus>('All');
  const [expiryStateFilter, setExpiryStateFilter] = useState<'All' | ConsumableEntry['expiryStatus']>('All');
  const [shoppingStateFilter, setShoppingStateFilter] = useState<'All' | ShoppingListItem['status']>('All');

  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [summaryData, entriesData, expiringData, suggestionData, shoppingData, rulesData, durableData, locationTreeData] = await Promise.all([
        getInventorySummary(baseUrl),
        getConsumableEntries(baseUrl),
        getExpiringConsumableEntries(baseUrl),
        getReplenishmentSuggestions(baseUrl),
        getShoppingListItems(baseUrl),
        getReplenishmentRules(baseUrl),
        getDurableItems(baseUrl),
        getLocationTree(baseUrl)
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
      setDurableItems(durableData);
      setLocationTree(locationTreeData);
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

  const setDurableFormField = (field: keyof DurableItemForm, value: string) => {
    setDurableForm((current) => ({ ...current, [field]: value }));
  };

  const getDurablePayload = () => {
    const purchaseValue = durableForm.purchaseValue.trim() ? Number(durableForm.purchaseValue) : null;

    if (!durableForm.displayName.trim()) {
      setError('Durable item name is required.');
      return null;
    }

    if (purchaseValue !== null && !Number.isFinite(purchaseValue)) {
      setError('Purchase value must be a valid number.');
      return null;
    }

    return {
      displayName: durableForm.displayName.trim(),
      description: toNullableString(durableForm.description),
      itemType: toNullableString(durableForm.itemType),
      brandManufacturer: toNullableString(durableForm.brandManufacturer),
      model: toNullableString(durableForm.model),
      serialNumber: toNullableString(durableForm.serialNumber),
      purchaseDate: durableForm.purchaseDate || null,
      purchaseValue,
      warrantyEndsOn: durableForm.warrantyEndsOn || null,
      status: durableForm.status,
      currentLocation: toNullableString(durableForm.currentLocation),
      notes: toNullableString(durableForm.notes),
      storageSlotId: toNullableString(durableForm.storageSlotId)
    };
  };

  const onSaveDurableItem = async (event: React.FormEvent) => {
    event.preventDefault();

    const payload = getDurablePayload();
    if (!payload) return;

    try {
      const saved = editingDurableItemId
        ? await updateDurableItem(baseUrl, editingDurableItemId, payload)
        : await createDurableItem(baseUrl, payload);

      setSelectedDurableItem(saved);
      setDurableForm(emptyDurableItemForm);
      setEditingDurableItemId(null);
      setError(null);
      await loadData();
    } catch (e) {
      setError(String(e));
    }
  };

  const navigate = (path: string) => {
    window.history.pushState({}, '', path);
    setRoute(getItemRoute(path));
  };

  const clearLocationForm = () => {
    setEditingLocationId(null);
    setLocationName('');
    setLocationType('House');
    setLocationParentId('');
    setLocationDescription('');
    setLocationSortOrder('');
  };

  const onEditLocation = (location: Location) => {
    setEditingLocationId(location.id);
    setLocationName(location.name);
    setLocationType(location.type);
    setLocationParentId(location.parentLocationId ?? '');
    setLocationDescription(location.description ?? '');
    setLocationSortOrder(location.sortOrder == null ? '' : String(location.sortOrder));
    if (route.kind === 'location') {
      navigate('/locations');
    }
  };

  const onSaveLocation = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!locationName.trim()) return;

    try {
      const payload = {
        name: locationName.trim(),
        type: locationType,
        parentLocationId: locationType === 'House' ? null : locationParentId || null,
        description: toNullableString(locationDescription),
        sortOrder: locationSortOrder ? Number(locationSortOrder) : null
      };
      const saved = editingLocationId
        ? await updateLocation(baseUrl, editingLocationId, payload)
        : await createLocation(baseUrl, payload);

      if (selectedLocation?.location.id === saved.id) {
        setSelectedLocation({
          ...selectedLocation,
          location: saved
        });
      }

      clearLocationForm();
      setError(null);
      await loadData();
    } catch (e) {
      setError(String(e));
    }
  };

  const onSearch = async (event: React.FormEvent) => {
    event.preventDefault();
    const query = searchQuery.trim();
    if (!query) {
      setSearchResults([]);
      setSearchError(null);
      return;
    }

    setIsSearching(true);
    setSearchError(null);
    try {
      setSearchResults(await searchPhoodab(baseUrl, query));
    } catch (e) {
      setSearchError(String(e));
    } finally {
      setIsSearching(false);
    }
  };

  const onOpenSearchResult = (result: GlobalSearchResult) => {
    if (result.kind === 'consumable') {
      navigate(`/items/consumable/${encodeURIComponent(result.id)}`);
      return;
    }
    if (result.kind === 'durable') {
      navigate(`/items/durable/${encodeURIComponent(result.id)}`);
      return;
    }
    if (result.kind === 'location') {
      navigate(`/locations/${encodeURIComponent(result.id)}`);
      return;
    }

    setShoppingStateFilter(result.state === 'ShoppingList' ? 'ShoppingList' : (result.state as ShoppingListItem['status']) || 'All');
    navigate('/#shopping-list');
  };

  const onOpenDurableItem = (item: DurableItem) => {
    if (!item.id) return;
    navigate(`/items/durable/${encodeURIComponent(item.id)}`);
  };

  const onEditDurableItem = (item: DurableItem) => {
    if (!item.id) return;

    setEditingDurableItemId(item.id);
    setDurableForm(toDurableItemForm(item));
    setSelectedDurableItem(item);
  };

  const onCancelDurableEdit = () => {
    setEditingDurableItemId(null);
    setDurableForm(emptyDurableItemForm);
  };

  const onRetireDurableItem = async (item: DurableItem) => {
    if (!item.id) return;

    try {
      const retired = await retireDurableItem(baseUrl, item.id, { notes: item.notes ?? null });
      setSelectedDurableItem(retired);
      setError(null);
      await loadData();
    } catch (e) {
      setError(String(e));
    }
  };

  useEffect(() => {
    getHealth(baseUrl).then((r) => setHealth(r.status)).catch((e) => setHealth(String(e)));
    getVersion(baseUrl).then((r) => setVersion(r.version)).catch((e) => setVersion(String(e)));
    loadData();
  }, []);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target;
      if (event.key !== '/' || (target instanceof Element && target.matches('input, textarea, select'))) return;
      event.preventDefault();
      searchInputRef.current?.focus();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);

  useEffect(() => {
    const onPopState = () => setRoute(getItemRoute(window.location.pathname));
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, []);

  useEffect(() => {
    if (route.kind !== 'durable') return;

    let isCurrent = true;
    setDurableDetailLoading(true);
    setSelectedDurableItem(null);
    setError(null);

    getDurableItem(baseUrl, route.entryId)
      .then((detail) => {
        if (isCurrent) setSelectedDurableItem(detail);
      })
      .catch((e) => {
        if (isCurrent) setError(String(e));
      })
      .finally(() => {
        if (isCurrent) setDurableDetailLoading(false);
      });

    return () => {
      isCurrent = false;
    };
  }, [route]);

  useEffect(() => {
    if (route.kind !== 'location') return;

    let isCurrent = true;
    setLocationDetailLoading(true);
    setSelectedLocation(null);
    setError(null);
    getLocationDetail(baseUrl, route.locationId)
      .then((detail) => {
        if (isCurrent) setSelectedLocation(detail);
      })
      .catch((e) => {
        if (isCurrent) setError(String(e));
      })
      .finally(() => {
        if (isCurrent) setLocationDetailLoading(false);
      });

    return () => {
      isCurrent = false;
    };
  }, [route]);

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

  const allLocations = flattenLocationTree(locationTree);
  const availableLocations = [...new Set([
    ...consumableEntries.map((entry) => entry.storageSlotId),
    ...durableItems.map((item) => item.currentLocation || item.storageSlotId)
  ].filter((location): location is string => Boolean(location)))].sort((a, b) => formatLocationPath(locationTree, a).localeCompare(formatLocationPath(locationTree, b)));
  const matchesLocation = (location: string | null | undefined) => locationFilter === 'All' || location === locationFilter;
  const visibleDurableItems = durableItems.filter((item) =>
    itemTypeFilter !== 'Consumable'
    && matchesLocation(item.currentLocation || item.storageSlotId)
    && (durableStatusFilter === 'All' || item.status === durableStatusFilter));
  const visibleConsumableEntries = consumableEntries.filter((entry) =>
    itemTypeFilter !== 'Durable'
    && matchesLocation(entry.storageSlotId)
    && (expiryStateFilter === 'All' || entry.expiryStatus === expiryStateFilter));
  const visibleSummary = summary.filter((item) =>
    itemTypeFilter !== 'Durable'
    && (locationFilter === 'All' || consumableEntries.some((entry) =>
      entry.itemDefinitionId === item.itemDefinitionId && entry.storageSlotId === locationFilter)));
  const visibleExpiringEntries = expiringEntries.filter((entry) =>
    itemTypeFilter !== 'Durable'
    && matchesLocation(entry.storageSlotId)
    && (expiryStateFilter === 'All' || entry.expiryStatus === expiryStateFilter));
  const visibleShoppingItems = shoppingListItems.filter((item) =>
    shoppingStateFilter === 'All' || getShoppingStatus(item) === shoppingStateFilter);

  const renderGlobalSearch = () => (
    <section aria-label="Global search" style={{ margin: '16px 0' }}>
      <form onSubmit={onSearch} style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <input
          ref={searchInputRef}
          aria-label="Search PHOODAB"
          placeholder="Search items, locations, and shopping"
          value={searchQuery}
          onChange={(event) => setSearchQuery(event.target.value)}
        />
        <button type="submit">Search</button>
        <span>Press / to focus</span>
      </form>
      {isSearching && <p>Searching...</p>}
      {searchError && <p role="alert">Search error: {searchError}</p>}
      {!isSearching && searchQuery.trim() && !searchError && searchResults.length === 0 && <p>No search results.</p>}
      {searchResults.length > 0 && (
        <ul aria-label="Search results">
          {searchResults.map((result) => (
            <li key={`${result.kind}-${result.id}`}>
              <button onClick={() => onOpenSearchResult(result)} aria-label={`Open ${result.typeLabel} ${result.title}`}>
                <strong>{result.title}</strong> [{result.typeLabel}]
                {result.location && <> - {result.location}</>}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );

  const renderSmartFilters = () => (
    <section id="smart-filters" aria-label="Smart filters" style={{ display: 'flex', flexWrap: 'wrap', gap: 12, margin: '16px 0' }}>
      <label>
        Item type
        <select aria-label="Item type filter" value={itemTypeFilter} onChange={(event) => setItemTypeFilter(event.target.value as typeof itemTypeFilter)}>
          <option value="All">All</option>
          <option value="Consumable">Consumable</option>
          <option value="Durable">Durable Item</option>
        </select>
      </label>
      <label>
        Location
        <select aria-label="Location filter" value={locationFilter} onChange={(event) => setLocationFilter(event.target.value)}>
          <option value="All">All</option>
          {availableLocations.map((location) => <option key={location} value={location}>{formatLocationPath(locationTree, location)}</option>)}
        </select>
      </label>
      <label>
        Durable status
        <select aria-label="Durable status filter" value={durableStatusFilter} onChange={(event) => setDurableStatusFilter(event.target.value as typeof durableStatusFilter)}>
          <option value="All">All</option>
          {durableStatuses.map((status) => <option key={status} value={status}>{status}</option>)}
        </select>
      </label>
      <label>
        Expiry state
        <select aria-label="Expiry state filter" value={expiryStateFilter} onChange={(event) => setExpiryStateFilter(event.target.value as typeof expiryStateFilter)}>
          <option value="All">All</option>
          {(['Unknown', 'Expired', 'Urgent', 'Soon', 'Safe'] as ConsumableEntry['expiryStatus'][]).map((status) => <option key={status} value={status}>{status}</option>)}
        </select>
      </label>
      <label>
        Shopping state
        <select aria-label="Shopping state filter" value={shoppingStateFilter} onChange={(event) => setShoppingStateFilter(event.target.value as typeof shoppingStateFilter)}>
          <option value="All">All</option>
          <option value="ShoppingList">Shopping List</option>
          <option value="InCart">In Cart / Buying</option>
          <option value="Bought">Bought</option>
          <option value="StockUpdateNeeded">Stock Update Needed</option>
        </select>
      </label>
    </section>
  );

  const selectedDurableDetail = selectedDurableItem;
  const expectedParentType: Partial<Record<LocationType, LocationType>> = {
    Room: 'House',
    StorageUnit: 'Room',
    StorageSlot: 'StorageUnit'
  };
  const possibleLocationParents = allLocations.filter((location) => location.type === expectedParentType[locationType] && location.id !== editingLocationId);
  const renderDurableEditor = () => (
    <form onSubmit={onSaveDurableItem} aria-label="Edit durable item">
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'end', marginTop: 12 }}>
        <input
          aria-label="Durable item name"
          placeholder="Durable item name"
          value={durableForm.displayName}
          onChange={(e) => setDurableFormField('displayName', e.target.value)}
        />
        <input
          aria-label="Durable item type"
          placeholder="Category or type"
          value={durableForm.itemType}
          onChange={(e) => setDurableFormField('itemType', e.target.value)}
        />
        <select
          aria-label="Durable item status"
          value={durableForm.status}
          onChange={(e) => setDurableFormField('status', e.target.value as DurableItemStatus)}
        >
          {durableStatuses.map((status) => <option key={status} value={status}>{status}</option>)}
        </select>
        <input
          aria-label="Durable item location"
          placeholder="Location"
          value={durableForm.currentLocation}
          onChange={(e) => setDurableFormField('currentLocation', e.target.value)}
        />
        <input
          aria-label="Durable item brand"
          placeholder="Brand / manufacturer"
          value={durableForm.brandManufacturer}
          onChange={(e) => setDurableFormField('brandManufacturer', e.target.value)}
        />
        <input
          aria-label="Durable item model"
          placeholder="Model"
          value={durableForm.model}
          onChange={(e) => setDurableFormField('model', e.target.value)}
        />
        <input
          aria-label="Durable item serial number"
          placeholder="Serial number"
          value={durableForm.serialNumber}
          onChange={(e) => setDurableFormField('serialNumber', e.target.value)}
        />
        <input
          aria-label="Durable item purchase date"
          type="date"
          value={durableForm.purchaseDate}
          onChange={(e) => setDurableFormField('purchaseDate', e.target.value)}
        />
        <input
          aria-label="Durable item purchase value"
          placeholder="Purchase value"
          type="number"
          step="any"
          value={durableForm.purchaseValue}
          onChange={(e) => setDurableFormField('purchaseValue', e.target.value)}
        />
        <input
          aria-label="Durable item warranty end"
          type="date"
          value={durableForm.warrantyEndsOn}
          onChange={(e) => setDurableFormField('warrantyEndsOn', e.target.value)}
        />
        <input
          type="hidden"
          value={durableForm.storageSlotId}
          readOnly
        />
        <StorageSlotTreePicker
          label="Durable item storage slot"
          nodes={locationTree}
          selectedId={durableForm.storageSlotId}
          onSelect={(id) => setDurableFormField('storageSlotId', id)}
        />
        <input
          aria-label="Durable item description"
          placeholder="Description"
          value={durableForm.description}
          onChange={(e) => setDurableFormField('description', e.target.value)}
        />
        <input
          aria-label="Durable item notes"
          placeholder="Notes"
          value={durableForm.notes}
          onChange={(e) => setDurableFormField('notes', e.target.value)}
        />
        <button type="submit">Save Durable Item</button>
        <button type="button" onClick={onCancelDurableEdit}>Cancel Durable Edit</button>
      </div>
    </form>
  );

  if (route.kind === 'locations') {
    return (
      <main style={{ fontFamily: 'system-ui', padding: 16 }}>
        <button onClick={() => navigate('/')} aria-label="Back to inventory">Back to Inventory</button>
        {renderGlobalSearch()}
        <h1>Locations</h1>
        <p>Browse inventory from House to Room to Storage Unit to Storage Slot.</p>
        {error && <p role="alert">Error: {error}</p>}
        <section aria-label="Location hierarchy">
          <h2>Location Hierarchy</h2>
          {!isLoading && locationTree.length === 0 && <p>No locations yet.</p>}
          {locationTree.length > 0 && (
            <LocationTreeList
              nodes={locationTree}
              onOpen={(id) => navigate(`/locations/${encodeURIComponent(id)}`)}
              onEdit={onEditLocation}
            />
          )}
        </section>
        <section aria-label={editingLocationId ? "Edit location" : "Create location"}>
          <h2>{editingLocationId ? 'Edit Location' : 'Create Location'}</h2>
          <form onSubmit={onSaveLocation} style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'end' }}>
            <label>
              Name
              <input aria-label="Location name" value={locationName} onChange={(event) => setLocationName(event.target.value)} />
            </label>
            <label>
              Type
              <select
                aria-label="Location type"
                value={locationType}
                onChange={(event) => {
                  setLocationType(event.target.value as LocationType);
                  setLocationParentId('');
                }}
              >
                {(['House', 'Room', 'StorageUnit', 'StorageSlot'] as LocationType[]).map((type) => <option key={type} value={type}>{type}</option>)}
              </select>
            </label>
            <label>
              Parent
              <select aria-label="Location parent" value={locationParentId} disabled={locationType === 'House'} onChange={(event) => setLocationParentId(event.target.value)}>
                <option value="">Select parent</option>
                {possibleLocationParents.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}
              </select>
            </label>
            <label>
              Description
              <input aria-label="Location description" value={locationDescription} onChange={(event) => setLocationDescription(event.target.value)} />
            </label>
            <label>
              Display order
              <input aria-label="Location display order" type="number" min={0} value={locationSortOrder} onChange={(event) => setLocationSortOrder(event.target.value)} />
            </label>
            <button type="submit">{editingLocationId ? 'Save Location' : 'Create Location'}</button>
            {editingLocationId && <button type="button" onClick={clearLocationForm}>Cancel Location Edit</button>}
          </form>
        </section>
      </main>
    );
  }

  if (route.kind === 'location') {
    const detail = selectedLocation;
    return (
      <main style={{ fontFamily: 'system-ui', padding: 16 }}>
        <button onClick={() => navigate('/locations')} aria-label="Back to locations">Back to Locations</button>
        {renderGlobalSearch()}
        {locationDetailLoading && <p>Loading location detail...</p>}
        {error && <p role="alert">Error: {error}</p>}
        {!locationDetailLoading && detail && (
          <section aria-label={`Location detail for ${detail.location.name}`}>
            <h1>{detail.location.name}</h1>
            <p>{detail.location.type} | ID: {detail.location.id}</p>
            {detail.location.description && <p>{detail.location.description}</p>}
            <p>{detail.childLocationCount} child locations, {detail.consumableCount} consumable lots, {detail.durableItemCount} durable items</p>
            <button type="button" onClick={() => onEditLocation(detail.location)} aria-label={`Edit location ${detail.location.name}`}>Edit Location</button>
            <h2>Child Locations</h2>
            {detail.children.length === 0 && <p>No child locations.</p>}
            <ul>
              {detail.children.map((child) => (
                <li key={child.id}>
                  <button onClick={() => navigate(`/locations/${encodeURIComponent(child.id)}`)}>{child.name} ({child.type})</button>
                </li>
              ))}
            </ul>
            <h2>Consumables Stored Here</h2>
            {detail.consumables.length === 0 && <p>No consumables stored here.</p>}
            <ul>{detail.consumables.map((entry) => <li key={entry.entryId}>{entry.itemName}: {entry.quantity} {entry.unit}</li>)}</ul>
            <h2>Durable Items Stored Here</h2>
            {detail.durableItems.length === 0 && <p>No durable items stored here.</p>}
            <ul>{detail.durableItems.map((item) => <li key={item.id}>{getDurableDisplayName(item)} ({item.status})</li>)}</ul>
          </section>
        )}
      </main>
    );
  }

  if (route.kind === 'consumable') {
    const item = summary.find((candidate) => candidate.itemDefinitionId === route.itemDefinitionId);
    const lots = consumableEntries.filter((entry) => entry.itemDefinitionId === route.itemDefinitionId);
    const rule = rules.find((candidate) => candidate.itemDefinitionId === route.itemDefinitionId);
    const suggestion = suggestions.find((candidate) => candidate.itemDefinitionId === route.itemDefinitionId);
    const shoppingItems = shoppingListItems.filter((candidate) => candidate.itemDefinitionId === route.itemDefinitionId);
    const activeShoppingItem = shoppingItems.find((candidate) => !candidate.isPurchased);
    const itemName = item?.itemName ?? lots[0]?.itemName ?? suggestion?.itemName ?? 'Consumable item';
    const locations = [...new Set(lots.map((lot) => lot.storageSlotId).filter((location): location is string => Boolean(location)))];

    return (
      <main style={{ fontFamily: 'system-ui', padding: 16 }}>
        <button onClick={() => navigate('/')} aria-label="Back to inventory">Back to Inventory</button>
        {renderGlobalSearch()}
        <section aria-label={`Consumable item detail for ${itemName}`} style={{ marginTop: 16 }}>
          <span style={{ background: '#dcfce7', color: '#166534', padding: '4px 8px', borderRadius: 12, fontWeight: 700 }}>Consumable</span>
          <h1>{itemName}</h1>
          {isLoading && <p>Loading consumable item detail...</p>}
          {!isLoading && !item && lots.length === 0 && <p>Consumable item not found.</p>}
          {!isLoading && (item || lots.length > 0) && (
            <>
              <h2>Current Amount</h2>
              <p>
                {item?.hasMixedUnits
                  ? `Mixed units across ${item.entryCount} lots`
                  : `${item?.totalQuantity ?? lots.reduce((total, lot) => total + lot.quantity, 0)} ${item?.unit ?? lots[0]?.unit ?? ''}`}
              </p>
              {item?.mixedUnitWarning && <p style={{ color: '#9a3412' }}>{item.mixedUnitWarning}</p>}

              <h2>Locations</h2>
              <p>{locations.length > 0 ? locations.map((location) => formatLocationPath(locationTree, location)).join(', ') : 'No location set'}</p>

              <h2>Lots / Batches</h2>
              {lots.length === 0 && <p>No lots recorded.</p>}
              {lots.map((entry) => {
                const edit = entryEdits[entry.entryId] ?? toEntryEdit(entry);
                const actionEdit = entryActionEdits[entry.entryId] ?? toEntryActionEdit(entry);
                return (
                  <article key={entry.entryId} style={{ ...getExpiryStyle(entry.expiryStatus), padding: 12, marginBottom: 12 }}>
                    <strong>Lot {entry.entryId}</strong>
                    <div>Quantity: {entry.quantity} {entry.unit}</div>
                    <div>Expiry: {entry.expiresOn ?? 'No expiry'} ({entry.expiryStatus})</div>
                    <div>Location: <LocationName nodes={locationTree} locations={allLocations} id={entry.storageSlotId} /></div>
                    {entryErrors[entry.entryId] && <p role="alert">{entryErrors[entry.entryId]}</p>}
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginTop: 8 }}>
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
                      <input aria-label={`Entry unit for ${entry.itemName}`} value={edit.unit} onChange={(e) => setEntryEdit(entry.entryId, { unit: e.target.value })} />
                      <input aria-label={`Entry expiry for ${entry.itemName}`} type="date" value={edit.expiresOn} onChange={(e) => setEntryEdit(entry.entryId, { expiresOn: e.target.value })} />
                      <StorageSlotTreePicker
                        label={`Entry storage slot for ${entry.itemName}`}
                        nodes={locationTree}
                        selectedId={edit.storageSlotId}
                        onSelect={(id) => setEntryEdit(entry.entryId, { storageSlotId: id })}
                      />
                      <button onClick={() => onSaveConsumableEntry(entry)}>Adjust Quantity / Expiry / Location</button>
                      <button onClick={() => onResetEntryEdit(entry)}>Undo Unsaved Changes</button>
                    </div>
                  </article>
                );
              })}

              <h2>Replenishment Rule</h2>
              {!rule && <p>No replenishment rule configured.</p>}
              {rule && (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'end' }}>
                  <label>
                    Target amount
                    <input aria-label={`Target amount for ${itemName}`} type="number" step="any" value={rule.desiredAmount} onChange={(e) => setRules((current) => current.map((candidate) => candidate.id === rule.id ? { ...candidate, desiredAmount: Number(e.target.value) } : candidate))} />
                  </label>
                  <label>
                    Target unit
                    <input aria-label={`Target unit for ${itemName}`} value={rule.desiredUnit} onChange={(e) => setRules((current) => current.map((candidate) => candidate.id === rule.id ? { ...candidate, desiredUnit: e.target.value } : candidate))} />
                  </label>
                  <label>
                    Expiry warning days
                    <input aria-label={`Expiry warning days for ${itemName}`} type="number" min={0} value={rule.expiryWarningDays} onChange={(e) => setRules((current) => current.map((candidate) => candidate.id === rule.id ? { ...candidate, expiryWarningDays: Number(e.target.value) } : candidate))} />
                  </label>
                  <label>
                    Disabled
                    <input aria-label={`Disable replenishment rule for ${itemName}`} type="checkbox" checked={rule.isDisabled} onChange={(e) => setRules((current) => current.map((candidate) => candidate.id === rule.id ? { ...candidate, isDisabled: e.target.checked } : candidate))} />
                  </label>
                  <button aria-label={`Save replenishment rule for ${itemName}`} onClick={() => onSaveRule(rule)}>Save Rule</button>
                </div>
              )}

              <h2>Shopping Suggestion</h2>
              {suggestion ? (
                <div>
                  <p>Suggested purchase: {suggestion.suggestedPurchaseAmount} {suggestion.unit}</p>
                  <p>Deficit: {suggestion.deficitAmount} {suggestion.unit}; expiring soon: {suggestion.expiringSoonAmount} {suggestion.unit}</p>
                  {activeShoppingItem ? (
                    <p>State: {getShoppingStatusLabel(activeShoppingItem)}</p>
                  ) : (
                    <button onClick={() => onCreateFromSuggestion(suggestion)}>Add to Shopping List</button>
                  )}
                </div>
              ) : shoppingItems.length > 0 ? (
                <p>State: {shoppingItems.map(getShoppingStatusLabel).join(', ')}</p>
              ) : (
                <p>No active shopping suggestion.</p>
              )}
            </>
          )}
        </section>
      </main>
    );
  }

  if (route.kind === 'durable') {
    const name = selectedDurableDetail ? getDurableDisplayName(selectedDurableDetail) : 'Durable item';
    return (
      <main style={{ fontFamily: 'system-ui', padding: 16 }}>
        <button onClick={() => navigate('/')} aria-label="Back to inventory">Back to Inventory</button>
        {renderGlobalSearch()}
        <section aria-label={`Durable item detail for ${name}`} style={{ marginTop: 16 }}>
          <span style={{ background: '#dbeafe', color: '#1e40af', padding: '4px 8px', borderRadius: 12, fontWeight: 700 }}>Durable</span>
          <h1>{name}</h1>
          {durableDetailLoading && <p>Loading durable item detail...</p>}
          {!durableDetailLoading && !selectedDurableDetail && !error && <p>Durable item not found.</p>}
          {error && <p>Error: {error}</p>}
          {!durableDetailLoading && selectedDurableDetail && (
            <>
              <h2>Identity</h2>
              <div>Type: {selectedDurableDetail.itemType || 'Uncategorized'}</div>
              <div>Description: {selectedDurableDetail.description || 'Not recorded'}</div>
              <div>Brand / manufacturer: {selectedDurableDetail.brandManufacturer || 'Not recorded'}</div>
              <div>Model: {selectedDurableDetail.model || 'Not recorded'}</div>
              <div>Serial number: {selectedDurableDetail.serialNumber || 'Not recorded'}</div>
              <h2>Status and Location</h2>
              <div>Status: {selectedDurableDetail.status}</div>
              <div>Location: {selectedDurableDetail.currentLocation || <LocationName nodes={locationTree} locations={allLocations} id={selectedDurableDetail.storageSlotId} />}</div>
              <h2>Purchase and Warranty</h2>
              <div>Purchase date: {selectedDurableDetail.purchaseDate || 'Not recorded'}</div>
              <div>Purchase value: {selectedDurableDetail.purchaseValue ?? 'Not recorded'}</div>
              <div>{getWarrantyIndicator(selectedDurableDetail)}</div>
              <h2>Notes</h2>
              <p>Notes: {selectedDurableDetail.notes || 'No notes recorded'}</p>
              <div style={{ display: 'flex', gap: 8 }}>
                <button aria-label={`Edit durable item ${name}`} onClick={() => onEditDurableItem(selectedDurableDetail)}>Edit Durable Item</button>
                {selectedDurableDetail.status !== 'Retired' && (
                  <button aria-label={`Retire durable item ${name}`} onClick={() => onRetireDurableItem(selectedDurableDetail)}>Retire Durable Item</button>
                )}
              </div>
              {editingDurableItemId === selectedDurableDetail.id && renderDurableEditor()}
            </>
          )}
        </section>
      </main>
    );
  }

  return (
    <main style={{ fontFamily: 'system-ui', padding: 16 }}>
      <h1>PHOODAB Pantry MVP</h1>
      <button onClick={() => navigate('/locations')}>Browse Locations</button>
      {renderGlobalSearch()}
      <p>Health: {health}</p>
      <p>Version: {version}</p>
      {renderSmartFilters()}

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
        <StorageSlotTreePicker
          label="Consumable storage slot"
          nodes={locationTree}
          selectedId={storageSlotId}
          onSelect={setStorageSlotId}
        />
        <button type="submit">Add Entry</button>
      </form>

      <h2>Durable Items</h2>
      <form onSubmit={onSaveDurableItem}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'end' }}>
          <input
            aria-label="Durable item name"
            placeholder="Durable item name"
            value={durableForm.displayName}
            onChange={(e) => setDurableFormField('displayName', e.target.value)}
          />
          <input
            aria-label="Durable item type"
            placeholder="Category or type"
            value={durableForm.itemType}
            onChange={(e) => setDurableFormField('itemType', e.target.value)}
          />
          <select
            aria-label="Durable item status"
            value={durableForm.status}
            onChange={(e) => setDurableFormField('status', e.target.value as DurableItemStatus)}
          >
            {durableStatuses.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
          <input
            aria-label="Durable item location"
            placeholder="Location"
            value={durableForm.currentLocation}
            onChange={(e) => setDurableFormField('currentLocation', e.target.value)}
          />
          <input
            aria-label="Durable item brand"
            placeholder="Brand / manufacturer"
            value={durableForm.brandManufacturer}
            onChange={(e) => setDurableFormField('brandManufacturer', e.target.value)}
          />
          <input
            aria-label="Durable item model"
            placeholder="Model"
            value={durableForm.model}
            onChange={(e) => setDurableFormField('model', e.target.value)}
          />
          <input
            aria-label="Durable item serial number"
            placeholder="Serial number"
            value={durableForm.serialNumber}
            onChange={(e) => setDurableFormField('serialNumber', e.target.value)}
          />
          <input
            aria-label="Durable item purchase date"
            type="date"
            value={durableForm.purchaseDate}
            onChange={(e) => setDurableFormField('purchaseDate', e.target.value)}
          />
          <input
            aria-label="Durable item purchase value"
            placeholder="Purchase value"
            type="number"
            step="any"
            value={durableForm.purchaseValue}
            onChange={(e) => setDurableFormField('purchaseValue', e.target.value)}
          />
          <input
            aria-label="Durable item warranty end"
            type="date"
            value={durableForm.warrantyEndsOn}
            onChange={(e) => setDurableFormField('warrantyEndsOn', e.target.value)}
          />
          <input
            type="hidden"
            value={durableForm.storageSlotId}
            readOnly
          />
          <StorageSlotTreePicker
            label="Durable item storage slot"
            nodes={locationTree}
            selectedId={durableForm.storageSlotId}
            onSelect={(id) => setDurableFormField('storageSlotId', id)}
          />
          <input
            aria-label="Durable item description"
            placeholder="Description"
            value={durableForm.description}
            onChange={(e) => setDurableFormField('description', e.target.value)}
          />
          <input
            aria-label="Durable item notes"
            placeholder="Notes"
            value={durableForm.notes}
            onChange={(e) => setDurableFormField('notes', e.target.value)}
          />
          <button type="submit">{editingDurableItemId ? 'Save Durable Item' : 'Create Durable Item'}</button>
          {editingDurableItemId && <button type="button" onClick={onCancelDurableEdit}>Cancel Durable Edit</button>}
        </div>
      </form>
      {!isLoading && !error && durableItems.length === 0 && <p>No durable items.</p>}
      {!isLoading && !error && durableItems.length > 0 && visibleDurableItems.length === 0 && <p>No durable items match the filters.</p>}
      {!isLoading && !error && visibleDurableItems.length > 0 && (
        <ul>
          {visibleDurableItems.map((item) => {
            const name = getDurableDisplayName(item);
            const location = item.currentLocation || formatLocationPath(locationTree, item.storageSlotId);
            return (
              <li key={item.id ?? `${name}-${item.itemDefinitionId ?? ''}`} style={{ marginBottom: 12 }}>
                <strong>{name}</strong>: {item.itemType || 'Uncategorized'} [{item.status}]
                <div>
                  Location: {location} | {getWarrantyIndicator(item)}
                </div>
                <button aria-label={`Open durable item ${name}`} onClick={() => onOpenDurableItem(item)}>Open Details</button>
                <button aria-label={`Edit durable item ${name}`} onClick={() => onEditDurableItem(item)}>Edit</button>
                {item.status !== 'Retired' && <button aria-label={`Retire durable item ${name}`} onClick={() => onRetireDurableItem(item)}>Retire</button>}
              </li>
            );
          })}
        </ul>
      )}

      <h2>Inventory Summary</h2>
      {isLoading && <p>Loading pantry data...</p>}
      {error && <p>Error: {error}</p>}
      {!isLoading && !error && summary.length === 0 && <p>No inventory yet.</p>}
      {!isLoading && !error && summary.length > 0 && visibleSummary.length === 0 && <p>No inventory matches the filters.</p>}
      {!isLoading && !error && visibleSummary.length > 0 && (
        <ul>
          {visibleSummary.map((item) => (
            <li key={item.itemDefinitionId}>
              {item.hasMixedUnits
                ? `${item.itemName}: mixed units (${item.entryCount} entries)`
                : `${item.itemName}: ${item.totalQuantity} ${item.unit ?? ''} (${item.entryCount} entries)`}
              {item.mixedUnitWarning && <strong style={{ color: '#9a3412', marginLeft: 8 }}>{item.mixedUnitWarning}</strong>}
              <button
                aria-label={`Open consumable item ${item.itemName}`}
                style={{ marginLeft: 8 }}
                onClick={() => navigate(`/items/consumable/${encodeURIComponent(item.itemDefinitionId)}`)}
              >
                Open Details
              </button>
            </li>
          ))}
        </ul>
      )}

      <h2>Consumable Entry Audit</h2>
      {!isLoading && !error && consumableEntries.length === 0 && <p>No consumable entries.</p>}
      {!isLoading && !error && consumableEntries.length > 0 && visibleConsumableEntries.length === 0 && <p>No consumable entries match the filters.</p>}
      {!isLoading && !error && visibleConsumableEntries.length > 0 && (
        <ul>
          {visibleConsumableEntries.map((entry) => {
            const edit = entryEdits[entry.entryId] ?? toEntryEdit(entry);
            const actionEdit = entryActionEdits[entry.entryId] ?? toEntryActionEdit(entry);
            const expiryStyle = getExpiryStyle(entry.expiryStatus);
            const lotLocation = formatLocationPath(locationTree, entry.storageSlotId);
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
                    <StorageSlotTreePicker
                      label={`Entry storage slot for ${entry.itemName}`}
                      nodes={locationTree}
                      selectedId={edit.storageSlotId}
                      onSelect={(id) => setEntryEdit(entry.entryId, { storageSlotId: id })}
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
      {!isLoading && !error && expiringEntries.length > 0 && visibleExpiringEntries.length === 0 && <p>No expiring entries match the filters.</p>}
      {!isLoading && !error && visibleExpiringEntries.length > 0 && (
        <ul>
          {visibleExpiringEntries.map((entry) => (
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

      <h2 id="shopping-list">Shopping List</h2>
      {!isLoading && !error && shoppingListItems.length === 0 && <p>No shopping items.</p>}
      {!isLoading && !error && shoppingListItems.length > 0 && visibleShoppingItems.length === 0 && <p>No shopping items match the filters.</p>}
      {!isLoading && !error && visibleShoppingItems.length > 0 && (
        <ul>
          {visibleShoppingItems.map((item) => {
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

const rootElement = document.getElementById('root');
if (rootElement) {
  ReactDOM.createRoot(rootElement).render(
    <React.StrictMode>
      <App />
    </React.StrictMode>
  );
}
