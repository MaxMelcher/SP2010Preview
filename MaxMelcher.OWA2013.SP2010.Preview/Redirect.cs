using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration.Claims;
using Microsoft.SharePoint.IdentityModel;
using Microsoft.SharePoint.IdentityModel.OAuth2;
using Microsoft.SharePoint.Utilities;

namespace MaxMelcher.OWA2013.SP2010.Preview
{
    public class Redirect : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            var param = context.Request.Params;
            string src = param["src"];
            
            SPUser user = SPContext.Current.Web.CurrentUser;

            string login = null;
            SPClaimProviderManager mgr = SPClaimProviderManager.Local;
            if (mgr != null)
            {
                login = mgr.DecodeClaim(user.LoginName).Value;
            }

            if (src.StartsWith("file://"))
            {
                src = src.Replace("file://", "\\\\").Replace("/", "\\");
            }

            var hash = Helper.GetHash(src, login);

            //todo add a time to live to the hash and the redirect url

            const string urlPreviewFormat = "http://sharepoint2013:1234/_vti_bin/preview.ashx?src={0}&user={1}&hash={2}";
            string urlPreview = string.Format(urlPreviewFormat, HttpUtility.UrlEncode(src), HttpUtility.UrlEncode(login), hash);

            const string owaUrlFormat = "http://owa2013.demo.com/op/embed.aspx?src={0}&action=interactivepreview";
            string owaUrl = string.Format(owaUrlFormat, HttpUtility.UrlEncode(urlPreview));

            context.Response.Redirect(owaUrl);
        }

        

        public bool IsReusable { get { return false; } }
    }
}
