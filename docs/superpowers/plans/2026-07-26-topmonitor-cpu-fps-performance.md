# TopMonitor CPU Temperature, Stable Width, FPS, and Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Intel 14th-generation CPU temperature available after one-time PawnIO setup, keep the overlay width stable, add automatic foreground-game FPS through PresentMon, and reduce measured idle overhead.

**Architecture:** Keep every data source behind `IMetricProvider`. Extract deterministic CPU sensor selection and display reservation logic into testable units, wrap PawnIO/PresentMon/Win32 operations in infrastructure services, and let a foreground FPS tracker own the PresentMon child-process lifecycle. Perform only targeted performance changes supported by tests and before/after measurements.

**Tech Stack:** C# 14, .NET 10, WPF, MVVM, LibreHardwareMonitorLib 0.9.6, PawnIO 2.x installer from LibreHardwareMonitor v0.9.6, PresentMon Console 2.4.1 x64, Serilog, Microsoft.Extensions.DependencyInjection, xUnit.

## Global Constraints

- Target Windows 10/11 x64 and `net10.0-windows`; do not introduce Electron, WebView, C++, Rust, Python, or Java runtime dependencies.
- TopMonitor must run as a normal user after one-time PawnIO and Performance Log Users setup.
- Never invent CPU temperature or FPS values; unavailable sources return `MetricStatus.Unavailable` or `MetricStatus.Restricted`.
- CPU temperature candidates are valid only when finite and between `-20°C` and `125°C`.
- CPU sensor order is Package, Core Max, Package/Tdie equivalent, then maximum valid core temperature.
- FPS sampling is enabled only when the FPS Widget is enabled; PresentMon must not remain running without a candidate game.
- PresentMon is pinned to `2.4.1` x64 with SHA-256 `D74183E7AE630F72CD3690BE0373ECBFD6CBB86578148AAB8FA2A7166068F34`.
- PawnIO setup is sourced from LibreHardwareMonitor tag `v0.9.6` with SHA-256 `A3A46226C5E2824F4CDD42BE0EECBABFC672C86F7889710F5AB1E6AD385B47A0`.
- Real-time value changes must not resize the overlay; configuration changes may recalculate slot widths once.
- Use cancellation for all background work, throttle repeated errors, and clean up PresentMon/ETW sessions on exit.
- Use TDD for production behavior: write one failing test, observe the expected failure, add the minimum implementation, then rerun the focused and full suites.
- Finish each task with a focused commit and push to `origin/main`.

---

## Planned File Structure

### New production files

- `src/TopMonitor.Domain/Formatting/MetricDisplayReservation.cs` — generates stable representative text per metric.
- `src/TopMonitor.Application/Hardware/HardwareAccessStatus.cs` — application-facing PawnIO status.
- `src/TopMonitor.Application/Hardware/IHardwareAccessService.cs` — one-time hardware setup contract.
- `src/TopMonitor.Application/Fps/ForegroundProcessInfo.cs` — foreground-process value model.
- `src/TopMonitor.Application/Fps/IForegroundProcessService.cs` — foreground process contract.
- `src/TopMonitor.Application/Fps/PresentedFrame.cs` — parsed frame timestamp.
- `src/TopMonitor.Application/Fps/IPresentMonSession.cs` — cancellable frame stream.
- `src/TopMonitor.Application/Fps/IPresentMonSessionFactory.cs` — starts one PID-targeted PresentMon session.
- `src/TopMonitor.Application/Metrics/MetricPreviewGate.cs` — suppresses hidden settings-preview work.
- `src/TopMonitor.Infrastructure/Hardware/CpuTemperatureCandidate.cs` — library-neutral sensor candidate.
- `src/TopMonitor.Infrastructure/Hardware/CpuTemperatureSelector.cs` — deterministic Intel-compatible selection.
- `src/TopMonitor.Infrastructure/Hardware/HardwareUpdateLimiter.cs` — prevents duplicate hardware refreshes.
- `src/TopMonitor.Infrastructure/Hardware/PawnIoHardwareAccessService.cs` — PawnIO detection and elevated installer launch.
- `src/TopMonitor.Infrastructure/Hardware/IPawnIoProbe.cs` — testable wrapper for LibreHardwareMonitor PawnIO state.
- `src/TopMonitor.Infrastructure/Hardware/IElevatedProcessRunner.cs` — explicit elevated-process boundary.
- `src/TopMonitor.Infrastructure/Hardware/PawnIoProbe.cs` — production PawnIO state wrapper.
- `src/TopMonitor.Infrastructure/Hardware/ElevatedProcessRunner.cs` — production `runas` process runner.
- `src/TopMonitor.Infrastructure/Fps/PresentMonCsvParser.cs` — header-driven CSV parser.
- `src/TopMonitor.Infrastructure/Fps/FpsSlidingWindow.cs` — one-second FPS aggregation.
- `src/TopMonitor.Infrastructure/Fps/PresentMonProcessSession.cs` — child process and stdout ownership.
- `src/TopMonitor.Infrastructure/Fps/PresentMonSessionFactory.cs` — fixed safe command-line construction.
- `src/TopMonitor.Infrastructure/Fps/WindowsForegroundProcessService.cs` — Win32 foreground PID lookup.
- `src/TopMonitor.Infrastructure/Fps/ForegroundFpsTracker.cs` — debounce, probe cache, target switching, and grace period.
- `src/TopMonitor.Infrastructure/Fps/PresentMonFpsProvider.cs` — `IMetricProvider` adapter.
- `src/TopMonitor.Infrastructure/Fps/PerformanceLogUsersService.cs` — status and one-time group setup.
- `scripts/fetch-runtime-dependencies.ps1` — downloads and hashes PresentMon/PawnIO.
- `scripts/measure-performance.ps1` — repeatable 60-second CPU, memory, thread, handle, and child-process sample.
- `third_party/NOTICE.md` — versions, origins, licenses, and hashes.

### New test files

- `tests/TopMonitor.Application.Tests/CpuTemperatureSelectorTests.cs`
- `tests/TopMonitor.Application.Tests/HardwareUpdateLimiterTests.cs`
- `tests/TopMonitor.Application.Tests/PresentMonCsvParserTests.cs`
- `tests/TopMonitor.Application.Tests/FpsSlidingWindowTests.cs`
- `tests/TopMonitor.Application.Tests/ForegroundFpsTrackerTests.cs`
- `tests/TopMonitor.Application.Tests/PawnIoHardwareAccessServiceTests.cs`
- `tests/TopMonitor.Application.Tests/PresentMonSessionFactoryTests.cs`
- `tests/TopMonitor.Application.Tests/PerformanceLogUsersServiceTests.cs`
- `tests/TopMonitor.Application.Tests/MetricPreviewGateTests.cs`
- `tests/TopMonitor.Domain.Tests/MetricDisplayReservationTests.cs`

### Existing files modified

