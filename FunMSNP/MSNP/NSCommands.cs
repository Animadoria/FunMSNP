using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FunMSNP.Entities;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FunMSNP.MSNP
{
    public partial class MSNPService
    {
        private async Task HandleNSCommand(string[] contents, NSClient client)
        {
            try
            {
                using Task command = (Task)GetType()
                    .GetMethod($"HandleNS{contents[0].Transform(To.LowerCase, To.SentenceCase)}")
                    .Invoke(this, new object[] { contents, client });

                await command;
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "NS/");
            }
        }

        public async Task RequireNSAuthentication(string[] contents, NSClient client)
        {
            if (client.Protocol == null || await client.Database.Users.FindAsync(client.UserID) == null)
                await client.SendAsync("715 " + contents[1], true);
        }

        public async Task HandleNSVer(string[] contents, NSClient client)
        {
            if (client.Protocol != null)
            {
                await client.SendAsync("715 " + contents[1], true);
            }

            var match = contents.Skip(2)
                .Where(x => Enum.IsDefined(typeof(Protocol), x))
                .OrderByDescending(x => x)
                .Select(x => (Protocol?)Enum.Parse(typeof(Protocol), x))
                .FirstOrDefault();

            if (match == null)
            {
                await client.SendAsync($"VER {contents[1]} 0", true);
            }

            client.Protocol = match;

            await client.SendAsync("VER " + contents[1] + " " + match);
        }

        public async Task HandleNSInf(string[] contents, NSClient client)
        {
            if (client.Protocol == null)
                await client.SendAsync($"715 {contents[1]}", true);

            await client.SendAsync($"INF {contents[1]} MD5");
        }

        public async Task HandleNSCvq(string[] contents, NSClient client) => await HandleNSCvr(contents, client);

        public async Task HandleNSCvr(string[] contents, NSClient client)
        {
            await client.SendAsync($"{contents[0]} {contents[1]} 1.0.0863 1.0.0863 1.0.0863 http://messenger.hotmail.com/mmsetup.exe http://messenger.hotmail.com");
        }

        public async Task HandleNSUsr(string[] contents, NSClient client)
        {
            if (client.Protocol == null)
                await client.SendAsync($"715 {contents[1]}", true);

            if (contents[2] != "MD5")
            {
                await client.SendAsync($"911 {contents[1]}", true);
            }

            switch (contents[3])
            {
                case "I":
                    client.UserHandle = contents[4];
                    client.AuthEpoch = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                    await client.SendAsync($"USR {contents[1]} MD5 S {client.AuthEpoch}");
                    break;

                case "S":
                    var user = await client.Database.Users.Where(x => x.Email == client.UserHandle).FirstOrDefaultAsync();

                    if (user == null)
                        await client.SendAsync("911 " + contents[1], true);

                    var code = (client.AuthEpoch + userManager.GetPassword(user)).ToMD5().ToLower();
                    if (code != contents[4])
                        await client.SendAsync("911 " + contents[1], true);

                    var con = NSClients.Where(x => client.Database.Users.Find(x.UserID) != null).Where(x => client.Database.Users.Find(x.UserID).Email == client.UserHandle);
                    foreach (var u in con)
                    {
                        await u.SendAsync("OUT OTH", true);
                    }

                    client.UserID = user.ID;

                    await client.SendAsync($"USR {contents[1]} OK {client.UserHandle} {user.SafeNickname}");
                    break;
                default:
                    await client.SendAsync("911 " + contents[1], true);
                    break;
            }
        }

        public async Task HandleNSSyn(string[] contents, NSClient client)
        {
            await RequireNSAuthentication(contents, client);
            if (!uint.TryParse(contents[2], out var count))
                await client.SendAsync("715 " + contents[1], true);

            var user = client.Database.Users.Where(x => x.ID == client.UserID).First();

            await client.SendAsync("SYN " + contents[1] + " " + user.SyncID);

            var fl = client.Database.Contacts.Where(x => x.ContactList == ContactList.ForwardList && x.User == client.UserID).ToArray();

            if (count != user.SyncID)
            {
                await client.SendAsync($"GTC {contents[1]} {user.SyncID} {(user.Notify ? "A" : "N")}");
                await client.SendAsync($"BLP {contents[1]} {user.SyncID} {(user.MessagePrivacy ? "AL" : "BL")}");

                await SendList(contents, user, client, ContactList.ForwardList);
                await SendList(contents, user, client, ContactList.AllowList);
                await SendList(contents, user, client, ContactList.BlockList);
                await SendList(contents, user, client, ContactList.ReverseList);
            }

            foreach (var forward in fl)
            {
                var u = client.Database.Users.Find(forward.Target);
                var uc = NSClients.Find(x => x.UserID == u.ID);
                var presence = uc != null ? ToPresenceAcronym(uc.Presence) : "FLN";
                await client.SendAsync($"ILN {contents[1]} {presence} {u.Email} {u.SafeNickname}");
            }
        }

        public async Task HandleNSChg(string[] contents, NSClient client)
        {
            await RequireNSAuthentication(contents, client);
            var presence = FromPresenceAcronym(contents[2]);

            if (presence == null)
            {
                client.Connection.Client.Close();
            }

            client.Presence = presence;

            await client.SendAsync("CHG " + contents[1] + " " + contents[2]);

            foreach (var x in client.Database.Contacts.Where(x => x.User == client.UserID && x.ContactList == ContactList.ReverseList))
            {
                var target = NSClients.Find(y => y.UserID == x.Target);
                if (target != null)
                {
                    var user = await client.Database.Users.FindAsync(x.User);
                    var uc = NSClients.Find(x => x.UserID == user.ID);
                    if (uc.Presence == Presence.Hidden)
                        await target.SendAsync($"FLN {user.Email}");

                    else await target.SendAsync($"NLN {ToPresenceAcronym(uc.Presence)} {user.Email} {user.SafeNickname}");
                }
            }
        }

        public async Task HandleNSAdd(string[] contents, NSClient client)
        {
            await RequireNSAuthentication(contents, client);

            if (!contents[3].IsEmail())
            {
                await client.SendAsync("201 " + contents[1]);
                return;
            }

            var match = await client.Database.Users.Where(x => x.Email == contents[3]).FirstOrDefaultAsync();
            var current = await client.Database.Users.Where(x => x.ID == client.UserID).FirstAsync();

            if (match == null)
            {
                await client.SendAsync("205 " + contents[1]);
                return;
            }

            switch (contents[2])
            {
                case "RL":
                    client.Connection.Client.Close();
                    break;
                case "FL":
                    if (userManager.AreUsersInGroup(ContactList.ForwardList, current, match))
                    {
                        await client.SendAsync("215 " + contents[1]);
                        return;
                    }

                    client.Database.Contacts.Add(new Contact
                    {
                        ContactList = ContactList.ForwardList,
                        User = current.ID,
                        Target = match.ID
                    });

                    client.Database.Contacts.Add(new Contact
                    {
                        ContactList = ContactList.ReverseList,
                        User = match.ID,
                        Target = current.ID
                    });

                    match.SyncID++;
                    current.SyncID++;

                    break;
                case "AL":
                    if (userManager.AreUsersInGroup(ContactList.AllowList, current, match))
                    {
                        await client.SendAsync("215 " + contents[1]);
                        return;
                    }

                    if (userManager.AreUsersInGroup(ContactList.BlockList, current, match))
                    {
                        await client.SendAsync("219 " + contents[1]);
                        return;
                    }

                    client.Database.Contacts.Add(new Contact
                    {
                        ContactList = ContactList.AllowList,
                        User = current.ID,
                        Target = match.ID
                    });

                    current.SyncID++;

                    break;

                case "BL":
                    if (userManager.AreUsersInGroup(ContactList.BlockList, current, match))
                    {
                        await client.SendAsync("215 " + contents[1]);
                        return;
                    }

                    if (userManager.AreUsersInGroup(ContactList.AllowList, current, match))
                    {
                        await client.SendAsync("219 " + contents[1]);
                        return;
                    }

                    client.Database.Contacts.Add(new Contact
                    {
                        ContactList = ContactList.BlockList,
                        User = current.ID,
                        Target = match.ID
                    });
                    current.SyncID++;

                    break;
                default:
                    await client.SendAsync("224 " + contents[1]);
                    break;
            }
            await client.Database.SaveChangesAsync();
            await client.SendAsync($"ADD {contents[1]} {contents[2]} {current.SyncID} {contents[3]} {contents[4]}");

            var clientMatch = NSClients.Where(x => x.UserID == match.ID).FirstOrDefault();

            if (clientMatch != null && contents[2] == "AL")
                await clientMatch.SendAsync($"ADD 0 RL {match.SyncID} {current.Email} {current.SafeNickname}");

            var status = clientMatch != null ? ToPresenceAcronym(clientMatch.Presence) : "FLN";
            await client.SendAsync($"ILN {contents[1]} {status} {contents[3]} {match.SafeNickname}");
        }

        public async Task HandleNSFnd(string[] contents, NSClient client)
        {
            await RequireNSAuthentication(contents, client);

            await client.SendAsync("FND " + contents[1] + " 0 0");
        }

        public async Task HandleNSGtc(string[] contents, NSClient client)
        {
            await RequireNSAuthentication(contents, client);

            if (contents[2] is not ("A" or "N"))
                client.Connection.Client.Close();

            var current = await client.Database.Users.Where(x => x.ID == client.UserID).FirstAsync();

            current.Notify = contents[2] == "A";
            await client.Database.SaveChangesAsync();

            await client.SendAsync($"GTC {contents[1]} {contents[2]}");
        }

        public async Task HandleNSBlp(string[] contents, NSClient client)
        {
            await RequireNSAuthentication(contents, client);

            if (contents[2] is not ("AL" or "BL"))
                client.Connection.Client.Close();

            var current = await client.Database.Users.Where(x => x.ID == client.UserID).FirstAsync();

            current.MessagePrivacy = contents[2] == "AL";
            await client.Database.SaveChangesAsync();

            await client.SendAsync($"BLP {contents[1]} {contents[2]}");
        }

        public async Task HandleNSSnd(string[] contents, NSClient client)
        {
            await RequireNSAuthentication(contents, client);
            await client.SendAsync($"SND {contents[1]} OK");
        }

        public async Task HandleNSRem(string[] contents, NSClient client)
        {
            await RequireNSAuthentication(contents, client);


            var match = client.Database.Users.Where(x => x.Email == contents[3]).FirstOrDefault();

            if (match == null)
            {
                await client.SendAsync("205 " + contents[1]);
                return;
            }

            if (contents[2] is not ("AL" or "BL" or "FL"))
            {
                await client.SendAsync("224 " + contents[1]);
                return;
            }

            var contact = client.Database.Contacts.Where(x => x.User == client.UserID
                                              && x.Target == match.ID
                                              && x.ContactList == FromContactAcronym(contents[2]));
            if (!contact.Any())
            {
                await client.SendAsync("225 " + contents[1]);
                return;
            }

            client.Database.Contacts.Remove(await contact.FirstAsync());
            var user = await client.Database.Users.FindAsync(client.UserID);
            user.SyncID++;
            await client.Database.SaveChangesAsync();

            await client.SendAsync($"REM {contents[1]} {contents[2]} {user.SyncID} {user.Email}");
        }

        public async Task HandleNSRea(string[] contents, NSClient client)
        {
            await RequireNSAuthentication(contents, client);
            var user = await client.Database.Users.FindAsync(client.UserID);

            if (contents[2] != client.UserHandle)
            {
                var target = client.Database.Users.Where(x => x.Email == contents[2]).FirstOrDefault();
                if (!userManager.AreUsersInGroup(ContactList.ForwardList, user, target))
                {
                    await client.SendAsync($"216 {contents[1]}", true);
                    return;
                }
                await client.SendAsync($"REA {contents[1]} {user.SyncID} {target.Email} {target.SafeNickname}");
                return;
            }

            user.Nickname = contents[3];
            user.SyncID++;
            await client.Database.SaveChangesAsync();

            await client.SendAsync($"REA {contents[1]} {user.SyncID} {user.Email} {user.Nickname}");
        }

        public async Task HandleNSOut(string[] contents, NSClient client)
        {
            foreach (var target in from x in client.Database.Contacts.AsEnumerable().Where(x => x.User == client.UserID && x.ContactList == ContactList.ReverseList)
                                   let target = NSClients.Find(y => y.UserID == x.Target)
                                   where target != null
                                   where target != client
                                   select target)
            {
                await target.SendAsync($"FLN {client.UserHandle}");
            }

            client.Connection.Client.Close();
        }

        public async Task HandleNSUrl(string[] contents, NSClient client)
        {
            await client.SendAsync($"URL {contents[1]} /cgi-bin/HoTMaiL https://loginnet.passport.com/ppsecure/md5auth.srf?lc=1033 2");
        }

        public async Task HandleNSXfr(string[] contents, NSClient client)
        {
            var authString = $"{(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds}.{RandomNumberGenerator.GetInt32(999999)}";

            client.SBAuths.Add(authString);
            await client.SendAsync($"XFR {contents[1]} SB {configuration.GetValue<string>("Switchboard")} CKI {authString}");
        }

        public static async Task SendList(string[] contents, User user, NSClient client, ContactList list)
        {
            var listContacts = client.Database.Contacts.Where(x => x.ContactList == list && x.User == client.UserID).ToArray();

            for (int i = 0; i < listContacts.Length; i++)
            {
                User u = client.Database.Users.Find(listContacts[i].Target);
                await client.SendAsync($"LST {contents[1]} {ToContactAcronym(list)} {user.SyncID} {i + 1} {listContacts.Length} {u.Email} {u.SafeNickname}");
            }

            if (!listContacts.Any())
                await client.SendAsync($"LST {contents[1]} {ToContactAcronym(list)} {user.SyncID} 0 0");
        }

    }
}