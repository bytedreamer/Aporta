using Aporta.Shared.Models;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using static Aporta.Shared.Calls.OutputCalls;
using static System.Net.WebRequestMethods;

namespace Aporta.Shared.Calls
{
    public class OutputCalls(HttpClient httpClient) : IOutputCalls
    {

        public async Task<IEnumerable<Endpoint>> GetAllOutputEndpoints()
        {
            string url = $"{Paths.Outputs}/outputendpoints";
            return await httpClient.GetFromJsonAsync<List<Endpoint>>(url);
        }

        public async Task<List<Output>> GetAllOutputs()
        {
            return await httpClient.GetFromJsonAsync<List<Output>>(Paths.Outputs);
        }

        public async Task<Output> GetOutput(int outputId)
        {
            return await httpClient.GetFromJsonAsync<Output>($"{Paths.Outputs}/{outputId}");
        }

        public async Task<bool?> GetOutputState(int outputId)
        {
            string url = $"{Paths.Outputs}/get/{outputId}";
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return bool.Parse(await response.Content.ReadAsStringAsync());
        }

        public async Task SetOutputState(int outputId, bool? checkedValue)
        {
            string url = $"{Paths.Outputs}/set/{outputId}";
            url = QueryHelpers.AddQueryString(url, "state", checkedValue.ToString());
            var response = await httpClient.PostAsync(url, new StringContent(string.Empty));
            if (!response.IsSuccessStatusCode)
            {
                //_snackbarMessage = "Unable to set output";
                //_snackbarColor = SnackbarColor.Danger;
                //await _snackbar.Show();
            }
        }

    }

    public interface IOutputCalls
    {
        public Task<IEnumerable<Endpoint>> GetAllOutputEndpoints();
        public Task<List<Output>> GetAllOutputs();
        public Task<Output> GetOutput(int outputId);
        public Task<bool?> GetOutputState(int outputId);
        public Task SetOutputState(int outputId, bool? checkedValue);
    }
}
