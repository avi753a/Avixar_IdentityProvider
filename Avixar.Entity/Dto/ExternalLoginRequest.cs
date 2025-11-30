using System;
using System.Collections.Generic;
using System.Text;

namespace Avixar.Entity
{
    public class ExternalLoginRequest
    {
        public string client_id { get; set; }
        public string redirect_uri { get; set; }
        public string response_type { get; set; }
        public string? state  { get; set; }
        public string? nonce  { get; set; }
    }
}