- `src/TopMonitor.Domain/Metrics/MetricIds.cs`
- `src/TopMonitor.Domain/Configuration/AppSettings.cs`
- `src/TopMonitor.Infrastructure/Configuration/JsonSettingsService.cs`
- `src/TopMonitor.Infrastructure/Hardware/LibreHardwareMetricProvider.cs`
- `src/TopMonitor.Domain/Formatting/MetricFormatter.cs`
- `src/TopMonitor.Infrastructure/Windows/NativeMethods.cs`
- `src/TopMonitor.App/App.xaml.cs`
- `src/TopMonitor.App/MainWindow.xaml`
- `src/TopMonitor.App/SettingsWindow.xaml`
- `src/TopMonitor.App/SettingsWindow.xaml.cs`
- `src/TopMonitor.App/ViewModels/MetricItemViewModel.cs`
- `src/TopMonitor.App/ViewModels/OverlayViewModel.cs`
- `src/TopMonitor.App/ViewModels/SettingsViewModel.cs`
- `src/TopMonitor.App/TopMonitor.App.csproj`
- `tests/TopMonitor.Application.Tests/JsonSettingsServiceTests.cs`
- `tests/TopMonitor.Domain.Tests/MetricFormatterTests.cs`
- `scripts/publish-win-x64.ps1`
- `docs/architecture.md`
- `docs/development-guide.md`
- `docs/sensor-compatibility.md`
- `docs/release-guide.md`

---

### Task 1: Intel-Compatible CPU Temperature Selection

**Files:**
- Create: `src/TopMonitor.Infrastructure/Hardware/CpuTemperatureCandidate.cs`
- Create: `src/TopMonitor.Infrastructure/Hardware/CpuTemperatureSelector.cs`
- Test: `tests/TopMonitor.Application.Tests/CpuTemperatureSelectorTests.cs`

**Interfaces:**
- Produces: `CpuTemperatureCandidate(string Id, string Name, double? Value)`
- Produces: `CpuTemperatureSelection? CpuTemperatureSelector.Select(IReadOnlyCollection<CpuTemperatureCandidate> candidates)`
- Produces: `CpuTemperatureSelection(CpuTemperatureCandidate Candidate, string Reason)`

- [ ] **Step 1: Write the failing selector tests**

```csharp
public sealed class CpuTemperatureSelectorTests
{
    [Fact]
    public void Package_with_valid_value_has_highest_priority()
    {
        var candidates = new[]
        {
            new CpuTemperatureCandidate("core-max", "Core Max", 72),
            new CpuTemperatureCandidate("package", "CPU Package", 68)
        };

        var selected = CpuTemperatureSelector.Select(candidates);

        Assert.Equal("package", selected?.Candidate.Id);
        Assert.Equal("package", selected?.Reason);
    }

    [Fact]
    public void Core_max_is_used_when_package_value_is_missing()
    {
        var candidates = new[]
        {
            new CpuTemperatureCandidate("package", "CPU Package", null),
            new CpuTemperatureCandidate("core-max", "Core Max", 74)
        };

        Assert.Equal("core-max", CpuTemperatureSelector.Select(candidates)?.Candidate.Id);
    }

    [Fact]
    public void Maximum_valid_core_is_last_resort()
    {
        var candidates = new[]
        {
            new CpuTemperatureCandidate("core-0", "CPU Core #1", 61),
            new CpuTemperatureCandidate("core-1", "CPU Core #2", 66)
        };

        Assert.Equal("core-1", CpuTemperatureSelector.Select(candidates)?.Candidate.Id);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-21)]
    [InlineData(126)]
    public void Invalid_values_are_rejected(double value)
    {
        var candidates = new[]
        {
            new CpuTemperatureCandidate("package", "CPU Package", value)
        };

        Assert.Null(CpuTemperatureSelector.Select(candidates));
    }
}
```

- [ ] **Step 2: Run the focused tests and observe the expected compile failure**

Run:

```powershell
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter FullyQualifiedName~CpuTemperatureSelectorTests
```

Expected: compilation fails because `CpuTemperatureCandidate` and `CpuTemperatureSelector` do not exist.

- [ ] **Step 3: Implement the selector**

Use ordinal-ignore-case matching. Rank exact Package names first, then Core Max, then names containing `Package`, `Tdie`, or `Tctl/Tdie`, then names containing `Core`. Within the last group select the highest value. Return reason values exactly `package`, `core-max`, `package-equivalent`, or `max-core`.

```csharp
public sealed record CpuTemperatureCandidate(string Id, string Name, double? Value);

public sealed record CpuTemperatureSelection(
    CpuTemperatureCandidate Candidate,
    string Reason);

public static class CpuTemperatureSelector
{
    public static CpuTemperatureSelection? Select(
        IReadOnlyCollection<CpuTemperatureCandidate> candidates)
    {
        var valid = candidates
            .Where(candidate => candidate.Value is { } value &&
                                double.IsFinite(value) &&
                                value is >= -20 and <= 125)
            .ToArray();

        return Find(valid, "CPU Package", "package", exact: true)
            ?? Find(valid, "Core Max", "core-max", exact: true)
            ?? FindAny(valid, ["Package", "Tdie", "Tctl/Tdie"], "package-equivalent")
            ?? valid
                .Where(candidate => candidate.Name.Contains(
                    "Core",
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(candidate => candidate.Value)
                .Select(candidate => new CpuTemperatureSelection(candidate, "max-core"))
                .FirstOrDefault();
    }
}
```

Implement private `Find` and `FindAny` helpers without culture-sensitive comparisons.

- [ ] **Step 4: Verify the focused and full test suites**

Run:

```powershell
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter FullyQualifiedName~CpuTemperatureSelectorTests
dotnet test TopMonitor.sln -c Debug
```

Expected: selector tests pass; all existing tests pass.

- [ ] **Step 5: Commit and push**

```powershell
git add src/TopMonitor.Infrastructure/Hardware/CpuTemperatureCandidate.cs `
        src/TopMonitor.Infrastructure/Hardware/CpuTemperatureSelector.cs `
        tests/TopMonitor.Application.Tests/CpuTemperatureSelectorTests.cs
git commit -m "feat: select Intel CPU temperature sensors"
git push origin main
```

---

### Task 2: CPU Provider Integration, Diagnostics, and Refresh Throttling

**Files:**
- Create: `src/TopMonitor.Infrastructure/Hardware/HardwareUpdateLimiter.cs`
- Modify: `src/TopMonitor.Infrastructure/Hardware/LibreHardwareMetricProvider.cs`
- Test: `tests/TopMonitor.Application.Tests/HardwareUpdateLimiterTests.cs`

**Interfaces:**
- Consumes: `CpuTemperatureSelector.Select(...)`
- Produces: `bool HardwareUpdateLimiter.ShouldUpdate(string hardwareId, DateTimeOffset now)`
- Produces: CPU discovery logs containing hardware name, candidates, chosen sensor, and reason.

- [ ] **Step 1: Write failing limiter tests**

```csharp
public sealed class HardwareUpdateLimiterTests
{
    [Fact]
    public void Same_hardware_is_updated_once_inside_minimum_interval()
    {
        var limiter = new HardwareUpdateLimiter(TimeSpan.FromMilliseconds(400));
        var start = DateTimeOffset.Parse("2026-07-26T00:00:00Z");

        Assert.True(limiter.ShouldUpdate("/intelcpu/0", start));
        Assert.False(limiter.ShouldUpdate("/intelcpu/0", start.AddMilliseconds(250)));
        Assert.True(limiter.ShouldUpdate("/intelcpu/0", start.AddMilliseconds(400)));
    }

    [Fact]
    public void Different_hardware_has_independent_timestamps()
    {
        var limiter = new HardwareUpdateLimiter(TimeSpan.FromMilliseconds(400));
        var now = DateTimeOffset.Parse("2026-07-26T00:00:00Z");

        Assert.True(limiter.ShouldUpdate("/intelcpu/0", now));
        Assert.True(limiter.ShouldUpdate("/gpu-nvidia/0", now));
    }
}
```

