using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.DataAccess;
using Aporta.Core.Hubs;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aporta.Core.Services;

public class UserService
{

    private readonly UserRepository _userRepository;
    private readonly PersonRepository _personRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;

    public UserService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext)
    {
        _hubContext = hubContext;
        _userRepository = new UserRepository(dataAccess);
        _personRepository = new PersonRepository(dataAccess);
    }

    public async Task<IEnumerable<User>> GetAll()
    {
        return await _userRepository.GetAll();
    }

    public async Task<User> Get(int userId)
    {
        return await _userRepository.Get(userId);
    }

    public async Task<LoginResult> Login(string password)
    {
        try
        {
            var user = await _userRepository.GetForPassword(password);
            var person = await _personRepository.Get(user.PersonId);
            user.Person = person;
            return new LoginResult() { Success = true, User = user };
        }
        catch (Exception ex)
        {
            return new LoginResult() { Success = false };
        }

    }

    public async Task Insert(User user)
    {
        await _userRepository.Insert(user);

        await _hubContext.Clients.All.SendAsync(Methods.UserInserted, user.Id);
    }

    public async Task Delete(int userId)
    {

        await _userRepository.Delete(userId);

        await _hubContext.Clients.All.SendAsync(Methods.UserDeleted, userId);
    }

}

