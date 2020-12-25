// File User.cs created by Animadoria (me@animadoria.cf) at 12/10/2020 9:51 PM.
// (C) Animadoria 2020 - All Rights Reserved
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FunMSNP.MSNP;
using Microsoft.Extensions.Logging;
using Tcp.NET.Core.Models;
using Tcp.NET.Server.Models;

namespace FunMSNP.Entities
{
    public class NSClient
    {
        public Guid InstanceGuid = Guid.NewGuid();
        public IConnectionServer Connection;
        public Protocol? Protocol = null;

        public MSNPContext Database;

        public string UserHandle { get; internal set; }
        public double AuthEpoch { get; internal set; }
        public List<string> SBAuths { get; internal set; } = new();

        public Presence? Presence = null;

        public uint? UserID { get; set; } = null;

        public async Task SendAsync(string message, bool close = false)
        {
            MSNPService.Instance.Logger.LogDebug("NS/{guid} << {message}", InstanceGuid.ToString()[..4], message.FirstLine());
            await MSNPService.Instance.NotificationServer.SendToConnectionRawAsync(message, Connection);
            if (close)
                Connection.Client.Close();
        }
    }
}
