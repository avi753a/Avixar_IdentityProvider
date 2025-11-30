using System;
using System.Collections.Generic;
using System.Text;

namespace Avixar.Entity.Entities
{
    public class Client
    {
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string ClientSecret { get; set; } // Hashed in real world
        public string[] AllowedRedirectUris { get; set; }
        public string[] AllowedLogoutUris { get; set; }
    }
}
