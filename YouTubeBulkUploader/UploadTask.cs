using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Google.Apis.YouTube.v3.VideosResource;

namespace YouTubeBulkUploader
{
    class UploadTask
    {
        private string fileName;
        private YouTubeService youTubeService = null;

        public UploadTask(string filename)
        {
            this.fileName = filename;
        }

        public async Task Run()
        {
            youTubeService = await AuthenticateWithYouTubeAsync();

            Video video = CreateVideoObjectWithMetaData();

            await UploadAsync(video);
        }

        private async Task UploadAsync(Video video)
        {
            using (var fs = new FileStream(fileName, FileMode.Open))
            {
                InsertMediaUpload videoInsertRequest = CreateInsertRequest(video, fs);

                await videoInsertRequest.UploadAsync();
            }
        }

        private InsertMediaUpload CreateInsertRequest(Video video, FileStream fs)
        {
            var videoInsertRequest = youTubeService.Videos.Insert(video, "snippet,status", fs, "video/*");
            videoInsertRequest.ProgressChanged += VideosInsertRequest_ProgressChanged;
            videoInsertRequest.ResponseReceived += VideosInsertRequest_ResponseReceived;
            videoInsertRequest.ChunkSize = 8 * 256 * 1024; //2MB in bytes
            return videoInsertRequest;
        }

        private static Video CreateVideoObjectWithMetaData()
        {
            var video = new Video();
            video.Snippet = new VideoSnippet();
            video.Snippet.Title = "Erstes Video vom Buup!";
            video.Snippet.Description = "Den Quellcode dazu findest du unter: https://github.com/LernMoment/YouTubeBulkUploader";
            video.Status = new VideoStatus();
            video.Status.PrivacyStatus = "unlisted"; // or "private" or "public"
            return video;
        }

        private void VideosInsertRequest_ProgressChanged(IUploadProgress progress)
        {
            switch (progress.Status)
            {
                case UploadStatus.Uploading:
                    Console.WriteLine($"{progress.BytesSent / 1024} KB bereits hochgeladen");
                    break;
                case UploadStatus.Failed:
                    Console.WriteLine($"Fehler beim Upload: {progress.Exception}");
                    break;
                default:
                    break;
            }
        }

        private void VideosInsertRequest_ResponseReceived(Video video)
        {
            Console.WriteLine($"Upload erfolgreich beendet! Das Video hat die Id: {video.Id}");
        }

        private async Task<YouTubeService> AuthenticateWithYouTubeAsync()
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
