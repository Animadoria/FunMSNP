using System;
using System.Linq;
using System.Net.Mail;
using System.Text;
using FunMSNP.Entities;
using FunMSNP.MSNP;
using Tcp.NET.Core.Events.Args;
using Tcp.NET.Server.Models;

namespace FunMSNP
{
    public static class Extensions
    {
        public static NSClient GetNSClient(this TcpMessageEventArgs<IConnectionServer> args)
        {
            return MSNPService.Instance.NSClients.Where(x => args.Connection.ConnectionId == x.Connection.ConnectionId).FirstOrDefault();
        }

        public static SBClient GetSBClient(this TcpMessageEventArgs<IConnectionServer> args)
        {
            return MSNPService.Instance.SBClients.Where(x => args.Connection.ConnectionId == x.Connection.ConnectionId).FirstOrDefault();
        }

        public static string ToMD5(this string input)
        {
            // Use input string to calculate MD5 hash
            using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Convert the byte array to hexadecimal string
            StringBuilder sb = new();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }

            return sb.ToString();

        }

        public static string ToHex(this string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hex = BitConverter.ToString(bytes);
            return hex.Replace("-", "");
        }

        public static bool IsEmail(this string input)
        {
            try
            {
                _ = new MailAddress(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string FirstLine(this string input)
        {
                return input.IndexOf("\r\n") > -1 ? input.Substring(0, input.IndexOf("\r\n")) : input;
        }
    }
}