- [ ] **Step 2: Run and observe the expected compile failure**

```powershell
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter FullyQualifiedName~HardwareUpdateLimiterTests
```

Expected: compilation fails because `HardwareUpdateLimiter` does not exist.

- [ ] **Step 3: Implement the limiter and integrate CPU selection**

`HardwareUpdateLimiter` stores the last successful update timestamp per stable hardware identifier. `LibreHardwareMetricProvider` must:

1. Call `UpdateHardwareIfDue(IHardware hardware)` instead of unconditional `hardware.Update()` in `ReadAsync`.
2. During discovery, update the CPU once, convert every temperature sensor into `CpuTemperatureCandidate` using `sensor.Identifier.ToString()`, select one, and bind the selected identifier back to the original `ISensor`.
3. Log candidates only during discovery/rescan.
4. Keep the CPU metric definition even when no candidate is selected so the UI shows `--`.

```csharp
private readonly HardwareUpdateLimiter _updateLimiter =
    new(TimeSpan.FromMilliseconds(400));

private void UpdateHardwareIfDue(IHardware hardware)
{
    if (_updateLimiter.ShouldUpdate(
            hardware.Identifier.ToString(),
            DateTimeOffset.UtcNow))
    {
        hardware.Update();
    }
}
```

- [ ] **Step 4: Run tests and build**

```powershell
dotnet test TopMonitor.sln -c Debug
dotnet build TopMonitor.sln -c Release
```

Expected: zero test failures and zero build errors.

- [ ] **Step 5: Commit and push**

```powershell
git add src/TopMonitor.Infrastructure/Hardware/HardwareUpdateLimiter.cs `
        src/TopMonitor.Infrastructure/Hardware/LibreHardwareMetricProvider.cs `
        tests/TopMonitor.Application.Tests/HardwareUpdateLimiterTests.cs
git commit -m "perf: reuse hardware sensor snapshots"
git push origin main
```

---

### Task 3: PawnIO Status and One-Time Installer

**Files:**
- Create: `src/TopMonitor.Application/Hardware/HardwareAccessStatus.cs`
- Create: `src/TopMonitor.Application/Hardware/IHardwareAccessService.cs`
- Create: `src/TopMonitor.Infrastructure/Hardware/IPawnIoProbe.cs`
- Create: `src/TopMonitor.Infrastructure/Hardware/IElevatedProcessRunner.cs`
- Create: `src/TopMonitor.Infrastructure/Hardware/PawnIoProbe.cs`
- Create: `src/TopMonitor.Infrastructure/Hardware/ElevatedProcessRunner.cs`
- Create: `src/TopMonitor.Infrastructure/Hardware/PawnIoHardwareAccessService.cs`
- Modify: `src/TopMonitor.App/App.xaml.cs`
- Modify: `src/TopMonitor.App/ViewModels/SettingsViewModel.cs`
- Modify: `src/TopMonitor.App/SettingsWindow.xaml`
- Test: `tests/TopMonitor.Application.Tests/PawnIoHardwareAccessServiceTests.cs`

**Interfaces:**
- Produces: `HardwareAccessStatus(bool IsInstalled, Version? Version, bool InstallerAvailable, string Message)`
- Produces: `Task<HardwareAccessStatus> IHardwareAccessService.InitializeAsync(CancellationToken cancellationToken)`
- Consumes: `MetricSamplingService.RescanProvidersAsync(...)`

- [ ] **Step 1: Write failing path/status tests**

Use an injected `IPawnIoProbe` and `IElevatedProcessRunner` so tests never install a driver.

```csharp
[Fact]
public void Missing_installer_is_reported_without_starting_a_process()
{
    var runner = new FakeElevatedProcessRunner();
    var service = new PawnIoHardwareAccessService(
        new FakePawnIoProbe(false, null),
        runner,
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
        NullLogger<PawnIoHardwareAccessService>.Instance);

    var status = service.GetStatus();

    Assert.False(status.IsInstalled);
    Assert.False(status.InstallerAvailable);
    Assert.Empty(runner.Starts);
}
```

- [ ] **Step 2: Run and observe the expected compile failure**

```powershell
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter FullyQualifiedName~PawnIoHardwareAccessServiceTests
```

Expected: the new service contracts do not exist.

- [ ] **Step 3: Implement detection and explicit elevated install**

`PawnIoProbe` wraps `LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled` and `.Version`. The installer path is:

```csharp
Path.Combine(appBaseDirectory, "Dependencies", "PawnIO_setup.exe")
```

The elevated process uses:

```csharp
new ProcessStartInfo(installerPath, "-install")
{
    UseShellExecute = true,
    Verb = "runas"
};
```

Only `InitializeAsync` may launch it. After a zero exit code, query PawnIO again and call `MetricSamplingService.RescanProvidersAsync`. A user cancellation returns the previous status and logs one information event, not an error loop.

- [ ] **Step 4: Add settings UI state and command**

Expose `HardwareAccessMessage` and `InitializeHardwareAccessCommand`. The command is user initiated, disables itself while running, then updates status and rescans providers. Add a button labeled `初始化 CPU 温度访问` in the Behavior tab.

- [ ] **Step 5: Run tests and build**

```powershell
dotnet test TopMonitor.sln -c Debug
dotnet build TopMonitor.sln -c Release
```

Expected: all tests pass; no build errors.

- [ ] **Step 6: Commit and push**

```powershell
git add src/TopMonitor.Application/Hardware `
        src/TopMonitor.Infrastructure/Hardware/PawnIoHardwareAccessService.cs `
        src/TopMonitor.App/App.xaml.cs `
        src/TopMonitor.App/ViewModels/SettingsViewModel.cs `
        src/TopMonitor.App/SettingsWindow.xaml `
        tests/TopMonitor.Application.Tests/PawnIoHardwareAccessServiceTests.cs
