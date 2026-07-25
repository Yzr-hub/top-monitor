# TopMonitor Unified Application Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate the approved performance-curve icon and use it consistently for the TopMonitor EXE, WPF windows, and system tray.

**Architecture:** Keep an editable SVG source beside the WPF application assets, render a 1024px PNG preview and a multi-resolution ICO deterministically, then embed the ICO as a WPF resource and Win32 application icon. Load that same packaged resource once for the tray icon, replacing the runtime-generated “TM” bitmap without adding background work.

**Tech Stack:** .NET 10, C#, WPF, H.NotifyIcon.Wpf 2.4.1, SVG, PNG, Windows ICO, PowerShell/Python asset tooling.

## Global Constraints

- The visual is a dark navy rounded square with transparent outer corners and a cyan-to-green rising performance curve.
- The icon contains no text.
- The ICO must contain 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixel images.
- The same packaged icon must be used by the EXE, main window, settings window, and system tray.
- Assets must be embedded in the application and must not require loose image files beside the published EXE.
- Icon loading must not add timers, worker threads, repeated rendering, or ongoing allocations.
- Existing Windows x64 publishing behavior must remain intact.

---

## File Structure

- `src/TopMonitor.App/Assets/TopMonitor.svg`: editable vector master.
- `src/TopMonitor.App/Assets/TopMonitor-1024.png`: high-resolution preview/source export.
- `src/TopMonitor.App/Assets/TopMonitor.ico`: embedded multi-resolution Windows icon.
- `src/TopMonitor.App/TopMonitor.App.csproj`: declares the Win32 application icon and WPF resource.
- `src/TopMonitor.App/MainWindow.xaml`: assigns the packaged icon to the overlay window.
- `src/TopMonitor.App/SettingsWindow.xaml`: assigns the packaged icon to the settings window.
- `src/TopMonitor.App/Services/TrayIconService.cs`: loads the packaged icon for H.NotifyIcon.
- `.gitignore`: excludes the local `.superpowers/` visual-companion workspace.

### Task 1: Create deterministic icon assets

**Files:**
- Create: `src/TopMonitor.App/Assets/TopMonitor.svg`
- Create: `src/TopMonitor.App/Assets/TopMonitor-1024.png`
- Create: `src/TopMonitor.App/Assets/TopMonitor.ico`
- Modify: `.gitignore`

**Interfaces:**
- Produces: `Assets/TopMonitor.ico`, consumed by MSBuild, WPF windows, and `TrayIconService`.
- Produces: nine ICO frames at 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixels.

- [ ] **Step 1: Add a failing asset validation command**

Run this before creating the files:

```powershell
$required = 16,20,24,32,40,48,64,128,256
$iconPath = 'src/TopMonitor.App/Assets/TopMonitor.ico'
if (-not (Test-Path $iconPath)) { throw "Missing $iconPath" }
```

Expected: FAIL with `Missing src/TopMonitor.App/Assets/TopMonitor.ico`.

- [ ] **Step 2: Create the SVG master**

Create an SVG with `viewBox="0 0 1024 1024"`, a transparent canvas, a rounded rectangle from `(72,72)` to `(952,952)` with corner radius `208` and fill `#101824`, and a performance curve whose rounded stroke transitions from cyan `#28D7FF` to green `#56F39A`. Keep all visible geometry at least 80 SVG units away from the canvas edge.

- [ ] **Step 3: Render PNG and ICO**

Use the workspace-provided Python runtime and installed imaging libraries to rasterize the SVG at 1024×1024, then downsample independently with high-quality Lanczos filtering for each required ICO size:

```python
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
source.save(
    "src/TopMonitor.App/Assets/TopMonitor.ico",
    format="ICO",
    sizes=[(size, size) for size in sizes],
)
```

Save the 1024×1024 RGBA render as `TopMonitor-1024.png`. Preserve transparent pixels outside the rounded square.

- [ ] **Step 4: Validate dimensions, frames, and alpha**

Run a script that opens the PNG and ICO and asserts:

```python
assert png.mode == "RGBA"
assert png.size == (1024, 1024)
assert png.getpixel((0, 0))[3] == 0
assert ico_sizes == {(16,16), (20,20), (24,24), (32,32), (40,40),
                     (48,48), (64,64), (128,128), (256,256)}
```

Expected: all assertions pass. Render the 16, 32, and 256 pixel frames to temporary PNG files and inspect them for clipping, muddy contrast, or broken transparency.

- [ ] **Step 5: Ignore local brainstorming files**

Add this exact entry to `.gitignore`:

```gitignore
.superpowers/
```

- [ ] **Step 6: Commit the assets**

```powershell
git add .gitignore src/TopMonitor.App/Assets
git commit -m "feat: add TopMonitor application icon assets"
```

### Task 2: Embed the icon in the EXE and WPF windows

**Files:**
- Modify: `src/TopMonitor.App/TopMonitor.App.csproj`
- Modify: `src/TopMonitor.App/MainWindow.xaml`
- Modify: `src/TopMonitor.App/SettingsWindow.xaml`

**Interfaces:**
- Consumes: `Assets/TopMonitor.ico` from Task 1.
- Produces: an EXE icon through `ApplicationIcon` and a WPF pack resource at `/Assets/TopMonitor.ico`.

