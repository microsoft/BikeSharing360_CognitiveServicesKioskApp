using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Core;

namespace BikeSharing.Clients.CogServicesKiosk.Data
{
    public class TextToSpeech
    {
        private CoreDispatcher _dispatcher;
        private SpeechSynthesizer _synthesizer = new SpeechSynthesizer();
        private MediaElement _mediaElement = new MediaElement();
        private TaskCompletionSource<bool> _tcs;

        public TextToSpeech(MediaElement media, CoreDispatcher dispatcher)
        {
            _synthesizer = new SpeechSynthesizer();

            // Get all of the installed voices.
            var voices = SpeechSynthesizer.AllVoices;
            var ziraVoice = voices.FirstOrDefault(v => v.DisplayName.Contains("Zira"));
            if (ziraVoice == null) ziraVoice = voices.First();
            _synthesizer.Voice = ziraVoice;

            _dispatcher = dispatcher;

            _mediaElement = media;
            _mediaElement.MediaEnded += _mediaElement_MediaEnded;
            _mediaElement.MediaFailed += _mediaElement_MediaFailed;
        }

        ~TextToSpeech()
        {
            _mediaElement.MediaEnded -= _mediaElement_MediaEnded;
            _mediaElement.MediaFailed -= _mediaElement_MediaFailed;
            _mediaElement = null;
        }

        private void _mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            _tcs.SetResult(true);
            Debug.WriteLine("Done playing audio.");
        }

        private void _mediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Debug.WriteLine("Failed to play audio.");
            _tcs.SetResult(true);
        }

        public async Task SpeakAsync(string text)
        {
            Debug.WriteLine($"Playing audio for {text}...");
            // Create a stream from the text
            var synthesizedStream = await _synthesizer.SynthesizeTextToStreamAsync(text);

            // Set the stream to the media element to be played by the platform
            _mediaElement.SetSource(synthesizedStream, synthesizedStream.ContentType);

            // Use a TaskCompletionSource to be triggered to complete once the media element is done playing the sound. 
            // This is used because the media element doesn't have async/await support to tell you when a media is completed playing.
            _tcs = new TaskCompletionSource<bool>();

            if (_dispatcher.HasThreadAccess)
                _mediaElement.Play();
            else
                await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => _mediaElement.Play());

            // Await the TaskCompletionSource and wait for it to be triggered by the _mediaElement_MediaEnded event.
            await _tcs.Task;
        }
    }
}
