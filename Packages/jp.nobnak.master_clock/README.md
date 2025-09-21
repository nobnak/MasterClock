# Master Clock

High-precision time synchronization system for Unity supporting both standalone and networked environments.

## Features

- **Dual Mode Support**: Standalone (`MasterClockStandalone`) and networked (`MasterClock`) implementations
- **EMA-based Offset Estimation**: Smooth clock synchronization using Exponential Moving Average
- **High Precision**: Uses `Time.timeAsDouble` and `NetworkTime.predictedTime` for maximum accuracy  
- **Common Interface**: `IMasterClock` interface for polymorphic usage
- **Editor Integration**: Real-time debug information in Unity Inspector
- **Pure Package**: No external dependencies beyond Unity and Mirror

## Quick Start

### Standalone Mode

```csharp
// Get MasterClockStandalone component
var masterClock = GetComponent<MasterClockStandalone>();

// Process external tick
masterClock.ProcessTick(tickValue);

// Get synchronized time
double syncTime = masterClock.GetSynchronizedTime();
```

### Networked Mode (Mirror)

```csharp
// Get MasterClock component (requires NetworkBehaviour)
var masterClock = GetComponent<MasterClock>();

// Server-only operations
if (NetworkServer.active) {
    masterClock.ProcessTick(tickValue);
}

// Client & Server can query synchronized time
double syncTime = masterClock.GetSynchronizedTime();
```

## API Reference

### Core Methods

- `ProcessTick(uint tickValue)` - Process external tick for synchronization
- `ProcessCurrentTimeTick()` - Generate tick from current time (debug)
- `ResetOffset()` - Reset EMA offset estimation
- `SetEmaDuration(int seconds)` - Dynamically change EMA duration
- `SetTickRate(int rate)` - Change tick rate (Hz)
- `Reinitialize()` - Complete reinitialization

### Query Methods

- `GetSynchronizedTime()` - Get synchronized time
- `GetRemoteSynchronizedTime()` - Get remote-compatible sync time
- `GetCurrentOffset()` - Get current offset estimation
- `GetEmaStatistics()` - Get EMA variance and standard deviation
- `GetLastInputTick()` - Get last processed tick value
- `GetCurrentTime()` - Get current reference time
- `GetCurrentTickTime()` - Get current tick-based time

## Dependencies

- Unity 2022.3+
- Mirror Networking (for networked mode)
- Unity Mathematics

## OSC Integration

For OSC (Open Sound Control) integration, use the separate `MasterClockOSCAdapter` class located in your project's Assets folder. This adapter uses UnityEvent for loose coupling with MasterClock instances.

### Setup Instructions

1. Add `MasterClockOSCAdapter` to a GameObject
2. In the Inspector, connect the `OnTickReceived` UnityEvent to MasterClock's `ProcessTick(uint)` method
3. Configure OSC message routing to call the adapter's `ListenTick()` method

```csharp
// Example: Manual tick sending (for testing)
var oscAdapter = GetComponent<MasterClockOSCAdapter>();
oscAdapter.SendTick(12345);

// Example: Runtime subscription
oscAdapter.GetTickEvent().AddListener(myMasterClock.ProcessTick);
```

### UnityEvent Architecture

The adapter is completely decoupled from MasterClock implementations. You can:
- Connect multiple MasterClock instances to one adapter
- Use Inspector to visually configure connections
- Easily swap MasterClock implementations
- Add custom tick processors alongside MasterClock

## License

MIT License - see LICENSE.md for details