- [ ] **Step 1: Verify the current build lacks the new resource contract**

Run:

```powershell
rg -n "ApplicationIcon|Assets.TopMonitor.ico|Icon=.*/Assets/TopMonitor.ico" src/TopMonitor.App
```

Expected: no matches.

- [ ] **Step 2: Configure the application and resource**

Add this property to the existing `PropertyGroup`:

```xml
<ApplicationIcon>Assets\TopMonitor.ico</ApplicationIcon>
```

Add this item group:

```xml
<ItemGroup>
  <Resource Include="Assets\TopMonitor.ico" />
</ItemGroup>
```

- [ ] **Step 3: Assign the icon to both windows**

Add this attribute to the root `<Window>` element in both XAML files:

```xml
Icon="/Assets/TopMonitor.ico"
```

- [ ] **Step 4: Build and verify the embedded EXE icon**

Run:

```powershell
dotnet build TopMonitor.sln -c Release
```

Expected: exit code 0 with 0 errors. Extract the associated icon from `src/TopMonitor.App/bin/Release/net10.0-windows/TopMonitor.exe` using `System.Drawing.Icon.ExtractAssociatedIcon`; save it to a temporary PNG and visually compare it with the approved asset.

- [ ] **Step 5: Commit EXE and window integration**

```powershell
git add src/TopMonitor.App/TopMonitor.App.csproj src/TopMonitor.App/MainWindow.xaml src/TopMonitor.App/SettingsWindow.xaml
git commit -m "feat: apply icon to executable and windows"
```

### Task 3: Use the packaged icon in the system tray

**Files:**
- Modify: `src/TopMonitor.App/Services/TrayIconService.cs`

**Interfaces:**
- Consumes: WPF resource URI `pack://application:,,,/Assets/TopMonitor.ico`.
- Produces: a frozen `BitmapImage` returned by `LoadIconSource()` and assigned to `TaskbarIcon.IconSource`.

- [ ] **Step 1: Establish the failing source check**

Run:

```powershell
rg -n "GeneratedIconSource|Text = \"TM\"" src/TopMonitor.App/Services/TrayIconService.cs
```

Expected: matches prove the tray still uses the old generated text icon.

- [ ] **Step 2: Add one-time packaged resource loading**

Replace the `GeneratedIconSource` block with:

```csharp
IconSource = LoadIconSource(),
```

Add `using System.Windows.Media.Imaging;`, remove the no-longer-needed media-brush import, and add:

```csharp
private static ImageSource LoadIconSource()
{
    var icon = new BitmapImage();
    icon.BeginInit();
    icon.UriSource = new Uri(
        "pack://application:,,,/Assets/TopMonitor.ico",
        UriKind.Absolute);
    icon.CacheOption = BitmapCacheOption.OnLoad;
    icon.EndInit();
    icon.Freeze();
    return icon;
}
```

The method performs no repeated work because `TrayIconService` is a singleton and calls it only during construction.

- [ ] **Step 3: Verify old generation code is gone**

Run:

```powershell
rg -n "GeneratedIconSource|Text = \"TM\"|SolidColorBrush|FontWeights" src/TopMonitor.App/Services/TrayIconService.cs
```

Expected: no matches.

- [ ] **Step 4: Run focused build verification**

Run:

```powershell
dotnet build src/TopMonitor.App/TopMonitor.App.csproj -c Release
```

Expected: exit code 0 with 0 warnings and 0 errors.

- [ ] **Step 5: Commit tray integration**

```powershell
git add src/TopMonitor.App/Services/TrayIconService.cs
git commit -m "feat: use application icon in system tray"
```

### Task 4: Full verification, publish, and delivery

**Files:**
- Verify: `TopMonitor.sln`
- Verify: `scripts/publish-win-x64.ps1`
- Verify: `artifacts/publish/win-x64/TopMonitor.exe`

**Interfaces:**
- Consumes: all changes from Tasks 1–3.
- Produces: verified published application and pushed Git history.

- [ ] **Step 1: Run all automated tests**

```powershell
dotnet test TopMonitor.sln -c Release --no-restore
```

Expected: all existing tests pass with 0 failures.

- [ ] **Step 2: Publish the Windows x64 application**

Avoid the machine-wide execution-policy restriction by invoking the script with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1
```

Expected: exit code 0 and `artifacts/publish/win-x64/TopMonitor.exe` exists.

- [ ] **Step 3: Validate the published artifact**

Extract the associated icon from the published EXE and compare it to the source. Confirm that `Assets/TopMonitor.ico` is not required as a loose file in the publish directory.

- [ ] **Step 4: Run a Windows smoke test**

Start the published EXE and verify:

- the process remains alive and responsive;
- the system tray shows the approved performance-curve icon;
- the main overlay and settings window use the same icon;
- tray hide/show, settings opening, and exit still work.

- [ ] **Step 5: Inspect repository state**

```powershell
git status --short
git log -4 --oneline
```

Expected: no uncommitted implementation files; the design, asset, window/EXE, and tray commits are present.

- [ ] **Step 6: Push the implementation**

```powershell
git push origin main
```

Expected: `main` is updated on `https://github.com/Yzr-hub/top-monitor.git`.
