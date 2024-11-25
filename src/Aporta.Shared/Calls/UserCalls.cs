using Aporta.Shared.Models;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Aporta.Shared.Calls;

public class UserCalls(HttpClient httpClient) : IUserCalls
{

    public async Task<LoginResult> Login(string password)
    {
        string url = $"{Paths.User}/login/";
        url = QueryHelpers.AddQueryString(url, "password", password);

        return await httpClient.GetFromJsonAsync<LoginResult>(url);
    }

}

public interface IUserCalls
{
    public Task<LoginResult> Login(string password);
}


