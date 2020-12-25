using System;
using System.Collections.Generic;

namespace FunMSNP.Entities
{
    public class Session
    {
        public uint ID;
        public List<SBClient> Clients = new List<SBClient>();
    }
}
