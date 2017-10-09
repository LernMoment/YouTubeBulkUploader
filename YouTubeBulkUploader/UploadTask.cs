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
        private VideoDescription videoMetaData;
        private string receiver;
        private YouTubeService youTubeService = null;

        public UploadTask(YouTubeService service, VideoDescription desc)
        {
            this.youTubeService = service;
            this.videoMetaData = desc;
        }

        public string UrlOfUploadedVideo { get; private set; }

        public async Task Run()
        {
            Video video = CreateVideoObjectWithMetaData();

            await UploadAsync(video);
        }

        private async Task UploadAsync(Video video)
        {
            using (var fs = new FileStream(videoMetaData.FileName, FileMode.Open))
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

        private Video CreateVideoObjectWithMetaData()
        {
            var video = new Video();
            video.Snippet = new VideoSnippet();
            video.Snippet.Title = $"Hallo {videoMetaData.Receiver} - Willkommen im C# Kurs!";
            video.Snippet.Description = "Weitere Informationen rund um C# und Softwareentwicklung findest du unter www.LernMoment.de";
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
            UrlOfUploadedVideo = "https://youtu.be/" + video.Id;
        }

    }
}
