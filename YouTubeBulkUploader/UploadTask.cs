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
        private const int maxRetries = 3;

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

                IUploadProgress progress = await videoInsertRequest.UploadAsync(CancellationToken.None);

                if (IsUploadResumable(progress))
                {
                    bool isCompleted = await ResumeUploadAsync(videoInsertRequest);
                    if (!isCompleted)
                    {
                        Console.WriteLine($"Could not upload {videoMetaData.FileName}");
                    }
                }
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

        private bool IsUploadResumable(IUploadProgress uploadStatusInfo)
        {
            if (uploadStatusInfo.Status != UploadStatus.Failed)
            {
                // upload did not fail. so we shouldn't resume it!
                return false;
            }

            bool isResumable = false;

            Google.GoogleApiException apiException = uploadStatusInfo.Exception as Google.GoogleApiException;
            if ((apiException == null) || (apiException.Error == null))
            {
                Console.WriteLine(string.Format("Upload Failed: {0}", uploadStatusInfo.Exception.Message));
                isResumable = true;
            }
            else
            {
                Console.WriteLine(string.Format("Upload Failed: {0}", apiException.Error.ToString()));
                // Do not retry if the request is in error
                int StatusCode = (int)apiException.HttpStatusCode;
                // See https://developers.google.com/youtube/v3/guides/using_resumable_upload_protocol
                if ((StatusCode / 100) == 4 || ((StatusCode / 100) == 5 && !(StatusCode == 500 | StatusCode == 502 | StatusCode == 503 | StatusCode == 504)))
                {
                    isResumable = false;
                }
                else
                {

                    isResumable = true;
                }
            }

            return isResumable;
        }

        private async Task<bool> ResumeUploadAsync(VideosResource.InsertMediaUpload videosInsertRequest)
        {
            bool isCompleted = false;
            int retryCount = 0;

            do
            {
                // Give network and server some time to resolve issue, if possible
                await Task.Delay(3000);

                // Try to resume upload
                Console.WriteLine("Resuming upload!");
                IUploadProgress progress = await videosInsertRequest.ResumeAsync(CancellationToken.None);

                // check whether we are done
                if (progress.Status == UploadStatus.Completed)
                {
                    retryCount = maxRetries;
                    isCompleted = true;
                }
                else if (IsUploadResumable(progress))
                {
                    retryCount++;
                }
                else
                {
                    throw new NotImplementedException($"ResumeUpload returned state {progress.Status} which is currently not handled.");
                }

            } while ((retryCount < maxRetries) && !isCompleted);

            return isCompleted;
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