git commit -m "feat: add one-time PawnIO setup"
git push origin main
```

---

### Task 4: Stable Metric Slot Width

**Files:**
- Create: `src/TopMonitor.Domain/Formatting/MetricDisplayReservation.cs`
- Modify: `src/TopMonitor.Domain/Formatting/MetricFormatter.cs`
- Modify: `src/TopMonitor.App/ViewModels/MetricItemViewModel.cs`
- Modify: `src/TopMonitor.App/MainWindow.xaml`
- Test: `tests/TopMonitor.Domain.Tests/MetricDisplayReservationTests.cs`
- Test: `tests/TopMonitor.Domain.Tests/MetricFormatterTests.cs`

**Interfaces:**
- Produces: `string MetricDisplayReservation.Create(MetricDefinition definition, WidgetConfig widget, bool showLabel, bool showUnit)`
- Produces: `MetricItemViewModel.ReservationText`

- [ ] **Step 1: Write failing reservation tests**

```csharp
public sealed class MetricDisplayReservationTests
{
    [Theory]
    [InlineData("hardware.cpu.temperature.package", "CPU", "°C", "CPU 125°C")]
    [InlineData("hardware.cpu.load.total", "CPU", "%", "CPU 100%")]
    [InlineData("graphics.foreground.fps", "FPS", "", "FPS 9999")]
    [InlineData("system.network.active.download.bytes_per_second", "↓", "B/s", "↓ 999.9 GB/s")]
    public void Reservation_is_stable_for_metric_kind(
        string id,
        string label,
        string unit,
        string expected)
    {
        var definition = new MetricDefinition(
            new MetricId(id),
            label,
            MetricCategory.Other,
            unit,
            TimeSpan.FromSeconds(1),
            false);
        var widget = new WidgetConfig(definition.Id, true, 10, label, "0.0");

        Assert.Equal(
            expected,
            MetricDisplayReservation.Create(definition, widget, true, true));
    }
}
```

- [ ] **Step 2: Run and observe the expected compile failure**

```powershell
dotnet test tests/TopMonitor.Domain.Tests/TopMonitor.Domain.Tests.csproj `
  -c Debug --filter FullyQualifiedName~MetricDisplayReservationTests
```

Expected: `MetricDisplayReservation` does not exist.

- [ ] **Step 3: Implement reservation text**

Use fixed numeric reservations by metric category/ID. Respect `showLabel` and `showUnit`; do not measure pixels in Domain.

```csharp
var reservedValue = definition.Id switch
{
    var id when id == MetricIds.CpuTemperaturePackage => "125",
    var id when id == MetricIds.CpuTotalLoad => "100",
    var id when id == MetricIds.Gpu0CoreTemperature => "125",
    var id when id == MetricIds.Gpu0CoreLoad => "100",
    var id when id.Value == "graphics.foreground.fps" => "9999",
    var id when id == MetricIds.MemoryUsagePercent => "100",
    var id when id == MetricIds.MemoryUsedBytes => "999.9 GB",
    var id when id == MetricIds.ActiveNetworkDownload ||
                id == MetricIds.ActiveNetworkUpload => "999.9 GB/s",
    _ => "9999.9"
};
```

Avoid duplicating units when the reserved value already contains its compact unit. Extend `MetricFormatter` so bytes and bytes-per-second use binary units (`KB`, `MB`, `GB`) with at most one decimal. Add tests asserting `15_308_623_872` becomes `RAM 14.3GB` and `12_582_912` becomes `↓ 12.0MB/s`; this also removes the raw-byte display seen in the current UI.

- [ ] **Step 4: Bind a hidden sizer and fixed visible text width**

In the item template, use a `Grid` containing:

```xml
<TextBlock x:Name="ReservationSizer"
           Opacity="0"
           IsHitTestVisible="False"
           Text="{Binding ReservationText}" />
<TextBlock Width="{Binding ActualWidth, ElementName=ReservationSizer}"
           TextAlignment="Right"
           TextTrimming="CharacterEllipsis"
           Text="{Binding DisplayText}" />
```

Apply identical font properties to both TextBlocks. Keep `SizeToContent`, because only configuration rebuilds the sizer.

- [ ] **Step 5: Run tests, build, and manually exercise changing values**

```powershell
dotnet test TopMonitor.sln -c Debug
dotnet build TopMonitor.sln -c Release
dotnet run --project src/TopMonitor.App/TopMonitor.App.csproj -c Debug
```

Expected: automated tests pass; values can change without changing `MainWindow.ActualWidth`. Font-size or enabled-widget changes may resize once.

- [ ] **Step 6: Commit and push**

```powershell
git add src/TopMonitor.Domain/Formatting/MetricDisplayReservation.cs `
        src/TopMonitor.Domain/Formatting/MetricFormatter.cs `
        src/TopMonitor.App/ViewModels/MetricItemViewModel.cs `
        src/TopMonitor.App/MainWindow.xaml `
        tests/TopMonitor.Domain.Tests/MetricDisplayReservationTests.cs `
        tests/TopMonitor.Domain.Tests/MetricFormatterTests.cs
git commit -m "fix: keep overlay metric widths stable"
git push origin main
```

---

### Task 5: FPS Domain Model and Settings Migration

**Files:**
- Modify: `src/TopMonitor.Domain/Metrics/MetricIds.cs`
- Modify: `src/TopMonitor.Domain/Configuration/AppSettings.cs`
- Modify: `src/TopMonitor.Infrastructure/Configuration/JsonSettingsService.cs`
- Modify: `src/TopMonitor.App/ViewModels/OverlayViewModel.cs`
- Test: `tests/TopMonitor.Domain.Tests/AppSettingsTests.cs`
- Test: `tests/TopMonitor.Application.Tests/JsonSettingsServiceTests.cs`

**Interfaces:**
- Produces: `MetricIds.ForegroundFps = new("graphics.foreground.fps")`
- Updates: `AppSettings.CurrentSchemaVersion` from 1 to 2.

- [ ] **Step 1: Write failing default and migration tests**

```csharp
[Fact]
public void Defaults_include_disabled_foreground_fps()
{
    var fps = Assert.Single(
        AppSettings.CreateDefault().Widgets,
        widget => widget.MetricId == MetricIds.ForegroundFps);

    Assert.False(fps.Enabled);
    Assert.Equal("FPS", fps.Label);
    Assert.Equal("0", fps.NumberFormat);
}
```

Add a JSON migration test that writes a schema-version-1 file containing one user-modified CPU Widget, loads it, and asserts:

```csharp
Assert.Contains(loaded.Widgets, widget => widget.MetricId == MetricIds.ForegroundFps);
Assert.Contains(loaded.Widgets, widget =>
    widget.MetricId == MetricIds.CpuTotalLoad && widget.Enabled == false);
Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
```

- [ ] **Step 2: Run and observe the expected failures**

```powershell
dotnet test tests/TopMonitor.Domain.Tests/TopMonitor.Domain.Tests.csproj `
  -c Debug --filter FullyQualifiedName~AppSettingsTests
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter FullyQualifiedName~JsonSettingsServiceTests
```

Expected: FPS default and schema migration assertions fail.

- [ ] **Step 3: Implement ID, default Widget, and additive migration**

Append the FPS Widget at order 90 and keep it disabled by default. `Normalize` must merge missing default metric IDs without replacing any existing Widget:

```csharp
var existingIds = settings.Widgets
    .Select(widget => widget.MetricId)
    .ToHashSet();
var migratedWidgets = settings.Widgets
    .Concat(AppSettings.CreateDefault().Widgets.Where(
        widget => !existingIds.Contains(widget.MetricId)))
    .OrderBy(widget => widget.Order)
    .ToArray();
```

Add FPS metadata to `OverlayViewModel.CreateDefaultDefinitions`.

- [ ] **Step 4: Run all tests**

```powershell
dotnet test TopMonitor.sln -c Debug
```

Expected: all tests pass.

- [ ] **Step 5: Commit and push**

