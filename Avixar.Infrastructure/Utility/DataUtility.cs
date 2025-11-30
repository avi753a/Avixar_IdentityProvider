using System;
using System.Collections.Generic;
using System.Text;

namespace Avixar.Infrastructure
{
    public static class DataUtility
    {
        
        public static string GetEncryptionKeys(string encKey, string blindKey)
        {
            string SetEncryptionKeys = "SET app.enc_key = '{0}'; SET app.blind_key = '{1}';";
            return string.Format(SetEncryptionKeys, encKey, blindKey);
        }
    }

}
