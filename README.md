# Aporta #

[![Build Status](https://dev.azure.com/jonathanhorvath/Aporta/_apis/build/status%2Fbytedreamer.Aporta?branchName=develop)](https://dev.azure.com/jonathanhorvath/Aporta/_build/latest?definitionId=2&branchName=develop)

Aporta is an open source physical access controller. The main purpose of the software is to secure doors from unauthorized access. This is accomplished by determining if a presented credential is authorized to enter a door. The door is unlocked momentarily by the software if the credential has the appropriate access rights.

Aporta doesn't intend to recreate what is already available by existing physical access controllers. The goal is to provide a level of flexibility that is not possible with existing closed physical access control systems. Below are the design considerations that are going to make Aporta truly versatile. 

- The software is written to be platform independent. This gives Aporta the ability to run on wide range of hardware. The same code runs on Windows, Mac, or Linux, even lower power devices such as Raspberry Pi.
- Modularity is built into the architecture of the software. There will be the ability to add custom plugins for enhanced capabilities. 
- Open standards are embraced and built into the software — most notably OSDP, and Z9/Open Community Profile.
- Features are not to be restricted by licensing. All the software's capabilities are available without being overburden with expensive and confusing license terms.

## Z9/Open Community Profile ##

Aporta uses the Z9/Open Community Profile data model for data storage, as well as host communications. The protocol and library are here: https://github.com/z9security/z9open-community. This profile covers important core electronic door access control functionality: credentials, access levels, schedules, holidays, card formats, events, status, door modes, and device actions such as momentary unlock.

## Z9/Flex Community Profile ##

Aporta supports the Z9/Flex Community Profile as a JSON/REST API, intended for use by the built-in web user interface. This API is not generally used for data synchronization with other systems, as Z9/Open Community Profile is much better suited for high-performance data synchronization. The protocol and library are here: https://github.com/z9security/z9flex-community. Z9/Open Community Profile and Z9/Flex Community Profile are essentially the same data model with 2 different representations (Google Protobuf, and JSON/REST, respectively).

## Completed ##

### Aporta
- Easy installation
    - Windows installer
    - Debian packages
- Self hosted web management
    - SSL required by default
- OSDP Driver
    - Install new devices with security channel encryption
    - Process standard Wiegand card reads
    - Detect if device is online
    - Control output
- Access Control
    - Enroll new credential
    - Read entire card data for card number
    - Log access events

### Z9/Open Community Profile
- Connection & Protocol
    - Outbound TCP connection from panel to host (panel-initiates model)
    - Protobuf-based Z9/Open Community protocol with identification handshake
    - Automatic configuration download on connect (DbChange messages)
- Device Management
    - OSDP credential readers over TCP/IP, configured from Dev messages
    - Door model with strike actuator, door contact sensor, and REX sensor — all wired automatically from the device hierarchy
    - Credential reader online/offline status reporting
    - Device status reporting to host via DevStatus messages
- Credential & Data Format Support
    - Credential mapping from Cred objects (card number, facility code, enabled/disabled, effective/expiry dates)
    - Credentials stored by card number; card reads decoded using DataFormat definitions to extract and match by card number
    - Card format definitions (DataFormat, DataLayout, DataGroup) for decoding raw card data into meaningful fields
    - Credential templates, data layouts, and data formats persisted to local SQLite database
- Access Control Decisions
    - Privilege-based access control using DoorAccessPriv and CredPrivBinding
    - Door-specific privilege checking (precision access and privilege group elements)
    - Element-level schedule restrictions on DoorAccessPrivElements
    - Schedule enforcement with SchedRestriction (time-of-day, day-of-week)
    - Holiday-aware schedule evaluation
    - Credential status checks: disabled, not yet effective, expired
    - Access granted and denied events reported to host with appropriate EvtSubCode
- Door Control
    - Door strike actuation on access grant with configurable strike time from DoorConfig
    - Extended strike time support via credential DoorAccessModifiers (extDoorTime flag)
    - Request to Exit (REX) with configurable activateStrikeOnRex
    - Door momentary unlock via DevActionReq from host
    - Door mode support: unlocked, locked, card-only, card+confirming PIN, unique PIN only, card-only or unique PIN
    - PIN support: keypad entry buffering, confirming PIN validation, unique PIN lookup
    - Default door mode applied from DoorConfig on door creation
    - Runtime door mode change via DevActionReq (DoorModeChange) from host, including reset-to-default
    - REX behavior respects current door mode
- Door Alarm Monitoring
    - Door state reporting: unlocked, locked, opened, closed
    - Door forced open detection with DoorForced/DoorNotForced events
    - Door held open detection with DoorHeld/DoorNotHeld events
    - Extended held time support via credential extDoorTime flag
- Events
    - CONTROLLER_STARTUP on service start
    - MOMENTARY_UNLOCK audit trail when host initiates a momentary unlock action
    - TAMPER / TAMPER_NORMAL events with dev state tracking when OSDP reader reports tamper
    - CRED_READER_POWER_CYCLE event when OSDP reader reports power cycle

## API Architecture ##

The web UI communicates with the server through the Z9/Flex Community Profile API (e.g., `/cred/list`, `/door/list`). The Extensions and Driver Configuration pages still use the original REST API (`/api/Extensions`), which is maintained as-is for now.

## TODO ##

### Aporta
- Self hosted web management
    - A master password to gain access
- OSDP Driver
    - Notify when input is tripped
- Access Control
    - Standalone UI for managing Z9/Open Community Profile data model objects including card formats, access levels, schedules, and holidays (Z9/Open handles backend privilege management)

### Z9/Open Community Profile
- DbChange: Handle device deletions (devDelete, devDeleteAll) — currently silently ignored, causing stale devices to accumulate
- EvtControl: Implement event flow control (StartContinuous, ConsumeUpTo) for generic delivery of events persisted while offline
- Events: CONTROLLER_ONLINE / CONTROLLER_OFFLINE — distinguish controller restart from connection drop
- Events: RAW_CRED_READ — audit trail of all card swipes regardless of access decision
- Events: SCHED_ACTIVE / SCHED_INACTIVE — requires schedule evaluation engine on the controller
- Events: Power/battery (POWER_PRIMARY/OFF_PRIMARY/NONE, BATTERY_OK/LOW/FAIL/CRITICAL) — requires power monitoring hardware

## Installation ##

Aporta has installers for both Windows and Linux Debian distributions.

### Windows ###

[64-bit Windows MSI Installer](https://www.z-bitco.com/downloads/Aporta.msi)

### Linux ###

DEB packages have been created both x64 and Arm processors.

- [amd64](https://www.z-bitco.com/downloads/Aporta.linux-amd64.deb) for Intel and AMD 64-bit processors
- [armhf](https://www.z-bitco.com/downloads/Aporta.linux-armhf.deb) for older 32-bit Raspberry PIs
- [arm64](https://www.z-bitco.com/downloads/Aporta.linux-arm64.deb) for Raspberry PIs 3+ and newer with 64-bit OS

Steps to install and run from Linux DEB packages

- Run commands from a shell terminal
- Install files by running the following command using the correct version of the package file
```shell
 sudo dpkg -i Aporta.linux-XXX.deb
 ```
- Change directory to ```/opt/Aporta```
- The following command will run the Aporta server
```shell
sudo ./Aporta
```
The log files will be placed in the standard Linux logging directory

```
/var/log/aporta.log
```

### Quick Start

After installing Aporta, browse to the local web url ```https://localhost:8443``` to get started. A [Quick Start Guide](https://github.com/bytedreamer/Aporta/wiki/Quick-start-guide) can be found in the Wiki section.

### Configuration ###

_appsettings.Production.json_ - Location for Aporta settings

```json
{
    "EventLog": {
        "LogLevel": {
            "Default": "Information", -> configure default logging level
            "Microsoft": "Warning",
            "Microsoft.Hosting.Lifetime": "Information"
            }
    },
    "AllowedHosts": "*",
    "Kestrel": {
        "EndPoints": {
            "Https": {
              "Url": "https://*:8443" -> web site port number (check machine firewall settings if connecting remotely)
            }
        }
    }
}
```

## Aporta Housekeeping

General information about configuring and setting up the Aporta project for development and running locally.

[Aporta HouseKeeping](https://github.com/bytedreamer/Aporta/wiki/Aporta-Housekeeping-%E2%80%90-Project-Setup)

## Aporta Core Abstractions and Concepts

[A summary of all of the Core Abstractions and Concepts](https://github.com/bytedreamer/Aporta/wiki/Aporta-Core-Abstractions-and-Concepts) used in the Aporta code.

Note: The Wiki documents the pre-Z9/Open Community Profile architecture.