```powershell
git add src/TopMonitor.Domain/Metrics/MetricIds.cs `
        src/TopMonitor.Domain/Configuration/AppSettings.cs `
        src/TopMonitor.Infrastructure/Configuration/JsonSettingsService.cs `
        src/TopMonitor.App/ViewModels/OverlayViewModel.cs `
        tests/TopMonitor.Domain.Tests/AppSettingsTests.cs `
        tests/TopMonitor.Application.Tests/JsonSettingsServiceTests.cs
git commit -m "feat: add foreground FPS metric configuration"
git push origin main
```

---

### Task 6: PresentMon Parser and FPS Sliding Window

**Files:**
- Create: `src/TopMonitor.Application/Fps/PresentedFrame.cs`
- Create: `src/TopMonitor.Infrastructure/Fps/PresentMonCsvParser.cs`
- Create: `src/TopMonitor.Infrastructure/Fps/FpsSlidingWindow.cs`
- Test: `tests/TopMonitor.Application.Tests/PresentMonCsvParserTests.cs`
- Test: `tests/TopMonitor.Application.Tests/FpsSlidingWindowTests.cs`

**Interfaces:**
- Produces: `PresentedFrame(int ProcessId, double TimeSeconds, string PresentMode)`
- Produces: `PresentMonCsvParser.TryReadHeader(string line)` and `TryReadFrame(string line, out PresentedFrame frame)`
- Produces: `int? FpsSlidingWindow.GetFps(double nowSeconds)`

- [ ] **Step 1: Write failing parser tests**

Use a quoted application name to prove CSV handling is not `Split(',')`:

```csharp
[Fact]
public void Parser_reads_pid_time_and_mode_from_header_driven_columns()
{
    var parser = new PresentMonCsvParser();
    Assert.True(parser.TryReadHeader(
        "Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,Dropped,TimeInSeconds,PresentMode"));

    Assert.True(parser.TryReadFrame(
        "\"Game, Shipping.exe\",4242,0x1,DXGI,0,0,0,12.500,Hardware: Independent Flip",
        out var frame));

    Assert.Equal(4242, frame.ProcessId);
    Assert.Equal(12.5, frame.TimeSeconds);
    Assert.Equal("Hardware: Independent Flip", frame.PresentMode);
}
```

- [ ] **Step 2: Write failing 60 FPS window test**

```csharp
[Fact]
public void Sixty_evenly_spaced_intervals_report_sixty_fps()
{
    var window = new FpsSlidingWindow(TimeSpan.FromSeconds(1));
    for (var index = 0; index <= 60; index++)
    {
        window.Add(new PresentedFrame(42, index / 60d, "Hardware"));
    }

    Assert.Equal(60, window.GetFps(1d));
}
```

- [ ] **Step 3: Run and observe expected compile failures**

```powershell
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter "FullyQualifiedName~PresentMonCsvParserTests|FullyQualifiedName~FpsSlidingWindowTests"
```

Expected: parser/window types do not exist.

- [ ] **Step 4: Implement parser and bounded window**

The parser stores column indexes from the header and uses a small CSV field scanner that supports quotes and escaped quotes. Reject dropped frames, missing/invalid time, non-positive PID, and header mismatches.

The sliding window stores timestamps in a queue, removes frames older than `now - 1.0`, and computes:

```csharp
var elapsed = last.TimeSeconds - first.TimeSeconds;
return elapsed <= 0 || count < 2
    ? null
    : (int)Math.Round(
        (count - 1) / elapsed,
        MidpointRounding.AwayFromZero);
```

- [ ] **Step 5: Verify focused and full tests**

```powershell
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter "FullyQualifiedName~PresentMonCsvParserTests|FullyQualifiedName~FpsSlidingWindowTests"
dotnet test TopMonitor.sln -c Debug
```

Expected: all tests pass.

- [ ] **Step 6: Commit and push**

```powershell
git add src/TopMonitor.Application/Fps/PresentedFrame.cs `
        src/TopMonitor.Infrastructure/Fps/PresentMonCsvParser.cs `
        src/TopMonitor.Infrastructure/Fps/FpsSlidingWindow.cs `
        tests/TopMonitor.Application.Tests/PresentMonCsvParserTests.cs `
        tests/TopMonitor.Application.Tests/FpsSlidingWindowTests.cs
git commit -m "feat: parse and aggregate PresentMon frames"
git push origin main
```

---

### Task 7: Foreground Process and PresentMon Session Lifecycle

**Files:**
- Create: `src/TopMonitor.Application/Fps/ForegroundProcessInfo.cs`
- Create: `src/TopMonitor.Application/Fps/IForegroundProcessService.cs`
- Create: `src/TopMonitor.Application/Fps/IPresentMonSession.cs`
- Create: `src/TopMonitor.Application/Fps/IPresentMonSessionFactory.cs`
- Create: `src/TopMonitor.Infrastructure/Fps/WindowsForegroundProcessService.cs`
- Create: `src/TopMonitor.Infrastructure/Fps/PresentMonProcessSession.cs`
- Create: `src/TopMonitor.Infrastructure/Fps/PresentMonSessionFactory.cs`
- Modify: `src/TopMonitor.Infrastructure/Windows/NativeMethods.cs`
- Test: `tests/TopMonitor.Application.Tests/PresentMonSessionFactoryTests.cs`

**Interfaces:**
- Produces: `ForegroundProcessInfo(int ProcessId, string ProcessName, DateTimeOffset StartTime)`
- Produces: `ForegroundProcessInfo? IForegroundProcessService.GetForegroundProcess()`
- Produces: `IPresentMonSession : IAsyncDisposable`
- Produces: `IAsyncEnumerable<PresentedFrame> IPresentMonSession.ReadFramesAsync(CancellationToken cancellationToken)`
- Produces: `Task<IPresentMonSession> IPresentMonSessionFactory.StartAsync(int processId, CancellationToken cancellationToken)`

- [ ] **Step 1: Write a failing safe-arguments test**

```csharp
[Fact]
public void Factory_targets_numeric_pid_and_stdout_without_shell()
{
    var options = PresentMonSessionFactory.CreateStartInfo(
        @"C:\TopMonitor\Dependencies\PresentMon.exe",
        4242);

    Assert.False(options.UseShellExecute);
    Assert.True(options.RedirectStandardOutput);
    Assert.Equal(
        "--process_id 4242 --output_stdout --v1_metrics --terminate_on_proc_exit --stop_existing_session",
        string.Join(" ", options.ArgumentList));
}
```

- [ ] **Step 2: Run and observe the expected compile failure**

```powershell
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter FullyQualifiedName~PresentMonSessionFactoryTests
```

Expected: session factory types do not exist.

- [ ] **Step 3: Implement Win32 foreground lookup**

Add P/Invoke declarations only in `NativeMethods`:

```csharp
[LibraryImport("user32.dll")]
internal static partial nint GetForegroundWindow();

[LibraryImport("user32.dll")]
internal static partial uint GetWindowThreadProcessId(
    nint hWnd,
    out uint processId);
