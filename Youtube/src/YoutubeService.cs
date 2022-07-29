using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Youtube.src
{
    public interface IYoutubeService
    {
        Task<StreamManifest> GetStreamManifest(string videoLink, CancellationToken token);
        Task<IVideo> GetvideoMetadata(string videoLink);
        IEnumerable<string> GetVideoResolutions(StreamManifest streamManifest);
        Task DownloadAsync(StreamManifest streamManifest, string videoLink, string resolution, string path, string title, IProgress<double> progress, CancellationToken cancellationToken);
    }

    public class YoutubeService : IYoutubeService
    {
        private readonly YoutubeClient _youtubeClient = new YoutubeClient();

        public async Task DownloadAsync(StreamManifest streamManifest, string videoLink, string resolution, string path, string title, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var format = "";
            try
            {
                switch (resolution)
                {
                    case Resolutions.p144:
                    case Resolutions.p240:
                    case Resolutions.p360:
                    case Resolutions.p480:
                    case Resolutions.p720:
                        format = "mp4";
                        await Task.Run(async () => await DownloadLowResAsync(streamManifest, path, title, progress, cancellationToken));
                        break;
                    case Resolutions.mp3:
                        format = "mp3";
                        await Task.Run(async () => await DownloadMp3(videoLink, path, title, progress, cancellationToken));
                        break;
                    default:
                        format = "mp4";
                        await Task.Run(async () => await DownloadHighResAsync(streamManifest, resolution, path, title, progress, cancellationToken));
                        break;
                }
            }
            catch (Exception ex)
            {
                await Task.Run(() =>
                {
                    var file = Path.Combine(path, $"{title}.{format}");
                    if (File.Exists(file)) File.Delete(file);
                });
                throw;
            }
        }

        private async Task DownloadHighResAsync(StreamManifest streamManifest, string resolution, string path, string title, IProgress<double> progress, CancellationToken cancellationToken)
        {
            IStreamInfo audioStreamInfo = streamManifest.GetAudioStreams().GetWithHighestBitrate();
            IVideoStreamInfo videoStreamInfo = streamManifest.GetVideoStreams().First(s => s.VideoQuality.Label == resolution);

            IStreamInfo[] streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };

            await _youtubeClient.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(string.Format(@"{0}\{1}.mp4", path, title)).Build(), progress, cancellationToken);
        }

        private async Task DownloadLowResAsync(StreamManifest streamManifest, string path, string title, IProgress<double> progress, CancellationToken cancellationToken)
        {
            IStreamInfo streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
            await _youtubeClient.Videos.Streams.DownloadAsync(streamInfo, string.Format(@"{0}\{1}.mp4", path, title), progress, cancellationToken);
        }

        private async Task DownloadMp3(string video_link, string path, string title, IProgress<double> progress, CancellationToken cancellationToken)
        {
            await _youtubeClient.Videos.DownloadAsync(video_link, string.Format(@"{0}\{1}.mp3", path, title), progress, cancellationToken);
        }

        public async Task<StreamManifest> GetStreamManifest(string videoLink, CancellationToken token)
        {
            return await Task.Run(() => _youtubeClient.Videos.Streams.GetManifestAsync(videoLink, token)).Result;
        }

        public async Task<IVideo> GetvideoMetadata(string videoLink)
        {
            return await Task.Run(() => _youtubeClient.Videos.GetAsync(videoLink)).Result;
        }

        public IEnumerable<string> GetVideoResolutions(StreamManifest streamManifest)
        {
            IEnumerable<string> resolutions = streamManifest.GetVideoStreams().Select(item => item.VideoQuality.Label);
            List<string> result = new List<string>();
            foreach (string res in resolutions)
            {
                if (!result.Contains(res) && res.Length < 8)
                {
                    result.Add(res);
                }
            }
            result.Add("mp3");
            return result;
        }
    }
}
