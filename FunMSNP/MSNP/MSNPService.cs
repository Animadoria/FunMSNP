using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FunMSNP.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tcp.NET.Server;
using Tcp.NET.Server.Events.Args;
using Tcp.NET.Server.Models;

namespace FunMSNP.MSNP
{
    public partial class MSNPService : IHostedService
    {
        public static MSNPService Instance;

        public TcpNETServer NotificationServer;
        public TcpNETServer SwitchboardServer;

        public List<NSClient> NSClients = new();
        public List<SBClient> SBClients = new();
        public List<Session> Sessions = new();

        public readonly ILogger Logger;
        private readonly UserManager userManager;
        private readonly IServiceProvider sp;
        private readonly IConfiguration configuration;

        public MSNPService(ILogger<MSNPService> logger, UserManager userMgr, IServiceProvider serviceProvider, IConfiguration config)
        {
            Instance = this;
            Logger = logger;
            userManager = userMgr;
            sp = serviceProvider;
            configuration = config;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Starting MSNP Service...");
            NotificationServer = new TcpNETServer(new ParamsTcpServer
            {
                Port = 1863,
                EndOfLineCharacters = "\r\n"
            });

            SwitchboardServer = new TcpNETServer(new ParamsTcpServer
            {
                Port = 1864,
                EndOfLineCharacters = "\r\n"
            });

            NotificationServer.ServerEvent += (sender, args) =>
            {
                Logger.LogInformation($"Started Notification Server. ({NotificationServer.Server.LocalEndpoint})");
                return Task.CompletedTask;
            };

            SwitchboardServer.ServerEvent += (sender, args) =>
            {
                Logger.LogInformation($"Started Switchboard Server. ({SwitchboardServer.Server.LocalEndpoint})");
                return Task.CompletedTask;
            };

            NotificationServer.MessageEvent += NS_MessageEvent;
            NotificationServer.ConnectionEvent += NS_ConnectionEvent;

            SwitchboardServer.MessageEvent += SB_MessageEvent;
            SwitchboardServer.ConnectionEvent += SB_ConnectionEvent;

            await NotificationServer.StartAsync();
            await SwitchboardServer.StartAsync();

            await Task.Delay(-1, cancellationToken);
        }


        async Task NS_ConnectionEvent(object sender, TcpConnectionServerEventArgs args)
        {
            MSNPContext db = (MSNPContext)sp.GetService(typeof(MSNPContext));
            switch (args.ConnectionEventType)
            {
                case PHS.Networking.Enums.ConnectionEventType.Connected:

                    var scope = sp.CreateScope();

                    var services = scope.ServiceProvider;
                    var dbContext = services.GetRequiredService<MSNPContext>();

                    var client = new NSClient
                    {
                        Connection = args.Connection,
                        Database = dbContext
                    };

                    Logger.LogDebug("NS/{guid} connected", client.InstanceGuid.ToString()[..4]);
                    NSClients.Add(client);
                    

                    break;

                case PHS.Networking.Enums.ConnectionEventType.Disconnect:
                    var cl = NSClients.Where(x => x.Connection.ConnectionId == args.Connection.ConnectionId).FirstOrDefault();

                    if (cl != null)
                    {
                        if (cl.UserID != null)
                            foreach (var x in db.Contacts.Where(x => x.User == cl.UserID && x.ContactList == ContactList.ReverseList))
                            {
                                var target = NSClients.Find(y => y.UserID == x.Target);
                                if (target != null)
                                    await target.SendAsync($"FLN {target.UserHandle}");

                            }

                        Logger.LogDebug("NS/{guid} disconnected", cl.InstanceGuid.ToString()[..4]);
                        NSClients.Remove(cl);
                    }

                    break;
                default:
                    break;
            }
        }

        Task SB_ConnectionEvent(object sender, TcpConnectionServerEventArgs args)
        {
            switch (args.ConnectionEventType)
            {
                case PHS.Networking.Enums.ConnectionEventType.Connected:
                    var client = new SBClient
                    {
                        Connection = args.Connection
                    };

                    Logger.LogDebug("SB/{guid} connected", client.InstanceGuid.ToString()[..4]);
                    SBClients.Add(client);

                    break;

                case PHS.Networking.Enums.ConnectionEventType.Disconnect:
                    var cl = SBClients.Where(x => x.Connection.ConnectionId == args.Connection.ConnectionId).FirstOrDefault();

                    if (cl != null)
                    {
                        Logger.LogDebug("SB/{guid} disconnected", cl.InstanceGuid.ToString()[..4]);
                        SBClients.Remove(cl);
                    }

                    break;
                default:
                    break;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Stopping...");
            return Task.CompletedTask;
        }

        async Task NS_MessageEvent(object sender, TcpMessageServerEventArgs args)
        {
            if (args.MessageEventType == PHS.Networking.Enums.MessageEventType.Sent) return;
            var client = args.GetNSClient();

            Logger.LogDebug("NS/{guid} >> {message}", client.InstanceGuid.ToString()[..4], args.Message.FirstLine());
            var contents = args.Message.Split(' ');

            try
            {
                await HandleNSCommand(contents, client);
            }
            catch (Exception)
            {
                client.Connection.Client.Close();
            }
        }

        async Task SB_MessageEvent(object sender, TcpMessageServerEventArgs args)
        {
            if (args.MessageEventType == PHS.Networking.Enums.MessageEventType.Sent) return;
            var client = args.GetSBClient();

            Logger.LogDebug("SB/{guid} >> {message}", client.InstanceGuid.ToString()[..4], args.Message.FirstLine());
            var contents = args.Message.Split(' ');

            try
            {
                await HandleSBCommand(contents, client);
            }
            catch (Exception)
            {
                client.Connection.Client.Close();
            }
        }

        // Static stuff
        public static ContactList? FromContactAcronym(string value) => value switch
        {
            "AL" => ContactList.AllowList,
            "BL" => ContactList.BlockList,
            "FL" => ContactList.ForwardList,
            "RL" => ContactList.ReverseList,
            _ => default,
        };

        public static string ToContactAcronym(ContactList value) => value switch
        {
            ContactList.AllowList => "AL",
            ContactList.BlockList => "BL",
            ContactList.ReverseList => "RL",
            ContactList.ForwardList => "FL",
            _ => default,
        };

        public static string ToPresenceAcronym(Presence? value) => value switch
        {
            Presence.Online => "NLN",
            Presence.Busy => "BSY",
            Presence.Idle => "IDL",
            Presence.BeRightBack => "BRB",
            Presence.Away => "AWY",
            Presence.OnThePhone => "PHN",
            Presence.OutToLunch => "LUN",
            Presence.Hidden => "HDN",
            null => "FLN",
            _ => "FLN"
        };

        public static Presence? FromPresenceAcronym(string value) => value switch
        {
            "NLN" => Presence.Online,
            "BSY" => Presence.Busy,
            "IDL" => Presence.Idle,
            "BRB" => Presence.BeRightBack,
            "AWY" => Presence.Away,
            "PHN" => Presence.OnThePhone,
            "LUN" => Presence.OutToLunch,
            "HDN" => Presence.Hidden,
            _ => default,
        };

    }
}
