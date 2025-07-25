﻿using Masa.Blazor.Components.Bootable;
using StyleBuilder = Masa.Blazor.Core.StyleBuilder;

namespace Masa.Blazor
{
    public partial class MDialog : MBootable, IThemeable, IDependent
    {
        [Inject] public MasaBlazor MasaBlazor { get; set; } = null!;

        [Parameter] public string? ContentClass { get; set; }

        [Parameter] public string? ContentStyle { get; set; }

        [Parameter]
        [MasaApiParameter("center center")]
        public string Origin { get; set; } = "center center";

        [Parameter] public string? OverlayScrimClass { get; set; }

        /// <summary>
        /// The lazy content would be created in a [data-permanent] element.
        /// It's useful when you use this component in a layout.
        /// </summary>
        [Parameter]
        public bool Permanent { get; set; }

        [Parameter] public bool Scrollable { get; set; }

        [Parameter]
        [MasaApiParameter("dialog-transition")]
        public string? Transition { get; set; } = "dialog-transition";

        [Inject] private OutsideClickJSModule OutsideClickJsModule { get; set; } = null!;

        [CascadingParameter] public IDependent? CascadingDependent { get; set; }

        [Parameter] public string? Attach { get; set; }

        [Parameter] public RenderFragment? ChildContent { get; set; }

        // NEXT MAJOR: This parameter overlaps with the ChildContent parameter,
        // but it's not possible to simply set the context for ChildContent,
        // so we need to keep it for now util next major.
        [Parameter] public RenderFragment<DialogContentContext>? OutcomeContent { get; set; }

        [Parameter] public bool Fullscreen { get; set; }

        [Parameter] public bool HideOverlay { get; set; }

        [Parameter] public StringNumber? MaxWidth { get; set; }

        [Parameter] public EventCallback<MouseEventArgs> OnOutsideClick { get; set; }

        [Parameter]
        [MasaApiParameter(ReleasedIn = "v1.5.0")]
        public bool NoPersistentAnimation { get; set; }

        [Parameter] public bool Persistent { get; set; }

        [Parameter] public StringNumber? Width { get; set; }

        [Parameter] public Dictionary<string, object>? ContentAttributes { get; set; }

        /// <summary>
        /// Disable the outside click event, internal use only for PageStack.
        /// </summary>
        [Parameter] [MasaApiParameter(Ignored = true)] public bool NoOutsideClick { get; set; }

        /// <summary>
        /// Disable to focus on the dialog when it's opened.
        /// </summary>
        [Parameter]
        [MasaApiParameter(ReleasedIn = "v1.8.0")]
        public bool DisableAutoFocus { get; set; }

        /// <summary>
        /// Re-render requests will also be responded to when inactive.
        /// By default, only active dialog will be rendered.
        /// </summary>
        [Parameter]
        [MasaApiParameter(ReleasedIn = "v1.10.3")]
        public bool ShouldRenderWhenInactive { get; set; }

        private readonly HashSet<IDependent> _dependents = new();

        private bool _attached;
        private DialogContentContext? _contentContext;

        private bool ShowOverlay => !Fullscreen && !HideOverlay;

        private ElementReference? OverlayRef => Overlay?.Ref;

        private int StackMinZIndex { get; set; } = 200;

        public ElementReference ContentRef { get; private set; }

        public ElementReference DialogRef { get; private set; }

        private MOverlay? Overlay { get; set; }

        private int ZIndex { get; set; }

        private bool Animated { get; set; }

        protected override async Task OnActiveUpdatingAsync(bool active)
        {
            if (ContentRef.Context is not null)
            {
                await AttachAsync(active);
            }
            else
            {
                NextTick(() => AttachAsync(active));
            }

            if (active)
            {
                ZIndex = await GetActiveZIndex(true);

                if (!DisableAutoFocus)
                {
                    NextTick(async () =>
                    {
                        // TODO: previousActiveElement
                        var contains = await Js.InvokeAsync<bool>(JsInteropConstants.ContainsActiveElement, DialogRef);
                        if (!contains)
                        {
                            await Js.InvokeVoidAsync(JsInteropConstants.Focus, DialogRef);
                        }
                    });
                }
            }

            await base.OnActiveUpdatingAsync(active);
        }

        private async Task AttachAsync(bool value)
        {
            if (!NoOutsideClick && OutsideClickJsModule is { Initialized: false })
            {
                var dependentSelectors = DependentSelectors.ToArray();
                await OutsideClickJsModule.InitializeAsync(this, dependentSelectors);
                CascadingDependent?.AddDependent(this);
            }

            if (_attached == false)
            {
                await Js.InvokeVoidAsync(JsInteropConstants.AddElementTo, OverlayRef, AttachSelector);
                await Js.InvokeVoidAsync(JsInteropConstants.AddElementTo, ContentRef, AttachSelector);
                _attached = true;
            }
        }

