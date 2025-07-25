﻿using Masa.Blazor.Components.ItemGroup;
using StyleBuilder = Masa.Blazor.Core.StyleBuilder;

namespace Masa.Blazor;

public partial class MChip : MGroupItem<MItemGroupBase>, IRoutable
{
    public MChip() : base(GroupType.ChipGroup)
    {
    }

    [Inject] private MasaBlazor MasaBlazor { get; set; } = null!;

    [Inject] public NavigationManager NavigationManager { get; set; } = null!;

    [Parameter] public string? Color { get; set; }

    [Parameter] public bool Draggable { get; set; }

    [Parameter] public bool Filter { get; set; }

    [Parameter] public string FilterIcon { get; set; } = "$complete";

    [Parameter] public bool Label { get; set; }

    [Parameter] public bool Large { get; set; }

    [Parameter] public bool Outlined { get; set; }

    [Parameter] public bool Pill { get; set; }

    [Parameter] [MasaApiParameter(true)] public bool Ripple { get; set; } = true;

    [Parameter] public bool Small { get; set; }

    [Parameter] public string? TextColor { get; set; }

    [Parameter] public bool XLarge { get; set; }

    [Parameter] public bool XSmall { get; set; }

    [Parameter] [MasaApiParameter(true)] public bool Active { get; set; } = true;

    [Parameter] public bool Close { get; set; }

    [Parameter] public string? CloseIcon { get; set; }

    [Parameter]
    [MasaApiParameter("Close")]
    public string? CloseLabel { get; set; } = "Close";

    [Parameter] public string? Href { get; set; }

    [Parameter] public bool Link { get; set; }

    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter] public bool OnClickStopPropagation { get; set; }

    [Parameter] public bool OnClickPreventDefault { get; set; }

    [Parameter] public EventCallback<MouseEventArgs> OnCloseClick { get; set; }

    [Parameter] [MasaApiParameter("span")] public string? Tag { get; set; } = "span";

    [Parameter] public string? Target { get; set; }

    [Parameter] [MasaApiParameter(ReleasedIn = "v1.11.0")]
    public bool Tonal { get; set; }

    private IRoutable? _router;

    public bool Exact { get; }

    public string? MatchPattern { get; }

    public bool IsClickable => _router?.IsClickable is true || Matched;

    public bool IsLink => _router?.IsLink is true;

    public int Tabindex => _router?.Tabindex ?? 0;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        CloseIcon ??= "$delete";

        _router = new Router(this);

        (Tag, Attributes) = _router.GenerateRouteLink();

        Attributes["ripple"] = Ripple && IsClickable;
        Attributes["draggable"] = Draggable ? "true" : null;
        Attributes["tabindex"] = Matched && !Disabled ? 0 : Tabindex;
    }

    private static Block _block = new("m-chip");
    private ModifierBuilder _modifierBuilder = _block.CreateModifierBuilder();

    protected override IEnumerable<string> BuildComponentClass()
    {
        var colorForText = Outlined || Tonal;
        
        yield return _modifierBuilder
            .Add("clickable", IsClickable)
            .Add(Disabled, Draggable, Label, Link, Outlined, Pill)
            .Add("no-color", string.IsNullOrWhiteSpace(Color))
            .Add("removable", Close)
            .Add("active", InternalIsActive)
            .Add(Tonal)
            .AddTheme(ComputedTheme)
            .AddBackgroundColor(Color, !colorForText)
            .AddTextColor(Color, colorForText)
            .AddTextColor(TextColor)
            .AddClass(ComputedActiveClass, InternalIsActive)
            .AddClass(CssClassUtils.GetSize(XSmall, Small, Large, XLarge))
            .Build();
    }

    protected override IEnumerable<string> BuildComponentStyle()
    {
        return new StyleBuilder()
            .AddIf("display", "none", !Active)
            .AddBackgroundColor(Color, !Outlined)
            .AddTextColor(Color, () => Outlined)
            .AddTextColor(TextColor)
            .GenerateCssStyles();
    }

    protected override bool AfterHandleEventShouldRender() => false;

    private async Task HandleOnClick(MouseEventArgs args)
    {
        await ToggleAsync();

        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(args);
        }
    }
}