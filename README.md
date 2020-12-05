# Aporta #

[![Build Status](https://dev.azure.com/jonathanhorvath/Aporta/_apis/build/status/bytedreamer.Aporta?branchName=main)](https://dev.azure.com/jonathanhorvath/Aporta/_build/latest?definitionId=2&branchName=main)

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
    - Enroll new cardholder
    - Basic access level assignment
    - Read entire card data for card number **(Done)**
    - Card number is a non-reversible hash **(Done)**
    - Log access events

## Installation ##

Aporta has installers for both Windows and Linux Debian distributions. The should be minimal steps needed to get a fully functional access controller operating.

For both platforms .NET Core 3.1 runtime must be installed first.

[.NET Core 3.1 Runtime](https://dotnet.microsoft.com/download)

### Windows ###

There is a standard MSI file that will install Aporta as a service.

[Aporta 64bit Windows Installer](https://www.z-bitco.com/downloads/Aporta.msi)

### Linux ###

DEB packages have been created both x64 and Arm processors. They have been compressed into a single compressed file.

[Linux Debian Packages](https://www.z-bitco.com/downloads/Aporta.tar.gz)
