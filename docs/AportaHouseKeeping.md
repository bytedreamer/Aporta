# Aporta Housekeeping - Project Setup

## Quick Start Video
[![](images/AportaHouseKeeping_RunTheProject.JPG)](AportaHousekeeping.mp4)

## Startup Project
Set Aporta as the startup project. 

![](images/AportaStartupProject.JPG)

## Running the Project

To run the Aporta project, switch from IIS Express to Aporta

![](images/RunningAporta.JPG)

## Configuration
The Aporta Configuration file is located in the **Aporta** Project. 

The configuration file is appsettings.json

![](images/AportaConfig.JPG)

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





