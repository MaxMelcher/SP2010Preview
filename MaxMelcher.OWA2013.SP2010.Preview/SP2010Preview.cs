using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using Microsoft.SharePoint.Client;
using File = Microsoft.SharePoint.Client.File;

namespace MaxMelcher.OWA2013.SP2010.Preview
{
    public class SP2010Preview : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {

            try
            {
                var param = context.Request.Params;
                string url = param["src"];
                string login = HttpUtility.UrlDecode(param["user"]);
                string hashInput = param["hash"];

                string hashOutput = Helper.GetHash(url, login);

                //todo add time to live
                if (hashInput != hashOutput)
                {
                    throw new UnauthorizedAccessException("No access");
                }


                //todo get the valid web url from the url - passing in the whole url breaks the client context 
                using (var clientContext = new ClientContext("http://sp2010"))
                {

                    //grant a user full read on the SP2010 server so that we can fetch the document on the SP2013 server an relay it to SP2013
                    clientContext.Credentials = new NetworkCredential("mmelcher", "pass@word1", "demo");
                    var web = clientContext.Web;
                    clientContext.Load(web);
                    clientContext.ExecuteQuery();

                    User user = web.EnsureUser(login);
                    clientContext.Load(user);
                    clientContext.ExecuteQuery();

                    Uri uri = new Uri(url);
                    string server;

                    if (uri.IsDefaultPort)
                    {
                        server = uri.Scheme + "://" + uri.Host;
                    }
                    else
                    {
                        server = uri.Scheme + "://" + uri.Host + ":" + uri.Port;
                    }

                    string relative = url.Substring(server.Length, url.Length - server.Length);

                    var file = web.GetFileByServerRelativeUrl(relative);
                    clientContext.Load(file, f => f.ListItemAllFields, f => f.Name);
                    clientContext.ExecuteQuery();

                    var listitem = file.ListItemAllFields;
                    clientContext.Load(listitem, f => f.RoleAssignments.Include(r => r.RoleDefinitionBindings, r => r.Member));
                    clientContext.ExecuteQuery();


                    //TODO check user permissions on the sp2010 server - this can not be done in CSOM as of now
                    //RoleAssignmentCollection roleAssignments = listitem.RoleAssignments;
                    
                    //clientContext.ExecuteQuery();
                    bool hasPermissions = true;
                    //foreach (var roleAssignment in roleAssignments)
                    //{
                    //    foreach (RoleDefinition role in roleAssignment.RoleDefinitionBindings)
                    //    {
                    //        clientContext.Load(role);
                    //        clientContext.ExecuteQuery();
                    //        hasPermissions = role.BasePermissions.Has(PermissionKind.Open);

                    //        if (hasPermissions) break;
                    //    }
                    //}


                    if (!hasPermissions)
                    {
                        throw new UnauthorizedAccessException("No access");
                    }

                    FileInformation fileInformation = File.OpenBinaryDirect(clientContext, (string)listitem["FileRef"]);

                    context.Response.AddHeader("Content-Disposition", "attachment;filename=" + HttpUtility.UrlEncode(file.Name, Encoding.UTF8));
                    HttpContext.Current.Response.BinaryWrite(ReadFully(fileInformation.Stream));
                };
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("No access");
            }

        }

        public byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public bool IsReusable { get { return false; } }
    }
}
