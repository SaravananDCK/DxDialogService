using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DxDialogService
{
    /// <summary>
    /// Programmatic dialog service for Blazor. The rendering is provided by
    /// <see cref="DxDialogHost"/>, which is built on the DevExpress <c>DxPopup</c> component.
    ///
    /// Register it as a scoped service (see <c>AddDxDialogService</c>) and place a single
    /// <c>&lt;DxDialogHost /&gt;</c> in your main layout.
    /// </summary>
    /// <example>
    /// <code>
    /// @inject DialogService DialogService
    /// &lt;DxButton Text="Show dialog" Click="@ShowDialog" /&gt;
    /// @code {
    ///   async Task ShowDialog()
    ///   {
    ///     var result = await DialogService.OpenAsync&lt;MyComponent&gt;("Title",
    ///       new Dictionary&lt;string, object?&gt; { { "Id", 42 } },
    ///       new DialogOptions { Width = "700px", Resizable = true, Draggable = true });
    ///   }
    /// }
    /// </code>
    /// </example>
    public class DialogService : IDisposable
    {
        /// <summary>
        /// Gets or sets the navigation manager used to auto-close dialogs on navigation. Optional.
        /// </summary>
        private NavigationManager? UriHelper { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogService"/> class.
        /// </summary>
        /// <param name="uriHelper">The navigation manager. When supplied, open dialogs are closed on location change.</param>
        public DialogService(NavigationManager? uriHelper = null)
        {
            UriHelper = uriHelper;

            if (UriHelper != null)
            {
                UriHelper.LocationChanged += UriHelper_OnLocationChanged;
            }
        }

        private void UriHelper_OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        {
            while (dialogs.Count > 0)
            {
                Close();
            }

            if (sideDialogResultTask?.Task.IsCompleted == false)
            {
                CloseSide();
            }
        }

        /// <summary>Raised when the last dialog is closed.</summary>
        public event Action<dynamic>? OnClose;

        /// <summary>Raised when a dialog requests a refresh.</summary>
        public event Action? OnRefresh;

        /// <summary>Raised when a new dialog is opened.</summary>
        public event Action<string?, Type, Dictionary<string, object?>, DialogOptions>? OnOpen;

        /// <summary>Raised when the side dialog is closed.</summary>
        public event Action<dynamic>? OnSideClose;

        /// <summary>Raised when the side dialog is opened.</summary>
        public event Action<Type, Dictionary<string, object?>, SideDialogOptions>? OnSideOpen;

        /// <summary>
        /// Opens a dialog with the specified component.
        /// </summary>
        public virtual void Open<T>(string title, Dictionary<string, object?>? parameters = null, DialogOptions? options = null) where T : ComponentBase
        {
            OpenDialog<T>(title, parameters, options);
        }

        /// <summary>
        /// Opens a dialog with the specified component type.
        /// </summary>
        public virtual void Open(string title, Type componentType, Dictionary<string, object?>? parameters = null, DialogOptions? options = null)
        {
            if (!typeof(ComponentBase).IsAssignableFrom(componentType))
            {
                throw new ArgumentException("The component type must be a subclass of ComponentBase.", nameof(componentType));
            }

            OpenDialog(title, componentType, parameters, options);
        }

        /// <summary>
        /// Invokes <see cref="OnRefresh" />.
        /// </summary>
        public void Refresh()
        {
            OnRefresh?.Invoke();
        }

        /// <summary>The pending result tasks.</summary>
        protected List<TaskCompletionSource<dynamic?>> tasks = new();
        private TaskCompletionSource<dynamic?>? sideDialogResultTask;
        private SideDialogOptions? currentSideDialogOptions;

        /// <summary>
        /// Opens a dialog with the specified component and awaits its result.
        /// </summary>
        /// <returns>The value passed to <see cref="Close" />.</returns>
        public virtual Task<dynamic?> OpenAsync<T>(string title, Dictionary<string, object?>? parameters = null, DialogOptions? options = null) where T : ComponentBase
        {
            var task = new TaskCompletionSource<dynamic?>();
            tasks.Add(task);

            OpenDialog<T>(title, parameters, options);

            return task.Task;
        }

        /// <summary>
        /// Opens a dialog with the specified component type and awaits its result.
        /// </summary>
        public virtual Task<dynamic?> OpenAsync(string title, Type componentType, Dictionary<string, object?>? parameters = null, DialogOptions? options = null)
        {
            if (!typeof(ComponentBase).IsAssignableFrom(componentType))
            {
                throw new ArgumentException("The component type must be a subclass of ComponentBase.", nameof(componentType));
            }

            var task = new TaskCompletionSource<dynamic?>();
            tasks.Add(task);

            OpenDialog(title, componentType, parameters, options);

            return task.Task;
        }

        /// <summary>
        /// Opens a side dialog with the specified component and awaits its result.
        /// </summary>
        public Task<dynamic?> OpenSideAsync<T>(string title, Dictionary<string, object?>? parameters = null, SideDialogOptions? options = null)
            where T : ComponentBase
        {
            CloseSideSilently();
            sideDialogResultTask = new TaskCompletionSource<dynamic?>();
            options ??= new SideDialogOptions();

            options.Title = title;
            currentSideDialogOptions = options;
            OnSideOpen?.Invoke(typeof(T), parameters ?? new Dictionary<string, object?>(), options);
            return sideDialogResultTask.Task;
        }

        /// <summary>
        /// Opens a side dialog with the specified component type and awaits its result.
        /// </summary>
        public Task<dynamic?> OpenSideAsync(string title, Type componentType, Dictionary<string, object?>? parameters = null, SideDialogOptions? options = null)
        {
            if (!typeof(ComponentBase).IsAssignableFrom(componentType))
            {
                throw new ArgumentException("The component type must be a subclass of ComponentBase.", nameof(componentType));
            }

            CloseSideSilently();
            sideDialogResultTask = new TaskCompletionSource<dynamic?>();
            options ??= new SideDialogOptions();

            options.Title = title;
            currentSideDialogOptions = options;
            OnSideOpen?.Invoke(componentType, parameters ?? new Dictionary<string, object?>(), options);

            return sideDialogResultTask.Task;
        }

        /// <summary>
        /// Opens a side dialog with the specified component (fire and forget).
        /// </summary>
        public void OpenSide<T>(string title, Dictionary<string, object?>? parameters = null, SideDialogOptions? options = null)
            where T : ComponentBase
        {
            CloseSideSilently();
            options ??= new SideDialogOptions();

            options.Title = title;
            currentSideDialogOptions = options;
            OnSideOpen?.Invoke(typeof(T), parameters ?? new Dictionary<string, object?>(), options);
        }

        /// <summary>
        /// Opens a side dialog with the specified component type (fire and forget).
        /// </summary>
        public void OpenSide(string title, Type componentType, Dictionary<string, object?>? parameters = null, SideDialogOptions? options = null)
        {
            if (!typeof(ComponentBase).IsAssignableFrom(componentType))
            {
                throw new ArgumentException("The component type must be a subclass of ComponentBase.", nameof(componentType));
            }

            CloseSideSilently();
            options ??= new SideDialogOptions();

            options.Title = title;
            currentSideDialogOptions = options;
            OnSideOpen?.Invoke(componentType, parameters ?? new Dictionary<string, object?>(), options);
        }

        private void CloseSideSilently()
        {
            if (sideDialogResultTask?.Task.IsCompleted == false)
            {
                sideDialogResultTask.TrySetResult(null!);
            }
            currentSideDialogOptions = null;
        }

        /// <summary>
        /// Closes the side dialog with an optional result.
        /// </summary>
        public virtual void CloseSide(dynamic? result = null)
        {
            if (sideDialogResultTask?.Task.IsCompleted == false)
            {
                sideDialogResultTask.TrySetResult(result);
            }

            currentSideDialogOptions = null;
            OnSideClose?.Invoke(result);
        }

        private TaskCompletionSource? sideDialogCloseTask;

        internal void OnSideCloseComplete()
        {
            sideDialogCloseTask?.TrySetResult();
            sideDialogCloseTask = null;
        }

        /// <summary>
        /// Closes the side dialog and waits for the closing animation to finish.
        /// </summary>
        public async Task CloseSideAsync(dynamic? result = null)
        {
            sideDialogCloseTask = new TaskCompletionSource();

            CloseSide(result);

            await sideDialogCloseTask.Task;
        }

        /// <summary>
        /// Opens a dialog with inline content and awaits its result.
        /// </summary>
        public virtual Task<dynamic?> OpenAsync(string title, RenderFragment<DialogService> childContent, DialogOptions? options = null, CancellationToken? cancellationToken = null)
        {
            var task = new TaskCompletionSource<dynamic?>();

            if (cancellationToken.HasValue)
                cancellationToken.Value.Register(() => task.TrySetCanceled());

            tasks.Add(task);

            options ??= new DialogOptions();
            options.ChildContent = childContent;

            OpenDialog<object>(title, null, options);

            return task.Task;
        }

        /// <summary>
        /// Opens a dialog with inline title and content and awaits its result.
        /// </summary>
        public virtual Task<dynamic?> OpenAsync(RenderFragment<DialogService> titleContent, RenderFragment<DialogService> childContent, DialogOptions? options = null, CancellationToken? cancellationToken = null)
        {
            var task = new TaskCompletionSource<dynamic?>();

            if (cancellationToken.HasValue)
                cancellationToken.Value.Register(() => task.TrySetCanceled());

            tasks.Add(task);

            options ??= new DialogOptions();
            options.ChildContent = childContent;
            options.TitleContent = titleContent;

            OpenDialog<object>(null, null, options);

            return task.Task;
        }

        /// <summary>
        /// Opens a dialog with inline content (fire and forget).
        /// </summary>
        public virtual void Open(string title, RenderFragment<DialogService> childContent, DialogOptions? options = null)
        {
            options ??= new DialogOptions();

            options.ChildContent = childContent;

            OpenDialog<object>(title, null, options);
        }

        /// <summary>The open dialogs.</summary>
        protected List<DialogOptions> dialogs = new();

        internal void OpenDialog<T>(string? title, Dictionary<string, object?>? parameters, DialogOptions? options)
        {
            OpenDialog(title, typeof(T), parameters, options);
        }

        internal void OpenDialog(string? title, Type componentType, Dictionary<string, object?>? parameters, DialogOptions? options)
        {
            options ??= new();
            parameters ??= new Dictionary<string, object?>();

            dialogs.Add(options);
            options.Width = !string.IsNullOrEmpty(options.Width) ? options.Width : "600px";
            options.Left = !string.IsNullOrEmpty(options.Left) ? options.Left : "";
            options.Top = !string.IsNullOrEmpty(options.Top) ? options.Top : "";
            options.Bottom = !string.IsNullOrEmpty(options.Bottom) ? options.Bottom : "";
            options.Height = !string.IsNullOrEmpty(options.Height) ? options.Height : "";
            options.Style = !string.IsNullOrEmpty(options.Style) ? options.Style : "";
            options.CssClass = !string.IsNullOrEmpty(options.CssClass) ? options.CssClass : "";
            options.WrapperCssClass = !string.IsNullOrEmpty(options.WrapperCssClass) ? options.WrapperCssClass : "";
            options.ContentCssClass = !string.IsNullOrEmpty(options.ContentCssClass) ? options.ContentCssClass : "";

            OnOpen?.Invoke(title, componentType, parameters, options);
        }

        /// <summary>
        /// Closes the last opened dialog with an optional result.
        /// </summary>
        public virtual void Close(dynamic? result = null)
        {
            var dialog = dialogs.LastOrDefault();

            if (dialog != null)
            {
                OnClose?.Invoke(result);
                dialogs.Remove(dialog);
            }

            var task = tasks.LastOrDefault();
            if (task != null && task.Task != null && !task.Task.IsCompleted)
            {
                tasks.Remove(task);
                task.SetResult(result);
            }
        }

        /// <summary>
        /// Attempts to close the last opened dialog, honoring <see cref="DialogOptionsBase.CanClose"/> if set.
        /// </summary>
        /// <returns><c>true</c> if the dialog was closed; <c>false</c> if prevented by <see cref="DialogOptionsBase.CanClose"/>.</returns>
        public virtual async Task<bool> TryCloseAsync(dynamic? result = null)
        {
            var dialogOptions = dialogs.LastOrDefault();

            if (dialogOptions?.CanClose is { } canClose && !await canClose())
            {
                return false;
            }

            Close(result);
            return true;
        }

        /// <summary>
        /// Attempts to close the side dialog, honoring <see cref="DialogOptionsBase.CanClose"/> if set.
        /// </summary>
        public virtual async Task<bool> TryCloseSideAsync(dynamic? result = null)
        {
            if (currentSideDialogOptions?.CanClose is { } canClose && !await canClose())
            {
                return false;
            }

            CloseSide(result);
            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (UriHelper != null)
            {
                UriHelper.LocationChanged -= UriHelper_OnLocationChanged;
            }
        }

        /// <summary>
        /// Displays a confirmation dialog with OK / Cancel buttons.
        /// </summary>
        /// <returns><c>true</c> if OK was clicked, <c>false</c> if Cancel, <c>null</c> if dismissed.</returns>
        public virtual async Task<bool?> Confirm(string message = "Confirm?", string title = "Confirm", ConfirmOptions? options = null, CancellationToken? cancellationToken = null)
        {
            options ??= new();
            options.OkButtonText = !string.IsNullOrEmpty(options.OkButtonText) ? options.OkButtonText : "Ok";
            options.CancelButtonText = !string.IsNullOrEmpty(options.CancelButtonText) ? options.CancelButtonText : "Cancel";
            options.CssClass = !string.IsNullOrEmpty(options.CssClass) ? $"dxds-dialog-confirm {options.CssClass}" : "dxds-dialog-confirm";

            return await OpenAsync(title, ds => BuildConfirmContent(ds, message, options, withCancel: true), options, cancellationToken);
        }

        /// <summary>
        /// Displays a confirmation dialog with custom message content.
        /// </summary>
        public virtual async Task<bool?> Confirm(RenderFragment message, string title = "Confirm", ConfirmOptions? options = null, CancellationToken? cancellationToken = null)
        {
            options ??= new();
            options.OkButtonText = !string.IsNullOrEmpty(options.OkButtonText) ? options.OkButtonText : "Ok";
            options.CancelButtonText = !string.IsNullOrEmpty(options.CancelButtonText) ? options.CancelButtonText : "Cancel";
            options.CssClass = !string.IsNullOrEmpty(options.CssClass) ? $"dxds-dialog-confirm {options.CssClass}" : "dxds-dialog-confirm";

            return await OpenAsync(title, ds => BuildConfirmContent(ds, message, options, withCancel: true), options, cancellationToken);
        }

        /// <summary>
        /// Displays an alert dialog with a single OK button.
        /// </summary>
        public virtual async Task<bool?> Alert(string message = "", string title = "Message", AlertOptions? options = null, CancellationToken? cancellationToken = null)
        {
            options ??= new();
            options.OkButtonText = !string.IsNullOrEmpty(options.OkButtonText) ? options.OkButtonText : "Ok";
            options.CssClass = !string.IsNullOrEmpty(options.CssClass) ? $"dxds-dialog-alert {options.CssClass}" : "dxds-dialog-alert";

            return await OpenAsync(title, ds => BuildAlertContent(ds, message, options), options, cancellationToken);
        }

        /// <summary>
        /// Displays an alert dialog with custom message content.
        /// </summary>
        public virtual async Task<bool?> Alert(RenderFragment message, string title = "Message", AlertOptions? options = null, CancellationToken? cancellationToken = null)
        {
            options ??= new();
            options.OkButtonText = !string.IsNullOrEmpty(options.OkButtonText) ? options.OkButtonText : "Ok";
            options.CssClass = !string.IsNullOrEmpty(options.CssClass) ? $"dxds-dialog-alert {options.CssClass}" : "dxds-dialog-alert";

            return await OpenAsync(title, ds => BuildAlertContent(ds, message, options), options, cancellationToken);
        }

        // Builds the OK/Cancel confirm content using DevExpress DxButton.
        private RenderFragment BuildConfirmContent(DialogService ds, object message, ConfirmOptions options, bool withCancel) => b =>
        {
            var i = 0;
            b.OpenElement(i++, "p");
            b.AddAttribute(i++, "class", "dxds-dialog-confirm-message");
            AddMessage(b, ref i, message);
            b.CloseElement();

            b.OpenElement(i++, "div");
            b.AddAttribute(i++, "class", "dxds-dialog-confirm-buttons");

            b.OpenComponent<DxButton>(i++);
            b.AddAttribute(i++, "Text", options.OkButtonText);
            b.AddAttribute(i++, "RenderStyle", ButtonRenderStyle.Primary);
            b.AddAttribute(i++, "Click", EventCallback.Factory.Create<MouseEventArgs>(this, () => ds.Close(true)));
            b.CloseComponent();

            if (withCancel)
            {
                b.OpenComponent<DxButton>(i++);
                b.AddAttribute(i++, "Text", options.CancelButtonText);
                b.AddAttribute(i++, "RenderStyle", ButtonRenderStyle.Secondary);
                b.AddAttribute(i++, "Click", EventCallback.Factory.Create<MouseEventArgs>(this, () => ds.Close(false)));
                b.CloseComponent();
            }

            b.CloseElement();
        };

        private RenderFragment BuildAlertContent(DialogService ds, object message, AlertOptions options) => b =>
        {
            var i = 0;
            b.OpenElement(i++, "p");
            b.AddAttribute(i++, "class", "dxds-dialog-alert-message");
            AddMessage(b, ref i, message);
            b.CloseElement();

            b.OpenElement(i++, "div");
            b.AddAttribute(i++, "class", "dxds-dialog-alert-buttons");

            b.OpenComponent<DxButton>(i++);
            b.AddAttribute(i++, "Text", options.OkButtonText);
            b.AddAttribute(i++, "RenderStyle", ButtonRenderStyle.Primary);
            b.AddAttribute(i++, "Click", EventCallback.Factory.Create<MouseEventArgs>(this, () => ds.Close(true)));
            b.CloseComponent();

            b.CloseElement();
        };

        private static void AddMessage(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder b, ref int i, object message)
        {
            if (message is RenderFragment fragment)
            {
                b.AddContent(i++, fragment);
            }
            else
            {
                b.AddContent(i++, message?.ToString());
            }
        }
    }

    /// <summary>
    /// Base class for dialog options.
    /// </summary>
    public abstract class DialogOptionsBase : INotifyPropertyChanged
    {
        /// <summary>Occurs when a property value changes.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Raises the <see cref="PropertyChanged" /> event.</summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool showTitle = true;
        /// <summary>Whether to show the title bar. Default <c>true</c>.</summary>
        public bool ShowTitle
        {
            get => showTitle;
            set { if (showTitle != value) { showTitle = value; OnPropertyChanged(nameof(ShowTitle)); } }
        }

        private bool showClose = true;
        /// <summary>Whether to show the close button. Default <c>true</c>.</summary>
        public bool ShowClose
        {
            get => showClose;
            set { if (showClose != value) { showClose = value; OnPropertyChanged(nameof(ShowClose)); } }
        }

        private string? ariaLabel = "Dialog";
        /// <summary>The dialog aria-label text when no title is rendered.</summary>
        public string? AriaLabel
        {
            get => ariaLabel;
            set { if (ariaLabel != value) { ariaLabel = value; OnPropertyChanged(nameof(AriaLabel)); } }
        }

        private string closeAriaLabel = "Close dialog";
        /// <summary>The close button aria-label text.</summary>
        public string CloseAriaLabel
        {
            get => closeAriaLabel;
            set { if (closeAriaLabel != value) { closeAriaLabel = value; OnPropertyChanged(nameof(CloseAriaLabel)); } }
        }

        private string? width;
        /// <summary>The width of the dialog (CSS value, e.g. <c>600px</c>).</summary>
        public string? Width
        {
            get => width;
            set { if (width != value) { width = value; OnPropertyChanged(nameof(Width)); } }
        }

        private string? height;
        /// <summary>The height of the dialog (CSS value).</summary>
        public string? Height
        {
            get => height;
            set { if (height != value) { height = value; OnPropertyChanged(nameof(Height)); } }
        }

        private string? style;
        /// <summary>Extra inline CSS applied to the dialog body wrapper.</summary>
        public string? Style
        {
            get => style;
            set { if (style != value) { style = value; OnPropertyChanged(nameof(Style)); } }
        }

        private bool closeDialogOnOverlayClick;
        /// <summary>Whether clicking the overlay closes the dialog.</summary>
        public bool CloseDialogOnOverlayClick
        {
            get => closeDialogOnOverlayClick;
            set { if (closeDialogOnOverlayClick != value) { closeDialogOnOverlayClick = value; OnPropertyChanged(nameof(CloseDialogOnOverlayClick)); } }
        }

        private string? cssClass;
        /// <summary>Custom CSS class added to the dialog.</summary>
        public string? CssClass
        {
            get => cssClass;
            set { if (cssClass != value) { cssClass = value; OnPropertyChanged(nameof(CssClass)); } }
        }

        private string? wrapperCssClass;
        /// <summary>CSS classes added to the dialog wrapper element.</summary>
        public string? WrapperCssClass
        {
            get => wrapperCssClass;
            set { if (wrapperCssClass != value) { wrapperCssClass = value; OnPropertyChanged(nameof(WrapperCssClass)); } }
        }

        private string? contentCssClass;
        /// <summary>CSS classes added to the dialog content element.</summary>
        public string? ContentCssClass
        {
            get => contentCssClass;
            set { if (contentCssClass != value) { contentCssClass = value; OnPropertyChanged(nameof(ContentCssClass)); } }
        }

        private int closeTabIndex;
        /// <summary>The close button tab index. Default <c>0</c>.</summary>
        public int CloseTabIndex
        {
            get => closeTabIndex;
            set { if (closeTabIndex != value) { closeTabIndex = value; OnPropertyChanged(nameof(CloseTabIndex)); } }
        }

        private Func<Task<bool>>? canClose;
        /// <summary>
        /// Callback invoked when the user attempts to close the dialog (close button, overlay click, or ESC).
        /// Return <c>false</c> to prevent closing. Not invoked when <see cref="DialogService.Close"/> is called programmatically.
        /// </summary>
        [JsonIgnore]
        public Func<Task<bool>>? CanClose
        {
            get => canClose;
            set { if (canClose != value) { canClose = value; OnPropertyChanged(nameof(CanClose)); } }
        }

        private RenderFragment<DialogService>? titleContent;
        private bool resizable;

        /// <summary>The title bar content.</summary>
        public RenderFragment<DialogService>? TitleContent
        {
            get => titleContent;
            set { if (titleContent != value) { titleContent = value; OnPropertyChanged(nameof(TitleContent)); } }
        }

        /// <summary>Whether the dialog is resizable. Default <c>false</c>.</summary>
        public bool Resizable
        {
            get => resizable;
            set { if (resizable != value) { resizable = value; OnPropertyChanged(nameof(Resizable)); } }
        }
    }

    /// <summary>
    /// Options for a side (drawer) dialog.
    /// </summary>
    public class SideDialogOptions : DialogOptionsBase
    {
        private string? title;
        /// <summary>The title displayed on the dialog.</summary>
        public string? Title
        {
            get => title;
            set { if (title != value) { title = value; OnPropertyChanged(nameof(Title)); } }
        }

        private DialogPosition position = DialogPosition.Right;
        /// <summary>The side the dialog is anchored to.</summary>
        public DialogPosition Position
        {
            get => position;
            set { if (position != value) { position = value; OnPropertyChanged(nameof(Position)); } }
        }

        private bool showMask = true;
        /// <summary>Whether to show a background mask. Default <c>true</c>.</summary>
        public bool ShowMask
        {
            get => showMask;
            set { if (showMask != value) { showMask = value; OnPropertyChanged(nameof(ShowMask)); } }
        }
    }

    /// <summary>
    /// The side a <see cref="SideDialogOptions"/> dialog is anchored to.
    /// </summary>
    public enum DialogPosition
    {
        /// <summary>Anchored to the right.</summary>
        Right,
        /// <summary>Anchored to the left.</summary>
        Left,
        /// <summary>Anchored to the top.</summary>
        Top,
        /// <summary>Anchored to the bottom.</summary>
        Bottom
    }

    /// <summary>
    /// Options for a centered (modal) dialog.
    /// </summary>
    public class DialogOptions : DialogOptionsBase
    {
        private Action<Size>? resize;
        /// <summary>Invoked when the dialog is resized.</summary>
        public Action<Size>? Resize
        {
            get => resize;
            set { if (resize != value) { resize = value; OnPropertyChanged(nameof(Resize)); } }
        }

        private bool draggable;
        /// <summary>Whether the dialog is draggable. Default <c>false</c>.</summary>
        public bool Draggable
        {
            get => draggable;
            set { if (draggable != value) { draggable = value; OnPropertyChanged(nameof(Draggable)); } }
        }

        private Action<Point>? drag;
        /// <summary>Invoked when the dialog is dragged.</summary>
        public Action<Point>? Drag
        {
            get => drag;
            set { if (drag != value) { drag = value; OnPropertyChanged(nameof(Drag)); } }
        }

        private string? left;
        /// <summary>The initial X position of the dialog (CSS value). Maps to DxPopup PositionX when in pixels.</summary>
        public string? Left
        {
            get => left;
            set { if (left != value) { left = value; OnPropertyChanged(nameof(Left)); } }
        }

        private string? top;
        /// <summary>The initial Y position of the dialog (CSS value). Maps to DxPopup PositionY when in pixels.</summary>
        public string? Top
        {
            get => top;
            set { if (top != value) { top = value; OnPropertyChanged(nameof(Top)); } }
        }

        private string? bottom;
        /// <summary>The <c>bottom</c> CSS value (advisory; DxPopup positions via X/Y).</summary>
        public string? Bottom
        {
            get => bottom;
            set { if (bottom != value) { bottom = value; OnPropertyChanged(nameof(Bottom)); } }
        }

        private RenderFragment<DialogService>? childContent;
        /// <summary>The inline content of the dialog.</summary>
        public RenderFragment<DialogService>? ChildContent
        {
            get => childContent;
            set { if (childContent != value) { childContent = value; OnPropertyChanged(nameof(ChildContent)); } }
        }

        private bool closeDialogOnEsc = true;
        /// <summary>Whether the dialog closes on ESC. Default <c>true</c>.</summary>
        public bool CloseDialogOnEsc
        {
            get => closeDialogOnEsc;
            set { if (closeDialogOnEsc != value) { closeDialogOnEsc = value; OnPropertyChanged(nameof(CloseDialogOnEsc)); } }
        }
    }

    /// <summary>
    /// Options for an alert dialog.
    /// </summary>
    public class AlertOptions : DialogOptions
    {
        private string? okButtonText;
        /// <summary>The text of the OK button.</summary>
        public string? OkButtonText
        {
            get => okButtonText;
            set { if (okButtonText != value) { okButtonText = value; OnPropertyChanged(nameof(OkButtonText)); } }
        }
    }

    /// <summary>
    /// Options for a confirm dialog.
    /// </summary>
    public class ConfirmOptions : AlertOptions
    {
        private string? cancelButtonText;
        /// <summary>The text of the Cancel button.</summary>
        public string? CancelButtonText
        {
            get => cancelButtonText;
            set { if (cancelButtonText != value) { cancelButtonText = value; OnPropertyChanged(nameof(CancelButtonText)); } }
        }
    }

    /// <summary>
    /// Represents a single open dialog instance (title + component type + parameters + options).
    /// </summary>
    public class Dialog : INotifyPropertyChanged
    {
        private string? title;
        /// <summary>The dialog title.</summary>
        public string? Title
        {
            get => title;
            set { if (title != value) { title = value; OnPropertyChanged(nameof(Title)); } }
        }

        private Type? type;
        /// <summary>The component type rendered inside the dialog.</summary>
        public Type? Type
        {
            get => type;
            set { if (type != value) { type = value; OnPropertyChanged(nameof(Type)); } }
        }

        private Dictionary<string, object?>? parameters;
        /// <summary>The parameters passed to the component.</summary>
        public Dictionary<string, object?>? Parameters
        {
            get => parameters;
            set { if (parameters != value) { parameters = value; OnPropertyChanged(nameof(Parameters)); } }
        }

        private DialogOptions? options;
        /// <summary>The dialog options.</summary>
        public DialogOptions? Options
        {
            get => options;
            set { if (options != value) { options = value; OnPropertyChanged(nameof(Options)); } }
        }

        /// <summary>Occurs when a property value changes.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Raises the <see cref="PropertyChanged"/> event.</summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
