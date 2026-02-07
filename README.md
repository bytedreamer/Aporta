# Aporta #

[![Build Status](https://dev.azure.com/jonathanhorvath/Aporta/_apis/build/status%2Fbytedreamer.Aporta?branchName=develop)](https://dev.azure.com/jonathanhorvath/Aporta/_build/latest?definitionId=2&branchName=develop)

Aporta is an open source physical access controller. The main purpose of the software is to secure doors from unauthorized access. This is accomplished by determining if a person presenting an issued credential is authorized to enter a door. The door is unlocked momentarily by the software if the person has been given access.

Aporta doesn't intend to recreate what is already available by existing physical access controllers. The goal is to provide a level of flexibility that is not possible with existing closed physical access control systems. Below are the design considerations that are going to make Aporta truly versatile. 

- The software is written to be platform independent. This gives Aporta the ability to run on wide range of hardware. The same code runs on Windows, Mac, or Linux, even lower power devices such as Raspberry Pi.
- Modularity is built into the architecture of the software. There will be the ability to add custom plugins for enhanced capabilities. 
- Open standards are embraced and built into the software.
- Features are not to be restricted by licensing. All the software's capabilities are available without being overburden with expensive and confusing license terms.

## Release Plan ##

The project is early in its development. After working on access control products for many years, my inclination is that there are a large number of features required for a viable access controller. The list below is an attempt to limit the feature targeted in the first release.

- Easy installation **(Done)**
    - Windows installer **(Done)**
    - Debian packages **(Done)**
- Self hosted web management
    - SSL required by default **(Done)**
    - A master password to gain access
- OSDP Driver
    - Install new devices with security channel encryption **(Done)**
    - Process standard Wiegand card reads **(Done)**
    - Detect if device is online **(Done)**
    - Control output **(Done)**
    - Notify when input is tripped
 - Access Control
    - Enroll new cardholder **(Done)**
    - Basic access level assignment
    - Read entire card data for card number **(Done)**
    - Card number is a non-reversible hash **(Done)**
    - Log access events **(Done)**

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

## Z9/Open Community Profile Integration

This fork adds support for the [Z9/Open Community Profile](https://z9security.com), allowing Aporta to operate as a managed community controller under a host. Aporta connects outbound to the host over TCP using protobuf messaging, receives its full configuration (credentials, privileges, schedules, devices), and reports events back.

### Connection & Protocol
- Outbound TCP connection from panel to host (panel-initiates model)
- Protobuf-based Z9/Open Community protocol with identification handshake
- Automatic configuration download on connect (DbChange messages)

### Device Management
- OSDP credential readers over TCP/IP, configured from Z9/Open Community Profile Dev messages
- Door model with strike actuator, door contact sensor, and REX sensor — all wired automatically from the device hierarchy
- Credential reader online/offline status reporting

### Credential & Data Format Support
- Credential mapping from Z9/Open Community Profile Cred objects (card number, facility code, enabled/disabled, effective/expiry dates)
- BinaryFormatter-based encoding and decoding of card data using Z9/Open Community Profile DataFormat definitions
- Credential templates, data layouts, and data formats persisted to local SQLite database

### Access Control Decisions
- Privilege-based access control using DoorAccessPriv and CredPrivBinding
- Door-specific privilege checking (precision access and privilege group elements)
- Schedule enforcement with SchedRestriction (time-of-day, day-of-week)
- Holiday calendar support in schedule evaluation
- Credential status checks: disabled, not yet effective, expired
- Access granted and denied events reported to host with appropriate EvtSubCode (NO_PRIV, INACTIVE, NOT_EFFECTIVE, EXPIRED, OUTSIDE_SCHED, UNKNOWN_CRED_NUM)

### Door Control
- Door strike actuation on access grant with configurable strike time from DoorConfig
- Extended strike time support via credential DoorAccessModifiers (extDoorTime flag)
- Request to Exit (REX) with configurable activateStrikeOnRex
- Door momentary unlock via DevActionReq from host
- Door mode support: unlocked (strike permanently on, bypasses access control), locked (denies all access), card-only (normal access control)
- Default door mode applied from DoorConfig on door creation
- Runtime door mode change via DevActionReq (DoorModeChange) from host, including reset-to-default
- Pin-based modes (card+pin, pin-only, card-or-pin) mapped to card-only until pin support is implemented
- REX behavior respects current door mode (no unlock action in unlocked or locked modes)

### Door Alarm Monitoring
- Door state reporting: unlocked, locked, opened, closed
- Door forced open detection (door opens without strike active) with DoorForced/DoorNotForced events
- Door held open detection (door stays open past configured heldTime) with DoorHeld/DoorNotHeld events
- Extended held time support via credential extDoorTime flag

## Aporta Housekeeping

General information about configuring and setting up the Aporta project for development and running locally.

[Aporta HouseKeeping](https://github.com/bytedreamer/Aporta/wiki/Aporta-Housekeeping-%E2%80%90-Project-Setup)

## Aporta Core Abstractions and Concepts

[A summary of all of the Core Abstractions and Concepts](https://github.com/bytedreamer/Aporta/wiki/Aporta-Core-Abstractions-and-Concepts) used in the Aporta code.

