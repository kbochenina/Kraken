using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Common
{
    public static class InstallationUtility
    {
        public static string DownloadFileByPath(string path, string localPath, string username, string password)
        {
            WebClient client = new WebClient();

            client.Credentials = new NetworkCredential(username, password);

            client.DownloadFile(path, localPath);

            return localPath;
        }

        public static void DownloadFileByPathAsync(string path, string localAddress, string username, string password, Action<Exception> packageObtainedCallback)
        {
            WebClient client = new WebClient();
            client.Credentials = new NetworkCredential(username, password);

            Uri target = new Uri(path);
            client.DownloadFileCompleted += (sender, e) =>
            {
                Common.Utility.LogInfo(string.Format("End download package: {0}", path));
                packageObtainedCallback(e.Error);
            };
            client.DownloadFileAsync(target, localAddress);

            Common.Utility.LogInfo(string.Format("Start download package: {0}", path));
        }

        public static string HEADRequest(string path, string username, string password, string tagName)
        {
            try
            {
                System.Net.WebRequest request = System.Net.WebRequest.Create(path);
                request.Method = "HEAD";
                request.Credentials = new NetworkCredential(username, password);
                var response = request.GetResponse();

                return response.Headers[tagName];
            }
            catch (Exception ex)
            {
                Common.Utility.LogError(string.Format("Failed to make Head Request by path:{0}", path), ex);
                return null;
            }

        }

        public static bool GetCurrentRevisionNumber(string etag, out int revNumber)
        {
            if (!(string.IsNullOrEmpty(etag) || string.IsNullOrWhiteSpace(etag)))
            {
                try
                {
                    var firstIndex = etag.IndexOf('\"');
                    var lastIndex = etag.LastIndexOf('\"');

                    var svninfo = etag.Substring(firstIndex + 1, lastIndex - firstIndex);
                    var revnumber = svninfo.Substring(0, svninfo.IndexOf('/'));
                    return Int32.TryParse(revnumber, out revNumber);
                }
                catch (Exception ex)
                {
                    Common.Utility.LogError(string.Format("Failed to proccess etag: {0}", etag), ex);
                }
            }

            revNumber = -1;
            return false;
        }

        public static int GetCurrentRevisionNumber(string path, string username, string password)
        {
            var response = HEADRequest(path, username, password, "ETag");
            int revNumber = -1;
            GetCurrentRevisionNumber(response, out revNumber);
            return revNumber;
        }
    }
}
