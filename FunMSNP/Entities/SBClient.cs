using System;
using System.Threading.Tasks;
using FunMSNP.MSNP;
using Microsoft.Extensions.Logging;
using Tcp.NET.Server.Models;

namespace FunMSNP.Entities
{
    public class SBClient
    {
        public NSClient NSClient;
        public Guid InstanceGuid = Guid.NewGuid();
        public IConnectionServer Connection;

        public string AuthString;
        public Session Session;

        public async Task SendAsync(string message, bool close = false)
        {
            MSNPService.Instance.Logger.LogDebug("SB/{guid} << {message}", InstanceGuid.ToString()[..4], message.FirstLine());
            await MSNPService.Instance.SwitchboardServer.SendToConnectionRawAsync(message, Connection);
            if (close)
                Connection.Client.Close();
        }
    }
}
