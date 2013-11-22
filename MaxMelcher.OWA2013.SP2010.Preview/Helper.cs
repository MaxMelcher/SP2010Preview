using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SharePoint;

namespace MaxMelcher.OWA2013.SP2010.Preview
{
    class Helper
    {
        public static string GetHash(string src, string userLogin)
        {
            const string salt = "Max Melcher SP2010 Preview Awesomeness";
            SHA256Managed hashstring = new SHA256Managed();
            var crypto = hashstring.ComputeHash(Encoding.UTF8.GetBytes(src + salt + userLogin));
            string hash = crypto.Aggregate(string.Empty, (current, bit) => current + bit.ToString("x2"));
            return hash;
        }
    }
}