        private async Task<int> GetActiveZIndex(bool isActive)
        {
            return !isActive
                ? await Js.InvokeAsync<int>(JsInteropConstants.GetZIndex, ContentRef)
                : await GetMaxZIndex() + 2;
        }

        private async Task<int> GetMaxZIndex()
        {
            var maxZindex = await Js.InvokeAsync<int>(JsInteropConstants.GetMenuOrDialogMaxZIndex,
                new List<ElementReference> { ContentRef }, Ref);

            return maxZindex > StackMinZIndex ? maxZindex : StackMinZIndex;
        }

        public Task Keydown(KeyboardEventArgs args)
        {
            if (args.Key == "Escape")
            {
                Close();
            }

            return Task.CompletedTask;
        }

        private void Close()
        {
            if (Persistent)
            {
                if (NoPersistentAnimation)
                {
                    return;
                }

                Animated = true;
                StateHasChanged();
                NextTick(async () =>
                {
                    //This animated need 150ms
                    await Task.Delay(150);
                    Animated = false;
                    StateHasChanged();
                });
            }
            else
            {
                RunDirectly(false);
            }
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            if (!_attached)
            {
                return;
            }

            if (ContentRef.Context != null)
            {
                await Js.InvokeVoidAsync(JsInteropConstants.DelElementFrom, ContentRef, AttachSelector);
            }

            if (OverlayRef?.Context != null)
            {
                await Js.InvokeVoidAsync(JsInteropConstants.DelElementFrom, OverlayRef, AttachSelector);
            }
            
            if (!NoOutsideClick)
            {
                await OutsideClickJsModule.UnbindAndDisposeAsync();
            }
        }

        public override async Task HandleOnOutsideClickAsync()
        {
            var maxZIndex = await GetMaxZIndex();

            // TODO: should ignore the click if e.target was dragged onto the overlay
            if (IsActive && ZIndex >= maxZIndex)
            {
                await OnOutsideClick.InvokeAsync();

                Close();
            }
        }

        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            if (firstRender && CascadingDependent is not null)
            {
                (this as IDependent).CascadingDependents.ForEach(item => item.AddDependent(this));
            }
        }

        public void AddDependent(IDependent dependent)
        {
            if (NoOutsideClick)
            {
                return;
            }
            
            _dependents.Add(dependent);
            NextTickIf(
                () => { _ = OutsideClickJsModule.UpdateDependentElementsAsync(DependentSelectors.ToArray()); },
                () => !OutsideClickJsModule.Initialized);
        }

        public Dictionary<string, object> ContentAttrs
        {
            get
            {
                var attrs = new Dictionary<string, object>();

                if (IsActive)
                {
                    attrs.Add("tabindex", 0);
                }

                if (ContentAttributes != null)
                {
                    foreach (var pair in ContentAttributes)
                    {
                        attrs[pair.Key] = pair.Value;
                    }
                }

                return attrs;
            }
        }

        protected string AttachSelector
            => Attach ?? (Permanent ? ".m-application__permanent" : ".m-application");

        public IEnumerable<string> DependentSelectors
        {
            get
            {
                var elements = _dependents.SelectMany(dependent => dependent.DependentSelectors).ToList();

                if (ContentRef.TryGetSelector(out var selector))
                {
                    elements.Add(selector);
                }

                elements.Add(MSnackbar.ROOT_CSS_SELECTOR);
                elements.Add(PEnqueuedSnackbars.ROOT_CSS_SELECTOR);

                return elements.Distinct();
            }
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            _contentContext = new DialogContentContext(() => RunDirectly(false));
        }

        private static Block _block = new("m-dialog");
        private ModifierBuilder _modifierBuilder = _block.CreateModifierBuilder();
        private ModifierBuilder _containerModifierBuilder = _block.Element("container").CreateModifierBuilder();
        private ModifierBuilder _contentModifierBuilder = _block.Element("content").CreateModifierBuilder();

        protected override bool NoClass => true;
        protected override bool NoStyle => true;

        protected override IEnumerable<string> BuildComponentClass()
        {
            yield return _modifierBuilder.Add("active", IsActive)
                .Add(Persistent, Fullscreen, Scrollable, Animated)
                // NEXT MAJOR: ContentClass should be added into "content" element, but due to its widespread usage
                // and the potential for breaking changes, we keep it unchanged.
                .AddClass(ContentClass)
                .Build();
        }

        protected override IEnumerable<string> BuildComponentStyle()
        {
            var styles = StyleBuilder.Create()
                .Add("transform-origin", Origin)
                .AddWidth(Width)
                .AddMaxWidth(MaxWidth)
                .GenerateCssStyles();

            if (ContentStyle != null)
            {
                // NEXT MAJOR: ContentClass should be added into "content" element, but due to its widespread usage
                // and the potential for breaking changes, we keep it unchanged.
                return styles.Concat(new[] { ContentStyle });
            }

            return styles;
        }
    }
}