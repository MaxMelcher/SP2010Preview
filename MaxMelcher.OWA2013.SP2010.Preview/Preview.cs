using System;
using System.IO;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Web;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Client;
using File = System.IO.File;
using SPFile = Microsoft.SharePoint.Client.File;
using System.Threading;

namespace MaxMelcher.OWA2013.SP2010.Preview
{
    public class Preview : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {

            try
            {
                var param = context.Request.Params;
                string url = HttpUtility.UrlDecode(param["src"]);
                string login = HttpUtility.UrlDecode(param["user"]);
                string hashInput = param["hash"];

                string hashOutput = Helper.GetHash(url, login);

                //todo add time to live
                if (hashInput != hashOutput)
                {
                    throw new UnauthorizedAccessException("No access");
                }

                if (url.StartsWith("http"))
                {
                    GetFileOfSP2010(context, login, url);
                }
                else if (url.StartsWith("\\\\"))
                {
                    GetFileOfFileShare(context, login, url);
                }

            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("No access");
            }

        }

        private void GetFileOfFileShare(HttpContext context, string login, string url)
        {
            SPSecurity.RunWithElevatedPrivileges(() =>
            {
                FileInfo file = new FileInfo(url);

                //var aclRead = file.GetAccessControl(AccessControlSections.Access);
                //CanRead(princ.Identity, file.FullName);

                FileStream s = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); 
                context.Response.AddHeader("Content-Disposition", "attachment;filename=" + HttpUtility.UrlEncode(file.Name, Encoding.UTF8));
                context.Response.BinaryWrite(ReadFully(s));
            });
        }

        private bool CanRead(WindowsIdentity user, string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                var fileSecurity = File.GetAccessControl(filePath, AccessControlSections.Access);
                foreach (FileSystemAccessRule fsRule in fileSecurity.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier)))
                {
                    foreach (var usrGroup in user.Groups)
                    {
                        if (fsRule.IdentityReference.Value == usrGroup.Value)
                            return true;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                //File is in use
                return false;
            }

            return false;
        }

        private void GetFileOfSP2010(HttpContext context, string login, string url)
        {
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

                FileInformation fileInformation = SPFile.OpenBinaryDirect(clientContext, (string) listitem["FileRef"]);

                context.Response.AddHeader("Content-Disposition", "attachment;filename=" + HttpUtility.UrlEncode(file.Name, Encoding.UTF8));
                HttpContext.Current.Response.BinaryWrite(ReadFully(fileInformation.Stream));
            }
            ;
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
