# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-09-22

### Added
- Initial release of Master Clock package
- `MasterClock` class for networked environments (Mirror)
- `MasterClockStandalone` class for single-instance applications
- `MasterClockCore` class providing shared logic
- `IMasterClock` interface for polymorphic usage
- EMA-based offset estimation for smooth synchronization
- Real-time debug information in Unity Inspector
- High-precision time handling with `Time.timeAsDouble` and `NetworkTime.predictedTime`
- Pure package architecture without external dependencies (except Unity and Mirror)

### Architecture
- OSC integration separated into `MasterClockOSCAdapter` (Assets folder) for dependency isolation
- Dual-mode operation (standalone/networked)
- Dynamic configuration changes (tick rate, EMA duration)
- Comprehensive query methods for time information
- Editor integration with live debugging
- Package manager compatible structure

### Design Decisions
- Removed StarOSC dependency from core package to maintain purity
- OSC functionality provided via separate adapter class using UnityEvent architecture
- UnityEvent-based loose coupling between OSC adapter and MasterClock instances
- Direct use of existing `ProcessTick(uint)` method for UnityEvent integration
- Example code (`ClockHandsController`) excluded from package core

### UnityEvent Architecture Benefits
- Complete decoupling between OSC adapter and MasterClock implementations
- Visual Inspector configuration for tick routing using existing `ProcessTick(uint)` methods
- Support for multiple MasterClock instances per adapter
- Runtime dynamic subscription/unsubscription
- Extensible design for custom tick processors
- Reuse of existing API without method duplication
