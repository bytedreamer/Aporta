using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Microsoft.Extensions.Logging;

namespace Aporta.Extensions.Hardware
{
    public interface IHardwareDriver : IExtension
    {
        /// <summary>
        /// Unique identifier for referencing the extension
        /// </summary>
        public Guid Id { get; }
        
        public IEnumerable<IEndpoint> Endpoints { get; }
        
        /// <summary>
        /// Load the driver
        /// </summary>
        /// <param name="configuration">Configuration settings for the driver</param>
        /// <param name="loggerFactory">Logging</param>
        void Load(string configuration, ILoggerFactory loggerFactory);
        
        /// <summary>
        /// Unload the driver
        /// </summary>
        void Unload();

        /// <summary>
        /// The initial configuration after loading the driver
        /// </summary>
        /// <returns>A configuration with initial values</returns>
        string CurrentConfiguration();

        /// <summary>
        /// Perform a custom action for the driver
        /// </summary>
        /// <param name="action">The action to be performed</param>
        /// <param name="parameters">Parameters needed to perform the action</param>
        /// <returns>Result of the action</returns>
        Task<string> PerformAction(string action, string parameters);
        
        event EventHandler<EventArgs> UpdatedEndpoints;
        
        public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;

        public event EventHandler<OutputStateChangedEventArgs> OutputStateChanged;
    }
}