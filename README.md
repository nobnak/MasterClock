# Master Clock

High-precision time synchronization system for Unity supporting both standalone and networked environments.

- Standalone version.<br>
[![Standalone ver. demo](http://img.youtube.com/vi/BwkbLiR88dA/sddefault.jpg)](https://youtu.be/BwkbLiR88dA)
- Client/Server (Mirror) version.<br>
[![MIrror ver. demo](http://img.youtube.com/vi/UguE4zgjXe0/sddefault.jpg)](https://youtu.be/UguE4zgjXe0)

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

## Installation

### Prerequisites

**Mirror Networking** is required for all functionality:

1. Download **Mirror Networking** from the [Unity Asset Store](https://assetstore.unity.com/packages/tools/network/mirror-129321)
2. Import Mirror into your project
3. Mirror is not available as a UPM package, so Asset Store installation is required

*Note: Both `MasterClock` and `MasterClockStandalone` depend on Mirror's EMA (Exponential Moving Average) library for time synchronization calculations.*

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

## Dependencies

- Unity 2022.3+
- Mirror Networking (for networked mode)
- Unity Mathematics

## License

MIT License - see LICENSE.md for details
