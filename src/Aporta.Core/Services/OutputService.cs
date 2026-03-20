using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Messaging;
using Microsoft.AspNetCore.SignalR;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

public class OutputService
{
    private readonly Z9DevRepository _z9DevRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly ExtensionService _extensionService;
    private readonly DevStateService _devStateService;

    public OutputService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
        ExtensionService extensionService, DevStateService devStateService)
    {
        _hubContext = hubContext;
        _extensionService = extensionService;
        _devStateService = devStateService;
        _z9DevRepository = new Z9DevRepository(dataAccess);

        _extensionService.StateChanged += ExtensionServiceOnOutputStateChanged;
    }

    private async void ExtensionServiceOnOutputStateChanged(object sender, StateChangedEventArgs eventArgs)
    {
        try
        {
            var dev = await _z9DevRepository.GetForDriverId(eventArgs.Endpoint.Id);

            if (dev != null && dev.DevType == DevType.Actuator)
            {
                await _hubContext.Clients.All.SendAsync(Methods.OutputStateChanged, dev.Unid, eventArgs.State);

                _devStateService.UpdateAspect(dev.Unid, DevAspect.Primary,
                    s => s.ActivityState = eventArgs.State ? ActivityState.Active : ActivityState.Inactive);
            }
        }
        catch
        {
            // ignored
        }
    }
}