```

`WindowsForegroundProcessService` returns null for PID 0, inaccessible/exited processes, TopMonitor itself, `explorer`, `dwm`, `ShellExperienceHost`, `SearchHost`, and `StartMenuExperienceHost`.

- [ ] **Step 4: Implement the PresentMon session**

Start one child process with the exact arguments from the test. Read the header first, then yield valid frames. On disposal:

1. Cancel stdout reading.
2. Wait up to two seconds for normal exit.
3. Kill only the owned process tree if still running.
4. Dispose process and cancellation resources.

Never invoke a command shell and never interpolate a process name.

- [ ] **Step 5: Run tests and build**

```powershell
dotnet test TopMonitor.sln -c Debug
dotnet build TopMonitor.sln -c Release
```

Expected: all tests pass; zero build errors.

- [ ] **Step 6: Commit and push**

```powershell
git add src/TopMonitor.Application/Fps `
        src/TopMonitor.Infrastructure/Fps/WindowsForegroundProcessService.cs `
        src/TopMonitor.Infrastructure/Fps/PresentMonProcessSession.cs `
        src/TopMonitor.Infrastructure/Fps/PresentMonSessionFactory.cs `
        src/TopMonitor.Infrastructure/Windows/NativeMethods.cs `
        tests/TopMonitor.Application.Tests/PresentMonSessionFactoryTests.cs
git commit -m "feat: manage PID-targeted PresentMon sessions"
git push origin main
```

---

### Task 8: Foreground FPS Tracker and Metric Provider

**Files:**
- Create: `src/TopMonitor.Infrastructure/Fps/ForegroundFpsTracker.cs`
- Create: `src/TopMonitor.Infrastructure/Fps/PresentMonFpsProvider.cs`
- Modify: `src/TopMonitor.App/App.xaml.cs`
- Test: `tests/TopMonitor.Application.Tests/ForegroundFpsTrackerTests.cs`

**Interfaces:**
- Consumes: `IForegroundProcessService`, `IPresentMonSessionFactory`, `TimeProvider`
- Produces: `Task<int?> ForegroundFpsTracker.GetCurrentFpsAsync(CancellationToken cancellationToken)`
- Produces: `PresentMonFpsProvider : IMetricProvider, IAsyncDisposable`

- [ ] **Step 1: Write failing tracker lifecycle tests**

Use fake foreground and session services. Cover:

```csharp
[Fact]
public async Task Stable_foreground_process_starts_one_session_after_debounce()
{
    var clock = new ManualTimeProvider(
        DateTimeOffset.Parse("2026-07-26T00:00:00Z"));
    var foreground = new FakeForegroundProcessService(
        new ForegroundProcessInfo(42, "game", DateTimeOffset.UtcNow));
    var sessions = new FakePresentMonSessionFactory();
    var tracker = new ForegroundFpsTracker(foreground, sessions, clock);

    Assert.Null(await tracker.GetCurrentFpsAsync(CancellationToken.None));
    clock.Advance(TimeSpan.FromMilliseconds(749));
    Assert.Null(await tracker.GetCurrentFpsAsync(CancellationToken.None));
    clock.Advance(TimeSpan.FromMilliseconds(1));
    await tracker.GetCurrentFpsAsync(CancellationToken.None);

    Assert.Equal([42], sessions.StartedProcessIds);
}
```

Also test:

- switching PID disposes the previous session after the grace period;
- a process with no frames during the probe timeout is cached until its start time changes;
- cancellation disposes the owned session;
- no foreground process never starts PresentMon.

Define the test clock in the same test file without another package:

```csharp
private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan elapsed) => _now += elapsed;
}
```

- [ ] **Step 2: Run and observe expected compile failures**

```powershell
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter FullyQualifiedName~ForegroundFpsTrackerTests
```

Expected: tracker/provider types do not exist.

- [ ] **Step 3: Implement debounce, probe, cache, and grace rules**

Use these constants:

```csharp
private static readonly TimeSpan CandidateDebounce = TimeSpan.FromMilliseconds(750);
private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
private static readonly TimeSpan ForegroundGrace = TimeSpan.FromSeconds(5);
private static readonly TimeSpan RestartBackoff = TimeSpan.FromSeconds(10);
```

The non-game cache key is `(ProcessId, StartTime)` so PID reuse is safe. Limit the cache to 128 entries and remove exited/old entries during target changes.

- [ ] **Step 4: Implement the Provider**

`DiscoverAsync` returns one definition for `MetricIds.ForegroundFps`, label `FPS`, empty unit, interval 500ms. `ReadAsync` performs no work unless the requested IDs contain FPS. It maps:

- valid positive FPS to `MetricValue.Create`;
- missing dependency or no frames to `Unavailable`;
- ETW access denied to `Restricted`;
- unexpected errors to `Failed`, with one-minute log throttling.

- [ ] **Step 5: Register services and verify tests**

Register:

```csharp
services.AddSingleton<IForegroundProcessService, WindowsForegroundProcessService>();
services.AddSingleton<IPresentMonSessionFactory, PresentMonSessionFactory>();
services.AddSingleton<ForegroundFpsTracker>();
services.AddSingleton<IMetricProvider, PresentMonFpsProvider>();
```

Run:

```powershell
dotnet test TopMonitor.sln -c Debug
dotnet build TopMonitor.sln -c Release
```

Expected: all tests pass; zero build errors.

- [ ] **Step 6: Commit and push**

```powershell
git add src/TopMonitor.Infrastructure/Fps/ForegroundFpsTracker.cs `
        src/TopMonitor.Infrastructure/Fps/PresentMonFpsProvider.cs `
        src/TopMonitor.App/App.xaml.cs `
        tests/TopMonitor.Application.Tests/ForegroundFpsTrackerTests.cs
git commit -m "feat: add automatic foreground-game FPS"
git push origin main
```

---

### Task 9: FPS Permission Setup and Settings Preview Activation

**Files:**
- Create: `src/TopMonitor.Infrastructure/Fps/PerformanceLogUsersService.cs`
- Modify: `src/TopMonitor.App/ViewModels/SettingsViewModel.cs`
- Modify: `src/TopMonitor.App/SettingsWindow.xaml`
- Modify: `src/TopMonitor.App/SettingsWindow.xaml.cs`
- Modify: `src/TopMonitor.App/App.xaml.cs`
- Test: `tests/TopMonitor.Application.Tests/PerformanceLogUsersServiceTests.cs`
- Test: `tests/TopMonitor.Application.Tests/MetricPreviewGateTests.cs`

**Interfaces:**
- Produces: `bool PerformanceLogUsersService.IsCurrentUserMember()`
- Produces: `Task<bool> PerformanceLogUsersService.AddCurrentUserAsync(CancellationToken cancellationToken)`
- Produces: `SettingsViewModel.SetPreviewActive(bool active)`

- [ ] **Step 1: Write failing command-generation and preview-gate tests**

Verify the group SID is used rather than a localized group name:

```csharp
[Fact]
public void Setup_command_uses_well_known_performance_log_users_sid()
{
    var command = PerformanceLogUsersService.CreateSetupCommand(
        @"DESKTOP\User");

    Assert.Contains("S-1-5-32-559", command, StringComparison.Ordinal);
    Assert.Contains("DESKTOP\\\\User", command, StringComparison.Ordinal);
}

