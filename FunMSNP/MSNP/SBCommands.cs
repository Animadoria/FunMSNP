using System;
using System.Threading.Tasks;
using FunMSNP.Entities;
using Humanizer;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace FunMSNP.MSNP
{
    public partial class MSNPService
    {
        private async Task HandleSBCommand(string[] contents, SBClient client)
        {
            using Task command = (Task)GetType()
                .GetMethod($"HandleSB{contents[0].Transform(To.LowerCase, To.SentenceCase)}")
                .Invoke(this, new object[] { contents, client });

            await command;
        }

        public async Task RequireSBAuthentication(string[] contents, SBClient client)
        {
            if (client.NSClient == null)
                await client.SendAsync("715 " + contents[1], true);
        }

        public async Task HandleSBUsr(string[] contents, SBClient client)
        {
            var list = NSClients.Where(x => x.UserHandle == contents[2]).Select(nsc => nsc.SBAuths).FirstOrDefault();
            if (list == null || !list.Contains(contents[3]))
            {
                await client.SendAsync($"911 {contents[1]}", true);
                return;
            }

            var nsc = NSClients.Where(x => x.UserHandle == contents[2]).FirstOrDefault();
            nsc.SBAuths.Remove(contents[3]);
            client.NSClient = nsc;
            client.AuthString = contents[3];
            await client.SendAsync($"USR {contents[1]} OK {nsc.UserHandle} {(await nsc.Database.Users.FindAsync(nsc.UserID)).SafeNickname}");
        }

        public async Task HandleSBCal(string[] contents, SBClient client)
        {
            await RequireSBAuthentication(contents, client);
            if (!contents[2].IsEmail())
            {
                await client.SendAsync($"208 {contents[1]}", true);
                return;
            }
            var user = await client.NSClient.Database.Users.FindAsync(client.NSClient.UserID);
            var match = NSClients.Where(x => x.UserHandle == contents[2]).FirstOrDefault();

            if (match == null || match.Presence == Presence.Hidden)
            {
                await client.SendAsync($"217 {contents[1]}", true);
                return;
            }

            var matchUser = await client.NSClient.Database.Users.FindAsync(match.UserID);

            Session session = client.Session ?? new Session
            {
                ID = (uint)RandomNumberGenerator.GetInt32(10000000),
            };

            if (session.Clients.Contains(client))
            {
                await client.SendAsync($"215 {contents[1]}", true);
                return;
            }


            if ((matchUser.MessagePrivacy && userManager.AreUsersInGroup(ContactList.BlockList, user, matchUser))
               || (!matchUser.MessagePrivacy && userManager.AreUsersInGroup(ContactList.AllowList, user, matchUser)))
            {
                await client.SendAsync($"216 {contents[1]}", true);
                return;
            }


            session.Clients.Add(client);
            client.Session = session;
            Sessions.Add(session);

            var authString = $"{(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds}.{RandomNumberGenerator.GetInt32(999999)}";
            match.SBAuths.Add(authString);

            // AL and is on block list
            // OR
            // BL and it isn't on Allow List.
            if ((matchUser.MessagePrivacy && userManager.AreUsersInGroup(ContactList.BlockList, user, matchUser))
                || (!matchUser.MessagePrivacy && userManager.AreUsersInGroup(ContactList.AllowList, user, matchUser)))
            {
                await client.SendAsync($"216 {contents[1]}", true);
                return;
            }


            await match.SendAsync($"RNG {session.ID} {configuration.GetValue<string>("Switchboard")} CKI {authString} {matchUser.Email} {matchUser.SafeNickname}");

            await client.SendAsync($"CAL {contents[1]} RINGING 0");
        }

        public async Task HandleSBAns(string[] contents, SBClient client)
        {
            var list = NSClients.Where(x => x.UserHandle == contents[2]).Select(nsc => nsc.SBAuths).FirstOrDefault();
            if (list == null || !list.Contains(contents[3]))
            {
                await client.SendAsync($"911 {contents[1]}", true);
                return;
            }

            var session = Sessions.Where(x => x.ID.ToString() == contents[4]).FirstOrDefault();
            if (session == null)
            {
                client.Connection.Client.Close();
                return;
            }

            var nsc = NSClients.Where(x => x.UserHandle == contents[2]).FirstOrDefault();
            nsc.SBAuths.Remove(contents[3]);
            client.NSClient = nsc;
            client.AuthString = contents[3];
            session.Clients.Add(client);
            client.Session = session;

            for (int i = 0; i < session.Clients.Count; i++)
            {
                var participant = session.Clients[i];
                if (participant == client) continue;
                var partUser = await client.NSClient.Database.Users.FindAsync(participant.NSClient.UserID);
                await client.SendAsync($"IRO {contents[1]} {i + 1} {session.Clients.Count - 1} {partUser.Email} {partUser.SafeNickname}");
            }

            await client.SendAsync($"ANS {contents[1]} OK");

            var usr = await client.NSClient.Database.Users.FindAsync(nsc.UserID);
            foreach (var cl in session.Clients.Where(x => x != client))
            {
                await cl.SendAsync($"JOI {usr.Email} {usr.SafeNickname}");
            }
        }

        public async Task HandleSBMsg(string[] contents, SBClient client)
        {
            await RequireSBAuthentication(contents, client);
            if (client.Session == null)
            {
                client.Connection.Client.Close();
                return;
            }

            var usr = await client.NSClient.Database.Users.FindAsync(client.NSClient.UserID);

            foreach (var cl in client.Session.Clients.Where(x => x != client))
            {
                await cl.SendAsync($"MSG {usr.Email} {usr.SafeNickname} {contents[3].Substring(0, contents[3].IndexOf("\r\n"))}" +
                    $"\r\n{string.Join("\r\n", string.Join(' ', contents).Split("\r\n").Skip(1))}");
            }
        }

        public async Task HandleSBOut(string[] contents, SBClient client)
        {
            await RequireSBAuthentication(contents, client);
            if (client.Session == null)
            {
                client.Connection.Client.Close();
                return;
            }

            client.Session.Clients.Remove(client);

            foreach (var x in client.Session.Clients)
            {
                await x.SendAsync($"BYE {x.NSClient.UserHandle}");
            }

            if (!client.Session.Clients.Any())
            {
                Sessions.RemoveAll(x => x.ID == client.Session.ID);
            }

            client.Connection.Client.Close();
        }
    }
}
