using System.Threading;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models;

namespace Aporta.Core.Services
{
    public class GlobalSettingService
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
            return _dataEncryption.Decrypt(await _globalSettingRepository.Get(SslCertificatePassword) ?? string.Empty);
        }

        public async Task SetSslCertificatePassword(string password)
        {
            string encryptedPassword = _dataEncryption.Encrypt(password);

            await InsertOrAdd(new GlobalSetting {Name = SslCertificatePassword, Value = encryptedPassword});
        }

        private async Task InsertOrAdd(GlobalSetting globalSetting)
        {
            await _semaphore.WaitAsync();

            try
            {
                string value = await _globalSettingRepository.Get(globalSetting.Name);
                if (value == null)
                {
                    await _globalSettingRepository.Insert(globalSetting);
                }
                else
                {
                    await _globalSettingRepository.Update(globalSetting);
                }

            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}