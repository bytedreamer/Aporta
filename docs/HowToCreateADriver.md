# How to create a new Aporta Hardware Driver

- Create a new class library for the driver. In this example, we'll name the class library project: ***Aporta.Drivers.NewDriver***
-    In the class library (Aporta.Drivers.NewDriver.csproj) project file, add the following project group:
   ```
<PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <LangVersion>default</LangVersion>
  </PropertyGroup>
  ```
- The Aporta.csproj file has a DriverFiles item group. Add your new project to it:

```

    <ItemGroup>
        <NewDriverFiles Include="$(ProjectDir)\..\Drivers\Aporta.Drivers.NewDriver\bin\$(Configuration)\$(TargetFramework)\*.*" />        
    </ItemGroup>

```
- Add this item group to the AfterBuild and AfterPublish targets in the Aporta.csproj file:

```
    <Target Name="CopyDrivers" AfterTargets="AfterBuild">
        <MakeDir Directories="$(OutDir)\Drivers" />
        <Copy SourceFiles="@(NewDriverFiles)" DestinationFiles="@(NewDriverFiles->'$(OutDir)\Drivers\New\%(RecursiveDir)%(Filename)%(Extension)')" SkipUnchangedFiles="true" OverwriteReadOnlyFiles="true" Retries="3" RetryDelayMilliseconds="300" />
        <MakeDir Directories="$(OutDir)\Data" />
    </Target>

    <Target Name="PublishDrivers" AfterTargets="AfterPublish">
        <MakeDir Directories="$(PublishDir)\Drivers" />
        <Copy SourceFiles="@(NewDriverFiles)" DestinationFiles="@(NewDriverFiles->'$(PublishDir)\Drivers\New\%(RecursiveDir)%(Filename)%(Extension)')" SkipUnchangedFiles="true" OverwriteReadOnlyFiles="true" Retries="3" RetryDelayMilliseconds="300" />
        <MakeDir Directories="$(PublishDir)\Data" />
    </Target>
```

- Add a new class to your project that implements the IHardwareDriver interface.
- The IHardwareDriver contract requires an ID of Type GUID
- In project Aporta.WebClient, DriverConfiguration.razor, copy this GUID to the conditional checks at the top of the page.
