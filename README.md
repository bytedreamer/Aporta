# Aporta #

[![Build Status](https://dev.azure.com/jonathanhorvath/Aporta/_apis/build/status%2Fbytedreamer.Aporta?branchName=develop)](https://dev.azure.com/jonathanhorvath/Aporta/_build/latest?definitionId=2&branchName=develop)

Aporta is an open source physical access controller. The main purpose of the software is to secure doors from unauthorized access. This is accomplished by determining if a person is authorized to enter a door. The door is unlocked momentarily by the software when authorized credential are presented.

Aporta doesn't intend to recreate what is already available by existing physical access controllers. The goal is to provide a level of flexibility that is not possible with existing closed physical access control systems. Below are the design considerations that are going to make Aporta truly versatile. 

- The software is written to be platform independent. This gives Aporta the ability to run on wide range of hardware. The same code runs on Windows, Mac, or Linux, even lower power devices such as Raspberry Pi.
- Modularity is built into the architecture of the software. There will be the ability to add custom plugins for enhanced capabilities. 
- Open standards are embraced and built into the software.
- Features are not to be restricted by licensing. All of the software's capabilities are available without being overburden with expensive and confusing license terms.

## Release Plan ##

The project is early in its development. After working on access control products for many years, my inclination is that there are a large number of features required for a viable access controller. The list below is a attempt to limit the feature targeted in the first release.

- Easy installation **(Done)**
    - Windows installer **(Done)**
    - Debian packages **(Done)**
- Self hosted web management
    - SSL required by default **(Done)**
    - A master password to gain access
- OSDP Driver
    - Install new devices with security channel encryption
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

- [amd64](https://www.z-bitco.com/downloads/Aporta.linux-amd64.deb) for Intel and AMD 64 bit processors
- [armhf](https://www.z-bitco.com/downloads/Aporta.linux-armhf.deb) for older 32-bit Raspberry PIs
- [arm64](https://www.z-bitco.com/downloads/Aporta.linux-arm64.deb) for Raspberry PIs 3+ and newer with 64-bit OS

Steps to install and run from Linux DEB packages

- Run commands from a shell terminal
- Install files by running the following command using the correct version of the package file
```shell
 sudo dpkg -i Aporta.linux-XXX.deb
 ```
- Change directory to ```/opt/Aporta```
- The followng command will run the Aporta server
```shell
sudo ./Aporta
```
## Aporta Housekeeping

General information about configuring and setting up the Aporta project for development and running locally.

[Aporta HouseKeeping](docs/AportaHouseKeeping.md)

### Configuration ###

Browse to the local web url ```https://localhost:8443``` to get started. A [Quick Start Guide](https://github.com/bytedreamer/Aporta/wiki/Quick-start-guide) can be found in the Wiki section.

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

## Documentation 
- [How to Create a Driver](docs/HowToCreateADriver.md)
- [How to Use the Virtual Device Driver](docs/HowToUseTheVirtualDeviceDriver.md)
