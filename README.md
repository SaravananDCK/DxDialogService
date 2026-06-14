# DxDialogService

A programmatic **dialog service for Blazor**, rendered with the **DevExpress `DxPopup`** component.

Call dialogs from C# instead of declaring them in markup — `Open`, `OpenAsync<T>`, `Confirm`,
`Alert`, side panels, nested/stacked dialogs, draggable/resizable windows, and `CanClose` guards —
all backed by `DxPopup`, so there's no other UI dependency on the page.

```csharp
var ok = await DialogService.Confirm("Delete this item?", "Confirm");
var result = await DialogService.OpenAsync<EditPerson>("Edit", parameters, new DialogOptions { Width = "600px" });
```

## Why

DevExpress ships `DxPopup`, but no service-based dialog API — you normally declare a `DxPopup` in
markup and toggle a `Visible` flag. This library gives you the ergonomic *call-and-await* pattern
(open a component as a dialog, `await` its result, close from inside) on top of `DxPopup`.

## Requirements

- .NET 10 / Blazor
- **DevExpress Blazor** (`DevExpress.Blazor`). It's a licensed package on the DevExpress NuGet feed;
  you need your own DevExpress license and feed access to restore it. This library targets
  `DevExpress.Blazor` 25.2.x.

## Install

```bash
dotnet add package DxDialogService
```

> Because `DevExpress.Blazor` comes from the DevExpress feed, make sure that feed is configured in
> your `NuGet.config` (or globally) so the transitive dependency restores.

## Setup

```csharp
// Program.cs
builder.Services.AddDevExpressBlazor();   // DevExpress itself
builder.Services.AddDxDialogService();     // registers DialogService (scoped)
```

In your `<head>`, alongside the DevExpress theme and `<DxResourceManager />`, reference the
stylesheet (it provides the flush side-drawer layout and the dim-behind-modal effect):

```html
<link rel="stylesheet" href="_content/DevExpress.Blazor.Themes/blazing-berry.bs5.min.css" />
<link rel="stylesheet" href="_content/DxDialogService/dxdialogservice.css" />
<DxResourceManager />
```

Place exactly one host in your main layout:

```razor
@* MainLayout.razor *@
<DxDialogHost />
@Body
```

## Usage

```razor
@inject DialogService DialogService

@* Alert / Confirm *@
await DialogService.Alert("Saved.", "Done");
bool? ok = await DialogService.Confirm("Delete this item?", "Confirm");

@* A component in a dialog, awaiting its result *@
var result = await DialogService.OpenAsync<EditPerson>("Edit",
    new Dictionary<string, object?> { { "Id", 42 } },
    new DialogOptions { Width = "600px", Draggable = true, Resizable = true });

@* Close from inside the dialog component *@
[CascadingParameter] public Dialog? Dialog { get; set; }
DialogService.Close(theResult);

@* Side / drawer dialog *@
await DialogService.OpenSideAsync<Filters>("Filters", null,
    new SideDialogOptions { Position = DialogPosition.Right, Width = "420px" });
```

### Option mapping (DialogOptions → DxPopup)

| DialogOptions | DxPopup |
|---|---|
| `Width` / `Height` | `Width` / `Height` |
| `ShowTitle` | `ShowHeader` |
| `ShowClose` | `ShowCloseButton` |
| `CloseDialogOnEsc` | `CloseOnEscape` |
| `CloseDialogOnOverlayClick` | `CloseOnOutsideClick` |
| `Draggable` / `Resizable` | `AllowDrag` / `AllowResize` |
| `Left` / `Top` (px) | `PositionX` / `PositionY` |
| `CssClass` | `CssClass` |
| `CanClose` | enforced in the `Closing` handler |
| `Drag` / `Resize` callbacks | wired to `DragCompleted` / `ResizeCompleted` |

`SideDialogOptions.Position` maps to `DxPopup` alignment; the drawer fills the cross-axis
(left/right → full height, top/bottom → full width).

## Nested dialogs

Stacking works in every direction:

- centered → centered → centered (open a dialog from inside a dialog, N deep)
- side panel → centered → confirm/alert

Each `DxPopup` gets its own incrementing z-index and backdrop, so popups layer correctly, and the
side drawer dims while a centered dialog is stacked above it. Closing always targets the top of the
stack (`Close` / `TryCloseAsync` for centered, `CloseSide` for the side panel).

> A component used as **side** dialog content closes itself with `DialogService.CloseSide(...)`;
> a **centered** dialog uses `DialogService.Close(...)`.

The `sample/` app demonstrates all of this — open the centered or a side dialog (Right/Left/Top/
Bottom) and use its *Open nested…* buttons to stack popups.

## Build / run

```bash
dotnet build src/DxDialogService/DxDialogService.csproj
dotnet run  --project sample/DxDialogService.Sample   # open the printed localhost URL
```

## Publishing

Publishing uses **NuGet Trusted Publishing** (OIDC) — no long-lived API key is stored.

One-time setup on nuget.org (Account → **Trusted Publishing**), create a policy with:
- **Repository owner:** `SaravananDCK`
- **Repository:** `DxDialogService`
- **Workflow file:** `publish.yml`

Then in the GitHub repo configure:
- Repository **variable** `NUGET_USER` — your nuget.org username (used by the `NuGet/login` action).
- Repository **secret** `DEVEXPRESS_NUGET_FEED` — your DevExpress feed URL (full
  `.../api/v3/index.json`) so CI can restore `DevExpress.Blazor`.

Tag a release to publish:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow requests a GitHub OIDC token (`id-token: write`), exchanges it for a short-lived
NuGet key via `NuGet/login`, then pushes the package.

## License

MIT — see [LICENSE](LICENSE).

Portions of the dialog service are adapted from an MIT-licensed project; the required attribution
is in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
