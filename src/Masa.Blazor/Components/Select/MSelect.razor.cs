﻿using System.Linq.Expressions;
using Masa.Blazor.Components.Input;
using Masa.Blazor.Components.Select;
using StyleBuilder = Masa.Blazor.Core.StyleBuilder;

namespace Masa.Blazor;

public partial class MSelect<TItem, TItemValue, TValue> : MTextField<TValue>, IOutsideClickJsCallback
{
    [Inject]
    protected I18n I18n { get; set; } = null!;

    [Inject]
    private OutsideClickJSModule OutsideClickJSModule { get; set; } = null!;

    [Parameter]
    [MasaApiParameter("$dropdown")]
    public override string? AppendIcon { get; set; } = "$dropdown";

    [Parameter]
    [MasaApiParameter(false)]
    public StringBoolean? Attach { get; set; } = false;

    [Parameter]
    public bool CacheItems { get; set; }

    [Parameter]
    public bool Chips { get; set; }

    [Parameter]
    public bool DeletableChips { get; set; }

    //TODO: DisableLookup,Eager

    [Parameter]
    public bool HideSelected { get; set; }

    [Parameter, EditorRequired]
    public IList<TItem> Items
    {
        get => GetValue((IList<TItem>)new List<TItem>())!;
        set => SetValue(value, disableIListAlwaysNotifying: true);
    }

    [Parameter]
    [MasaApiParameter("primary")]
    public string ItemColor { get; set; } = "primary";

    [Parameter]
    public Func<TItem, bool>? ItemDisabled { get; set; }

    [Parameter, EditorRequired]
    public Func<TItem, string> ItemText { get; set; } = null!;

    [Parameter, EditorRequired]
    public Func<TItem, TItemValue?> ItemValue { get; set; } = null!;

    [Parameter]
    public Action<BMenuProps>? MenuProps { get; set; }

    [Parameter]
    public bool Eager { get; set; }

    [Parameter]
    public bool Multiple { get; set; }

    //TODO:OpenOnClear

    [Parameter]
    public bool SmallChips { get; set; }

    //TODO:remove this
    [Parameter]
    public StringNumber? MinWidth { get; set; }

    [Parameter]
    public string? NoDataText { get; set; }

    [Parameter]
    [Obsolete("Use OnSelect instead.")]
    public EventCallback<TItem> OnSelectedItemUpdate { get; set; }

    [Parameter]
    public EventCallback<(TItem Item, bool Selected)> OnSelect { get; set; }

    [Parameter]
    public RenderFragment? AppendItemContent { get; set; }

    [Parameter]
    public RenderFragment<SelectListItemProps<TItem>>? ItemContent { get; set; }

    [Parameter]
    public RenderFragment? NoDataContent { get; set; }

    [Parameter]
    public RenderFragment? PrependItemContent { get; set; }

    [Parameter]
    public RenderFragment<SelectSelectionProps<TItem>>? SelectionContent { get; set; }

    private static Func<TItem, string?> ItemHeader { get; } = GetFuncOrDefault<string>("Header");

    private static Func<TItem, bool> ItemDivider { get; } = GetFuncOrDefault<bool>("Divider");
    
    private bool _onClearInvoked;
    private bool _onSelectItemInvoked;

    private IList<TItem> CachedItems { get; set; } = new List<TItem>();

    protected bool IsMenuActive
    {
        get => GetValue<bool>();
        set => SetValue(value);
    }

    protected int MenuListIndex { get; private set; } = -1;

    protected int SelectedIndex { get; set; } = -1;

    protected MMenu? MMenu { get; set; }

    protected BMenuProps ComputedMenuProps
    {
        get
        {
            var defaults = GetDefaultMenuProps();
            MenuProps?.Invoke(defaults);
            if (defaults.OffsetY && defaults.NudgeBottom is null)
            {
                defaults.NudgeBottom = 1;
            }

            return defaults;
        }
    }

    protected bool HasChips => Chips || SmallChips;

    protected override bool IsDirty => SelectedItems.Count > 0;

    public override Action<TextFieldNumberProperty>? NumberProps { get; set; }

