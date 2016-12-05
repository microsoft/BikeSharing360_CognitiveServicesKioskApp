using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;

namespace BikeSharing.Clients.CogServicesKiosk.Data
{
    public class SpeechToText : IDisposable
    {
        #region Variables

        uint MaxRecognitionResultAlternates = 4;
        private SpeechRecognizer _recognizer;
        private int _recognizerInitialSilenceTimeOutInSeconds;
        private int _recognizerBabbleTimeoutInSeconds;
        private int _recognizerEndSilenceTimeoutInSeconds;
        private string _languageName;

        #endregion

        #region Events

        public event EventHandler<string> OnHypothesis;
        public event EventHandler CapturingStarted;
        public event EventHandler CapturingEnded;

        #endregion

        #region Constructors

        public SpeechToText(
            string languageName = "en-Us",
            int recognizerInitialSelenceTimeOutInSeconds = 6,
            int recognizerBabbleTimeoutInSeconds = 0,
            int recognizerEndSilenceTimeoutInSeconds = 3)
        {
            this._languageName = languageName;
            this._recognizerInitialSilenceTimeOutInSeconds = recognizerInitialSelenceTimeOutInSeconds;
            this._recognizerBabbleTimeoutInSeconds = recognizerBabbleTimeoutInSeconds;
            this._recognizerEndSilenceTimeoutInSeconds = recognizerEndSilenceTimeoutInSeconds;
        }

        #endregion

        #region Methods

        public async Task<bool> InitializeRecognizerAsync()
        {
            Debug.WriteLine("[Speech to Text]: initializing Speech Recognizer...");
            var language = new Windows.Globalization.Language(_languageName);
            _recognizer = new SpeechRecognizer(language);
            // Set timeout settings.
            _recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(_recognizerInitialSilenceTimeOutInSeconds);
            _recognizer.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(_recognizerBabbleTimeoutInSeconds);
            _recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(_recognizerEndSilenceTimeoutInSeconds);
            // Set UI text
            _recognizer.UIOptions.AudiblePrompt = "Say what you want to do...";

            if (!this.IsOffline())
            {
                // This requires internet connection
                SpeechRecognitionTopicConstraint topicConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "Development");
                _recognizer.Constraints.Add(topicConstraint);
            }
            else
            {
                // In case of network issue
                string[] responses =
                {
                    "I would like to rent a bike",
                    "I want to rent a bike",
                    "I'd like to rent a bike",
                    "rent a bike",
                    "I would like to rent a bicycle",
                    "I want to rent a bicycle",
                    "I'd like to rent a bicycle",
                    "rent a bicycle"
                };

                // Add a list constraint to the recognizer.
                var listConstraint = new SpeechRecognitionListConstraint(responses, "rentBikePhrases");
                _recognizer.Constraints.Add(listConstraint);
            }

            SpeechRecognitionCompilationResult result = await _recognizer.CompileConstraintsAsync();   // Required

            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                Debug.WriteLine("[Speech to Text]: Grammar Compilation Failed: " + result.Status.ToString());
                return false;
            }

            _recognizer.HypothesisGenerated += Recognizer_HypothesisGenerated;
            _recognizer.StateChanged += Recognizer_StateChanged;
            _recognizer.ContinuousRecognitionSession.ResultGenerated += (s, e) => { Debug.WriteLine($"[Speech to Text]: recognizer results: {e.Result.Text}, {e.Result.RawConfidence.ToString()}, {e.Result.Confidence.ToString()}"); };
            Debug.WriteLine("[Speech to Text]: done initializing Speech Recognizer");
            return true;
        }

        private bool IsOffline()
        {
            var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
            bool isConnected = profile?.GetNetworkConnectivityLevel() == Windows.Networking.Connectivity.NetworkConnectivityLevel.InternetAccess;
            return !isConnected;
        }

        private void Recognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            Debug.WriteLine("[Speech to Text]: ********* Partial Result *********");
            Debug.WriteLine($"[Speech to Text]: {args.Hypothesis.Text}");
            Debug.WriteLine("[Speech to Text]: ");

            this.OnHypothesis?.Invoke(this, args.Hypothesis.Text);
        }

        private void Recognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine($"[Speech to Text]: recognizer state changed to: {args.State.ToString()}");

            switch (args.State)
            {
                case SpeechRecognizerState.Capturing:
                    this.CapturingStarted?.Invoke(this, null);
                    break;
                case SpeechRecognizerState.Processing:
                case SpeechRecognizerState.Idle:
                    this.CapturingEnded?.Invoke(this, null);
                    break;
                default:
                    break;
            }
        }

        public async Task<string> GetTextFromSpeechAsync(bool withUI = false)
        {
            if (_recognizer == null)
            {
                await InitializeRecognizerAsync();
            }

            SpeechRecognitionResult recognition = null;
            if (withUI)
            {
                recognition = await _recognizer.RecognizeWithUIAsync();
            }
            else
            {
                recognition = await _recognizer.RecognizeAsync();
            }

            if (recognition.Status == SpeechRecognitionResultStatus.Success &&
                recognition.Confidence != SpeechRecognitionConfidence.Rejected)
            {
                Debug.WriteLine($"[Speech to Text]: result: {recognition.Text}, {recognition.RawConfidence.ToString()}, {recognition.Confidence.ToString()}");
                var alternativeResults = recognition.GetAlternates(MaxRecognitionResultAlternates);

                foreach (var r in alternativeResults)
                {
                    Debug.WriteLine($"[Speech to Text]: alternative: {r.Text}, {r.RawConfidence.ToString()}, {r.Confidence.ToString()}");
                }

                var topResult = alternativeResults.Where(r => r.Confidence == SpeechRecognitionConfidence.High).FirstOrDefault();
                if (topResult != null) return topResult.Text;

                topResult = alternativeResults.Where(r => r.Confidence == SpeechRecognitionConfidence.Medium).FirstOrDefault();
                if (topResult != null) return topResult.Text;

                topResult = alternativeResults.Where(r => r.Confidence == SpeechRecognitionConfidence.Low).FirstOrDefault();
                if (topResult != null) return topResult.Text;
            }

            return string.Empty;
        }

        public async Task Stop()
        {
            try
            {
                await _recognizer.StopRecognitionAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Speech to Text]: an error occured while stoping Speech Recognition session: {ex.ToString()}");
            }
        }

        public void Dispose()
        {
            if (_recognizer != null)
            {
                _recognizer.HypothesisGenerated -= Recognizer_HypothesisGenerated;
                _recognizer.Dispose();
                _recognizer = null;
            }
        }

        #endregion
    }
}