[Fact]
public void Setup_command_escapes_single_quotes_in_account_name()
{
    var command = PerformanceLogUsersService.CreateSetupCommand(
        @"DESKTOP\User'Name");

    Assert.Contains("DESKTOP\\\\User''Name", command, StringComparison.Ordinal);
}
```

Add `MetricPreviewGateTests` proving `ShouldProcess` is false by default, true after `SetActive(true)`, and false after `SetActive(false)`.

- [ ] **Step 2: Run and observe expected failures**

```powershell
dotnet test tests/TopMonitor.Application.Tests/TopMonitor.Application.Tests.csproj `
  -c Debug --filter "FullyQualifiedName~PerformanceLogUsersServiceTests|FullyQualifiedName~MetricPreviewGateTests"
```

Expected: permission service and activation API do not exist.

- [ ] **Step 3: Implement one-time group setup**

Check membership against `SecurityIdentifier("S-1-5-32-559")`. For explicit setup, launch 64-bit Windows PowerShell elevated with a Base64-encoded UTF-16LE command that calls:

```powershell
Add-LocalGroupMember `
  -SID ([System.Security.Principal.SecurityIdentifier]'S-1-5-32-559') `
  -Member 'DESKTOP\User'
```

Escape PowerShell single quotes in the account name by doubling them before Base64 encoding. Do not auto-run this at startup. After success, display `注销并重新登录后 FPS 权限生效`.

- [ ] **Step 4: Activate settings preview only while visible**

Create `MetricPreviewGate` in Application. `SetPreviewActive(true)` activates the gate and applies `MetricValueCache.Snapshot`; `SetPreviewActive(false)` deactivates it. `OnMetricValuesChanged` returns before posting to the UI synchronization context when the gate is inactive. `SettingsWindow.ShowAndActivate` activates before showing; `OnClosing` deactivates before hiding.

- [ ] **Step 5: Add UI status and setup command**

Add `FPS 权限` status text and a button `配置 FPS 权限`. Disable the button while elevated setup is in progress.

- [ ] **Step 6: Run tests and build**

```powershell
dotnet test TopMonitor.sln -c Debug
dotnet build TopMonitor.sln -c Release
```

Expected: all tests pass and build succeeds.

- [ ] **Step 7: Commit and push**

```powershell
git add src/TopMonitor.Infrastructure/Fps/PerformanceLogUsersService.cs `
        src/TopMonitor.Application/Metrics/MetricPreviewGate.cs `
        src/TopMonitor.App/ViewModels/SettingsViewModel.cs `
        src/TopMonitor.App/SettingsWindow.xaml `
        src/TopMonitor.App/SettingsWindow.xaml.cs `
        src/TopMonitor.App/App.xaml.cs `
        tests/TopMonitor.Application.Tests/PerformanceLogUsersServiceTests.cs `
        tests/TopMonitor.Application.Tests/MetricPreviewGateTests.cs
git commit -m "feat: add FPS permission setup and suspend hidden preview"
git push origin main
```

---

### Task 10: Runtime Dependency Fetch and Portable Publishing

**Files:**
- Create: `scripts/fetch-runtime-dependencies.ps1`
- Create: `third_party/NOTICE.md`
- Modify: `src/TopMonitor.App/TopMonitor.App.csproj`
- Modify: `scripts/publish-win-x64.ps1`
- Modify: `.gitignore`

**Interfaces:**
- Produces: `third_party/runtime/PresentMon.exe`
- Produces: `third_party/runtime/PawnIO_setup.exe`
- Publishes both files under `Dependencies/`.

- [ ] **Step 1: Add exact dependency manifest values to the fetch script**

```powershell
$dependencies = @(
    @{
        Name = 'PresentMon.exe'
        Uri = 'https://github.com/GameTechDev/PresentMon/releases/download/v2.4.1/PresentMon-2.4.1-x64.exe'
        Sha256 = 'D74183E7AE630F72CD3690BE0373ECBFD6CBB86578148AAB8FA2A7166068F34'
    },
    @{
        Name = 'PawnIO_setup.exe'
        Uri = 'https://raw.githubusercontent.com/LibreHardwareMonitor/LibreHardwareMonitor/3d331e3370efb858411f19511373eff65a218701/LibreHardwareMonitor/Resources/PawnIO_setup.exe'
        Sha256 = 'A3A46226C5E2824F4CDD42BE0EECBABFC672C86F7889710F5AB1E6AD385B47A0'
    }
)
```

Download to a temporary filename in `third_party/runtime`, verify SHA-256, then move atomically. A hash mismatch deletes the temporary file and terminates with a non-zero exit code.

- [ ] **Step 2: Make publishing fetch and include dependencies**

Call the fetch script before restore/build. Add conditional Content items:

```xml
<Content Include="..\..\third_party\runtime\PresentMon.exe"
         Link="Dependencies\PresentMon.exe"
         CopyToOutputDirectory="PreserveNewest"
         CopyToPublishDirectory="PreserveNewest"
         Condition="Exists('..\..\third_party\runtime\PresentMon.exe')" />
<Content Include="..\..\third_party\runtime\PawnIO_setup.exe"
         Link="Dependencies\PawnIO_setup.exe"
         CopyToOutputDirectory="PreserveNewest"
         CopyToPublishDirectory="PreserveNewest"
         Condition="Exists('..\..\third_party\runtime\PawnIO_setup.exe')" />
