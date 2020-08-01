using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models;

namespace Aporta.Core.Services
{
    public class OutputService
    {
        private readonly OutputRepository _outputRepository;
        private readonly EndpointRepository _endpointRepository;
        private readonly ExtensionService _extensionService;

        public OutputService(IDataAccess dataAccess, ExtensionService extensionService)
        {
            _extensionService = extensionService;
            _outputRepository = new OutputRepository(dataAccess);
            _endpointRepository = new EndpointRepository(dataAccess);
        }
        
        public async Task<IEnumerable<Output>> GetAll()
        {
            return await _outputRepository.GetAll();
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

        public async Task<IEnumerable<Endpoint>> AvailableControlPoints()
        {
            var endpoints = await _endpointRepository.GetAll();
            var outputs = await _outputRepository.GetAll();
            return endpoints.Where(endpoint =>
                endpoint.Type == EndpointType.Output &&
                !outputs.Select(output => output.EndpointId).Contains(endpoint.Id));
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