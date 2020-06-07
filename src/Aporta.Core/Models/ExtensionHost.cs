using System.Text.Json.Serialization;
using Aporta.Core.Extension;
using Aporta.Extensions.Hardware;

namespace Aporta.Core.Models
{
    public class ExtensionHost : Shared.Models.Extension
    {
        [JsonIgnore]
        public string AssemblyPath { get; set; }
        
        [JsonIgnore]
        public Host<IHardwareDriver> Host { get; set; }
    }
}