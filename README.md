# Aporta #

[![Build Status](https://dev.azure.com/jonathanhorvath/Aporta/_apis/build/status/bytedreamer.Aporta?branchName=main)](https://dev.azure.com/jonathanhorvath/Aporta/_build/latest?definitionId=2&branchName=main)

Aporta is an open source physical access controller. The main purpose of the software is to secure doors from unauthorized access. This is accomplished by determining if a person is authorized to enter a door. The door is unlocked momentarily by the software when authorized credential are presented.

There are many existing controllers that provide this functionality. Aporta doesn't intend to recreate what is already available. The goal is to provide a level of flexibility that is not possible with existing closed physical access control systems. Below are the design considerations that are going to make Aporta truly versatile. 

- The software is written to be platform independent. This gives Aporta the ability to run on wide range of hardware. The same code runs on Windows, Mac, or Linux, even lower power devices such as Raspberry Pi.
- Modularity is built into the architecture of the software. There will be the ability to add custom plugins for enhanced capabilities. 
- Open standards are embraced and built into the software.
- Features are not to be restricted by licensing. All of the software's capabilities are available without being overburden with expensive and confusing license terms.