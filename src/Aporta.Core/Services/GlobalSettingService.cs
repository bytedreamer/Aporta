using System;
using System.Threading;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Extensions;
using Aporta.Shared.Models;

namespace Aporta.Core.Services;

public class GlobalSettingService : IDisposable
{
    private readonly IDataEncryption _dataEncryption;
    private readonly GlobalSettingRepository _globalSettingRepository;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private const string SslCertificatePassword = "SslCertificatePassword";
        
    public GlobalSettingService(IDataAccess dataAccess, IDataEncryption dataEncryption)
    {
        _dataEncryption = dataEncryption;
        _globalSettingRepository = new GlobalSettingRepository(dataAccess);
    }

    public async Task<string> GetSslCertificatePassword()
    {
        await _semaphore.WaitAsync();

        try
        {
            var encryptedPassword = await _globalSettingRepository.Get(SslCertificatePassword);
            if (encryptedPassword != null)
            {
                return _dataEncryption.Decrypt(encryptedPassword);
            }

            string password = _dataEncryption.GeneratePassword();
            encryptedPassword = _dataEncryption.Encrypt(password);
            await _globalSettingRepository.Insert(new GlobalSetting
                {Name = SslCertificatePassword, Value = encryptedPassword});

            return password;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}