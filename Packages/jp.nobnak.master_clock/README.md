# Master Clock

High-precision time synchronization system for Unity supporting both standalone and networked environments.

## Features

- **Dual Mode Support**: Standalone (`MasterClockStandalone`) and networked (`MasterClock`) implementations
- **Interface Separation**: `IMasterClockQuery` for read-only operations, `IMasterClock` for full control
- **Query Facade**: `MasterClockQuery` component for switching between implementations dynamically
- **EMA-based Offset Estimation**: Smooth clock synchronization using Exponential Moving Average
- **High Precision**: Uses `Time.timeAsDouble` and `NetworkTime.predictedTime` for maximum accuracy  
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

### Query Facade Mode

```csharp
// Get MasterClockQuery component (query-only facade)
var clockQuery = GetComponent<MasterClockQuery>();

// Switch between standalone and networked implementations
clockQuery.CurrentType = ClockType.Networked;

// Query operations only (read-only interface)
double syncTime = clockQuery.GetSynchronizedTime();
double offset = clockQuery.GetCurrentOffset();
var stats = clockQuery.GetEmaStatistics();

// Operation methods are not available (compile-time safety)
// clockQuery.ProcessTick(123); // ← Compile error
```

## API Reference

### Interfaces

#### IMasterClockQuery (Read-only Interface)
Properties and query methods for time synchronization data:

**Properties:**
- `Settings` - Read-only access to configuration
- `name` - Component name

**Query Methods:**
- `GetSynchronizedTime()` - Get synchronized time
- `GetRemoteSynchronizedTime()` - Get remote-compatible sync time
- `GetCurrentOffset()` - Get current offset estimation
- `GetEmaStatistics()` - Get EMA variance and standard deviation
- `GetLastInputTick()` - Get last processed tick value
- `GetCurrentTime()` - Get current reference time
- `GetCurrentTickTime()` - Get current tick-based time

#### IMasterClock (Full Interface)
Inherits from `IMasterClockQuery` and adds operation methods:

**Core Methods:**
- `ProcessTick(uint tickValue)` - Process external tick for synchronization
- `ProcessCurrentTimeTick()` - Generate tick from current time (debug)
- `ResetOffset()` - Reset EMA offset estimation
- `SetEmaDuration(int seconds)` - Dynamically change EMA duration
- `SetTickRate(int rate)` - Change tick rate (Hz)
- `Reinitialize()` - Complete reinitialization

### Components

#### MasterClockQuery
Facade component for switching between clock implementations:

**Properties:**
- `CurrentType` - Get/set current clock type (Standalone or Networked)

**Setup:**
- Assign `standaloneClockReference` to a `MasterClockStandalone` component
- Assign `networkedClockReference` to a `MasterClock` component
- Use `CurrentType` to switch between implementations at runtime

## Advanced Usage Examples

### Interface Segregation Pattern

```csharp
public class TimeDisplayUI : MonoBehaviour 
{
    // Accept only query interface - prevents accidental state modification
    public void UpdateDisplay(IMasterClockQuery clockQuery) 
    {
        double syncTime = clockQuery.GetSynchronizedTime();
        double offset = clockQuery.GetCurrentOffset();
        
        // This would cause a compile error:
        // clockQuery.ProcessTick(123);
    }
}

public class ClockController : MonoBehaviour 
{
    // Accept full interface for control operations
    public void UpdateClock(IMasterClock clock, uint newTick) 
    {
        clock.ProcessTick(newTick);
        
        // Can also query data
        double syncTime = clock.GetSynchronizedTime();
    }
}
```

### Dynamic Switching Between Implementations

```csharp
public class AdaptiveClockManager : MonoBehaviour 
{
    [SerializeField] private MasterClockQuery clockQuery;
    
    void Start() 
    {
        // Start with standalone mode
        clockQuery.CurrentType = ClockType.Standalone;
        
        // Listen for network state changes
        NetworkManager.singleton.OnClientConnect.AddListener(OnNetworkConnected);
        NetworkManager.singleton.OnClientDisconnect.AddListener(OnNetworkDisconnected);
    }
    
    void OnNetworkConnected() 
    {
        // Switch to networked mode when connected
        clockQuery.CurrentType = ClockType.Networked;
        Debug.Log("Switched to networked clock");
    }
    
    void OnNetworkDisconnected() 
    {
        // Fall back to standalone mode
        clockQuery.CurrentType = ClockType.Standalone;
        Debug.Log("Switched to standalone clock");
    }
}
```

## Dependencies

- Unity 2022.3+
- Mirror Networking (for networked mode)
- Unity Mathematics

## OSC Integration

For OSC (Open Sound Control) integration, use the separate `MasterClockOSCAdapter` class located in your project's Assets folder. This adapter uses UnityEvent for loose coupling with MasterClock instances.

### Setup Instructions

1. Add `MasterClockOSCAdapter` to a GameObject
2. In the Inspector, connect the `OnTickReceived` UnityEvent to your clock's `ProcessTick(uint)` method:
   - For direct control: Connect to `MasterClock` or `MasterClockStandalone`
   - For facade pattern: Note that `MasterClockQuery` is read-only and doesn't expose `ProcessTick`
3. Configure OSC message routing to call the adapter's `ListenTick()` method

```csharp
// Example: Manual tick sending (for testing)
var oscAdapter = GetComponent<MasterClockOSCAdapter>();
oscAdapter.SendTick(12345);

// Example: Runtime subscription to full interface
var masterClock = GetComponent<MasterClock>();
oscAdapter.GetTickEvent().AddListener(masterClock.ProcessTick);

// Example: Mixed setup with query facade for display
var clockQuery = GetComponent<MasterClockQuery>();
var displayUI = GetComponent<TimeDisplayUI>();
// OSC connects to the actual clock for operations
// UI connects to query facade for safe data access
```

### UnityEvent Architecture

The adapter is completely decoupled from MasterClock implementations. You can:
- Connect multiple MasterClock instances to one adapter
- Use Inspector to visually configure connections
- Easily swap MasterClock implementations
- Add custom tick processors alongside MasterClock
- Combine with `MasterClockQuery` for safe read-only access patterns

## License

MIT License - see LICENSE.md for details
