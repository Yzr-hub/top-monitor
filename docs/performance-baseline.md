# TopMonitor performance baseline

## Measurement environment

- Date: 2026-07-26
- OS: Windows 11 build 26200
- Runtime/SDK: .NET 10 / SDK 10.0.302
- Logical processors: 28
- Build: self-contained `win-x64` Release publish
- State: overlay visible, settings window hidden, default enabled metrics, FPS
  disabled, no game running
- Duration: 60 one-second process samples per run

The raw CSV files are generated under the ignored
`artifacts/performance/` directory by `scripts/measure-performance.ps1`.

## Results

| Metric | Before request-plan cache | After request-plan cache |
|---|---:|---:|
| Samples | 60 | 60 |
| Average process CPU | 0.1308% | 0.1137% |
| Average working set | 155.30 MiB | 154.67 MiB |
| Average private bytes | 85.05 MiB | 84.10 MiB |
| Private-byte range | 81.18–90.87 MiB | 79.64–89.91 MiB |
| First-to-last private bytes | +6.80 MiB | +0.53 MiB |
| Average threads | 23.50 | 23.37 |
| Average handles | 739.32 | 739.23 |
| Samples with PresentMon running | 0/60 | 0/60 |

`dotnet-counters` was also collected for 20 seconds. Total allocation
averaged approximately 76 KB/s before and 98 KB/s after, with zero Gen 0,
Gen 1, or Gen 2 collections in both windows. The difference is measurement
noise from the whole process and does not demonstrate a global allocation-rate
improvement.

## Decision

The immutable provider request plan was implemented. A regression test proves
that repeated samples now reuse the exact same metric ID collection instead of
running `GroupBy` and `ToArray` on every tick. The total process allocation
counter is dominated by other runtime and UI activity, so no broader allocation
claim is made.

The measured idle CPU remains well below the 1% target, private bytes did not
grow monotonically in the steady-state run, and PresentMon was absent for every
idle sample. Automated tracker tests additionally verify that enabling FPS with
no eligible foreground process does not start a PresentMon session.

`AllowsTransparency` remains unchanged. This baseline does not identify WPF
transparent-window rendering as a dominant cost, and removing it would require
a separate visual/interaction design.
