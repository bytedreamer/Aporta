using System.Threading.Tasks;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models;

namespace Aporta.Core.Services
{
    public class OutputService
    {
        private readonly OutputRepository _outputRepository;
        private readonly EndpointRepository _endpointRepository;
        private readonly ExtensionService _extensionService;

        public OutputService(OutputRepository outputRepository,
            EndpointRepository endpointRepository, ExtensionService extensionService)
        {
            _extensionService = extensionService;
            _outputRepository = outputRepository;
            _endpointRepository = endpointRepository;
        }

        public async Task<int> Insert(Output output)
        {
            await _outputRepository.Insert(output);
            return output.Id;
        }

        public async Task Delete(int id)
        {
            await _outputRepository.Delete(id);
        }

        public async Task Set(int outputId, bool state)
        {
            var output = await _outputRepository.Get(outputId);
            var endpoint = await _endpointRepository.Get(output.EndpointId);
            await _extensionService.GetControlPoint(endpoint.ExtensionId, endpoint.DriverEndpointId).Set(state);
        }

        public async Task<bool?> Get(int outputId)
        {
            var output = await _outputRepository.Get(outputId);
            var endpoint = await _endpointRepository.Get(output.EndpointId);
            return await _extensionService.GetControlPoint(endpoint.ExtensionId, endpoint.DriverEndpointId).Get();
        }
    }
}