    protected override Dictionary<string, object> InputAttrs => new(Attributes)
    {
        ["type"] = Type,
        ["value"] = null,
        ["readonly"] = true
    };

    protected List<TItem> AllItems => FilterDuplicates(CachedItems.Concat(Items)).ToList();

    protected virtual List<TItem> ComputedItems => AllItems;
    
    private List<TItem> VirtualizedItems => MMenu?.Auto is true ? ComputedItems : ComputedItems.Take(lastItem).ToList();

    protected List<TItem> ComputedItemsIfHideSelected =>
        HideSelected ? ComputedItems.Where(item => !SelectedItems.Any(u => u.Item?.Equals(item) is true)).ToList() : ComputedItems;

    protected IList<TItemValue> InternalValues
    {
        get
        {
            return InternalValue switch
            {
                IList<TItemValue> values => values,
                TItemValue value => new List<TItemValue> { value },
                _ => new List<TItemValue>()
            };
        }
    }

    internal virtual List<SelectedItem<TItem>> SelectedItems { get; set; } = [];

    protected string TilesSelector =>
        $"{MMenu!.ContentElement.GetSelector()} .m-list-item, {MMenu.ContentElement.GetSelector()} .m-divider, {MMenu.ContentElement.GetSelector()} .m-subheader";

    protected virtual bool MenuCanShow => true;

    protected override bool DisableSetValueByJsInterop => true;

    protected virtual BMenuProps GetDefaultMenuProps() => new()
    {
        CloseOnClick = false,
        CloseOnContentClick = false,
        DisableKeys = true,
        OpenOnClick = false,
        MaxHeight = 304
    };

    protected virtual string? GetText(TItem? item) => item is null ? null : ItemText(item);
    
    internal string? GetText(SelectedItem<TItem>? item) => item is null ? null : item.InputText ?? GetText(item.Item);

    protected TItemValue? GetValue(TItem item) => ItemValue(item);

    protected bool GetDisabled(TItem item) => ItemDisabled?.Invoke(item) is true;

    internal bool GetDisabled(SelectedItem<TItem> item) => item.Item is not null && GetDisabled(item.Item);
    
    protected virtual IEnumerable<string> KeysForKeyDownWithPreventDefault { get; } =
    [
        KeyCodes.Space
    ];

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        NoDataText = I18n.T("$masaBlazor.noDataText");

        await base.SetParametersAsync(parameters);

        Items.ThrowIfNull(ComponentName);
        ItemText.ThrowIfNull(ComponentName);
        ItemValue.ThrowIfNull(ComponentName);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        CachedItems = CacheItems ? Items : new List<TItem>();
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            string[] keys =
            [
                KeyCodes.ArrowUp, KeyCodes.ArrowDown, KeyCodes.Home, KeyCodes.End, KeyCodes.Enter, KeyCodes.Escape,
                ..KeysForKeyDownWithPreventDefault
            ];

