# Master Clock

High-precision time synchronization system for Unity supporting both standalone and networked environments.

- Standalone version.<br>
[![Standalone ver. demo](http://img.youtube.com/vi/BwkbLiR88dA/sddefault.jpg)](https://youtu.be/BwkbLiR88dA)
- Client/Server (Mirror) version.<br>
[![MIrror ver. demo](http://img.youtube.com/vi/UguE4zgjXe0/sddefault.jpg)](https://youtu.be/UguE4zgjXe0)

**Namespace**: All classes are organized under the `Nobnak.MasterClock` namespace.

## Overview

Master Clock implements a **remarkably simple** time synchronization system:

1. **External Time Source** sends OSC packets containing tick values at **30 Hz** (30 ticks per second)
2. **Master Clock** receives these ticks and calculates the time offset using EMA (Exponential Moving Average)
3. **Synchronized Time** is provided to your application with sub-millisecond accuracy

**Key Benefits:**
- **Simple**: No complex NTP protocols - just 30 tick values per second over OSC
- **Fast**: Direct tick-to-time conversion with minimal latency
- **Robust**: EMA smoothing handles network jitter and packet loss
- **Scalable**: One time source synchronizes unlimited Unity instances
- **Flexible**: Dual mode support (standalone + networked) with read-only query interface

## New Features

### ThreadSafeTime
A thread-safe static time utility class that provides high-precision time access from any thread:

```csharp
using Nobnak.MasterClock;

// Access from any thread
double currentTime = ThreadSafeTime.realtimeSinceStartupAsDouble;
double timeInMs = ThreadSafeTime.realtimeSinceStartupAsMilliseconds;

// Check drift from Unity Time (main thread only)
double drift = ThreadSafeTime.GetDriftFromUnityTime();

// Get detailed validation information
var validation = ThreadSafeTime.GetValidationInfo();
var stats = ThreadSafeTime.QuickValidationTest(100);

// Get detailed debug information
string debugInfo = ThreadSafeTime.GetDebugInfo();
```

**Features:**
- **Auto-initialization**: Uses `RuntimeInitializeOnLoadMethod` for automatic startup
- **High precision**: Based on `System.Diagnostics.Stopwatch`
- **Unity compatible**: Provides equivalent values to `Time.realtimeSinceStartupAsDouble`
- **Thread-safe**: Safe access from multiple threads
- **Synchronization**: Re-sync with Unity Time when needed

### Independent EMA Implementation
Master Clock now uses its own Exponential Moving Average implementation:
- **Mirror independent**: No longer depends on Mirror's EMA library
- **Full compatibility**: 100% API compatible with previous version
- **High precision**: Includes variance and standard deviation calculations

## Installation

### Prerequisites

**Mirror Networking** is required for networked functionality:

1. Download **Mirror Networking** from the [Unity Asset Store](https://assetstore.unity.com/packages/tools/network/mirror-129321)
2. Import Mirror into your project
3. Mirror is not available as a UPM package, so Asset Store installation is required

*Note: Master Clock now uses its own independent EMA (Exponential Moving Average) implementation and includes a thread-safe time utility class.*

### Via Package Manager (OpenUPM Registry)

1. Open **Window > Package Manager** in Unity
2. Click the **Settings** gear icon and select **Advanced Project Settings**
3. Add a new Scoped Registry:
   - **Name**: OpenUPM
   - **URL**: `https://package.openupm.com`
   - **Scope**: `jp.nobnak`
4. Click **Save**
5. In the Package Manager, switch to **My Registries** and install **Master Clock**

### Via Git URL

1. Open **Window > Package Manager** in Unity
2. Click the **+** button and select **Add package from git URL...**
3. Enter: `https://github.com/nobnak/MasterClock.git?path=Packages/jp.nobnak.master_clock`

## Quick Start

### Standalone Mode

```csharp
using Nobnak.MasterClock;

// Get MasterClockStandalone component
var masterClock = GetComponent<MasterClockStandalone>();

// Process external tick
masterClock.ProcessTick(tickValue);

// Get synchronized time
double syncTime = masterClock.GetSynchronizedTime();

// Access global instance
double globalSyncTime = MasterClock.Global?.GetSynchronizedTime() ?? 0.0;
```

### Networked Mode (Mirror)

```csharp
using Nobnak.MasterClock;
using Mirror;

// Get MasterClockNet component (requires NetworkBehaviour)
var masterClock = GetComponent<MasterClockNet>();

// Server-only operations
if (NetworkServer.active) {
    masterClock.ProcessTick(tickValue);
}

// Client & Server can query synchronized time
double syncTime = masterClock.GetSynchronizedTime();

// Access global instance
double globalSyncTime = MasterClock.Global?.GetSynchronizedTime() ?? 0.0;
```

### Global Access Mode

```csharp
using Nobnak.MasterClock;

// Access global MasterClock instance (auto-selected from active components)
var globalClock = MasterClock.Global;

if (globalClock != null) {
    // Query operations only (read-only interface)
    double syncTime = globalClock.GetSynchronizedTime();
    double offset = globalClock.GetCurrentOffset();
    var stats = globalClock.GetEmaStatistics();
    string clockName = globalClock.name;
}
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

#### MasterClockStandalone
Standalone Unity MonoBehaviour component for time synchronization:

**Features:**
- Direct Unity Time integration
- No network dependencies
- Automatic global instance registration
- ThreadSafeTime integration for external tick processing

#### MasterClockNet
Network-enabled MonoBehaviour component using Mirror:

**Features:**
- Server-client synchronization via SyncVar
- NetworkTime.predictedTime integration
- Server-only tick processing with client synchronization
- Automatic global instance registration on server

#### Global Instance Access
Both components automatically register as global instances:
- Use `MasterClock.Global` to access the currently active clock
- Only one global instance is active at a time
- Provides `IMasterClockQuery` interface for read-only operations

## Usage Notes

### Namespace Import
All MasterClock classes are in the `Nobnak.MasterClock` namespace:

```csharp
using Nobnak.MasterClock;
```

### Global Instance Management
- Both `MasterClockStandalone` and `MasterClockNet` automatically register as global instances
- Use `MasterClock.Global` to access the currently active clock from anywhere
- Only one global instance is active at a time (last enabled component takes precedence)

### Thread Safety
- `ThreadSafeTime` provides thread-safe time access from any thread
- Main MasterClock operations should be called from the main thread
- Use `ThreadSafeTime.realtimeSinceStartupAsDouble` for external tick processing

## Dependencies

- Unity 2022.3+
- Unity Mathematics
- Mirror Networking (for MasterClockNet component only)

**Note**: Mirror is only required if you use the `MasterClockNet` component. The `MasterClockStandalone` component has no external dependencies beyond Unity and Mathematics.

## License

MIT License - see LICENSE.md for details
