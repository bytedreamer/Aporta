using Aporta.Shared.Models;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace Aporta.Shared.Calls
{
    public class InputCalls(HttpClient httpClient) : IInputCalls
    {


        public async Task<IEnumerable<Input>> GetAllInputs()
        {
            return await httpClient.GetFromJsonAsync<List<Input>>(Paths.Inputs);
        }

        public async Task<bool?> GetInputState(int inputId)
        {
            string url = $"{Paths.Inputs}/state/{inputId}";
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return bool.Parse(await response.Content.ReadAsStringAsync());
        }


        public async Task SetInputState(int inputId, bool? checkedValue)
        {
            string url = $"{Paths.Inputs}/set/{inputId}";
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

    public interface IInputCalls
    {
        public Task<IEnumerable<Input>> GetAllInputs();
        public Task<bool?> GetInputState(int inputId);
        public Task SetInputState(int inputId, bool? checkedValue);
    }
}