            _ = Js.InvokeVoidAsync(JsInteropConstants.PreventDefaultForSpecificKeys, InputElement, keys);
        }
        else
        {
            PrepareMenu();
        }
    }

    protected override void RegisterWatchers(PropertyWatcher watcher)
    {
        base.RegisterWatchers(watcher);

        watcher.Watch<IList<TItem>>(nameof(Items), _ => OnItemsChange(), immediate: true)
               .Watch<bool>(nameof(IsMenuActive), IsMenuActiveChanged);
    }

    private async void IsMenuActiveChanged(bool val)
    {
        if (val) return;

        if (_onSelectItemInvoked || _onClearInvoked)
        {
            await TryInvokeFieldChangeOfInputsFilter(isClear: _onClearInvoked);

            _onClearInvoked = false;
            _onSelectItemInvoked = false;
        }
    }

    private void OnItemsChange()
    {
        if (CacheItems)
        {
            NextTick(() =>
            {
                CachedItems = FilterDuplicates(CachedItems.Concat(Items));
                StateHasChanged();
            });
        }

        SetSelectedItems();

        StateHasChanged();
    }

    private IList<TItem> FilterDuplicates(IEnumerable<TItem> list)
    {
        var uniqueKeys = new List<TItemValue>();
        var uniqueItems = new List<TItem>();

        foreach (var item in list)
        {
            if (item is null)
            {
                continue;
            }

            if (ItemDivider(item) || ItemHeader(item) is not null)
            {
                uniqueItems.Add(item);
                continue;
            }

            var val = GetValue(item);
            if (!uniqueKeys.Contains(val))
            {
                uniqueKeys.Add(val);
                uniqueItems.Add(item);
            }
        }

        return uniqueItems;
    }

    private void PrepareMenu()
    {
        if (MMenu is not null && InputSlotAttrs.Keys.Count == 0)
        {
            InputSlotAttrs = MMenu.ActivatorAttributes;
            MMenu.BeforeShowContent ??= OnMenuBeforeShowContent;
            MMenu.AfterShowContent ??= OnMenuAfterShowContent;

            _ = OutsideClickJSModule.InitializeAsync(this, InputSlotElement.GetSelector());

            StateHasChanged();
        }
    }

    private StringBoolean? GetMenuAttach()
    {
        if (Attach is null) return null;

        if ((Attach.IsT0 && string.IsNullOrWhiteSpace(Attach.AsT0)) || (Attach.IsT1 && Attach.AsT1))
        {
            return Ref.TryGetSelector(out var selector) ? selector : null;
        }

        return Attach;
    }

    protected virtual Task OnMenuBeforeShowContent()
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnMenuAfterShowContent(bool isLazyContent)
    {
        if (isLazyContent)
        {
            if (OutsideClickJSModule.Initialized && MMenu!.ContentElement.Context is not null)
            {
                _ = OutsideClickJSModule.UpdateDependentElementsAsync(InputSlotElement.GetSelector(), MMenu.ContentElement.GetSelector());
            }
        }

        OnMenuActiveChange(true);

        return Task.CompletedTask;
    }

    protected async void OnMenuActiveChange(bool val)
    {
        if ((Multiple && !val) || GetMenuIndex() > -1)
        {
            return;
        }

        if (MMenu!.ContentElement.Context is null || !IsDirty) return;

        var index = await Js.InvokeAsync<int>(JsInteropConstants.GetListIndexWhereAttributeExists,
            TilesSelector,
            "aria-selected", "True");

        if (index > -1)
        {
            await SetMenuIndex(index);

            StateHasChanged();
        }
    }

    public async Task Blur()
    {
        var prevIsMenuActive = IsMenuActive;
        var prevIsFocused = IsFocused;
        var prevSelectedIndex = SelectedIndex;
        var prevMenuListIndex = MenuListIndex;

        IsMenuActive = false;
        IsFocused = false;
        SelectedIndex = -1;
        await SetMenuIndex(-1);

        if (prevIsMenuActive || prevIsFocused || prevSelectedIndex != -1 || prevMenuListIndex != -1)
        {
            StateHasChanged();
        }
    }

    public void ActivateMenu()
    {
        if (!IsInteractive || IsMenuActive)
        {
            return;
        }

        IsMenuActive = true;
    }

    private static Block _block = new("m-select");
    private ModifierBuilder _modifierBuilder = _block.CreateModifierBuilder();
    private ModifierBuilder _selectionModifierBuilder = _block.Element("selection").CreateModifierBuilder();

    protected override IEnumerable<string> BuildComponentClass()
    {
        return base.BuildComponentClass().Concat(
            new[]
            {
                _modifierBuilder.Add(IsMenuActive)
                    .Add("is-multi", Multiple)
                    .Add(Chips)
                    .Add("chips--small", SmallChips)
                    .Build()
            }
        );
    }

    protected override IEnumerable<string> BuildComponentStyle()
    {
        return base.BuildComponentStyle().Concat(
            StyleBuilder.Create().AddMinWidth(MinWidth).GenerateCssStyles()
        );
    }

    protected override void OnInternalValueChange(TValue val)
    {
        base.OnInternalValueChange(val);

        NextTick(SetSelectedItems);

        if (Multiple)
        {
            NextTick(() =>
            {
                // TODO: need this?
                // await MMenu.UpdateDimensionsAsync();

                return Task.CompletedTask;
            });
        }

        StateHasChanged();
    }

    protected async Task SelectItemByIndex(int index)
    {
        if (index > -1 && index < ComputedItems.Count)
        {
            var item = ComputedItems[index];
            await SelectItem(new SelectedItem<TItem>(item));
        }
    }

    internal virtual async Task SelectItem(SelectedItem<TItem> item, bool closeOnSelect = true)
    {
        _onSelectItemInvoked = true;

        bool isSelected;

        var value = item.InputText is not null ? (TItemValue)(object)item.InputText : ItemValue(item.Item);

        if (!Multiple)
        {
            if (value is TValue val)
            {
                await SetsValue(val);
            }

            if (closeOnSelect)
            {
                IsMenuActive = false;
            }

            isSelected = true;
        }
        else
        {
            var internalValues = InternalValues.ToList();
            if (internalValues.Contains(value))
            {
                internalValues.Remove(value);
                isSelected = false;
            }
            else
            {
                internalValues.Add(value);
                isSelected = true;
            }

            if (internalValues is TValue val)
            {
                await SetsValue(val);
            }
        }

        if (HideSelected)
        {
            await SetMenuIndex(-1);
        }
        else
        {
            var index = ComputedItems.IndexOf(item.Item);
            if (index > -1)
            {
                await SetMenuIndex(index);
            }
        }

        _ = OnSelectedItemUpdate.InvokeAsync(item.Item);

        _ = OnSelect.InvokeAsync((item.Item, isSelected));
    }

    public override async Task HandleOnKeyDownAsync(KeyboardEventArgs args)
    {
        if (IsReadonly && args.Key != KeyCodes.Tab) return;

        if (OnKeyDown.HasDelegate)
        {
            await OnKeyDown.InvokeAsync(args);
        }

        var keyCode = args.Key;

        // If menu is active, allow default
        // listIndex change from menu
        if (IsMenuActive && new[] { KeyCodes.ArrowUp, KeyCodes.ArrowDown, KeyCodes.Home, KeyCodes.End, KeyCodes.Enter }.Contains(keyCode))
        {
            NextTick(async () =>
            {
                await ChangeMenuListIndex(keyCode);
                StateHasChanged();
            });
        }

        // If enter, space, open menu
        if (new[] { KeyCodes.Enter, KeyCodes.Space }.Contains(keyCode))
        {
            ActivateMenu();
        }

        // If menu is not active, up/down/home/end can do
        // one of 2 things. If multiple, opens the
        // menu, if not, will cycle through all
        // available options
        if (!IsMenuActive && new[] { KeyCodes.ArrowUp, KeyCodes.ArrowDown, KeyCodes.Home, KeyCodes.End }.Contains(keyCode))
        {
            await OnUpDown(keyCode);
            return;
        }

        // If escape deactivate the menu
        if (keyCode == KeyCodes.Escape)
        {
            await OnEscDown();
            return;
        }

        // If tab - select item or close menu
        if (keyCode == KeyCodes.Tab)
        {
            await OnTabDown(args);
            return;
        }

        // If space preventDefault
        if (keyCode == KeyCodes.Space)
        {
            // invoke preventDefault at js interop
        }
    }

    private async Task ChangeMenuListIndex(string code)
    {
        if (code == KeyCodes.ArrowUp)
        {
            PrevTile();
        }
        else if (code == KeyCodes.ArrowDown)
        {
            NextTile();
        }
        else if (code == KeyCodes.Home)
        {
            FirstTile();
        }
        else if (code == KeyCodes.End)
        {
            LastTile();
        }
        else if (code == KeyCodes.Enter && MenuListIndex != -1)
        {
            await SelectItemByIndex(MenuListIndex);
        }

        await SetMenuIndex(MenuListIndex);
    }

    private void NextTile()
    {
        var nextIndex = MenuListIndex + 1;

        if (nextIndex >= ComputedItemsIfHideSelected.Count)
        {
            if (ComputedItemsIfHideSelected.Count == 0) return;

            MenuListIndex = -1;
            NextTile();

            return;
        }

        var nextItem = ComputedItemsIfHideSelected.ElementAtOrDefault(nextIndex);

        MenuListIndex++;
        if (nextItem is not null && (ItemDivider(nextItem) || ItemHeader(nextItem) is not null || ItemDisabled?.Invoke(nextItem) is true))
        {
            NextTile();
        }
    }

    private void PrevTile()
    {
        var prevIndex = MenuListIndex - 1;

        if (prevIndex < 0)
        {
            if (ComputedItemsIfHideSelected.Count == 0) return;

            MenuListIndex = ComputedItemsIfHideSelected.Count;
            PrevTile();

            return;
        }

        var prevItem = ComputedItemsIfHideSelected.ElementAt(prevIndex);

        MenuListIndex--;
        if (ItemDivider(prevItem) || ItemHeader(prevItem) is not null || ItemDisabled?.Invoke(prevItem) is true)
        {
            PrevTile();
        }
    }

    private void LastTile()
    {
        var lastIndex = ComputedItemsIfHideSelected.Count - 1;

        if (lastIndex == -1) return;

        var last = ComputedItemsIfHideSelected.ElementAt(lastIndex);

        MenuListIndex = lastIndex;

        if (ItemDivider(last) || ItemHeader(last) is not null || ItemDisabled?.Invoke(last) is true)
        {
            PrevTile();
        }
    }

    private void FirstTile()
    {
        if (ComputedItemsIfHideSelected.Count == 0) return;

        var firstItem = ComputedItemsIfHideSelected.First();

        MenuListIndex = 0;

        if (ItemDivider(firstItem) || ItemHeader(firstItem) is not null || ItemDisabled?.Invoke(firstItem) is true)
        {
            NextTile();
        }
    }

    protected virtual async Task OnUpDown(string code)
    {
        // Multiple selects do not cycle their value
        // when pressing up or down, instead activate
        // the menu
        if (Multiple)
        {
            ActivateMenu();
            return;
        }

        // TODO: menu.hasClickableTiles

        await ChangeMenuListIndex(code);

        await SelectItemByIndex(MenuListIndex);
    }

    protected virtual Task OnEscDown()
    {
        if (IsMenuActive)
        {
            IsMenuActive = false;
        }

        return Task.CompletedTask;
    }

    protected virtual async Task OnTabDown(KeyboardEventArgs args)
    {
        // An item that is selected by
        // menu-index should toggled
        if (!Multiple && MenuListIndex != -1 && IsMenuActive)
        {
            // TODO: need e.preventDefault() and e.stopPropagation()?

            await SelectItemByIndex(MenuListIndex);
        }
        else
        {
            // If we make it here,
            // the user has no selected indexes
            // and is probably tabbing out
            await Blur();
        }
    }

    private int lastItem = 20;
    private CancellationTokenSource? _scrollCts;

    private async Task OnMenuScroll(WheelEventArgs args)
    {
        try
        {
            if (!IsMenuActive || lastItem > ComputedItems.Count)
            {
                return;
            }

            _scrollCts?.Cancel();
            _scrollCts = new CancellationTokenSource();
            await Task.Delay(16, _scrollCts.Token);

            var content = MMenu?.Dimensions.Content;
            if (content is not null)
            {
                var showMoreItems = await Js.InvokeAsync<bool>(JsInteropConstants.IsScrollNearBottom, MMenu!.ContentElement, 200);
                if (showMoreItems)
                {
                    lastItem += 20;
                    StateHasChanged();
                }
            }
        }
        catch (TaskCanceledException)
        {
            // ignored
        }
    }

    public override async Task HandleOnBlurAsync(FocusEventArgs args)
    {
        if (OnBlur.HasDelegate)
        {
            await OnBlur.InvokeAsync(args);
        }
    }

    public override async Task HandleOnClickAsync(ExMouseEventArgs args)
    {
        if (!IsInteractive) return;

        if (await IsAppendInner(args.Target!) is false)
        {
            IsMenuActive = true;
        }

        if (!IsFocused)
        {
            IsFocused = true;

            if (OnFocus.HasDelegate)
            {
                await OnFocus.InvokeAsync();
            }
        }

        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(args);
        }
    }

    public override async Task HandleOnMouseUpAsync(ExMouseEventArgs args)
    {
        if (HasMouseDown && args.Button != 2 && IsInteractive)
        {
            // If append inner is present
            // and the target is itself
            // or inside, toggle menu
            if (await IsAppendInner(args.Target!))
            {
                IsMenuActive = !IsMenuActive;
            }
        }

        await base.HandleOnMouseUpAsync(args);
    }

    /// <summary>
    /// target is itself or inside
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    protected ValueTask<bool> IsAppendInner(EventTarget target)
    {
        return Js.InvokeAsync<bool>(JsInteropConstants.EqualsOrContains, AppendInnerElement, target.Selector);
    }

    public override async Task HandleOnClearClickAsync(MouseEventArgs args)
    {
        _onClearInvoked = true;
        
        var value = Multiple ? (TValue)(IList<TItemValue>)new List<TItemValue>() : default;

        await SetsValue(value);

        if (OnClearClick.HasDelegate)
        {
            await OnClearClick.InvokeAsync(args);
        }

        if (!IsMenuActive)
        {
            // invoke the field change event of inputs filter when the menu is not active
            IsMenuActiveChanged(false);
        }

        await OnChange.InvokeAsync(value);

        await SetMenuIndex(-1);

        // whether to need NextTick?
        await InputElement.FocusAsync();

        // TODO: OpenOnClear
    }

    protected async Task SetMenuIndex(int index)
    {
        MenuListIndex = index;

        if (index == -1)
        {
            return;
        }

        var i = index;
        var menuItem = ComputedItems.ElementAtOrDefault(index);
        if (menuItem is not null)
        {
            i = ComputedItemsIfHideSelected.IndexOf(menuItem);
        }

        if (i > -1 && MMenu?.ContentElement.TryGetSelector(out var selector) is true)
        {
            await Js.InvokeVoidAsync(JsInteropConstants.ScrollToTile,
                selector,
                TilesSelector,
                i);
        }
    }

    protected virtual void SetSelectedItems()
    {
        var selectedItems = new List<SelectedItem<TItem>>();

        var values = InternalValues;

        foreach (var value in values)
        {
            var index = AllItems.FindIndex(v => EqualityComparer<TItemValue>.Default.Equals(value, GetValue(v)));

            if (index > -1)
            {
                selectedItems.Add(new SelectedItem<TItem>(AllItems[index], null));
            }
        }

        SelectedItems = selectedItems;

        StateHasChanged();
    }

    protected int GetMenuIndex()
    {
        return MenuListIndex;
    }

    internal virtual async Task OnChipInput(SelectedItem<TItem> item)
    {
        if (Multiple)
        {
            await SelectItem(item);
        }
        else
        {
            await SetsValue(default);
        }
        
        await TryInvokeFieldChangeOfInputsFilter();

        // if all items have been deleted, open the menu
        IsMenuActive = SelectedItems.Count == 0;

        SelectedIndex = -1;
    }

    private static Func<TItem, T?> GetFuncOrDefault<T>(string name)
    {
        Func<TItem, T?> func;
        try
        {
            var parameterExpression = Expression.Parameter(typeof(TItem), "item");
            var propertyExpression = Expression.Property(parameterExpression, name);

            var lambdaExpression = Expression.Lambda<Func<TItem, T>>(propertyExpression, parameterExpression);
            func = lambdaExpression.Compile();
        }
        catch
        {
            func = _ => default;
        }

        return func;
    }

    protected virtual async Task SetsValue(TValue value)
    {
        if (!EqualityComparer<TValue>.Default.Equals(InternalValue, value))
        {
            UpdateInternalValue(value, InternalValueChangeType.InternalOperation);
            if (OnChange.HasDelegate)
            {
                await OnChange.InvokeAsync(value);
            }
        }
    }

    public async Task HandleOnOutsideClickAsync()
    {
        if (IsFocused || IsMenuActive)
        {
            await Blur();
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await OutsideClickJSModule.UnbindAndDisposeAsync();
        await base.DisposeAsyncCore();
    }
}
