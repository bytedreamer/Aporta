using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Extension;
using Aporta.Core.Hubs;
using Aporta.Core.Models;
using Aporta.Extensions;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

/// <summary>
///
/// </summary>
public class ExtensionService(
    IDataAccess dataAccess,
    IHubContext<DataChangeNotificationHub> hubContext,
    IDataEncryption dataEncryption,
    ILogger<ExtensionService> logger,
    ILoggerFactory loggerFactory)
{
    private static readonly SemaphoreSlim EndpointUpdateSemaphore = new(1, 1);

    private readonly ExtensionRepository _extensionRepository = new(dataAccess);
    private readonly Z9DevRepository _z9DevRepository = new(dataAccess);
    private readonly List<ExtensionHost> _extensions = new();
    private readonly object _extensionLock = new ();

    public string CurrentDirectory { get; init; }

    public async Task Startup()
    {
        logger.LogInformation("Starting extension service");

        await DiscoverExtensions();

        LoadExtensions();
    }

    public void Shutdown()
    {
        logger.LogInformation("Shutting down extension service");

        UnloadExtensions();
    }

    public async Task EnableExtension(Guid extensionId, bool enabled)
    {
        try
        {
            var matchingExtension = _extensions.First(extension => extension.Id == extensionId);

            logger.LogInformation("{Enabled} extension {Name}", enabled ? "Enabling" : "Disabling", matchingExtension.Name);

            matchingExtension.Enabled = enabled;
            await _extensionRepository.Update(matchingExtension);

            if (enabled)
            {
                LoadExtension(matchingExtension);
            }
            else
            {
                UnloadExtension(matchingExtension);
            }
        }
        finally
        {
            await hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged, extensionId);
        }
    }

    public async Task<string> PerformAction(Guid extensionId, string action, string parameters)
    {
        var matchingExtension = MatchingExtensionHost(extensionId);

        logger.LogInformation("Performing {Action} for extension {Name}", action, matchingExtension.Name);

        string result = await matchingExtension.Driver.PerformAction(action, parameters);

        logger.LogInformation("Saving configuration for extension {Name}", matchingExtension.Name);

        await SaveCurrentConfiguration(matchingExtension);

        return result;
    }

    private ExtensionHost MatchingExtensionHost(Guid extensionId)
    {
        return _extensions.First(extension => extension.Id == extensionId);
    }

    public IOutput GetControlPoint(Guid extensionId, string endpointId)
    {
        return _extensions.First(extension => extension.Id == extensionId).Driver.Endpoints
            .First(endpoint => endpoint.Id == endpointId) as IOutput;
    }

    public IInput GetMonitorPoint(Guid extensionId, string endpointId)
    {
        return _extensions.First(extension => extension.Id == extensionId).Driver.Endpoints
            .First(endpoint => endpoint.Id == endpointId) as IInput;
    }

    public IAccess GetAccessPoint(Guid extensionId, string endpointId)
    {
        return _extensions.First(extension => extension.Id == extensionId).Driver.Endpoints
            .First(endpoint => endpoint.Id == endpointId) as IAccess;
    }

    /// <summary>
    /// Returns all endpoints from all loaded drivers, paired with their extension (driver) GUID.
    /// </summary>
    public IEnumerable<(IEndpoint Endpoint, Guid ExtensionId)> GetAllDriverEndpoints()
    {
        return _extensions
            .Where(e => e.Loaded && e.Driver != null)
            .SelectMany(e => e.Driver.Endpoints.Select(ep => (ep, e.Id)));
    }

    /// <summary>
    /// Checks if a driver endpoint exists in any loaded driver.
    /// </summary>
    public bool DriverEndpointExists(string driverEndpointId)
    {
        return _extensions
            .Where(e => e.Loaded && e.Driver != null)
            .Any(e => e.Driver.Endpoints.Any(ep => ep.Id == driverEndpointId));
    }

    public IEnumerable<Shared.Models.Extension> GetExtensions()
    {
        return _extensions.Select(extension =>
        {
            var clone =  extension.ShallowCopy();
            clone.Configuration = extension.Driver?.ScrubSensitiveConfigurationData(clone.Configuration);
            return clone;
        });
    }

    public Shared.Models.Extension GetExtension(Guid extensionId)
    {
        var extension = _extensions.FirstOrDefault(extension => extension.Id == extensionId);
        var clone = extension?.ShallowCopy();

        if (clone == null) return null;

        clone.Configuration = extension.Driver?.ScrubSensitiveConfigurationData(clone.Configuration);

        return clone;
    }

    public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;

    public event EventHandler<StateChangedEventArgs> StateChanged;

    public event EventHandler<OnlineStatusChangedEventArgs> OnlineStatusChanged;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task DiscoverExtensions()
    {
        var extensionFinder = new Finder<IHardwareDriver>();
        var assemblyPaths =
            extensionFinder.FindAssembliesWithPlugins(
                Path.Combine(CurrentDirectory ??
                             Path.GetDirectoryName(CurrentDirectory ?? Assembly.GetEntryAssembly()?.Location) ?? Environment.CurrentDirectory,
                    "Drivers"), loggerFactory.CreateLogger<Finder<IHardwareDriver>>());

        foreach (string assemblyPath in assemblyPaths)
        {
            try
            {
                await GetExtensionsFromAssembly(assemblyPath);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unable to discover assembly {AssemblyPath}", assemblyPath);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task GetExtensionsFromAssembly(string assemblyPath)
    {
        var host = new Host<IHardwareDriver>(assemblyPath);
        host.Load();

        foreach (var driver in host.GetExtensions())
        {
            var extension = await _extensionRepository.Get(driver.Id);
            if (extension == null)
            {
                extension = new ExtensionHost
                    {Enabled = false, Id = driver.Id, Name = driver.Name};
                await _extensionRepository.Insert(extension);
            }

            extension.AssemblyPath = assemblyPath;

            _extensions.Add(extension);
        }

        host.Unload();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LoadExtensions()
    {
        foreach (var extension in _extensions.Where(extension => extension.Enabled))
        {
            try
            {
                LoadExtension(extension);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unable to load extension {Name}", extension.Name);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LoadExtension(ExtensionHost extension)
    {
        lock (_extensionLock)
        {
            if (extension.Loaded)
            {
                return;
            }

            extension.Host = new Host<IHardwareDriver>(extension.AssemblyPath);
            extension.Host.Load();
            extension.Driver = extension.Host.GetExtensions().First(ext => ext.Id == extension.Id);
            extension.Driver.UpdatedEndpoints += DriverOnUpdatedEndpoints;
            extension.Driver.AccessCredentialReceived += DriverOnAccessCredentialReceived;
            extension.Driver.StateChanged += DriverOnStateChanged;
            extension.Driver.OnlineStatusChanged += DriverOnOnlineStatusChanged;

            extension.Driver.Load(extension.Configuration, dataEncryption, loggerFactory);
            extension.Configuration = extension.Driver.CurrentConfiguration();

            extension.Loaded = true;
        }
        // Controller z9_dev is created lazily in DriverOnUpdatedEndpoints when the driver
        // first reports its endpoints, avoiding a race with the concurrent endpoint sync.
    }

    /// <summary>
    /// Ensures an IO_CONTROLLER_EXTERNAL z9_dev exists for the given driver extension.
    /// </summary>
    private async Task EnsureControllerDev(ExtensionHost extension)
    {
        var existingController = await _z9DevRepository.GetController(extension.Id);
        if (existingController != null)
            return;

        var controllerDev = new Dev
        {
            Name = extension.Name,
            DevType = DevType.IoController,
            DevMod = DevMod.IoControllerExternal,
            DevPlatform = DevPlatform.External,
            ExternalDevModId = extension.Id.ToString(),
            ExternalDevModText = extension.Name,
            ExternalId = extension.Id.ToString(),
        };
        SpCoreProtoUtil.InitRequired(controllerDev);
        await _z9DevRepository.Insert(controllerDev);

        logger.LogInformation("Created IO_CONTROLLER_EXTERNAL z9_dev for driver {Name} (extensionId={ExtensionId})",
            extension.Name, extension.Id);
    }

    private void DriverOnUpdatedEndpoints(object sender, EventArgs eventArgs)
    {
        if (sender is not IHardwareDriver driver) return;

        Task.Run(async () =>
        {
            await EndpointUpdateSemaphore.WaitAsync();

            try
            {
                // Ensure controller exists before syncing endpoints
                var controllerDev = await _z9DevRepository.GetController(driver.Id);
                if (controllerDev == null)
                {
                    // Controller not yet created; EnsureControllerDev will handle it
                    var extension = _extensions.FirstOrDefault(e => e.Id == driver.Id);
                    if (extension != null)
                    {
                        await EnsureControllerDev(extension);
                        controllerDev = await _z9DevRepository.GetController(driver.Id);
                    }
                }

                if (controllerDev == null)
                {
                    logger.LogWarning("No controller z9_dev found for driver {DriverId}, skipping endpoint sync", driver.Id);
                    return;
                }

                var existingPoolDevs = (await _z9DevRepository.GetPhysicalChildren(controllerDev.Unid))
                    .Where(d => d.DevPlatformCase == Dev.DevPlatformOneofCase.DevPlatform &&
                                d.DevPlatform == DevPlatform.External)
                    .ToArray();

                var existingExternalIds = existingPoolDevs
                    .Select(d => d.ExternalId)
                    .ToHashSet();

                // Insert new endpoints as pool z9_devs
                foreach (var endpoint in driver.Endpoints.Where(ep =>
                    ep.ExtensionId == driver.Id && !existingExternalIds.Contains(ep.Id)))
                {
                    var devType = endpoint switch
                    {
                        IAccess => DevType.CredReader,
                        IOutput => DevType.Actuator,
                        IInput => DevType.Sensor,
                        _ => DevType.Reserved0
                    };

                    var poolDev = new Dev
                    {
                        Name = endpoint.Name,
                        DevType = devType,
                        DevPlatform = DevPlatform.External,
                        ExternalId = endpoint.Id,
                        PhysicalParentUnid = controllerDev.Unid,
                    };
                    SpCoreProtoUtil.InitRequired(poolDev);
                    await _z9DevRepository.Insert(poolDev);
                }

                // Update existing pool z9_devs (name changes etc.)
                var driverEndpointIds = driver.Endpoints
                    .Where(ep => ep.ExtensionId == driver.Id)
                    .Select(ep => ep.Id)
                    .ToHashSet();

                foreach (var poolDev in existingPoolDevs.Where(d => driverEndpointIds.Contains(d.ExternalId)))
                {
                    var driverEndpoint = driver.Endpoints.First(ep => ep.Id == poolDev.ExternalId);
                    if (poolDev.Name != driverEndpoint.Name)
                    {
                        poolDev.Name = driverEndpoint.Name;
                        await _z9DevRepository.Upsert(poolDev);
                    }
                }

                // Delete pool z9_devs for endpoints no longer reported by driver
                foreach (var poolDev in existingPoolDevs.Where(d => !driverEndpointIds.Contains(d.ExternalId)))
                {
                    // Only delete pool entries (DevPlatform=External); assigned devices are left alone
                    await _z9DevRepository.Delete(poolDev.Unid);
                }

                await SaveCurrentConfiguration(MatchingExtensionHost(driver.Id));
            }
            finally
            {
                EndpointUpdateSemaphore.Release();
            }
        });
    }

    private void DriverOnAccessCredentialReceived(object sender, AccessCredentialReceivedEventArgs eventArgs)
    {
        AccessCredentialReceived?.Invoke(this, eventArgs);
    }

    private void DriverOnStateChanged(object sender, StateChangedEventArgs eventArgs)
    {
        StateChanged?.Invoke(this, eventArgs);
    }

    private void DriverOnOnlineStatusChanged(object sender, OnlineStatusChangedEventArgs eventArgs)
    {
        OnlineStatusChanged?.Invoke(this, eventArgs);
    }

    private async Task SaveCurrentConfiguration(ExtensionHost extension)
    {
        try
        {
            extension.Configuration = extension.Driver.CurrentConfiguration();
            await _extensionRepository.Update(extension);
        }
        finally
        {
            await hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged, extension.Id);
        }
    }

    private void UnloadExtensions()
    {
        foreach (var extension in _extensions)
        {
            try
            {
                UnloadExtension(extension);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unable to unload extension {Name}", extension.Name);
            }
        }

        _extensions.Clear();
    }

    private void UnloadExtension(ExtensionHost extension)
    {
        lock (_extensionLock)
        {
            if (!extension.Loaded)
            {
                return;
            }

            extension.Driver.Unload();
            extension.Driver.UpdatedEndpoints -= DriverOnUpdatedEndpoints;
            extension.Driver.AccessCredentialReceived -= DriverOnAccessCredentialReceived;
            extension.Driver.StateChanged -= DriverOnStateChanged;
            extension.Driver.OnlineStatusChanged -= DriverOnOnlineStatusChanged;

            extension.Host.Unload();
            extension.Loaded = false;
        }
    }
}
