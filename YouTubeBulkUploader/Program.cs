using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using static Google.Apis.YouTube.v3.VideosResource;
using Newtonsoft.Json;

namespace YouTubeBulkUploader
{
    /// <summary>
    /// YouTube Data API v3 sample: search by keyword.
    /// Relies on the Google APIs Client Library for .NET, v1.7.0 or higher.
    /// See https://developers.google.com/api-client-library/dotnet/get_started
    ///
    /// Set ApiKey to the API key value from the APIs & auth > Registered apps tab of
    ///   https://cloud.google.com/console
    /// Please ensure that you have enabled the YouTube Data API for your project.
    /// </summary>
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("Der YouTube BuUp - YouTube Bulk Uploader");
            Console.WriteLine("========================================");

            YouTubeService ytService = AuthenticateWithYouTube();

            List<VideoDescription> descriptions = LoadVideoDescriptionsFromJson("VideosToUpload.json");

            foreach (VideoDescription videoDesc in descriptions)
            {
                try
                {
                    new UploadTask(ytService, videoDesc).Run().Wait();
                }
                catch (AggregateException ex)
                {
                    foreach (var e in ex.InnerExceptions)
                    {
                        Console.WriteLine("Error: " + e.Message);
                    }
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static YouTubeService AuthenticateWithYouTube()
        {
            Task<YouTubeService> authenticationTask = AuthenticateWithYouTubeAsync();
            authenticationTask.Wait();
            return authenticationTask.Result;
        }

        private static List<VideoDescription> LoadVideoDescriptionsFromJson(string filename)
        {
            string jsonFileContent = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject<List<VideoDescription>>(jsonFileContent);
        }

        private static async Task<YouTubeService> AuthenticateWithYouTubeAsync()
        {
            UserCredential credentials;

            using (FileStream fileStream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                   GoogleClientSecrets.Load(fileStream).Secrets,
                   new[] { YouTubeService.Scope.YoutubeUpload },
                   "user",
                   CancellationToken.None);
            }

            return new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credentials,
                GZipEnabled = true,
                ApplicationName = "YouTubeUploader"
            });
        }

    }
}
