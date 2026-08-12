using TopMonitor.Infrastructure.Fps;

namespace TopMonitor.Application.Tests;

public sealed class PresentMonCsvParserTests
{
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

    [Fact]
    public void Parser_keeps_dropped_presents_for_game_fps()
    {
        var parser = new PresentMonCsvParser();
        Assert.True(parser.TryReadHeader(
            "Application,ProcessID,Dropped,TimeInSeconds,PresentMode"));

        Assert.True(parser.TryReadFrame(
            "DeltaForceClient-Win64-Shipping.exe,39028,1,0.0140331,Hardware: Independent Flip",
            out var frame));

        Assert.Equal(39028, frame.ProcessId);
        Assert.Equal(0.0140331, frame.TimeSeconds);
    }
}
