using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Layouts;
using Phoodab.Application;
using Phoodab.Domain;

namespace Phoodab.Mobile;

public sealed class MainPage : ContentPage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string TargetAmountHelp = "Amount to keep stocked before replenishment is suggested.";
    private const string ExpiryWarningDaysHelp = "Treat entries expiring within this many days as warning items.";

    private readonly IInventoryMvpStore _store;
    private readonly ReplenishmentSuggestionService _suggestionService;
    private readonly IUtcDateProvider _utcDateProvider;
    private readonly Label _healthLabel = new() { Text = "Health: loading" };
    private readonly Label _versionLabel = new() { Text = "Version: loading" };
    private readonly Label _statusLabel = new() { TextColor = Colors.DarkRed };
    private readonly Entry _searchEntry = new() { Placeholder = "Search items, locations, and shopping" };
    private readonly VerticalStackLayout _searchResults = new() { Spacing = 8 };
    private readonly Entry _itemNameEntry = new() { Placeholder = "Item name" };
    private readonly Entry _desiredAmountEntry = new() { Placeholder = "Desired amount (optional)", Keyboard = Keyboard.Numeric };
    private readonly Entry _desiredUnitEntry = new() { Placeholder = "Desired unit (optional)" };
    private readonly Picker _entryItemPicker = new() { Title = "Select item" };
    private readonly Entry _quantityEntry = new() { Placeholder = "Quantity", Keyboard = Keyboard.Numeric };
    private readonly Entry _unitEntry = new() { Placeholder = "Unit" };
    private readonly Entry _expiryDateEntry = new() { Placeholder = "Expiry date YYYY-MM-DD (optional)" };
    private readonly VerticalStackLayout _entryStorageSlotPicker = new() { Spacing = 6 };
    private readonly Entry _durableNameEntry = new() { Placeholder = "Durable item name" };
    private readonly Entry _durableTypeEntry = new() { Placeholder = "Category or type" };
    private readonly Picker _durableStatusPicker = new() { Title = "Status" };
    private readonly Entry _durableLocationEntry = new() { Placeholder = "Location" };
    private readonly Entry _durableBrandEntry = new() { Placeholder = "Brand / manufacturer" };
    private readonly Entry _durableModelEntry = new() { Placeholder = "Model" };
    private readonly Entry _durableSerialEntry = new() { Placeholder = "Serial number" };
    private readonly Entry _durablePurchaseDateEntry = new() { Placeholder = "Purchase date YYYY-MM-DD (optional)" };
    private readonly Entry _durablePurchaseValueEntry = new() { Placeholder = "Purchase value (optional)", Keyboard = Keyboard.Numeric };
    private readonly Entry _durableWarrantyEntry = new() { Placeholder = "Warranty end YYYY-MM-DD (optional)" };
    private readonly Entry _durableDescriptionEntry = new() { Placeholder = "Description" };
    private readonly Entry _durableNotesEntry = new() { Placeholder = "Notes" };
    private readonly VerticalStackLayout _durableStorageSlotPicker = new() { Spacing = 6 };
    private readonly Button _durableSaveButton = new() { Text = "Create Durable Item" };
    private readonly Button _durableCancelButton = new() { Text = "Cancel Durable Edit", IsVisible = false };
    private readonly Entry _locationNameEntry = new() { Placeholder = "Location name" };
    private readonly Picker _locationTypePicker = new() { Title = "Location type" };
    private readonly Picker _locationParentPicker = new() { Title = "Parent location" };
    private readonly Entry _locationDescriptionEntry = new() { Placeholder = "Description (optional)" };
    private readonly Entry _locationSortOrderEntry = new() { Placeholder = "Display order (optional)", Keyboard = Keyboard.Numeric };
    private readonly Button _locationSaveButton = new() { Text = "Create Location" };
    private readonly Button _locationCancelButton = new() { Text = "Cancel Location Edit", IsVisible = false };
    private readonly VerticalStackLayout _content = new() { Spacing = 16, Padding = 16 };
    private readonly FlexLayout _navigation = new() { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Stretch };
    private readonly Label _pageTitleLabel = new() { FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black };
    private readonly VerticalStackLayout _dataContent = new() { Spacing = 16 };
    private readonly VerticalStackLayout _durableDetailContent = new() { Spacing = 8 };
    private readonly Dictionary<PageId, Button> _navigationButtons = [];

    private readonly List<ItemOption> _createdItems = [];
    private readonly List<InventorySummaryItem> _summary = [];
    private readonly List<DurableItem> _durableItems = [];
    private readonly List<ConsumableEntry> _consumableEntries = [];
    private readonly List<ConsumableEntry> _expiringEntries = [];
    private readonly List<ReplenishmentSuggestion> _suggestions = [];
    private readonly List<ShoppingListItem> _shoppingListItems = [];
    private readonly List<ReplenishmentRule> _rules = [];
    private readonly List<LocationItem> _locations = [];
    private readonly List<LocationTreeNodeItem> _locationTree = [];
    private readonly Dictionary<string, HashSet<string>> _expandedStorageSlotPickerNodeIds = [];

    private bool _hasLoaded;
    private PageId _activePage = PageId.Dashboard;
    private string? _editingDurableEntryId;
    private string? _editingLocationId;
    private string? _selectedEntryStorageSlotId;
    private string? _selectedDurableStorageSlotId;
    private string? _selectedDurableEntryId;
    private string? _selectedConsumableItemDefinitionId;
    private string _inventoryLocationFilter = "All";
    private string _expiryStatusFilter = "All";
    private string _durableStatusFilter = "All";
    private string _durableLocationFilter = "All";
    private string _locationItemTypeFilter = "All";
    private string _locationFilter = "All";
    private string _shoppingStatusFilter = "All";

    public MainPage(
        IInventoryMvpStore store,
        ReplenishmentSuggestionService suggestionService,
        IUtcDateProvider utcDateProvider)
    {
        _store = store;
        _suggestionService = suggestionService;
        _utcDateProvider = utcDateProvider;
        Title = "PHOODAB Pantry";
        BackgroundColor = Colors.White;
        _durableStatusPicker.ItemsSource = Enum.GetNames<DurableItemStatus>().ToList();
        _durableStatusPicker.SelectedItem = DurableItemStatus.Active.ToString();
        _durableSaveButton.Clicked += async (_, _) => await SaveDurableItemAsync();
        _durableCancelButton.Clicked += (_, _) => ClearDurableForm();
        _locationTypePicker.ItemsSource = Enum.GetNames<LocationType>().ToList();
        _locationTypePicker.SelectedItem = LocationType.House.ToString();
        _locationTypePicker.SelectedIndexChanged += (_, _) => RefreshLocationParentPicker();
        _locationSaveButton.Clicked += async (_, _) => await SaveLocationAsync();
        _locationCancelButton.Clicked += (_, _) => ClearLocationForm();
        _searchEntry.ReturnType = ReturnType.Search;
        _searchEntry.Completed += async (_, _) => await SearchAsync();
        SemanticProperties.SetDescription(_searchEntry, "Search all PHOODAB items, locations, and shopping entries");

        Content = new ScrollView { Content = _content };
        BuildShell();
        RebuildDataSections();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadDataAsync();
    }

    private void BuildShell()
    {
        _content.Children.Clear();
        _content.Children.Add(new Label
        {
            Text = "PHOODAB",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        });

        _content.Children.Add(new Label
        {
            Text = "Pantry, shopping, locations, and durable inventory",
            TextColor = Colors.DarkSlateGray
        });
        var searchSection = Section("Global Search");
        searchSection.Children.Add(GridRows(_searchEntry, Button("Search", SearchAsync)));
        searchSection.Children.Add(_searchResults);
        _content.Children.Add(searchSection);
        BuildNavigation();
        _content.Children.Add(_navigation);
        _content.Children.Add(Button("Refresh", async () => await LoadDataAsync()));
        _content.Children.Add(_healthLabel);
        _content.Children.Add(_versionLabel);
        _content.Children.Add(_statusLabel);
        _content.Children.Add(_pageTitleLabel);
        _content.Children.Add(_dataContent);
    }

    private void RebuildDataSections()
    {
        UpdateNavigation();
        _pageTitleLabel.Text = PageTitle(_activePage);
        DetachReusableControls();
        _dataContent.Children.Clear();

        switch (_activePage)
        {
            case PageId.Inventory:
                AddInventoryFiltersSection();
                AddSelectedConsumableSection();
                AddCreateItemSection();
                AddEntrySection();
                AddInventorySummarySection();
                AddConsumableAuditSection();
                AddExpiringSection();
                AddSuggestionsSection();
                AddRulesSection();
                break;
            case PageId.Shopping:
                AddShoppingFiltersSection();
                AddShoppingSection();
                break;
            case PageId.Locations:
                AddLocationFiltersSection();
                AddLocationsSection();
                break;
            case PageId.Durable:
                AddDurableFiltersSection();
                AddDurableFormSection();
                AddDurableItemsSection();
                break;
            default:
                AddDashboardSection();
                break;
        }
    }

    private void BuildNavigation()
    {
        _navigation.Children.Clear();
        _navigationButtons.Clear();

        foreach (var page in Enum.GetValues<PageId>())
        {
            var button = Button(PageTitle(page), () =>
            {
                _activePage = page;
                RebuildDataSections();
                return Task.CompletedTask;
            });
            button.BorderWidth = 1;
            button.WidthRequest = 150;
            button.Margin = new Thickness(0, 0, 8, 8);
            _navigationButtons[page] = button;
            _navigation.Children.Add(button);
        }

        UpdateNavigation();
    }

    private void UpdateNavigation()
    {
        foreach (var (page, button) in _navigationButtons)
        {
            var isActive = _activePage == page;
            button.BackgroundColor = isActive ? Colors.LightSeaGreen : Colors.White;
            button.TextColor = isActive ? Colors.White : Colors.Black;
            button.BorderColor = isActive ? Colors.LightSeaGreen : Colors.LightGray;
        }
    }

    private async Task SearchAsync()
    {
        var query = (_searchEntry.Text ?? string.Empty).Trim();
        _searchResults.Children.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            _searchResults.Children.Add(new Label { Text = "Enter a search term." });
            return;
        }

        SetStatus("Searching...", isError: false);
        try
        {
            var results = await Task.Run(() => _store.Search(query));
            if (results.Count == 0)
            {
                _searchResults.Children.Add(new Label { Text = "No search results." });
            }
            else
            {
                foreach (var result in results)
                {
                    var text = $"{result.Title} [{result.TypeLabel}]";
                    if (!string.IsNullOrWhiteSpace(result.Location))
                    {
                        text += $" - {result.Location}";
                    }

                    _searchResults.Children.Add(WithDescription(
                        Button(text, () => OpenSearchResultAsync(result)),
                        $"Open {result.TypeLabel} {result.Title}"));
                }
            }

            SetStatus(string.Empty, isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Search error: {ex.Message}", isError: true);
        }
    }

    private Task OpenSearchResultAsync(GlobalSearchResultReadModel result)
    {
        switch (result.Kind)
        {
            case "consumable":
                _selectedConsumableItemDefinitionId = result.Id;
                _activePage = PageId.Inventory;
                break;
            case "durable":
                _selectedDurableEntryId = result.Id;
                _activePage = PageId.Durable;
                break;
            case "location":
                _locationFilter = result.Title;
                _activePage = PageId.Locations;
                break;
            case "shopping":
                _shoppingStatusFilter = string.IsNullOrWhiteSpace(result.State) ? "All" : result.State;
                _activePage = PageId.Shopping;
                break;
        }

        RebuildDataSections();
        return Task.CompletedTask;
    }

    private void AddInventoryFiltersSection()
    {
        var section = Section("Pantry Filters");
        section.Children.Add(GridRows(
            FilterPicker("Location", LocationOptions(), _inventoryLocationFilter, value => _inventoryLocationFilter = value),
            FilterPicker("Expiry state", ["All", "Unknown", "Expired", "Urgent", "Soon", "Safe"], _expiryStatusFilter, value => _expiryStatusFilter = value)));
        _dataContent.Children.Add(section);
    }

    private void AddDurableFiltersSection()
    {
        var section = Section("Equipment Filters");
        section.Children.Add(GridRows(
            FilterPicker("Status", ["All", .. Enum.GetNames<DurableItemStatus>()], _durableStatusFilter, value => _durableStatusFilter = value),
            FilterPicker("Location", LocationOptions(), _durableLocationFilter, value => _durableLocationFilter = value)));
        _dataContent.Children.Add(section);
    }

    private void AddLocationFiltersSection()
    {
        var section = Section("Location Filters");
        section.Children.Add(GridRows(
            FilterPicker("Item type", ["All", "Consumable", "Durable Item"], _locationItemTypeFilter, value => _locationItemTypeFilter = value),
            FilterPicker("Location", LocationOptions(), _locationFilter, value => _locationFilter = value)));
        _dataContent.Children.Add(section);
    }

    private void AddShoppingFiltersSection()
    {
        var section = Section("Shopping Filters");
        section.Children.Add(FilterPicker(
            "Shopping state",
            ["All", "ShoppingList", "InCart", "Bought", "StockUpdateNeeded"],
            _shoppingStatusFilter,
            value => _shoppingStatusFilter = value));
        _dataContent.Children.Add(section);
    }

    private void AddSelectedConsumableSection()
    {
        if (string.IsNullOrWhiteSpace(_selectedConsumableItemDefinitionId))
        {
            return;
        }

        var summary = _summary.FirstOrDefault(item => item.ItemDefinitionId == _selectedConsumableItemDefinitionId);
        var lots = _consumableEntries.Where(entry => entry.ItemDefinitionId == _selectedConsumableItemDefinitionId).ToList();
        var itemName = summary?.ItemName ?? lots.FirstOrDefault()?.ItemName ?? "Consumable item";
        var section = Section("Selected Consumable");
        section.Children.Add(new Label { Text = $"{itemName} [Consumable]", FontAttributes = FontAttributes.Bold });

        if (summary is not null)
        {
            section.Children.Add(new Label
            {
                Text = summary.HasMixedUnits
                    ? $"Mixed units across {summary.EntryCount} lots"
                    : $"Current amount: {summary.TotalQuantity?.ToString(CultureInfo.InvariantCulture) ?? "0"} {summary.Unit}"
            });
        }

        if (lots.Count == 0)
        {
            section.Children.Add(new Label { Text = "No lots recorded." });
        }
        else
        {
            foreach (var lot in lots)
            {
                section.Children.Add(new Label
                {
                    Text = $"Lot {lot.EntryId}: {lot.Quantity.ToString(CultureInfo.InvariantCulture)} {lot.Unit}; {lot.ExpiryStatus}; location {lot.StorageSlotId ?? "not set"}"
                });
            }
        }

        _dataContent.Children.Add(section);
    }

    private void AddDashboardSection()
    {
        _dataContent.Children.Add(new Label
        {
            Text = "What should I care about right now?",
            TextColor = Colors.DarkSlateGray
        });

        var lowStock = _suggestions.Where(suggestion => suggestion.DeficitAmount > 0).Take(4).ToList();
        _dataContent.Children.Add(DashboardCard(
            "Low-stock consumables",
            lowStock.Count == 0
                ? ["Everything with a replenishment rule has enough usable stock."]
                : lowStock.Select(suggestion => $"{suggestion.ItemName} needs {suggestion.SuggestedPurchaseAmount.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}")));

        _dataContent.Children.Add(DashboardCard(
            "Expiring items",
            _expiringEntries.Count == 0
                ? ["No expiring or expired lots need attention."]
                : _expiringEntries.Take(4).Select(entry => $"{entry.ItemName} - {entry.Quantity.ToString(CultureInfo.InvariantCulture)} {entry.Unit} - {entry.ExpiryStatus}")));

        _dataContent.Children.Add(DashboardCard(
            "Recently updated inventory",
            _consumableEntries.Count == 0
                ? ["No inventory lots have been recorded yet."]
                : _consumableEntries.Take(3).Select(entry => $"{entry.ItemName} lot {entry.EntryId}: {entry.Quantity.ToString(CultureInfo.InvariantCulture)} {entry.Unit}")));

        _dataContent.Children.Add(DashboardCard(
            "Replenishment suggestions",
            _suggestions.Count == 0
                ? ["No replenishment needed right now."]
                : _suggestions.Take(4).Select(suggestion => $"{suggestion.ItemName}: Buy {suggestion.SuggestedPurchaseAmount.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}")));

        var shoppingActions = _shoppingListItems
            .Where(item => ShoppingStatus(item) is "ShoppingList" or "InCart" or "StockUpdateNeeded")
            .Take(4)
            .ToList();
        _dataContent.Children.Add(DashboardCard(
            "Shopping actions",
            shoppingActions.Count == 0
                ? ["No shopping list or cart items need action."]
                : shoppingActions.Select(item => $"{item.ItemName}: {item.Quantity.ToString(CultureInfo.InvariantCulture)} {item.Unit} [{ShoppingStatusLabel(item)}]")));
    }

    private void AddLocationsSection()
    {
        var manageSection = Section(_editingLocationId is null ? "Create Location" : "Edit Location");
        RefreshLocationParentPicker();
        manageSection.Children.Add(_locationNameEntry);
        manageSection.Children.Add(GridRows(_locationTypePicker, _locationParentPicker));
        manageSection.Children.Add(_locationDescriptionEntry);
        manageSection.Children.Add(_locationSortOrderEntry);
        manageSection.Children.Add(GridRows(_locationSaveButton, _locationCancelButton));
        _dataContent.Children.Add(manageSection);

        var hierarchySection = Section("Location Hierarchy");
        if (_locationTree.Count == 0)
        {
            hierarchySection.Children.Add(new Label { Text = "No managed locations yet." });
        }
        else
        {
            foreach (var node in _locationTree)
            {
                AddLocationTreeNode(hierarchySection, node, depth: 0);
            }
        }

        _dataContent.Children.Add(hierarchySection);

        var section = Section("Inventory by Location");
        var rows = _consumableEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.StorageSlotId))
            .Select(entry => (Type: "Consumable", Location: DisplayLocation(entry.StorageSlotId), Title: $"{entry.ItemName} lot {entry.EntryId}", Detail: $"{entry.Quantity.ToString(CultureInfo.InvariantCulture)} {entry.Unit}"))
            .Concat(_durableItems
                .Where(item => !string.IsNullOrWhiteSpace(item.CurrentLocation) || !string.IsNullOrWhiteSpace(item.StorageSlotId))
                .Select(item => (Type: "Durable Item", Location: DurableLocation(item), Title: item.DisplayName, Detail: $"{DurableType(item)} [{item.Status}]")))
            .Where(row => (_locationItemTypeFilter == "All" || row.Type == _locationItemTypeFilter)
                && (_locationFilter == "All" || row.Location == _locationFilter))
            .ToList();

        if (rows.Count == 0)
        {
            section.Children.Add(new Label { Text = "No inventory or durable items have assigned locations yet." });
        }
        else
        {
            foreach (var row in rows)
            {
                var layout = new VerticalStackLayout { Spacing = 4 };
                layout.Children.Add(new Label { Text = $"Location: {row.Location}", FontAttributes = FontAttributes.Bold });
                layout.Children.Add(new Label { Text = $"{row.Title} [{row.Type}]" });
                layout.Children.Add(new Label { Text = row.Detail, TextColor = Colors.DarkSlateGray });
                section.Children.Add(Card(layout));
            }
        }

        _dataContent.Children.Add(section);
    }

    private void AddLocationTreeNode(Layout section, LocationTreeNodeItem node, int depth)
    {
        var layout = new VerticalStackLayout { Spacing = 4, Margin = new Thickness(depth * 16, 0, 0, 8) };
        layout.Children.Add(new Label
        {
            Text = $"{node.Location.Name} ({node.Location.Type})",
            FontAttributes = FontAttributes.Bold
        });

        if (!string.IsNullOrWhiteSpace(node.Location.Description))
        {
            layout.Children.Add(new Label { Text = node.Location.Description, TextColor = Colors.DarkSlateGray });
        }

        var actions = new HorizontalStackLayout { Spacing = 8 };
        actions.Children.Add(Button("Edit", () =>
        {
            StartLocationEdit(node.Location);
            return Task.CompletedTask;
        }));
        layout.Children.Add(actions);
        section.Children.Add(Card(layout));

        foreach (var child in node.Children)
        {
            AddLocationTreeNode(section, child, depth + 1);
        }
    }

    private async Task LoadDataAsync()
    {
        SetStatus("Loading pantry data...", isError: false);

        try
        {
            var snapshot = await LoadPantrySnapshotAsync();

            _healthLabel.Text = "Health: local app services";
            _versionLabel.Text = $"Version: {typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0"}";
            Replace(_summary, snapshot.Summary);
            Replace(_durableItems, snapshot.DurableItems);
            Replace(_consumableEntries, snapshot.ConsumableEntries);
            Replace(_expiringEntries, snapshot.ExpiringEntries);
            Replace(_suggestions, snapshot.Suggestions);
            Replace(_shoppingListItems, snapshot.ShoppingListItems);
            Replace(_rules, snapshot.Rules);
            Replace(_locations, snapshot.Locations);
            Replace(_locationTree, snapshot.LocationTree);
            SetStatus(string.Empty, isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", isError: true);
        }

        RefreshItemPicker();
        RefreshLocationParentPicker();
        RebuildDataSections();
    }

    private Task<PantrySnapshot> LoadPantrySnapshotAsync()
    {
        return Task.Run(() =>
        {
            var todayUtc = _utcDateProvider.TodayUtc;
            var rules = _store.GetRules();
            var entries = _store.GetConsumableEntries();

            return new PantrySnapshot(
                ProjectList<InventorySummaryItem>(_store.GetSummary()),
                _store.GetDurableEntries().Select(ToMobileDurableItem).ToList(),
                _store.GetConsumableEntryReadModels(todayUtc).Select(ToMobileEntry).ToList(),
                _store.GetExpiringConsumableEntries(todayUtc).Select(ToMobileEntry).ToList(),
                _suggestionService.GetSuggestions(rules, entries).Select(ToMobileSuggestion).ToList(),
                ProjectList<ShoppingListItem>(_store.GetShoppingListItems()),
                rules.Select(ToMobileRule).ToList(),
                _store.GetLocations().Select(ToMobileLocation).ToList(),
                _store.GetLocationTree().Select(ToMobileLocationTreeNode).ToList());
        });
    }

    private void AddCreateItemSection()
    {
        var section = Section("Create Consumable Item");
        section.Children.Add(_itemNameEntry);
        section.Children.Add(_desiredAmountEntry);
        section.Children.Add(_desiredUnitEntry);
        section.Children.Add(Button("Create Item", CreateItemAsync));
        _dataContent.Children.Add(section);
    }

    private void AddEntrySection()
    {
        RefreshItemPicker();
        RefreshStorageSlotPicker(_entryStorageSlotPicker, "new-entry", _selectedEntryStorageSlotId, selectedId => _selectedEntryStorageSlotId = selectedId);

        var section = Section("Add Consumable Entry");
        section.Children.Add(_entryItemPicker);
        section.Children.Add(_quantityEntry);
        section.Children.Add(_unitEntry);
        section.Children.Add(_expiryDateEntry);
        section.Children.Add(_entryStorageSlotPicker);
        section.Children.Add(Button("Add Entry", AddConsumableEntryAsync));
        _dataContent.Children.Add(section);
    }

    private void AddDurableFormSection()
    {
        var section = Section("Create Durable Item");
        RefreshStorageSlotPicker(_durableStorageSlotPicker, "durable-form", _selectedDurableStorageSlotId, selectedId => _selectedDurableStorageSlotId = selectedId);
        section.Children.Add(_durableNameEntry);
        section.Children.Add(GridRows(_durableTypeEntry, _durableStatusPicker));
        section.Children.Add(_durableLocationEntry);
        section.Children.Add(GridRows(_durableBrandEntry, _durableModelEntry));
        section.Children.Add(_durableSerialEntry);
        section.Children.Add(GridRows(_durablePurchaseDateEntry, _durablePurchaseValueEntry));
        section.Children.Add(_durableWarrantyEntry);
        section.Children.Add(_durableDescriptionEntry);
        section.Children.Add(_durableNotesEntry);
        section.Children.Add(_durableStorageSlotPicker);
        section.Children.Add(_durableSaveButton);
        section.Children.Add(_durableCancelButton);
        _dataContent.Children.Add(section);
    }

    private void AddDurableItemsSection()
    {
        var section = Section("Equipment");
        var visibleItems = _durableItems
            .Where(item => (_durableStatusFilter == "All" || item.Status == _durableStatusFilter)
                && (_durableLocationFilter == "All" || DurableLocation(item) == _durableLocationFilter))
            .ToList();

        if (_durableItems.Count == 0)
        {
            section.Children.Add(new Label { Text = "No durable items." });
        }
        else if (visibleItems.Count == 0)
        {
            section.Children.Add(new Label { Text = "No durable items match the filters." });
        }
        else
        {
            foreach (var item in visibleItems)
            {
                section.Children.Add(DurableItemCard(item));
            }
        }

        section.Children.Add(new Label
        {
            Text = "Durable Item Detail",
            FontAttributes = FontAttributes.Bold
        });
        section.Children.Add(_durableDetailContent);
        RefreshDurableDetail();

        _dataContent.Children.Add(section);
    }

    private View DurableItemCard(DurableItem item)
    {
        var location = DurableLocation(item);
        var layout = new VerticalStackLayout { Spacing = 6 };
        layout.Children.Add(new Label
        {
            Text = $"{item.DisplayName}: {DurableType(item)} [{item.Status}]",
            FontAttributes = FontAttributes.Bold
        });
        layout.Children.Add(new Label { Text = $"Location: {location} | {WarrantyIndicator(item)}" });

        var actions = new HorizontalStackLayout { Spacing = 8 };
        actions.Children.Add(Button("Open Details", () =>
        {
            _selectedDurableEntryId = item.Id;
            RefreshDurableDetail();
            return Task.CompletedTask;
        }));
        actions.Children.Add(Button("Edit", () =>
        {
            StartDurableEdit(item);
            return Task.CompletedTask;
        }));

        if (!string.Equals(item.Status, DurableItemStatus.Retired.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            actions.Children.Add(Button("Retire", async () => await RetireDurableItemAsync(item)));
        }

        layout.Children.Add(actions);
        return Card(layout);
    }

    private void RefreshDurableDetail()
    {
        _durableDetailContent.Children.Clear();
        var selected = _durableItems.FirstOrDefault(item => item.Id == _selectedDurableEntryId);
        _durableDetailContent.Children.Add(selected is null
            ? new Label { Text = "Open a durable item to view details." }
            : DurableDetailCard(selected));
    }

    private View DurableDetailCard(DurableItem item)
    {
        var layout = new VerticalStackLayout { Spacing = 4 };
        layout.Children.Add(new Label { Text = item.DisplayName, FontAttributes = FontAttributes.Bold });
        layout.Children.Add(new Label { Text = $"Type: {DurableType(item)}" });
        layout.Children.Add(new Label { Text = $"Status: {item.Status}" });
        layout.Children.Add(new Label { Text = $"Location: {DurableLocation(item)}" });
        layout.Children.Add(new Label { Text = $"Brand / manufacturer: {NotRecorded(item.BrandManufacturer)}" });
        layout.Children.Add(new Label { Text = $"Model: {NotRecorded(item.Model)}" });
        layout.Children.Add(new Label { Text = $"Serial number: {NotRecorded(item.SerialNumber)}" });
        layout.Children.Add(new Label { Text = $"Purchase date: {NotRecorded(item.PurchaseDate)}" });
        layout.Children.Add(new Label { Text = $"Purchase value: {(item.PurchaseValue.HasValue ? item.PurchaseValue.Value.ToString(CultureInfo.InvariantCulture) : "Not recorded")}" });
        layout.Children.Add(new Label { Text = WarrantyIndicator(item) });
        layout.Children.Add(new Label { Text = $"Description: {NotRecorded(item.Description)}" });
        layout.Children.Add(new Label { Text = $"Notes: {NotRecorded(item.Notes)}" });
        return Card(layout);
    }

    private void AddInventorySummarySection()
    {
        var section = Section("Inventory Summary");
        var visibleItems = _summary
            .Where(item => _inventoryLocationFilter == "All" && _expiryStatusFilter == "All"
                || _consumableEntries.Any(entry => entry.ItemDefinitionId == item.ItemDefinitionId && MatchesInventoryFilters(entry)))
            .ToList();

        if (_summary.Count == 0)
        {
            section.Children.Add(new Label { Text = "No inventory yet." });
        }
        else if (visibleItems.Count == 0)
        {
            section.Children.Add(new Label { Text = "No inventory matches the filters." });
        }
        else
        {
            foreach (var item in visibleItems)
            {
                var text = item.HasMixedUnits
                    ? $"{item.ItemName}: mixed units ({item.EntryCount} entries)"
                    : $"{item.ItemName}: {item.TotalQuantity?.ToString(CultureInfo.InvariantCulture) ?? "0"} {item.Unit} ({item.EntryCount} entries)";

                if (!string.IsNullOrWhiteSpace(item.MixedUnitWarning))
                {
                    text += $" - {item.MixedUnitWarning}";
                }

                section.Children.Add(Button(text, () =>
                {
                    _selectedConsumableItemDefinitionId = item.ItemDefinitionId;
                    RebuildDataSections();
                    return Task.CompletedTask;
                }));
            }
        }

        _dataContent.Children.Add(section);
    }

    private void AddConsumableAuditSection()
    {
        var section = Section("Consumable Entry Audit");
        var visibleEntries = _consumableEntries.Where(MatchesInventoryFilters).ToList();

        if (_consumableEntries.Count == 0)
        {
            section.Children.Add(new Label { Text = "No consumable entries." });
        }
        else if (visibleEntries.Count == 0)
        {
            section.Children.Add(new Label { Text = "No consumable entries match the filters." });
        }
        else
        {
            foreach (var entry in visibleEntries)
            {
                section.Children.Add(EntryCard(entry));
            }
        }

        _dataContent.Children.Add(section);
    }

    private View EntryCard(ConsumableEntry entry)
    {
        var quantity = new Entry
        {
            Text = entry.Quantity.ToString(CultureInfo.InvariantCulture),
            Keyboard = Keyboard.Numeric,
            Placeholder = "Quantity"
        };
        var unit = new Entry { Text = entry.Unit, Placeholder = "Unit" };
        var expiry = new Entry { Text = entry.ExpiresOn ?? string.Empty, Placeholder = "Expiry date YYYY-MM-DD" };
        var selectedStorageSlotId = entry.StorageSlotId;
        var storageSlotPicker = new VerticalStackLayout { Spacing = 6 };
        RefreshStorageSlotPicker(storageSlotPicker, $"entry-{entry.EntryId}", selectedStorageSlotId, selectedId => selectedStorageSlotId = selectedId);
        var addStock = new Entry { Placeholder = "Add stock amount", Keyboard = Keyboard.Numeric };
        var addStockUnit = new Entry { Text = entry.Unit, Placeholder = "Add stock unit" };
        var consumeStock = new Entry { Placeholder = "Consume stock amount", Keyboard = Keyboard.Numeric };
        var consumeStockUnit = new Entry { Text = entry.Unit, Placeholder = "Consume stock unit" };

        var layout = new VerticalStackLayout { Spacing = 8 };
        layout.Children.Add(new Label { Text = entry.ItemName, FontAttributes = FontAttributes.Bold });
        layout.Children.Add(new Label { Text = $"Lot {entry.EntryId}" });
        layout.Children.Add(new Label
        {
            Text = $"Quantity: {entry.Quantity.ToString(CultureInfo.InvariantCulture)} {entry.Unit} | Expiry: {entry.ExpiresOn ?? "No expiry"} ({entry.ExpiryStatus}) | Location: {DisplayLocation(entry.StorageSlotId)}"
        });
        layout.Children.Add(GridRows(addStock, addStockUnit));
        layout.Children.Add(GridRows(
            Button("Add Stock", async () => await AdjustStockAsync(entry, addStock.Text, addStockUnit.Text, isAdd: true)),
            Button("Mark Lot Depleted", async () => await MarkLotDepletedAsync(entry))));
        layout.Children.Add(GridRows(consumeStock, consumeStockUnit));
        layout.Children.Add(Button("Consume Stock", async () => await AdjustStockAsync(entry, consumeStock.Text, consumeStockUnit.Text, isAdd: false)));
        layout.Children.Add(GridRows(quantity, unit));
        layout.Children.Add(expiry);
        layout.Children.Add(storageSlotPicker);
        layout.Children.Add(GridRows(
            Button("Save Lot", async () => await SaveEntryAsync(entry, quantity.Text, unit.Text, expiry.Text, selectedStorageSlotId)),
            Button("Undo Unsaved Changes", async () => await LoadDataAsync())));

        return Card(layout, ExpiryColor(entry.ExpiryStatus));
    }

    private void AddExpiringSection()
    {
        var section = Section("Expiring Consumables");
        var visibleEntries = _expiringEntries.Where(MatchesInventoryFilters).ToList();

        if (_expiringEntries.Count == 0)
        {
            section.Children.Add(new Label { Text = "No expiring consumables." });
        }
        else if (visibleEntries.Count == 0)
        {
            section.Children.Add(new Label { Text = "No expiring consumables match the filters." });
        }
        else
        {
            foreach (var entry in visibleEntries)
            {
                section.Children.Add(new Label
                {
                    Text = $"{entry.ItemName}: {entry.Quantity.ToString(CultureInfo.InvariantCulture)} {entry.Unit}, expires {entry.ExpiresOn ?? "unknown"} ({entry.ExpiryStatus})"
                });
            }
        }

        _dataContent.Children.Add(section);
    }

    private void AddSuggestionsSection()
    {
        var section = Section("Replenishment Suggestions");

        if (_suggestions.Count == 0)
        {
            section.Children.Add(new Label { Text = "No replenishment suggestions." });
        }
        else
        {
            foreach (var suggestion in _suggestions)
            {
                var breakdown = SuggestionBreakdown(suggestion);
                var layout = new VerticalStackLayout { Spacing = 8 };
                layout.Children.Add(new Label { Text = suggestion.ItemName, FontAttributes = FontAttributes.Bold });
                layout.Children.Add(new Label
                {
                    Text = $"Buy {suggestion.SuggestedPurchaseAmount.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}; desired {suggestion.DesiredQuantity.ToString(CultureInfo.InvariantCulture)}, usable {suggestion.UsableCurrentQuantity.ToString(CultureInfo.InvariantCulture)}, deficit {suggestion.DeficitAmount.ToString(CultureInfo.InvariantCulture)}, expiring soon {suggestion.ExpiringSoonAmount.ToString(CultureInfo.InvariantCulture)}"
                });
                layout.Children.Add(WithHelp(new Label
                {
                    Text = "Breakdown",
                    TextColor = Colors.DarkBlue,
                    TextDecorations = TextDecorations.Underline
                }, breakdown));
                layout.Children.Add(Button("Add To Shopping List", async () => await CreateShoppingItemFromSuggestionAsync(suggestion)));
                section.Children.Add(Card(layout));
            }
        }

        _dataContent.Children.Add(section);
    }

    private void AddShoppingSection()
    {
        var section = Section("Shopping List");
        var visibleItems = _shoppingListItems
            .Where(item => _shoppingStatusFilter == "All" || ShoppingStatus(item) == _shoppingStatusFilter)
            .ToList();

        if (_shoppingListItems.Count == 0)
        {
            section.Children.Add(new Label { Text = "No shopping list items." });
        }
        else if (visibleItems.Count == 0)
        {
            section.Children.Add(new Label { Text = "No shopping items match the filter." });
        }
        else
        {
            foreach (var item in visibleItems)
            {
                var layout = new VerticalStackLayout { Spacing = 8 };
                layout.Children.Add(new Label { Text = item.ItemName, FontAttributes = FontAttributes.Bold });
                layout.Children.Add(new Label
                {
                    Text = $"{item.Quantity.ToString(CultureInfo.InvariantCulture)} {item.Unit} - {ShoppingStatusLabel(item)}"
                });

                if (!string.IsNullOrWhiteSpace(item.NextInventoryAction))
                {
                    layout.Children.Add(new Label { Text = item.NextInventoryAction });
                }

                layout.Children.Add(ShoppingButtons(item));
                section.Children.Add(Card(layout));
            }
        }

        _dataContent.Children.Add(section);
    }

    private View ShoppingButtons(ShoppingListItem item)
    {
        var status = ShoppingStatus(item);
        var row = new HorizontalStackLayout { Spacing = 8 };

        if (status == "ShoppingList")
        {
            row.Children.Add(Button("Move To Cart", async () => await UpdateShoppingStatusAsync(item.Id, "InCart")));
        }

        if (status is "ShoppingList" or "InCart")
        {
            row.Children.Add(Button("Mark Bought", async () => await UpdateShoppingStatusAsync(item.Id, "Bought")));
        }

        if (status is "Bought" or "StockUpdateNeeded")
        {
            row.Children.Add(Button("Remove Bought Item", async () => await DeleteShoppingItemAsync(item.Id)));
        }

        return row;
    }

    private void AddRulesSection()
    {
        var section = Section("Replenishment Rules");

        if (_rules.Count == 0)
        {
            section.Children.Add(new Label { Text = "No replenishment rules." });
        }
        else
        {
            foreach (var rule in _rules)
            {
                section.Children.Add(RuleCard(rule));
            }
        }

        _dataContent.Children.Add(section);
    }

    private View RuleCard(ReplenishmentRule rule)
    {
        var itemName = RuleItemName(rule);
        var desiredAmount = new Entry
        {
            Text = rule.DesiredAmount.ToString(CultureInfo.InvariantCulture),
            Keyboard = Keyboard.Numeric,
            Placeholder = "Target amount"
        };
        var desiredUnit = new Entry { Text = rule.DesiredUnit, Placeholder = "Target unit" };
        var warningDays = new Entry
        {
            Text = rule.ExpiryWarningDays.ToString(CultureInfo.InvariantCulture),
            Keyboard = Keyboard.Numeric,
            Placeholder = "Expiry warning days"
        };
        var disabled = new CheckBox { IsChecked = rule.IsDisabled };

        var layout = new VerticalStackLayout { Spacing = 8 };
        layout.Children.Add(new Label { Text = $"Item: {itemName}", FontAttributes = FontAttributes.Bold });
        layout.Children.Add(GridRows(
            LabeledField("Target amount", desiredAmount, $"Target amount for {itemName}", TargetAmountHelp),
            LabeledField("Target unit", desiredUnit, $"Target unit for {itemName}")));
        layout.Children.Add(LabeledField("Expiry warning days", warningDays, $"Expiry warning days for {itemName}", ExpiryWarningDaysHelp));
        layout.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Disabled", VerticalOptions = LayoutOptions.Center },
                WithDescription(disabled, $"Disable replenishment rule for {itemName}")
            }
        });
        layout.Children.Add(WithDescription(
            Button("Save", async () => await SaveRuleAsync(rule, desiredAmount.Text, desiredUnit.Text, warningDays.Text, disabled.IsChecked)),
            $"Save replenishment rule for {itemName}"));

        return Card(layout);
    }

    private async Task CreateItemAsync()
    {
        var name = (_itemNameEntry.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Item name is required.", isError: true);
            return;
        }

        var desiredAmount = ParseNullableDecimal(_desiredAmountEntry.Text);
        if (desiredAmount is null && !string.IsNullOrWhiteSpace(_desiredAmountEntry.Text))
        {
            SetStatus("Desired amount must be a number.", isError: true);
            return;
        }

        try
        {
            var created = _store.CreateItemDefinition(
                name,
                ItemKind.Consumable,
                desiredAmount,
                BlankToNull(_desiredUnitEntry.Text));

            _createdItems.RemoveAll(item => item.Id == created.Id.ToString());
            _createdItems.Insert(0, new ItemOption(created.Id.ToString(), created.Name));
            _itemNameEntry.Text = string.Empty;
            _desiredAmountEntry.Text = string.Empty;
            _desiredUnitEntry.Text = string.Empty;
            await LoadDataAsync();
            SelectItem(created.Id.ToString());
        }
        catch (Exception ex)
        {
            SetStatus($"Create item failed: {ex.Message}", isError: true);
        }
    }

    private async Task AddConsumableEntryAsync()
    {
        if (_entryItemPicker.SelectedItem is not ItemOption option)
        {
            SetStatus("Select an item before adding an entry.", isError: true);
            return;
        }

        var quantity = ParseRequiredDecimal(_quantityEntry.Text, "Quantity");
        if (quantity is null)
        {
            return;
        }

        var unit = (_unitEntry.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(unit))
        {
            SetStatus("Unit is required.", isError: true);
            return;
        }

        if (!Guid.TryParse(option.Id, out var itemDefinitionId))
        {
            SetStatus("Selected item is invalid.", isError: true);
            return;
        }

        if (!TryParseOptionalDate(_expiryDateEntry.Text, "Expiry date", out var expiresOn))
        {
            return;
        }

        var storageSlotId = ParseStoredGuid(_selectedEntryStorageSlotId);

        try
        {
            var created = _store.AddConsumableEntry(itemDefinitionId, quantity.Value, unit, expiresOn, storageSlotId);
            if (created is null)
            {
                SetStatus("Selected item was not found.", isError: true);
                return;
            }

            _quantityEntry.Text = string.Empty;
            _unitEntry.Text = string.Empty;
            _expiryDateEntry.Text = string.Empty;
            _selectedEntryStorageSlotId = null;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Add consumable entry failed: {ex.Message}", isError: true);
        }
    }

    private async Task SaveLocationAsync()
    {
        var name = (_locationNameEntry.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Location name is required.", isError: true);
            return;
        }

        if (!TryParseSelectedLocationType(out var type))
        {
            return;
        }

        Guid? parentLocationId = null;
        if (type != LocationType.House)
        {
            if (_locationParentPicker.SelectedItem is not ItemOption parentOption || !Guid.TryParse(parentOption.Id, out var parsedParentId))
            {
                SetStatus("Select a parent location.", isError: true);
                return;
            }

            parentLocationId = parsedParentId;
        }

        int? sortOrder = null;
        if (!string.IsNullOrWhiteSpace(_locationSortOrderEntry.Text))
        {
            sortOrder = ParseRequiredInt(_locationSortOrderEntry.Text, "Display order");
            if (sortOrder is null)
            {
                return;
            }
        }

        try
        {
            LocationReadModel? saved;
            if (Guid.TryParse(_editingLocationId, out var locationId))
            {
                saved = _store.UpdateLocation(
                    locationId,
                    name,
                    type,
                    parentLocationId,
                    BlankToNull(_locationDescriptionEntry.Text),
                    sortOrder);

                if (saved is null)
                {
                    SetStatus("Location was not found.", isError: true);
                    return;
                }
            }
            else
            {
                saved = _store.CreateLocation(
                    name,
                    type,
                    parentLocationId,
                    BlankToNull(_locationDescriptionEntry.Text),
                    sortOrder);
            }

            ClearLocationForm();
            SetStatus($"Saved location {saved.Name}.", isError: false);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Save location failed: {ex.Message}", isError: true);
        }
    }

    private async Task SaveDurableItemAsync()
    {
        var displayName = (_durableNameEntry.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            SetStatus("Durable item name is required.", isError: true);
            return;
        }

        if (!TryParseSelectedDurableStatus(out var status) ||
            !TryParseOptionalDate(_durablePurchaseDateEntry.Text, "Purchase date", out var purchaseDate) ||
            !TryParseOptionalDate(_durableWarrantyEntry.Text, "Warranty end", out var warrantyEndsOn))
        {
            return;
        }

        var storageSlotId = ParseStoredGuid(_selectedDurableStorageSlotId);

        var purchaseValue = ParseNullableDecimal(_durablePurchaseValueEntry.Text);
        if (purchaseValue is null && !string.IsNullOrWhiteSpace(_durablePurchaseValueEntry.Text))
        {
            SetStatus("Purchase value must be a number.", isError: true);
            return;
        }

        try
        {
            DurableItemReadModel? saved;

            if (Guid.TryParse(_editingDurableEntryId, out var entryId))
            {
                saved = _store.UpdateDurableEntry(
                    entryId,
                    displayName,
                    BlankToNull(_durableDescriptionEntry.Text),
                    BlankToNull(_durableTypeEntry.Text),
                    BlankToNull(_durableBrandEntry.Text),
                    BlankToNull(_durableModelEntry.Text),
                    BlankToNull(_durableSerialEntry.Text),
                    purchaseDate,
                    purchaseValue,
                    warrantyEndsOn,
                    status,
                    BlankToNull(_durableLocationEntry.Text),
                    BlankToNull(_durableNotesEntry.Text),
                    storageSlotId);

                if (saved is null)
                {
                    SetStatus("Durable item was not found.", isError: true);
                    return;
                }
            }
            else
            {
                saved = _store.CreateDurableItem(
                    displayName,
                    BlankToNull(_durableDescriptionEntry.Text),
                    BlankToNull(_durableTypeEntry.Text),
                    BlankToNull(_durableBrandEntry.Text),
                    BlankToNull(_durableModelEntry.Text),
                    BlankToNull(_durableSerialEntry.Text),
                    purchaseDate,
                    purchaseValue,
                    warrantyEndsOn,
                    status,
                    BlankToNull(_durableLocationEntry.Text),
                    BlankToNull(_durableNotesEntry.Text),
                    storageSlotId);
            }

            _selectedDurableEntryId = saved.Id.ToString();
            ClearDurableForm();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Save durable item failed: {ex.Message}", isError: true);
        }
    }

    private async Task RetireDurableItemAsync(DurableItem item)
    {
        var confirmed = await DisplayAlert("Retire durable item", $"Retire {item.DisplayName}?", "Retire", "Cancel");
        if (!confirmed)
        {
            return;
        }

        if (!Guid.TryParse(item.Id, out var entryId))
        {
            SetStatus("Durable item ID is invalid.", isError: true);
            return;
        }

        var retired = _store.RetireDurableEntry(entryId, item.Notes);
        if (retired is null)
        {
            SetStatus("Durable item was not found.", isError: true);
            return;
        }

        _selectedDurableEntryId = retired.Id.ToString();
        await LoadDataAsync();
    }

    private async Task SaveEntryAsync(ConsumableEntry entry, string? quantityText, string? unitText, string? expiryText, string? storageSlotText)
    {
        var quantity = ParseRequiredDecimal(quantityText, "Quantity");
        if (quantity is null)
        {
            return;
        }

        var unit = (unitText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(unit))
        {
            SetStatus("Quantity and unit are required.", isError: true);
            return;
        }

        if (!TryParseOptionalDate(expiryText, "Expiry date", out var expiresOn))
        {
            return;
        }

        var storageSlotId = ParseStoredGuid(storageSlotText);
        await PatchEntryAsync(entry.EntryId, quantity.Value, unit, expiresOn, storageSlotId);
    }

    private async Task AdjustStockAsync(ConsumableEntry entry, string? amountText, string? unitText, bool isAdd)
    {
        var amount = ParseRequiredDecimal(amountText, isAdd ? "Add stock amount" : "Consume amount");
        if (amount is null)
        {
            return;
        }

        if (amount <= 0)
        {
            SetStatus(isAdd ? "Add stock amount must be greater than zero." : "Consume amount must be greater than zero.", isError: true);
            return;
        }

        var unit = (unitText ?? string.Empty).Trim();
        if (!string.Equals(unit, entry.Unit, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus($"{(isAdd ? "Add stock" : "Consume stock")} unit must match the lot unit {entry.Unit}.", isError: true);
            return;
        }

        var nextQuantity = isAdd ? entry.Quantity + amount.Value : entry.Quantity - amount.Value;
        if (nextQuantity < 0)
        {
            SetStatus("Consume amount cannot exceed the lot quantity.", isError: true);
            return;
        }

        await PatchEntryAsync(entry.EntryId, nextQuantity, entry.Unit, ParseStoredDate(entry.ExpiresOn), ParseStoredGuid(entry.StorageSlotId));
    }

    private async Task MarkLotDepletedAsync(ConsumableEntry entry)
    {
        var confirmed = await DisplayAlert("Mark depleted", $"Mark {entry.ItemName} lot {entry.EntryId} as depleted?", "Mark Depleted", "Cancel");
        if (!confirmed)
        {
            return;
        }

        await PatchEntryAsync(entry.EntryId, 0, entry.Unit, ParseStoredDate(entry.ExpiresOn), ParseStoredGuid(entry.StorageSlotId));
    }

    private async Task PatchEntryAsync(string entryId, decimal quantity, string unit, DateOnly? expiresOn, Guid? storageSlotId)
    {
        try
        {
            if (!Guid.TryParse(entryId, out var parsedEntryId))
            {
                SetStatus("Consumable entry ID is invalid.", isError: true);
                return;
            }

            var updated = _store.UpdateConsumableEntry(parsedEntryId, quantity, unit, expiresOn, storageSlotId, _utcDateProvider.TodayUtc);
            if (updated is null)
            {
                SetStatus("Consumable entry was not found.", isError: true);
                return;
            }

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Update consumable entry failed: {ex.Message}", isError: true);
        }
    }

    private async Task CreateShoppingItemFromSuggestionAsync(ReplenishmentSuggestion suggestion)
    {
        try
        {
            if (!Guid.TryParse(suggestion.ItemDefinitionId, out var itemDefinitionId))
            {
                SetStatus("Suggestion item ID is invalid.", isError: true);
                return;
            }

            _store.CreateOrUpdateShoppingListItemFromSuggestion(
                itemDefinitionId,
                suggestion.SuggestedPurchaseAmount,
                suggestion.Unit,
                suggestion.DeficitAmount,
                suggestion.ExpiringSoonAmount,
                suggestion.SuggestedPurchaseAmount);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Create shopping list item failed: {ex.Message}", isError: true);
        }
    }

    private async Task UpdateShoppingStatusAsync(string shoppingListItemId, string status)
    {
        try
        {
            if (!Guid.TryParse(shoppingListItemId, out var parsedShoppingListItemId))
            {
                SetStatus("Shopping list item ID is invalid.", isError: true);
                return;
            }

            var updated = _store.UpdateShoppingListItemStatus(parsedShoppingListItemId, null, null, status);
            if (updated is null)
            {
                SetStatus("Shopping list item was not found.", isError: true);
                return;
            }

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Update shopping list item failed: {ex.Message}", isError: true);
        }
    }

    private async Task DeleteShoppingItemAsync(string shoppingListItemId)
    {
        try
        {
            if (!Guid.TryParse(shoppingListItemId, out var parsedShoppingListItemId))
            {
                SetStatus("Shopping list item ID is invalid.", isError: true);
                return;
            }

            if (!_store.DeleteShoppingListItem(parsedShoppingListItemId))
            {
                SetStatus("Shopping list item was not found.", isError: true);
                return;
            }

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Delete shopping list item failed: {ex.Message}", isError: true);
        }
    }

    private async Task SaveRuleAsync(ReplenishmentRule rule, string? desiredAmountText, string? desiredUnitText, string? expiryWarningDaysText, bool isDisabled)
    {
        var desiredAmount = ParseRequiredDecimal(desiredAmountText, "Target amount");
        if (desiredAmount is null)
        {
            return;
        }

        var expiryWarningDays = ParseRequiredInt(expiryWarningDaysText, "Expiry warning days");
        if (expiryWarningDays is null)
        {
            return;
        }

        try
        {
            if (!Guid.TryParse(rule.Id, out var parsedRuleId))
            {
                SetStatus("Replenishment rule ID is invalid.", isError: true);
                return;
            }

            var updated = _store.UpdateRule(parsedRuleId, desiredAmount, (desiredUnitText ?? string.Empty).Trim(), isDisabled, expiryWarningDays);
            if (updated is null)
            {
                SetStatus("Replenishment rule was not found.", isError: true);
                return;
            }

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Update rule failed: {ex.Message}", isError: true);
        }
    }

    private void StartDurableEdit(DurableItem item)
    {
        _editingDurableEntryId = item.Id;
        _selectedDurableEntryId = item.Id;
        _durableNameEntry.Text = item.DisplayName;
        _durableTypeEntry.Text = item.ItemType;
        _durableStatusPicker.SelectedItem = item.Status;
        _durableLocationEntry.Text = item.CurrentLocation;
        _durableBrandEntry.Text = item.BrandManufacturer;
        _durableModelEntry.Text = item.Model;
        _durableSerialEntry.Text = item.SerialNumber;
        _durablePurchaseDateEntry.Text = item.PurchaseDate;
        _durablePurchaseValueEntry.Text = item.PurchaseValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _durableWarrantyEntry.Text = item.WarrantyEndsOn;
        _durableDescriptionEntry.Text = item.Description;
        _durableNotesEntry.Text = item.Notes;
        _selectedDurableStorageSlotId = item.StorageSlotId;
        _durableSaveButton.Text = "Save Durable Item";
        _durableCancelButton.IsVisible = true;
    }

    private void StartLocationEdit(LocationItem location)
    {
        _editingLocationId = location.Id;
        _locationNameEntry.Text = location.Name;
        _locationTypePicker.SelectedItem = location.Type;
        _locationDescriptionEntry.Text = location.Description;
        _locationSortOrderEntry.Text = location.SortOrder?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _locationSaveButton.Text = "Save Location";
        _locationCancelButton.IsVisible = true;
        RefreshLocationParentPicker(location.ParentLocationId);
        RebuildDataSections();
    }

    private void ClearDurableForm()
    {
        _editingDurableEntryId = null;
        _durableNameEntry.Text = string.Empty;
        _durableTypeEntry.Text = string.Empty;
        _durableStatusPicker.SelectedItem = DurableItemStatus.Active.ToString();
        _durableLocationEntry.Text = string.Empty;
        _durableBrandEntry.Text = string.Empty;
        _durableModelEntry.Text = string.Empty;
        _durableSerialEntry.Text = string.Empty;
        _durablePurchaseDateEntry.Text = string.Empty;
        _durablePurchaseValueEntry.Text = string.Empty;
        _durableWarrantyEntry.Text = string.Empty;
        _durableDescriptionEntry.Text = string.Empty;
        _durableNotesEntry.Text = string.Empty;
        _selectedDurableStorageSlotId = null;
        _durableSaveButton.Text = "Create Durable Item";
        _durableCancelButton.IsVisible = false;
    }

    private void ClearLocationForm()
    {
        _editingLocationId = null;
        _locationNameEntry.Text = string.Empty;
        _locationTypePicker.SelectedItem = LocationType.House.ToString();
        _locationDescriptionEntry.Text = string.Empty;
        _locationSortOrderEntry.Text = string.Empty;
        _locationSaveButton.Text = "Create Location";
        _locationCancelButton.IsVisible = false;
        RefreshLocationParentPicker();
    }

    private void DetachReusableControls()
    {
        View[] controls =
        [
            _itemNameEntry,
            _desiredAmountEntry,
            _desiredUnitEntry,
            _entryItemPicker,
            _quantityEntry,
            _unitEntry,
            _expiryDateEntry,
            _entryStorageSlotPicker,
            _durableNameEntry,
            _durableTypeEntry,
            _durableStatusPicker,
            _durableLocationEntry,
            _durableBrandEntry,
            _durableModelEntry,
            _durableSerialEntry,
            _durablePurchaseDateEntry,
            _durablePurchaseValueEntry,
            _durableWarrantyEntry,
            _durableDescriptionEntry,
            _durableNotesEntry,
            _durableStorageSlotPicker,
            _durableSaveButton,
            _durableCancelButton,
            _durableDetailContent,
            _locationNameEntry,
            _locationTypePicker,
            _locationParentPicker,
            _locationDescriptionEntry,
            _locationSortOrderEntry,
            _locationSaveButton,
            _locationCancelButton
        ];

        foreach (var control in controls)
        {
            if (control.Parent is Layout parent)
            {
                parent.Children.Remove(control);
            }
        }
    }

    private bool TryParseSelectedDurableStatus(out DurableItemStatus status)
    {
        var value = _durableStatusPicker.SelectedItem as string ?? DurableItemStatus.Active.ToString();
        if (Enum.TryParse(value, ignoreCase: true, out status) &&
            Enum.IsDefined(typeof(DurableItemStatus), status))
        {
            return true;
        }

        SetStatus("Durable item status is invalid.", isError: true);
        return false;
    }

    private bool TryParseSelectedLocationType(out LocationType type)
    {
        var value = _locationTypePicker.SelectedItem as string ?? LocationType.House.ToString();
        if (Enum.TryParse(value, ignoreCase: true, out type) &&
            Enum.IsDefined(typeof(LocationType), type))
        {
            return true;
        }

        SetStatus("Location type is invalid.", isError: true);
        return false;
    }

    private void RefreshItemPicker()
    {
        var selectedId = (_entryItemPicker.SelectedItem as ItemOption)?.Id;
        var options = _createdItems
            .Concat(_summary
                .Where(item => _createdItems.All(created => created.Id != item.ItemDefinitionId))
                .Select(item => new ItemOption(item.ItemDefinitionId, item.ItemName)))
            .ToList();

        _entryItemPicker.ItemsSource = options;
        SelectItem(selectedId);
    }

    private void SelectItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || _entryItemPicker.ItemsSource is not IEnumerable<ItemOption> options)
        {
            return;
        }

        _entryItemPicker.SelectedItem = options.FirstOrDefault(option => option.Id == itemId);
    }

    private void RefreshLocationParentPicker(string? selectedParentId = null)
    {
        selectedParentId ??= (_locationParentPicker.SelectedItem as ItemOption)?.Id;

        if (!TryParseSelectedLocationType(out var selectedType))
        {
            _locationParentPicker.ItemsSource = new List<ItemOption>();
            _locationParentPicker.IsEnabled = false;
            return;
        }

        if (selectedType == LocationType.House)
        {
            _locationParentPicker.ItemsSource = new List<ItemOption>();
            _locationParentPicker.SelectedItem = null;
            _locationParentPicker.IsEnabled = false;
            return;
        }

        var parentType = selectedType switch
        {
            LocationType.Room => LocationType.House,
            LocationType.StorageUnit => LocationType.Room,
            LocationType.StorageSlot => LocationType.StorageUnit,
            _ => LocationType.House
        };

        var options = _locations
            .Where(location => string.Equals(location.Type, parentType.ToString(), StringComparison.OrdinalIgnoreCase)
                && !IsLocationOrDescendant(location.Id, _editingLocationId))
            .OrderBy(location => location.SortOrder ?? int.MaxValue)
            .ThenBy(location => location.Name, StringComparer.OrdinalIgnoreCase)
            .Select(location => new ItemOption(location.Id, location.Name))
            .ToList();

        _locationParentPicker.ItemsSource = options;
        _locationParentPicker.IsEnabled = true;
        _locationParentPicker.SelectedItem = options.FirstOrDefault(option => option.Id == selectedParentId);
    }

    private bool IsLocationOrDescendant(string locationId, string? ancestorId)
    {
        if (string.IsNullOrWhiteSpace(ancestorId))
        {
            return false;
        }

        if (locationId == ancestorId)
        {
            return true;
        }

        var location = _locations.FirstOrDefault(candidate => candidate.Id == locationId);
        return location?.ParentLocationId is not null && IsLocationOrDescendant(location.ParentLocationId, ancestorId);
    }

    private void RefreshStorageSlotPicker(
        VerticalStackLayout target,
        string pickerKey,
        string? selectedStorageSlotId,
        Action<string?> onSelected)
    {
        target.Children.Clear();
        target.Children.Add(new Label { Text = "Storage slot", FontAttributes = FontAttributes.Bold });
        target.Children.Add(new Label { Text = $"Selected: {(string.IsNullOrWhiteSpace(selectedStorageSlotId) ? "No storage slot selected" : DisplayLocation(selectedStorageSlotId))}" });
        target.Children.Add(Button("No storage slot selected", () =>
        {
            onSelected(null);
            RefreshStorageSlotPicker(target, pickerKey, null, onSelected);
            return Task.CompletedTask;
        }));

        if (_locationTree.Count == 0)
        {
            target.Children.Add(new Label { Text = "No managed storage slots yet." });
            return;
        }

        foreach (var node in _locationTree)
        {
            AddStorageSlotTreeNode(target, pickerKey, node, depth: 0, selectedStorageSlotId, onSelected);
        }
    }

    private void AddStorageSlotTreeNode(
        Layout parent,
        string pickerKey,
        LocationTreeNodeItem node,
        int depth,
        string? selectedStorageSlotId,
        Action<string?> onSelected)
    {
        var isStorageSlot = string.Equals(node.Location.Type, LocationType.StorageSlot.ToString(), StringComparison.OrdinalIgnoreCase);
        var indent = new Thickness(depth * 16, 0, 0, 4);

        if (isStorageSlot)
        {
            var isSelected = node.Location.Id == selectedStorageSlotId;
            var selectButton = Button($"{(isSelected ? "Selected: " : string.Empty)}{node.Location.Name}", () =>
            {
                onSelected(node.Location.Id);
                RefreshStorageSlotPicker((VerticalStackLayout)parent, pickerKey, node.Location.Id, onSelected);
                return Task.CompletedTask;
            });
            selectButton.Margin = indent;
            parent.Children.Add(selectButton);
            return;
        }

        var isExpanded = StorageSlotPickerExpandedIds(pickerKey).Contains(node.Location.Id);
        var toggleButton = Button($"{(isExpanded ? "[-]" : "[+]")} {node.Location.Name} ({node.Location.Type})", () =>
        {
            ToggleStorageSlotPickerNode(pickerKey, node.Location.Id);
            RefreshStorageSlotPicker((VerticalStackLayout)parent, pickerKey, selectedStorageSlotId, onSelected);
            return Task.CompletedTask;
        });
        toggleButton.Margin = indent;
        parent.Children.Add(toggleButton);

        if (!isExpanded)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            AddStorageSlotTreeNode(parent, pickerKey, child, depth + 1, selectedStorageSlotId, onSelected);
        }
    }

    private HashSet<string> StorageSlotPickerExpandedIds(string pickerKey)
    {
        if (!_expandedStorageSlotPickerNodeIds.TryGetValue(pickerKey, out var expandedIds))
        {
            expandedIds = [];
            _expandedStorageSlotPickerNodeIds[pickerKey] = expandedIds;
        }

        return expandedIds;
    }

    private void ToggleStorageSlotPickerNode(string pickerKey, string locationId)
    {
        var expandedIds = StorageSlotPickerExpandedIds(pickerKey);
        if (!expandedIds.Add(locationId))
        {
            expandedIds.Remove(locationId);
        }
    }

    private decimal? ParseRequiredDecimal(string? value, string fieldName)
    {
        var parsed = ParseNullableDecimal(value);
        if (parsed is null)
        {
            SetStatus($"{fieldName} must be a number.", isError: true);
            return null;
        }

        if (parsed < 0)
        {
            SetStatus($"{fieldName} must be zero or greater.", isError: true);
            return null;
        }

        return parsed;
    }

    private static decimal? ParseNullableDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private int? ParseRequiredInt(string? value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            SetStatus($"{fieldName} must be a whole number.", isError: true);
            return null;
        }

        if (parsed < 0)
        {
            SetStatus($"{fieldName} must be zero or greater.", isError: true);
            return null;
        }

        return parsed;
    }

    private static void Replace<T>(List<T> target, List<T> source)
    {
        target.Clear();
        target.AddRange(source);
    }

    private bool MatchesInventoryFilters(ConsumableEntry entry)
    {
        return (_inventoryLocationFilter == "All" || entry.StorageSlotId == _inventoryLocationFilter)
            && (_expiryStatusFilter == "All" || entry.ExpiryStatus == _expiryStatusFilter);
    }

    private List<string> LocationOptions()
    {
        return [
            "All",
            .. _consumableEntries
                .Select(entry => DisplayLocation(entry.StorageSlotId))
                .Concat(_durableItems.Select(DurableLocation))
                .Concat(_locations.Select(location => location.Name))
                .Where(location => !string.IsNullOrWhiteSpace(location))
                .Select(location => location!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(location => location, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static string? BlankToNull(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private bool TryParseOptionalDate(string? value, string fieldName, out DateOnly? date)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            date = null;
            return true;
        }

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }

        date = null;
        SetStatus($"{fieldName} must use YYYY-MM-DD.", isError: true);
        return false;
    }

    private bool TryParseOptionalGuid(string? value, string fieldName, out Guid? id)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            id = null;
            return true;
        }

        if (Guid.TryParse(trimmed, out var parsed))
        {
            id = parsed;
            return true;
        }

        id = null;
        SetStatus($"{fieldName} must be a valid GUID.", isError: true);
        return false;
    }

    private static DateOnly? ParseStoredDate(string? value)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static Guid? ParseStoredGuid(string? value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private void SetStatus(string message, bool isError)
    {
        _statusLabel.Text = message;
        _statusLabel.TextColor = isError ? Colors.DarkRed : Colors.DarkGreen;
    }

    private static VerticalStackLayout Section(string title)
    {
        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black
                }
            }
        };
    }

    private View FilterPicker(string title, IEnumerable<string> options, string selected, Action<string> onChanged)
    {
        var picker = new Picker
        {
            Title = title,
            ItemsSource = options.ToList(),
            SelectedItem = selected
        };
        SemanticProperties.SetDescription(picker, $"{title} filter");
        picker.SelectedIndexChanged += (_, _) =>
        {
            if (picker.SelectedItem is not string value || value == selected)
            {
                return;
            }

            onChanged(value);
            RebuildDataSections();
        };

        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = title },
                picker
            }
        };
    }

    private static View DashboardCard(string title, IEnumerable<string> rows)
    {
        var layout = new VerticalStackLayout { Spacing = 6 };
        layout.Children.Add(new Label { Text = title, FontAttributes = FontAttributes.Bold });

        foreach (var row in rows)
        {
            layout.Children.Add(new Label { Text = row });
        }

        return Card(layout);
    }

    private static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Text = text };
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private static View Card(View content, Color? accentColor = null)
    {
        return new Frame
        {
            BorderColor = accentColor ?? Colors.LightGray,
            BackgroundColor = Colors.White,
            CornerRadius = 6,
            Padding = 10,
            Content = content
        };
    }

    private static Grid GridRows(View left, View right)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        grid.Add(left, 0);
        grid.Add(right, 1);
        return grid;
    }

    private static string PageTitle(PageId page)
    {
        return page switch
        {
            PageId.Inventory => "Pantry",
            PageId.Shopping => "Shopping List",
            PageId.Locations => "Locations",
            PageId.Durable => "Equipment",
            _ => "Dashboard"
        };
    }

    private static string ShoppingStatus(ShoppingListItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Status))
        {
            return item.Status;
        }

        return item.IsPurchased ? item.IsResolved ? "Bought" : "StockUpdateNeeded" : "ShoppingList";
    }

    private static string ShoppingStatusLabel(ShoppingListItem item)
    {
        return ShoppingStatus(item) switch
        {
            "InCart" => "In cart / buying",
            "Bought" => "Bought",
            "StockUpdateNeeded" => "Stock update needed",
            _ => "Added to shopping list"
        };
    }

    private static Color ExpiryColor(string status)
    {
        return status switch
        {
            "Expired" => Colors.Red,
            "Urgent" => Colors.OrangeRed,
            "Soon" => Colors.Goldenrod,
            _ => Colors.Green
        };
    }

    private string RuleItemName(ReplenishmentRule rule)
    {
        return _summary.FirstOrDefault(item => item.ItemDefinitionId == rule.ItemDefinitionId)?.ItemName
            ?? (!string.IsNullOrWhiteSpace(rule.ItemName) ? rule.ItemName : rule.ItemDefinitionId);
    }

    private static string DurableType(DurableItem item)
    {
        return string.IsNullOrWhiteSpace(item.ItemType) ? "Uncategorized" : item.ItemType;
    }

    private string DurableLocation(DurableItem item)
    {
        return !string.IsNullOrWhiteSpace(item.CurrentLocation)
            ? DisplayLocation(item.CurrentLocation)
            : !string.IsNullOrWhiteSpace(item.StorageSlotId)
                ? DisplayLocation(item.StorageSlotId)
                : "No location set";
    }

    private string DisplayLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return "No location set";
        }

        return TryFormatLocationPath(location, out var path) ? path : location;
    }

    private bool TryFormatLocationPath(string locationId, out string path)
    {
        foreach (var node in _locationTree)
        {
            if (TryBuildLocationPath(node, locationId, [], out var names))
            {
                path = string.Join(" › ", names);
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    private static bool TryBuildLocationPath(LocationTreeNodeItem node, string locationId, List<string> parentNames, out List<string> names)
    {
        var nextNames = parentNames.Append(node.Location.Name).ToList();
        if (node.Location.Id == locationId)
        {
            names = nextNames;
            return true;
        }

        foreach (var child in node.Children)
        {
            if (TryBuildLocationPath(child, locationId, nextNames, out names))
            {
                return true;
            }
        }

        names = [];
        return false;
    }

    private static string WarrantyIndicator(DurableItem item)
    {
        return string.IsNullOrWhiteSpace(item.WarrantyEndsOn) ? "No warranty recorded" : $"Warranty through {item.WarrantyEndsOn}";
    }

    private static string NotRecorded(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not recorded" : value;
    }

    private static string SuggestionBreakdown(ReplenishmentSuggestion suggestion)
    {
        return $"Breakdown: current {suggestion.UsableCurrentQuantity.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}; required {suggestion.RequiredAmount.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}; suggested {suggestion.SuggestedPurchaseAmount.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}; rule source replenishment target; desired {suggestion.DesiredQuantity.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}; usable {suggestion.UsableCurrentQuantity.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}; deficit {suggestion.DeficitAmount.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}; expiring soon {suggestion.ExpiringSoonAmount.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}";
    }

    private static List<T> ProjectList<T>(IEnumerable<object> source)
    {
        return source.Select(Project<T>).ToList();
    }

    private static T Project<T>(object source)
    {
        var json = JsonSerializer.Serialize(source, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Could not project {typeof(T).Name}.");
    }

    private static ConsumableEntry ToMobileEntry(ConsumableEntryReadModel entry)
    {
        return new ConsumableEntry(
            entry.EntryId.ToString(),
            entry.ItemDefinitionId.ToString(),
            entry.ItemName,
            entry.Quantity,
            entry.Unit,
            entry.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            entry.ExpiresInDays,
            entry.ExpiryStatus,
            entry.StorageSlotId?.ToString());
    }

    private static ReplenishmentSuggestion ToMobileSuggestion(ReplenishmentSuggestionReadModel suggestion)
    {
        return new ReplenishmentSuggestion(
            suggestion.ItemDefinitionId.ToString(),
            suggestion.ItemName,
            suggestion.CurrentQuantity,
            suggestion.UsableCurrentQuantity,
            suggestion.DesiredQuantity,
            suggestion.DeficitAmount,
            suggestion.ExpiringSoonAmount,
            suggestion.SuggestedPurchaseAmount,
            suggestion.RequiredAmount,
            suggestion.Unit,
            suggestion.Entries.Select(ToMobileEntry).ToList());
    }

    private static ReplenishmentRule ToMobileRule(Phoodab.Domain.ReplenishmentRule rule)
    {
        return new ReplenishmentRule(
            rule.Id.ToString(),
            rule.ItemDefinitionId.ToString(),
            string.Empty,
            rule.TargetAmount.Value,
            rule.Unit.Value,
            rule.ExpiryWarningDays,
            rule.IsDisabled);
    }

    private static DurableItem ToMobileDurableItem(DurableItemReadModel item)
    {
        return new DurableItem(
            item.Id.ToString(),
            item.ItemDefinitionId.ToString(),
            item.DisplayName,
            item.Description,
            item.ItemType,
            item.BrandManufacturer,
            item.Model,
            item.SerialNumber,
            item.PurchaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            item.PurchaseValue,
            item.WarrantyEndsOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            item.Status,
            item.CurrentLocation,
            item.Notes,
            item.StorageSlotId?.ToString());
    }

    private static LocationItem ToMobileLocation(LocationReadModel location)
    {
        return new LocationItem(
            location.Id.ToString(),
            location.Name,
            location.Type,
            location.ParentLocationId?.ToString(),
            location.Description,
            location.SortOrder,
            location.IsArchived);
    }

    private static LocationTreeNodeItem ToMobileLocationTreeNode(LocationTreeNodeReadModel node)
    {
        return new LocationTreeNodeItem(
            ToMobileLocation(node.Location),
            node.Children.Select(ToMobileLocationTreeNode).ToList());
    }

    private static View LabeledField(string labelText, View field, string description, string? help = null)
    {
        var label = new Label { Text = labelText };
        WithDescription(field, description);

        if (!string.IsNullOrWhiteSpace(help))
        {
            WithHelp(label, help);
            WithHelp(field, help);
        }

        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                label,
                field
            }
        };
    }

    private static T WithHelp<T>(T view, string help) where T : View
    {
        ToolTipProperties.SetText(view, help);
        SemanticProperties.SetHint(view, help);
        return view;
    }

    private static T WithDescription<T>(T view, string description) where T : View
    {
        SemanticProperties.SetDescription(view, description);
        return view;
    }

    private sealed record ItemOption(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record InventorySummaryItem(string ItemDefinitionId, string ItemName, decimal? TotalQuantity, string? Unit, int EntryCount, bool HasMixedUnits, string? MixedUnitWarning);
    private sealed record DurableItem(string Id, string ItemDefinitionId, string DisplayName, string? Description, string? ItemType, string? BrandManufacturer, string? Model, string? SerialNumber, string? PurchaseDate, decimal? PurchaseValue, string? WarrantyEndsOn, string Status, string? CurrentLocation, string? Notes, string? StorageSlotId);
    private sealed record ConsumableEntry(string EntryId, string ItemDefinitionId, string ItemName, decimal Quantity, string Unit, string? ExpiresOn, int? ExpiresInDays, string ExpiryStatus, string? StorageSlotId);
    private sealed record ReplenishmentSuggestion(string ItemDefinitionId, string ItemName, decimal CurrentQuantity, decimal UsableCurrentQuantity, decimal DesiredQuantity, decimal DeficitAmount, decimal ExpiringSoonAmount, decimal SuggestedPurchaseAmount, decimal RequiredAmount, string Unit, List<ConsumableEntry> Entries);
    private sealed record ReplenishmentRule(string Id, string ItemDefinitionId, string ItemName, decimal DesiredAmount, string DesiredUnit, int ExpiryWarningDays, bool IsDisabled);
    private sealed record ShoppingListItem(string Id, string ItemDefinitionId, string ItemName, decimal Quantity, string Unit, bool IsResolved, bool IsPurchased, string Status, bool StockUpdateNeeded, string? NextInventoryAction, decimal? SourceDeficitAmount, decimal? SourceExpiringSoonAmount, decimal? SourceSuggestedPurchaseAmount);
    private sealed record LocationItem(string Id, string Name, string Type, string? ParentLocationId, string? Description, int? SortOrder, bool IsArchived);
    private sealed record LocationTreeNodeItem(LocationItem Location, List<LocationTreeNodeItem> Children);
    private sealed record PantrySnapshot(
        List<InventorySummaryItem> Summary,
        List<DurableItem> DurableItems,
        List<ConsumableEntry> ConsumableEntries,
        List<ConsumableEntry> ExpiringEntries,
        List<ReplenishmentSuggestion> Suggestions,
        List<ShoppingListItem> ShoppingListItems,
        List<ReplenishmentRule> Rules,
        List<LocationItem> Locations,
        List<LocationTreeNodeItem> LocationTree);

    private enum PageId
    {
        Dashboard,
        Inventory,
        Shopping,
        Locations,
        Durable
    }
}
