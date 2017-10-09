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
                    UploadTask upload = new UploadTask(ytService, videoDesc);
                    upload.Run().Wait();

                    string messageToReceiver = CreateTextMessageForReceiver(videoDesc, upload);
                    PrintTextMessageToConsole(messageToReceiver);
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

        private static void PrintTextMessageToConsole(string messageToReceiver)
        {
            Console.WriteLine("Generierte Nachricht für den Empfänger der Videonachricht:");
            Console.WriteLine(messageToReceiver);
            Console.WriteLine();
        }

        private static string CreateTextMessageForReceiver(VideoDescription videoDesc, UploadTask upload)
        {
            string messageToReceiver = $"Hallo {videoDesc.Receiver}," + Environment.NewLine;
            messageToReceiver += Environment.NewLine;
            messageToReceiver += "super, dass du dich für meinen C# Kurs entschieden hast. In folgender persönlicher Videonachricht erkläre ich dir, wie du den Kurs für dich noch individueller nutzen kannst:";
            messageToReceiver += Environment.NewLine;
            messageToReceiver += upload.UrlOfUploadedVideo + Environment.NewLine;
            messageToReceiver += Environment.NewLine;
            messageToReceiver += "Viel Spaß und vor allem viel Erfolg mit dem Kurs" + Environment.NewLine;
            messageToReceiver += Environment.NewLine;
            messageToReceiver += "Jan von LernMoment.de";
            return messageToReceiver;
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
