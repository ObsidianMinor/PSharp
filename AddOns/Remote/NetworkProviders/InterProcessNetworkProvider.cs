﻿//-----------------------------------------------------------------------
// <copyright file="InterProcessNetworkProvider.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;

using Microsoft.PSharp.Net;

namespace Microsoft.PSharp.Remote
{
    /// <summary>
    /// Class implementing a network provider for inter-process communication.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    internal class InterProcessNetworkProvider : INetworkProvider, IRemoteCommunication
    {
        #region fields

        /// <summary>
        /// Instance of the P# runtime.
        /// </summary>
        private Runtime Runtime;

        /// <summary>
        /// The local id address.
        /// </summary>
        private string IpAddress;

        /// <summary>
        /// The local port.
        /// </summary>
        private string Port;

        /// <summary>
        /// Channel for remote communication.
        /// </summary>
        private IRemoteCommunication Channel;

        /// <summary>
        /// The application assembly.
        /// </summary>
        private Assembly ApplicationAssembly;

        #endregion

        #region initialization

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="ipAddress">IpAddress</param>
        /// <param name="port">Port</param>
        public InterProcessNetworkProvider(string ipAddress, string port)
        {
            this.IpAddress = ipAddress;
            this.Port = port;
        }

        /// <summary>
        /// Initializes the network provider.
        /// </summary>
        /// <param name="runtime">Runtime</param>
        /// <param name="applicationAssembly">ApplicationAssembly</param>
        public void Initialize(Runtime runtime, Assembly applicationAssembly)
        {
            this.Runtime = runtime;
            this.ApplicationAssembly = applicationAssembly;

            //var channels = new Dictionary<string, IRemoteCommunication>();

            if (runtime.Configuration.ContainerId == 0)
            {
                Uri address = new Uri("http://" + this.IpAddress + ":" + this.Port + "/request/" + 1 + "/");

                WSHttpBinding binding = new WSHttpBinding();
                EndpointAddress endpoint = new EndpointAddress(address);

                this.Channel = ChannelFactory<IRemoteCommunication>.CreateChannel(binding, endpoint);
            }
            else
            {
                Uri address = new Uri("http://" + this.IpAddress + ":" + this.Port + "/request/" + 0 + "/");

                WSHttpBinding binding = new WSHttpBinding();
                EndpointAddress endpoint = new EndpointAddress(address);

                this.Channel = ChannelFactory<IRemoteCommunication>.CreateChannel(binding, endpoint);
            }
        }

        #endregion

        #region network provider methods

        /// <summary>
        /// Creates a new remote machine of the specified type
        /// and with the specified event. An optional friendly
        /// name can be specified. If the friendly name is null
        /// or the empty string, a default value will be given.
        /// </summary>
        /// <param name="type">Type of the machine</param>
        /// <param name="friendlyName">Friendly machine name used for logging</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="e">Event</param>
        /// <returns>MachineId</returns> 
        async Task<MachineId> INetworkProvider.RemoteCreateMachineAsync(Type type, string friendlyName,
            string endpoint, Event e)
        {
            return await this.Channel.CreateMachineAsync(type.FullName, friendlyName, e);
        }

        /// <summary>
        /// Sends an event to a remote machine.
        /// </summary>
        /// <param name="target">Target machine id</param>
        /// <param name="e">Event</param>
        async Task INetworkProvider.RemoteSendEventAsync(MachineId target, Event e)
        {
            await this.Channel.SendEventAsync(target, e);
        }

        /// <summary>
        /// Returns the local endpoint.
        /// </summary>
        /// <returns>Endpoint</returns>
        string INetworkProvider.GetLocalEndPoint()
        {
            return this.IpAddress + ":" + this.Port;
        }

        #endregion

        #region remote communication methods

        /// <summary>
        /// Creates a new machine of the given type and with
        /// the given event. An optional friendly name can be
        /// specified. If the friendly name is null or the empty
        /// string, a default value will be given.
        /// </summary>
        /// <param name="typeName">Type of the machine</param>
        /// <param name="friendlyName">Friendly machine name used for logging</param>
        /// <param name="e">Event</param>
        /// <returns>MachineId</returns> 
        async Task<MachineId> IRemoteCommunication.CreateMachineAsync(string typeName, string friendlyName, Event e)
        {
            this.Runtime.Log("<RemoteLog> Received request to create remote machine of type {0}", typeName);
            var resolvedType = this.GetMachineType(typeName);
            return await this.Runtime.CreateMachineAsync(resolvedType, friendlyName, e);
        }

        /// <summary>
        /// Sends an asynchronous event to a machine.
        /// </summary>
        /// <param name="target">Target machine id</param>
        /// <param name="e">Event</param>
        async Task IRemoteCommunication.SendEventAsync(MachineId target, Event e)
        {
            this.Runtime.Log("<RemoteLog> Received remotely sent event {0}", e.GetType());
            await this.Runtime.SendEventAsync(target, e);
        }

        #endregion

        #region helper methods

        /// <summary>
        /// Gets the Type object of the machine with the specified type.
        /// </summary>
        /// <param name="typeName">TypeName</param>
        internal Type GetMachineType(string typeName)
        {
            Type machineType = this.ApplicationAssembly.GetType(typeName);
            this.Runtime.Assert(machineType != null, "Could not infer type of " + typeName + ".");
            this.Runtime.Assert(machineType.IsSubclassOf(typeof(Machine)), typeName +
                " is not a subclass of type Machine.");
            return machineType;
        }

        #endregion
    }
}
