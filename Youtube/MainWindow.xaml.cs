using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Youtube.src;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Youtube
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private readonly YoutubeService _youtubeService = new YoutubeService();
        private readonly CommonOpenFileDialog _dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true
        };
        private bool _isVideoValid = false;
        private StreamManifest _streamManifest;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private IVideo _metadata;
        private Progress<double> _progress;
        private string _videoId;
        private readonly List<string> _videoMetadata = new List<string>();
        private const string UrlPattern = @"youtube\..+?/watch.*?v=(.*?)(?:&|/|$)";
        private string _videoLink = "";
        private string _customPath;
        private readonly string _downloadFolder = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "{374DE290-123F-4565-9164-39C4925E467B}", string.Empty).ToString();
        private string FolderPath => string.IsNullOrEmpty(_customPath) ? _downloadFolder : _customPath;
        public MainWindow()
        {
            InitializeComponent();
            OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Download Path is set to => {FolderPath}\n");

            //ResolutionMenu.Items.Add(Resolutions.mp3);
            //ResolutionMenu.Items.Add(Resolutions.p360);
            //ResolutionMenu.Items.Add(Resolutions.p480);
            //ResolutionMenu.Items.Add(Resolutions.p720);
            //ResolutionMenu.Items.Add(Resolutions.p72060);
            //ResolutionMenu.Items.Add(Resolutions.p1080);
            //ResolutionMenu.Items.Add(Resolutions.p108060);

        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            string resolution = ResolutionMenu.Text;
            CancellationToken token = _tokenSource.Token;
            if (Download.Content.ToString() == "Cancel")
            {
                _tokenSource.Cancel();
            }

            if (!_isVideoValid)
            {
                OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Invalid YouTube video ID or URL {VideoLink.Text.Trim()}.\n");
            }

            if (resolution.Length == 0)
            {
                OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Please select a video resolution!\n");
            }

            if (resolution.Length != 0 && _isVideoValid && Download.Content.ToString() == "Download")
            {
                OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Downloading => {_videoLink} In {resolution} To {FolderPath}\n");

                try
                {
                    string Title = string.Concat(_videoMetadata[0].Split(Path.GetInvalidFileNameChars()));
                    _progress = resolution == "mp3"
                        ? new Progress<double>(value => progressBar1.Value = value * 670)
                        : new Progress<double>(value => progressBar1.Value = value * 100);
                    Download.Content = "Cancel";
                    await _youtubeService.DownloadAsync(_streamManifest, _videoLink, resolution, FolderPath, Title, _progress, token);
                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Download is completed!\n");
                    Download.Content = "Download";
                    progressBar1.Value = 0;
                }
                catch (OperationCanceledException)
                {
                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Operation canceled!\n");
                    _tokenSource.Dispose();
                    _tokenSource = new CancellationTokenSource();
                    Download.Content = "Download";
                    progressBar1.Value = 0;
                }
                catch (Exception error)
                {
                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: {error.Message}!\n");
                    Download.Content = "Download";
                    progressBar1.Value = 0;
                }
            }
        }

        private async void VideoLink_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_videoLink))
            {
                _tokenSource.Cancel();
                _tokenSource.Dispose();
                _tokenSource = new CancellationTokenSource();
            }

            CancellationToken token = _tokenSource.Token;

            string videoUrl = VideoLink.Text.Trim();
            ResolutionMenu.Items.Clear();
            _isVideoValid = false;
            _videoMetadata.Clear();
            _videoId = Regex.Match(videoUrl, @"youtu(?:\.be|be\.com)\/(?:.*v(?:\/|=)|(?:.*\/)?)([a-zA-Z0-9-_]){11}").Groups[1].Value;
            _isVideoValid = !string.IsNullOrEmpty(_videoId) && Regex.IsMatch(videoUrl, UrlPattern);
            _videoLink = videoUrl;


            if (_isVideoValid)
            {
                try
                {
                    OutputMsg.Text = "";
                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Checking video...\n");

                    _metadata = await _youtubeService.GetvideoMetadata(videoUrl);
                    _streamManifest = await _youtubeService.GetStreamManifest(videoUrl, token);

                    IEnumerable<string> Res = _youtubeService.GetVideoResolutions(_streamManifest);

                    foreach (var res in Res)
                    {
                        ResolutionMenu.Items.Add(res);
                    }

                    OutputMsg.Text = "";
                    _videoMetadata.Add(_metadata.Title.ToString());
                    _videoMetadata.Add(_metadata.Author.ToString());
                    _videoMetadata.Add(_metadata.Duration.ToString());


                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Title : {_videoMetadata[0]}\n");
                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Author : {_videoMetadata[1]}\n");
                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Duration : {_videoMetadata[2]}\n");
                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Please select a video resolution or mp3 format to begin downloading!\n");
                }
                catch (HttpRequestException)
                {
                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Please check your Internet connection!\n");
                }
                catch (Exception error)
                {
                    OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: {error.Message}\n");
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CommonFileDialogResult result = _dialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                _customPath = _dialog.FileName;
                OutputMsg.AppendText($"[{DateTime.Now:HH:mm:ss}]: Download Path is set to => {_customPath}\n");
            }
        }
    }
}
