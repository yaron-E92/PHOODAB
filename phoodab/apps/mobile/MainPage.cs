using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Phoodab.Mobile;

public sealed class MainPage : ContentPage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly Entry _baseUrlEntry = new() { Text = "http://localhost:5199", Placeholder = "API base URL" };
    private readonly Label _healthLabel = new() { Text = "Health: loading" };
    private readonly Label _versionLabel = new() { Text = "Version: loading" };
    private readonly Label _statusLabel = new() { TextColor = Colors.DarkRed };
    private readonly Entry _itemNameEntry = new() { Placeholder = "Item name" };
    private readonly Entry _desiredAmountEntry = new() { Placeholder = "Desired amount (optional)", Keyboard = Keyboard.Numeric };
    private readonly Entry _desiredUnitEntry = new() { Placeholder = "Desired unit (optional)" };
    private readonly Picker _entryItemPicker = new() { Title = "Select item" };
    private readonly Entry _quantityEntry = new() { Placeholder = "Quantity", Keyboard = Keyboard.Numeric };
    private readonly Entry _unitEntry = new() { Placeholder = "Unit" };
    private readonly Entry _expiryDateEntry = new() { Placeholder = "Expiry date YYYY-MM-DD (optional)" };
    private readonly Entry _storageSlotEntry = new() { Placeholder = "Storage slot ID (optional)" };
    private readonly VerticalStackLayout _content = new() { Spacing = 16, Padding = 16 };
    private readonly VerticalStackLayout _dataContent = new() { Spacing = 16 };

    private readonly List<ItemOption> _createdItems = [];
    private readonly List<InventorySummaryItem> _summary = [];
    private readonly List<ConsumableEntry> _consumableEntries = [];
    private readonly List<ConsumableEntry> _expiringEntries = [];
    private readonly List<ReplenishmentSuggestion> _suggestions = [];
    private readonly List<ShoppingListItem> _shoppingListItems = [];
    private readonly List<ReplenishmentRule> _rules = [];

    private bool _hasLoaded;

    public MainPage(HttpClient httpClient)
    {
        _httpClient = httpClient;
        Title = "PHOODAB Pantry";
        BackgroundColor = Colors.White;

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

    private string BaseUrl => (_baseUrlEntry.Text ?? string.Empty).Trim().TrimEnd('/');

    private void BuildShell()
    {
        _content.Children.Clear();
        _content.Children.Add(new Label
        {
            Text = "PHOODAB Pantry MVP",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        });

        _content.Children.Add(_baseUrlEntry);
        _content.Children.Add(Button("Refresh", async () => await LoadDataAsync()));
        _content.Children.Add(_healthLabel);
        _content.Children.Add(_versionLabel);
        _content.Children.Add(_statusLabel);

        AddCreateItemSection();
        AddEntrySection();
        _content.Children.Add(_dataContent);
    }

    private void RebuildDataSections()
    {
        _dataContent.Children.Clear();
        AddInventorySummarySection();
        AddConsumableAuditSection();
        AddExpiringSection();
        AddSuggestionsSection();
        AddShoppingSection();
        AddRulesSection();
    }

    private async Task LoadDataAsync()
    {
        SetStatus("Loading pantry data...", isError: false);

        try
        {
            var healthTask = GetAsync<HealthResponse>("/health");
            var versionTask = GetAsync<VersionResponse>("/version");
            var summaryTask = GetAsync<List<InventorySummaryItem>>("/api/inventory/summary");
            var entriesTask = GetAsync<List<ConsumableEntry>>("/api/consumable-entries");
            var expiringTask = GetAsync<List<ConsumableEntry>>("/api/consumable-entries/expiring");
            var suggestionsTask = GetAsync<List<ReplenishmentSuggestion>>("/api/replenishment/suggestions");
            var shoppingTask = GetAsync<List<ShoppingListItem>>("/api/shopping-list-items");
            var rulesTask = GetAsync<List<ReplenishmentRule>>("/api/replenishment/rules");

            await Task.WhenAll(healthTask, versionTask, summaryTask, entriesTask, expiringTask, suggestionsTask, shoppingTask, rulesTask);

            _healthLabel.Text = $"Health: {(await healthTask).Status}";
            _versionLabel.Text = $"Version: {(await versionTask).Version}";
            Replace(_summary, await summaryTask);
            Replace(_consumableEntries, await entriesTask);
            Replace(_expiringEntries, await expiringTask);
            Replace(_suggestions, await suggestionsTask);
            Replace(_shoppingListItems, await shoppingTask);
            Replace(_rules, await rulesTask);
            SetStatus(string.Empty, isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", isError: true);
        }

        RefreshItemPicker();
        RebuildDataSections();
    }

    private void AddCreateItemSection()
    {
        var section = Section("Create Consumable Item");
        section.Children.Add(_itemNameEntry);
        section.Children.Add(_desiredAmountEntry);
        section.Children.Add(_desiredUnitEntry);
        section.Children.Add(Button("Create Item", CreateItemAsync));
        _content.Children.Add(section);
    }

    private void AddEntrySection()
    {
        RefreshItemPicker();

        var section = Section("Add Consumable Entry");
        section.Children.Add(_entryItemPicker);
        section.Children.Add(_quantityEntry);
        section.Children.Add(_unitEntry);
        section.Children.Add(_expiryDateEntry);
        section.Children.Add(_storageSlotEntry);
        section.Children.Add(Button("Add Entry", AddConsumableEntryAsync));
        _content.Children.Add(section);
    }

    private void AddInventorySummarySection()
    {
        var section = Section("Inventory Summary");

        if (_summary.Count == 0)
        {
            section.Children.Add(new Label { Text = "No inventory yet." });
        }
        else
        {
            foreach (var item in _summary)
            {
                var text = item.HasMixedUnits
                    ? $"{item.ItemName}: mixed units ({item.EntryCount} entries)"
                    : $"{item.ItemName}: {item.TotalQuantity?.ToString(CultureInfo.InvariantCulture) ?? "0"} {item.Unit} ({item.EntryCount} entries)";

                if (!string.IsNullOrWhiteSpace(item.MixedUnitWarning))
                {
                    text += $" - {item.MixedUnitWarning}";
                }

                section.Children.Add(new Label { Text = text });
            }
        }

        _dataContent.Children.Add(section);
    }

    private void AddConsumableAuditSection()
    {
        var section = Section("Consumable Entry Audit");

        if (_consumableEntries.Count == 0)
        {
            section.Children.Add(new Label { Text = "No consumable entries." });
        }
        else
        {
            foreach (var entry in _consumableEntries)
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
        var storageSlot = new Entry { Text = entry.StorageSlotId ?? string.Empty, Placeholder = "Storage slot ID" };
        var addStock = new Entry { Placeholder = "Add stock amount", Keyboard = Keyboard.Numeric };
        var addStockUnit = new Entry { Text = entry.Unit, Placeholder = "Add stock unit" };
        var consumeStock = new Entry { Placeholder = "Consume stock amount", Keyboard = Keyboard.Numeric };
        var consumeStockUnit = new Entry { Text = entry.Unit, Placeholder = "Consume stock unit" };

        var layout = new VerticalStackLayout { Spacing = 8 };
        layout.Children.Add(new Label { Text = entry.ItemName, FontAttributes = FontAttributes.Bold });
        layout.Children.Add(new Label { Text = $"Lot {entry.EntryId}" });
        layout.Children.Add(new Label
        {
            Text = $"Quantity: {entry.Quantity.ToString(CultureInfo.InvariantCulture)} {entry.Unit} | Expiry: {entry.ExpiresOn ?? "No expiry"} ({entry.ExpiryStatus}) | Location: {entry.StorageSlotId ?? "No location set"}"
        });
        layout.Children.Add(GridRows(addStock, addStockUnit));
        layout.Children.Add(GridRows(
            Button("Add Stock", async () => await AdjustStockAsync(entry, addStock.Text, addStockUnit.Text, isAdd: true)),
            Button("Mark Lot Depleted", async () => await MarkLotDepletedAsync(entry))));
        layout.Children.Add(GridRows(consumeStock, consumeStockUnit));
        layout.Children.Add(Button("Consume Stock", async () => await AdjustStockAsync(entry, consumeStock.Text, consumeStockUnit.Text, isAdd: false)));
        layout.Children.Add(GridRows(quantity, unit));
        layout.Children.Add(GridRows(expiry, storageSlot));
        layout.Children.Add(GridRows(
            Button("Save Lot", async () => await SaveEntryAsync(entry, quantity.Text, unit.Text, expiry.Text, storageSlot.Text)),
            Button("Undo Unsaved Changes", async () => await LoadDataAsync())));

        return Card(layout, ExpiryColor(entry.ExpiryStatus));
    }

    private void AddExpiringSection()
    {
        var section = Section("Expiring Consumables");

        if (_expiringEntries.Count == 0)
        {
            section.Children.Add(new Label { Text = "No expiring consumables." });
        }
        else
        {
            foreach (var entry in _expiringEntries)
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
                var layout = new VerticalStackLayout { Spacing = 8 };
                layout.Children.Add(new Label { Text = suggestion.ItemName, FontAttributes = FontAttributes.Bold });
                layout.Children.Add(new Label
                {
                    Text = $"Buy {suggestion.SuggestedPurchaseAmount.ToString(CultureInfo.InvariantCulture)} {suggestion.Unit}; desired {suggestion.DesiredQuantity.ToString(CultureInfo.InvariantCulture)}, usable {suggestion.UsableCurrentQuantity.ToString(CultureInfo.InvariantCulture)}, deficit {suggestion.DeficitAmount.ToString(CultureInfo.InvariantCulture)}, expiring soon {suggestion.ExpiringSoonAmount.ToString(CultureInfo.InvariantCulture)}"
                });
                layout.Children.Add(Button("Add To Shopping List", async () => await CreateShoppingItemFromSuggestionAsync(suggestion)));
                section.Children.Add(Card(layout));
            }
        }

        _dataContent.Children.Add(section);
    }

    private void AddShoppingSection()
    {
        var section = Section("Shopping List");

        if (_shoppingListItems.Count == 0)
        {
            section.Children.Add(new Label { Text = "No shopping list items." });
        }
        else
        {
            foreach (var item in _shoppingListItems)
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
        layout.Children.Add(new Label { Text = rule.ItemName, FontAttributes = FontAttributes.Bold });
        layout.Children.Add(GridRows(desiredAmount, desiredUnit));
        layout.Children.Add(warningDays);
        layout.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Disable rule", VerticalOptions = LayoutOptions.Center },
                disabled
            }
        });
        layout.Children.Add(Button("Save Rule", async () => await SaveRuleAsync(rule, desiredAmount.Text, desiredUnit.Text, warningDays.Text, disabled.IsChecked)));

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
            var created = await PostAsync<ItemDefinition>("/api/item-definitions", new
            {
                name,
                kind = 1,
                desiredAmount,
                desiredUnit = BlankToNull(_desiredUnitEntry.Text)
            });

            _createdItems.RemoveAll(item => item.Id == created.Id);
            _createdItems.Insert(0, new ItemOption(created.Id, created.Name));
            _itemNameEntry.Text = string.Empty;
            _desiredAmountEntry.Text = string.Empty;
            _desiredUnitEntry.Text = string.Empty;
            await LoadDataAsync();
            SelectItem(created.Id);
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

        try
        {
            await PostAsync("/api/consumable-entries", new
            {
                itemDefinitionId = option.Id,
                quantity,
                unit,
                expiresOn = BlankToNull(_expiryDateEntry.Text),
                storageSlotId = BlankToNull(_storageSlotEntry.Text)
            });

            _quantityEntry.Text = string.Empty;
            _unitEntry.Text = string.Empty;
            _expiryDateEntry.Text = string.Empty;
            _storageSlotEntry.Text = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Add consumable entry failed: {ex.Message}", isError: true);
        }
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

        await PatchEntryAsync(entry.EntryId, quantity.Value, unit, BlankToNull(expiryText), BlankToNull(storageSlotText));
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

        await PatchEntryAsync(entry.EntryId, nextQuantity, entry.Unit, entry.ExpiresOn, entry.StorageSlotId);
    }

    private async Task MarkLotDepletedAsync(ConsumableEntry entry)
    {
        var confirmed = await DisplayAlert("Mark depleted", $"Mark {entry.ItemName} lot {entry.EntryId} as depleted?", "Mark Depleted", "Cancel");
        if (!confirmed)
        {
            return;
        }

        await PatchEntryAsync(entry.EntryId, 0, entry.Unit, entry.ExpiresOn, entry.StorageSlotId);
    }

    private async Task PatchEntryAsync(string entryId, decimal quantity, string unit, string? expiresOn, string? storageSlotId)
    {
        try
        {
            await PatchAsync<ConsumableEntry>($"/api/consumable-entries/{entryId}", new
            {
                quantity,
                unit,
                expiresOn,
                storageSlotId
            });
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
            await PostAsync<ShoppingListItem>("/api/shopping-list-items/from-suggestion", new
            {
                itemDefinitionId = suggestion.ItemDefinitionId,
                quantity = suggestion.SuggestedPurchaseAmount,
                unit = suggestion.Unit,
                deficitAmount = suggestion.DeficitAmount,
                expiringSoonAmount = suggestion.ExpiringSoonAmount,
                suggestedPurchaseAmount = suggestion.SuggestedPurchaseAmount
            });
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
            await PatchAsync<ShoppingListItem>($"/api/shopping-list-items/{shoppingListItemId}", new { status });
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
            using var response = await _httpClient.DeleteAsync($"{BaseUrl}/api/shopping-list-items/{shoppingListItemId}");
            response.EnsureSuccessStatusCode();
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
            await PatchAsync<ReplenishmentRule>($"/api/replenishment/rules/{rule.Id}", new
            {
                desiredAmount,
                desiredUnit = (desiredUnitText ?? string.Empty).Trim(),
                expiryWarningDays,
                isDisabled
            });
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Update rule failed: {ex.Message}", isError: true);
        }
    }

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await _httpClient.GetAsync($"{BaseUrl}{path}");
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return value ?? throw new InvalidOperationException($"Empty response for {path}.");
    }

    private async Task PostAsync(string path, object payload)
    {
        using var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}{path}", payload, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T> PostAsync<T>(string path, object payload)
    {
        using var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}{path}", payload, JsonOptions);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return value ?? throw new InvalidOperationException($"Empty response for {path}.");
    }

    private async Task<T> PatchAsync<T>(string path, object payload)
    {
        using var response = await _httpClient.PatchAsJsonAsync($"{BaseUrl}{path}", payload, JsonOptions);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return value ?? throw new InvalidOperationException($"Empty response for {path}.");
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

    private static string? BlankToNull(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
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

    private sealed record ItemOption(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record HealthResponse(string Status);
    private sealed record VersionResponse(string Version);
    private sealed record ItemDefinition(string Id, string Name, string Kind);
    private sealed record InventorySummaryItem(string ItemDefinitionId, string ItemName, decimal? TotalQuantity, string? Unit, int EntryCount, bool HasMixedUnits, string? MixedUnitWarning);
    private sealed record ConsumableEntry(string EntryId, string ItemDefinitionId, string ItemName, decimal Quantity, string Unit, string? ExpiresOn, int? ExpiresInDays, string ExpiryStatus, string? StorageSlotId);
    private sealed record ReplenishmentSuggestion(string ItemDefinitionId, string ItemName, decimal CurrentQuantity, decimal UsableCurrentQuantity, decimal DesiredQuantity, decimal DeficitAmount, decimal ExpiringSoonAmount, decimal SuggestedPurchaseAmount, decimal RequiredAmount, string Unit, List<ConsumableEntry> Entries);
    private sealed record ReplenishmentRule(string Id, string ItemDefinitionId, string ItemName, decimal DesiredAmount, string DesiredUnit, int ExpiryWarningDays, bool IsDisabled);
    private sealed record ShoppingListItem(string Id, string ItemDefinitionId, string ItemName, decimal Quantity, string Unit, bool IsResolved, bool IsPurchased, string Status, bool StockUpdateNeeded, string? NextInventoryAction, decimal? SourceDeficitAmount, decimal? SourceExpiringSoonAmount, decimal? SourceSuggestedPurchaseAmount);
}