```

Ignore only `third_party/runtime/*.exe`; track `NOTICE.md`.

- [ ] **Step 3: Run dependency and publish verification**

```powershell
.\scripts\fetch-runtime-dependencies.ps1
Get-FileHash third_party\runtime\PresentMon.exe -Algorithm SHA256
Get-FileHash third_party\runtime\PawnIO_setup.exe -Algorithm SHA256
.\scripts\publish-win-x64.ps1
Test-Path artifacts\publish\win-x64\Dependencies\PresentMon.exe
Test-Path artifacts\publish\win-x64\Dependencies\PawnIO_setup.exe
```

Expected: hashes exactly match the pinned values; both final `Test-Path` calls return `True`.

- [ ] **Step 4: Commit and push**

```powershell
git add scripts/fetch-runtime-dependencies.ps1 `
        scripts/publish-win-x64.ps1 `
        src/TopMonitor.App/TopMonitor.App.csproj `
        third_party/NOTICE.md `
        .gitignore
git commit -m "build: package verified runtime dependencies"
git push origin main
```

---

### Task 11: Performance Measurement and Hot-Path Cleanup

**Files:**
- Create: `scripts/measure-performance.ps1`
- Modify: `src/TopMonitor.Application/Metrics/MetricSamplingService.cs` only if measurements show repeated per-tick allocation.
- Modify: `docs/development-guide.md`
- Create: `docs/performance-baseline.md`

**Interfaces:**
- Produces: a CSV with timestamp, process CPU delta, working set, private bytes, threads, handles, and PresentMon process presence.
- Uses `dotnet-counters` separately for allocation rate and Gen 0 collection measurements.

- [ ] **Step 1: Implement the repeatable measurement script**

The script resolves exactly one target process, samples once per second, and writes CSV. CPU percent is calculated from the delta in `TotalProcessorTime` divided by elapsed wall-clock time and logical processor count.

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ProcessName,
    [ValidateRange(10, 3600)]
    [int]$Seconds = 60,
    [Parameter(Mandatory)]
    [string]$OutputPath
)

$logicalProcessors = [Environment]::ProcessorCount
$samples = [System.Collections.Generic.List[object]]::new()
$previous = Get-Process -Name $ProcessName -ErrorAction Stop |
    Select-Object -First 1
$previousCpu = $previous.TotalProcessorTime.TotalSeconds
$previousAt = [DateTimeOffset]::UtcNow

for ($index = 0; $index -lt $Seconds; $index++) {
    Start-Sleep -Seconds 1
    $process = Get-Process -Id $previous.Id -ErrorAction Stop
    $now = [DateTimeOffset]::UtcNow
    $cpu = $process.TotalProcessorTime.TotalSeconds
    $elapsed = ($now - $previousAt).TotalSeconds
    $cpuPercent = (($cpu - $previousCpu) / $elapsed / $logicalProcessors) * 100
    $presentMonRunning = [bool](Get-Process -Name PresentMon -ErrorAction SilentlyContinue)

    $samples.Add([pscustomobject]@{
        Timestamp = $now.ToString('O')
        CpuPercent = [Math]::Round($cpuPercent, 4)
        WorkingSetBytes = $process.WorkingSet64
        PrivateBytes = $process.PrivateMemorySize64
        ThreadCount = $process.Threads.Count
        HandleCount = $process.HandleCount
        PresentMonRunning = $presentMonRunning
    })

    $previousCpu = $cpu
    $previousAt = $now
}

$directory = Split-Path -Parent $OutputPath
if ($directory) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}
$samples | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $OutputPath
```

- [ ] **Step 2: Capture the pre-optimization baseline**

Before changing the sampling hot path, run the Release app for 60 seconds in each state:

```powershell
.\scripts\measure-performance.ps1 -ProcessName TopMonitor -Seconds 60 `
  -OutputPath artifacts\performance\idle-before.csv
.\scripts\measure-performance.ps1 -ProcessName TopMonitor -Seconds 60 `
  -OutputPath artifacts\performance\fps-enabled-no-game-before.csv
```

Expected: the script records 60 one-second samples and whether PresentMon is running.

- [ ] **Step 3: Add a regression test before changing worker plans**

If allocation tracing identifies `SampleOnceAsync` grouping as a material hot path, add a test to `MetricSamplingServiceTests` that updates subscriptions once, samples repeatedly, and asserts providers continue receiving exactly their owned metric arrays after a cached worker plan is introduced.

```csharp
Assert.All(provider.Requests, request =>
    Assert.Equal(expectedIds, request.OrderBy(id => id.Value)));
```

Run it before implementation and verify it fails because the cached plan diagnostics/count are absent.

- [ ] **Step 4: Cache immutable provider request plans only when justified**

Build provider groups when subscriptions change:

```csharp
private sealed record ProviderRequest(
    IMetricProvider Provider,
    MetricId[] MetricIds);
```

Each worker receives `ProviderRequest[]` and reuses it on every tick. Do not pool `MetricValue` domain objects and do not change provider concurrency semantics.

- [ ] **Step 5: Run tests and capture after measurements**

```powershell
dotnet test TopMonitor.sln -c Release
.\scripts\measure-performance.ps1 -ProcessName TopMonitor -Seconds 60 `
  -OutputPath artifacts\performance\idle-after.csv
.\scripts\measure-performance.ps1 -ProcessName TopMonitor -Seconds 60 `
  -OutputPath artifacts\performance\fps-enabled-no-game-after.csv
```

Expected:

- no test failures;
- PresentMon absent when no game is active;
- idle CPU average below 1% where the machine permits stable measurement;
- no monotonic private-byte growth during the 60-second sample;
- no regression from the before baseline.

- [ ] **Step 6: Document measured values and decision**

`docs/performance-baseline.md` must contain the exact before/after averages and state whether the LINQ plan caching was implemented or rejected. Keep `AllowsTransparency` unchanged unless profiling identifies it as a dominant cost and a separate design is approved.

- [ ] **Step 7: Commit and push**

```powershell
git add scripts/measure-performance.ps1 `
        docs/performance-baseline.md `
        docs/development-guide.md `
        src/TopMonitor.Application/Metrics/MetricSamplingService.cs `
        tests/TopMonitor.Application.Tests/MetricSamplingServiceTests.cs
git commit -m "perf: reduce measured sampling overhead"
git push origin main
```

If sampling code did not need modification, omit the two unchanged source/test paths and use commit message `docs: record TopMonitor performance baseline`.

---

### Task 12: Documentation, Full Verification, and Windows Acceptance

**Files:**
- Modify: `docs/architecture.md`
- Modify: `docs/development-guide.md`
- Modify: `docs/sensor-compatibility.md`
- Modify: `docs/release-guide.md`

**Interfaces:**
- Documents PawnIO initialization, Performance Log Users setup, PresentMon lifecycle, FPS limitations, exact dependency versions, and troubleshooting.

- [ ] **Step 1: Update documentation**

Document:

- CPU selection order and the selected-source log entry.
- One-time PawnIO setup and ordinary-user runtime behavior.
- FPS auto-targeting, 750ms debounce, two-second probe, five-second grace period, and unsupported-app behavior.
- PresentMon/PawnIO origins, hashes, license notices, and publish paths.
- The requirement to sign out/in after adding Performance Log Users.
- How to inspect `%LocalAppData%\TopMonitor\logs`.
- How to verify no PresentMon process remains after exit.

- [ ] **Step 2: Run fresh full verification**

```powershell
dotnet restore TopMonitor.sln
dotnet build TopMonitor.sln -c Release --no-restore
dotnet test TopMonitor.sln -c Release --no-build
.\scripts\publish-win-x64.ps1
```

Expected: every command exits 0; all tests pass; publish output is `artifacts/publish/win-x64`.

- [ ] **Step 3: Verify published dependency and process cleanup**

```powershell
Get-Item artifacts\publish\win-x64\TopMonitor.exe
Get-Item artifacts\publish\win-x64\Dependencies\PresentMon.exe
Get-Item artifacts\publish\win-x64\Dependencies\PawnIO_setup.exe
Get-AuthenticodeSignature artifacts\publish\win-x64\Dependencies\PawnIO_setup.exe
```

Expected: all files exist; record the actual signature status in `docs/release-guide.md`.

- [ ] **Step 4: Perform manual Windows checks**

1. Exit the existing tray instance.
2. Start published `TopMonitor.exe` as a normal user.
3. Run one-time PawnIO setup from Settings and rescan.
4. Confirm i7-14700KF shows Package, Core Max, or maximum core temperature and logs the selected source.
5. Change CPU/GPU/network values and confirm overlay width stays constant.
6. Enable FPS, start a DirectX game, and confirm automatic integer FPS.
7. Alt+Tab for under five seconds and confirm no rapid PresentMon restart loop.
8. Exit the game and confirm FPS becomes `--`.
9. Exit TopMonitor from the tray and confirm no `TopMonitor` or owned `PresentMon` process remains.

- [ ] **Step 5: Commit final documentation and push**

```powershell
git add docs/architecture.md `
        docs/development-guide.md `
        docs/sensor-compatibility.md `
        docs/release-guide.md
git commit -m "docs: document CPU temperature and FPS setup"
git push origin main
git status -sb
```

Expected: local `main` is clean and aligned with `origin/main`.
