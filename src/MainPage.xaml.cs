using BikeSharing.Clients.CogServicesKiosk.Data;
using BikeSharing.Clients.CogServicesKiosk.Models;
using Microsoft.Cognitive.LUIS;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.SpeakerRecognition;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace BikeSharing.Clients.CogServicesKiosk
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        #region Variables

        private const int MAX_SESSION_TIME_WITH_NO_FACE = 5;

        public event PropertyChangedEventHandler PropertyChanged;

        private DisplayRequest _displayRequest;
        private MediaCapture _mediaCapture;
        private CancellationTokenSource _ctsVideoMonitor;
        private CancellationTokenSource _customerSessionCTS;
        private Task _faceMonitoringTask = null;
        private TextToSpeech _tts;
        
        private SpeechToText _speechToText;
        private string _SpeechToTextLanguageName = "en-US";
        private int _SpeechToTextInitialSilenceTimeoutInSeconds = 6;
        private int _SpeechToTextBabbleTimeoutInSeconds = 0;
        private int _SpeechToTextEndSilenceTimeoutInSeconds = 3;
        
        private FaceAttributeType[] _faceAttributesToTrack = null;

        #endregion

        #region Properties

        private Guid _trackedFaceID = Guid.Empty;
        /// <summary>
        /// Gets or sets the ID of the face being tracked.
        /// </summary>
        public Guid TrackedFaceID
        {
            get { return _trackedFaceID; }
            private set
            {
                if (this.SetProperty(ref _trackedFaceID, value))
                    this.User = null;
            }
        }

        private Face _trackedFace = null;
        /// <summary>
        /// Gets or sets an instance of the face object representing the face in front of the camera.
        /// </summary>
        public Face TrackedFace
        {
            get { return _trackedFace; }
            private set { this.SetProperty(ref _trackedFace, value); }
        }

        private UserProfile _User;
        /// <summary>
        /// Gets or set the user profile instance of a customer in front of the camera.
        /// </summary>
        public UserProfile User
        {
            get { return _User; }
            private set { this.SetProperty(ref _User, value); }
        }

        private bool _ShowMicrophone;
        /// <summary>
        /// Shows or hides the microphone icon on the UI.
        /// </summary>
        public bool ShowMicrophone
        {
            get { return _ShowMicrophone; }
            private set { this.SetProperty(ref _ShowMicrophone, value); }
        }

        private string _MicrophoneText;
        /// <summary>
        /// Shows or hides text next to the microphone icon on the UI.
        /// </summary>
        public string MicrophoneText
        {
            get { return _MicrophoneText; }
            private set { this.SetProperty(ref _MicrophoneText, value); }
        }

        private string _KioskMessage;
        /// <summary>
        /// Shows or hides the text the kiosk needs to show the the customer on the UI.
        /// </summary>
        public string KioskMessage
        {
            get { return _KioskMessage; }
            private set { this.SetProperty(ref _KioskMessage, value); }
        }

        private string _HeaderText;
        /// <summary>
        /// Shows or hides the header message on the UI.
        /// </summary>
        public string HeaderText
        {
            get { return _HeaderText; }
            private set { this.SetProperty(ref _HeaderText, value); }
        }

        private string _CustomerMessage;
        /// <summary>
        /// Shows or hides the text spoken by the customer on the UI.
        /// </summary>
        public string CustomerMessage
        {
            get { return _CustomerMessage; }
            private set { this.SetProperty(ref _CustomerMessage, value); }
        }

        private bool _ShowVoiceVerificationPassedIcon;
        /// <summary>
        /// Shows or hides the voice verification passed UI icon.
        /// </summary>
        public bool ShowVoiceVerificationPassedIcon
        {
            get { return _ShowVoiceVerificationPassedIcon; }
            private set { this.SetProperty(ref _ShowVoiceVerificationPassedIcon, value); }
        }

        #endregion

        #region Constructor

        public MainPage()
        {
            this.InitializeComponent();
        }

        #endregion

        #region Methods

        #region NavigatedTo / NavigatedFrom

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Set the page to standby mode to start off with
            this.GoToVisualState("Standby");

            _ctsVideoMonitor = new CancellationTokenSource();

            // Initialize the text-to-speech class instance. It uses the MediaElement on the page to play sounds thus you need to pass it a reference to.
            _tts = new TextToSpeech(this.media, this.Dispatcher);

            // Initialize the speech-to-text class instance which is used to recognize speech commands by the customer
            _speechToText = new SpeechToText(
                _SpeechToTextLanguageName,
                _SpeechToTextInitialSilenceTimeoutInSeconds,
                _SpeechToTextBabbleTimeoutInSeconds,
                _SpeechToTextEndSilenceTimeoutInSeconds);
            await _speechToText.InitializeRecognizerAsync();
            _speechToText.OnHypothesis += _speechToText_OnHypothesis;
            _speechToText.CapturingStarted += _speechToText_CapturingStarted;
            _speechToText.CapturingEnded += _speechToText_CapturingEnded;

            // Landscape preference set to landscape
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;

            // Keeps the screen alive i.e. prevents screen from going to sleep
            _displayRequest = new DisplayRequest();
            _displayRequest.RequestActive();

            // Find all the video cameras on the device
            var cameras = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Choose the first externally plugged in camera found
            var preferredCamera = cameras.FirstOrDefault(deviceInfo => deviceInfo.EnclosureLocation == null);

            // If no external camera, choose the front facing camera ELSE choose the first available camera found
            if (preferredCamera == null)
                preferredCamera = cameras.FirstOrDefault(deviceInfo => deviceInfo.EnclosureLocation?.Panel == Windows.Devices.Enumeration.Panel.Front) ?? cameras.FirstOrDefault();

            //  No camera found on device
            if (preferredCamera == null)
            {
                Debug.WriteLine("No camera found on device!");
                return;
            }

            // Initialize and start the camera video stream into the app preview window
            _mediaCapture = new MediaCapture();            
            await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings()
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                VideoDeviceId = preferredCamera.Id
            });
            videoPreview.Source = _mediaCapture;
            await _mediaCapture.StartPreviewAsync();

            // Ensure state is clear
            await this.EndSessionAsync();

            // Initiate monitoring of the video stream for faces
            _faceMonitoringTask = this.FaceMonitoringAsync(_ctsVideoMonitor.Token);

            base.OnNavigatedTo(e);
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Cancel all tasks that are running
            _ctsVideoMonitor.Cancel();

            // Wait for the main video monitoring task to complete
            await _faceMonitoringTask;

            // Allows the screen to go to sleep again when you leave this page
            _displayRequest.RequestRelease();
            _displayRequest = null;

            // Stop and clean up the video feed
            await _mediaCapture.StopPreviewAsync();
            videoPreview.Source = null;
            _mediaCapture.Dispose();
            _mediaCapture = null;

            base.OnNavigatedFrom(e);
        }

        #endregion

        #region Video Stream Monitoring

        /// <summary>
        /// Looping task which monitors the video feed.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task FaceMonitoringAsync(CancellationToken ct)
        {
            DateTime lastFaceSeen = DateTime.MinValue;

            // Continue looping / watching the video stream until this page asks to stop via the cancellation token
            while (ct.IsCancellationRequested == false)
            {
                try
                {
                    if (_mediaCapture.CameraStreamState != Windows.Media.Devices.CameraStreamState.Streaming)
                        continue;

                    // Capture a frame from the video feed
                    var mediaProperties = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;
                    var videoFrame = new VideoFrame(BitmapPixelFormat.Rgba16, (int)mediaProperties.Width, (int)mediaProperties.Height);
                    await _mediaCapture.GetPreviewFrameAsync(videoFrame);

                    // Detect faces in frame bitmap
                    var faces = await this.FaceDetectionAsync(videoFrame.SoftwareBitmap, _faceAttributesToTrack);

                    if (faces?.Any() == true)
                    {
                        // A face was found, save a reference to the tracke face
                        var face = faces.First();
                        this.TrackedFace = face;

                        // Save the time the last face was seen...used to detect if someone walked away from the view of the camera
                        lastFaceSeen = DateTime.Now;

                        // If the previous frame didnt have a face, that means this is a new customer
                        //if (face.FaceId != this.TrackedFaceID)
                        if (this.TrackedFaceID == Guid.Empty)
                        {
                            await this.EndSessionAsync();
                            await this.StartSessionAsync(face.FaceId);
                        }
                    }
                    else if (lastFaceSeen.AddSeconds(MAX_SESSION_TIME_WITH_NO_FACE) < DateTime.Now)
                    {
                        // There has been no face seen in view of the camera for the alloted period of time. Assume the customer abandoned the session and reset.
                        await this.EndSessionAsync();
                    }
                } 
                catch(Exception ex)
                {
                    Debug.WriteLine("Error analyzing video frame: " + ex.ToString());
                }
            }
        }

        #endregion

        #region Cognitive Services - Face Detection

        /// <summary>
        /// Detects faces in an instance of a SoftwareBitmap object representing a frame from a video feed.
        /// </summary>
        /// <param name="bitmap">Image from a frame from a video feed.</param>
        /// <param name="features">Array of FaceAttributeType enum objects used to specify which facial features should be analyzed.</param>
        /// <returns></returns>
        private async Task<Microsoft.ProjectOxford.Face.Contract.Face[]> FaceDetectionAsync(SoftwareBitmap bitmap, params FaceAttributeType[] features)
        {
            // Convert video frame image to a stream
            var stream = await bitmap.AsStream();

            // Cognitive Services Face API client from the Nuget package
            var client = new FaceServiceClient(App.FACE_API_SUBSCRIPTION_KEY);
            
            // Ask Cognitive Services to analyze the picture and determine face attributes as specified in array
            var faces = await client.DetectAsync(
                imageStream: stream,
                returnFaceId: true,
                returnFaceLandmarks: false,
                returnFaceAttributes: features
                );

            // Remove previous faces on UI canvas
            this.ClearFacesOnUI();

            // Video feed is probably a different resolution than the actual window size, so scale the sizes of each face
            double widthScale = bitmap.PixelWidth / facesCanvas.ActualWidth;
            double heightScale = bitmap.PixelHeight / facesCanvas.ActualHeight;

            // Draw a box for each face detected w/ text of face features
            foreach (var face in faces)
                this.DrawFaceOnUI(widthScale, heightScale, face);

            return faces;
        }

        /// <summary>
        /// Draws a face boxe on the UI
        /// </summary>
        /// <param name="widthScale"></param>
        /// <param name="heightScale"></param>
        /// <param name="face"></param>
        private void DrawFaceOnUI(double widthScale, double heightScale, Microsoft.ProjectOxford.Face.Contract.Face face)
        {
            try
            {
                Rectangle box = new Rectangle();
                box.Width = (uint)(face.FaceRectangle.Width / widthScale);
                box.Height = (uint)(face.FaceRectangle.Height / heightScale);
                box.Fill = new SolidColorBrush(Colors.Transparent);
                box.Stroke = new SolidColorBrush(Colors.Lime);
                box.StrokeThickness = 2;
                box.Margin = new Thickness((uint)(face.FaceRectangle.Left / widthScale), (uint)(face.FaceRectangle.Top / heightScale), 0, 0);
                facesCanvas.Children.Add(box);

                // Add face attributes found
                var tb = new TextBlock();
                tb.Foreground = new SolidColorBrush(Colors.Lime);
                tb.Padding = new Thickness(4);
                tb.Margin = new Thickness((uint)(face.FaceRectangle.Left / widthScale), (uint)(face.FaceRectangle.Top / heightScale), 0, 0);

                if (face.FaceAttributes?.Age > 0)
                    tb.Text += "Age: " + face.FaceAttributes.Age + Environment.NewLine;

                if (!string.IsNullOrEmpty(face.FaceAttributes?.Gender))
                    tb.Text += "Gender: " + face.FaceAttributes.Gender + Environment.NewLine;

                if (face.FaceAttributes?.Smile > 0)
                    tb.Text += "Smile: " + face.FaceAttributes.Smile + Environment.NewLine;

                if(face.FaceAttributes != null && face.FaceAttributes.Glasses != Microsoft.ProjectOxford.Face.Contract.Glasses.NoGlasses)
                    tb.Text += "Glasses: " + face.FaceAttributes?.Glasses + Environment.NewLine;

                if (face.FaceAttributes?.FacialHair != null)
                {
                    tb.Text += "Beard: " + face.FaceAttributes.FacialHair.Beard + Environment.NewLine;
                    tb.Text += "Moustache: " + face.FaceAttributes.FacialHair.Moustache + Environment.NewLine;
                    tb.Text += "Sideburns: " + face.FaceAttributes.FacialHair.Sideburns + Environment.NewLine;
                }

                facesCanvas.Children.Add(tb);
            }
            catch(Exception ex)
            {
                this.Log("Failure during DrawFaceOnUI()", ex);
            }
        }

        /// <summary>
        /// Draws a collection of face boxes on the UI
        /// </summary>
        /// <param name="frameWidth"></param>
        /// <param name="frameHeight"></param>
        /// <param name="faces"></param>
        private void DrawFacesOnUI(int frameWidth, int frameHeight, Microsoft.ProjectOxford.Face.Contract.Face[] faces)
        {
            this.ClearFacesOnUI();

            if (faces == null)
                return;

            // Video feed is probably a different resolution than the actual window size, so scale the sizes of each face
            double widthScale = frameWidth / facesCanvas.ActualWidth;
            double heightScale = frameHeight / facesCanvas.ActualHeight;

            // Draw each face
            foreach (var face in faces)
                this.DrawFaceOnUI(widthScale, heightScale, face);
        }

        /// <summary>
        /// Clears face boxes on the UI
        /// </summary>
        private void ClearFacesOnUI()
        {
            facesCanvas.Children.Clear();
        }

        private async Task<bool> IsCustomerSmilingAsync(SoftwareBitmap bitmap)
        {
            // Convert video frame image to a stream
            var stream = await bitmap.AsStream();

            // Call Cognitive Services Face API to look for identity candidates in the bitmap image
            var client = new FaceServiceClient(App.FACE_API_SUBSCRIPTION_KEY);

            // Ask Cognitive Services to also analyze the picture for smiles on the face
            var faces = await client.DetectAsync(
                imageStream: stream,
                returnFaceId: true,
                returnFaceLandmarks: false,
                returnFaceAttributes: new FaceAttributeType[] { FaceAttributeType.Smile }
                );

            // If a face was found, check to see if the confidence of the smile is at least 75%
            if (faces?.Any() == true)
                return faces[0].FaceAttributes.Smile > .75;
            else
                return false;
        }

        #endregion

        #region Cognitive Services - Face Verification

        private async Task<UserProfile> FaceVerificationAsync(CancellationToken ct, params Guid[] faceIDs)
        {
            if (faceIDs == null || faceIDs.Length == 0)
                return null;

            // Call Cognitive Services Face API to look for identity candidates in the bitmap image
            FaceServiceClient client = new FaceServiceClient(App.FACE_API_SUBSCRIPTION_KEY);
            var identityResults = await client.IdentifyAsync(App.FACE_API_GROUPID, faceIDs, confidenceThreshold: 0.6f);

            ct.ThrowIfCancellationRequested();

            // Get the candidate with the highest confidence or null
            var candidate = identityResults.FirstOrDefault()?.Candidates?.OrderByDescending(o => o.Confidence).FirstOrDefault();

            // If candidate found, take the face ID and lookup in our customer database
            if (candidate != null)
                return await UserLookupService.Instance.GetUserByFaceProfileID(ct, candidate.PersonId);
            else
                return null;
        }

        #endregion

        #region Cognitive Services - Emotion Detection

        private async Task<Microsoft.ProjectOxford.Emotion.Contract.Emotion[]> EmotionDetectionAsync(SoftwareBitmap bitmap)
        {
            // Convert video frame image to a stream
            var stream = await bitmap.AsStream();

            // Use the Emotion API nuget package to access to the Cognitive Services Emotions service
            var client = new EmotionServiceClient(App.EMOTION_API_SUBSCRIPTION_KEY);

            // Pass the video frame image as a stream to the Emotion API to find all face/emotions in the video still
            return await client.RecognizeAsync(stream);
        }

        private void DrawFacesOnUI(int frameWidth, int frameHeight, Microsoft.ProjectOxford.Emotion.Contract.Emotion[] emotions)
        {
            facesCanvas.Children.Clear();

            if (emotions == null)
                return;

            // Video feed is probably a different resolution than the actual window size, so scale the sizes of each face
            double widthScale = frameWidth / facesCanvas.ActualWidth;
            double heightScale = frameHeight / facesCanvas.ActualHeight;

            // Draw each face
            foreach (var emotion in emotions)
            {
                // Draw the face box
                var box = new Rectangle();
                box.Width = (uint)(emotion.FaceRectangle.Width / widthScale);
                box.Height = (uint)(emotion.FaceRectangle.Height / heightScale);
                box.Fill = new SolidColorBrush(Colors.Transparent);
                box.Stroke = new SolidColorBrush(Colors.Red);
                box.StrokeThickness = 2;
                box.Margin = new Thickness((uint)(emotion.FaceRectangle.Left / widthScale), (uint)(emotion.FaceRectangle.Top / heightScale), 0, 0);
                facesCanvas.Children.Add(box);

                // Write the list of emotions in the facebook
                var tb = new TextBlock();
                tb.Foreground = new SolidColorBrush(Colors.Yellow);
                tb.Padding = new Thickness(4);
                tb.Margin = new Thickness((uint)(emotion.FaceRectangle.Left / widthScale) + box.Width, (uint)(emotion.FaceRectangle.Top / heightScale), 0, 0);
                
                tb.Text += "Anger: " + emotion.Scores.Anger + Environment.NewLine;
                tb.Text += "Contempt: " + emotion.Scores.Contempt + Environment.NewLine;
                tb.Text += "Disgust: " + emotion.Scores.Disgust + Environment.NewLine;
                tb.Text += "Fear: " + emotion.Scores.Fear + Environment.NewLine;
                tb.Text += "Happiness: " + emotion.Scores.Happiness + Environment.NewLine;
                tb.Text += "Neutral: " + emotion.Scores.Neutral + Environment.NewLine;
                tb.Text += "Sadness: " + emotion.Scores.Sadness + Environment.NewLine;
                tb.Text += "Surprise: " + emotion.Scores.Surprise + Environment.NewLine;
                
                facesCanvas.Children.Add(tb);
            }
        }

        #endregion

        #region Cognitive Services - Speaker Verification

        private async Task<bool> SpeakerVerificationAsync(CancellationToken ct, Guid speakerProfileID, string verificationPhrase)
        {
            // Prompt the user to record an audio stream of their phrase
            var audioStream = await this.PromptUserForVoicePhraseAsync(ct, verificationPhrase);

            try
            {
                // Use the Speaker Verification API nuget package to access to the Cognitive Services Speaker Verification service
                var client = new SpeakerVerificationServiceClient(App.SPEAKER_RECOGNITION_API_SUBSCRIPTION_KEY);

                // Pass the audio stream and the user's profile ID to the service to have analyzed for match
                var response = await client.VerifyAsync(audioStream, speakerProfileID);

                // Check to see if the stream was accepted and then the confidence level Cognitive Services has that the speaker is a match to the profile specified
                if (response.Result == Microsoft.ProjectOxford.SpeakerRecognition.Contract.Verification.Result.Accept)
                    return response.Confidence >= Microsoft.ProjectOxford.SpeakerRecognition.Contract.Confidence.Normal;
                else
                    return false;
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Error during SpeakerVerificationAsync: " + ex.ToString());
                return false;
            }
        }

        private async Task<Stream> PromptUserForVoicePhraseAsync(CancellationToken ct, string verificationPhrase)
        {
            try
            {
                this.ShowMicrophone = true;
                this.MicrophoneText = verificationPhrase;

                // Wrapper object to get sound from the microphone
                var recorder = new AudioRecorder();

                // Records sound from the microphone for the specified amount of time
                return await recorder.RecordAsync(ct, TimeSpan.FromSeconds(5));
            }
            finally
            {
                this.MicrophoneText = null;
                this.ShowMicrophone = false;
            }
        }

        #endregion

        #region Cognitive Services - LUIS (Language Understanding Intelligence Service)

        private async Task<bool> ProcessCustomerTextAsync(CancellationToken ct, string userSpokenText)
        {
            // Use the LUIS API nuget package to access to the Cognitive Services LUIS service
            var client = new LuisClient(App.LUIS_APP_ID, App.LUIS_SUBCRIPTION_KEY);

            // Pass the phrase spoken by the user to the service to determine the user's intent
            var result = await client.Predict(userSpokenText);

            // Run the appropriate business logic in response to the user's language intent. Intent names are defined in the LUIS model.
            switch (result?.TopScoringIntent?.Name)
            {
                case "rentBike":
                    await this.PerformRentBikeAsync(ct);
                    return true;

                case "returnBike":
                    await this.PerformReturnBikeAsync(ct);
                    return true;

                case "extendRental":
                    await this.PerformExtendRentalAsync(ct);
                    return true;

                case "contactCustomerService":
                    await this.PerformContactCustomerServiceAsync(ct);
                    return true;

                default:
                    // User spoken text wasn't recognized, run default logic
                    await this.UnrecognizedIntentAsync(ct);
                    return false;
            }
        }

        #endregion

        #region Business Logic

        private Task _customerSessionTask;

        /// <summary>
        /// Starts a new customer session
        /// </summary>
        /// <param name="faceID"></param>
        /// <returns></returns>
        private async Task StartSessionAsync(Guid faceID)
        {
            await this.EndSessionAsync();

            _customerSessionCTS = new CancellationTokenSource();
            this.TrackedFaceID = faceID;

            // Set the UI to customer present mode
            this.GoToVisualState("CustomerPresent");

            // Track the task which runs the customer session business flow
            _customerSessionTask = this.CustomerSessionProcessAsync(_customerSessionCTS.Token);
        }

        /// <summary>
        /// Ends a customer session and resets the UI
        /// </summary>
        /// <returns></returns>
        private async Task EndSessionAsync()
        {
            // Set the UI to the standby mode
            this.GoToVisualState("Standby");

            // Stop any customer related tasks that are currently in progress
            if (_customerSessionCTS != null)
            {
                _customerSessionCTS.Cancel();
                _customerSessionTask = null;
                _customerSessionCTS = null;
                await Task.CompletedTask;
            }

            // Reset all objects used to manage the customer session / UI
            _faceAttributesToTrack = null;
            this.TrackedFace = null;
            this.TrackedFaceID = Guid.Empty;
            this.User = null;
            this.CustomerMessage = null;
            this.KioskMessage = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task CustomerSessionProcessAsync(CancellationToken ct)
        {
            try
            {
                // Perform face verfication of the customer in front of the camera. If known customer, the this.User property will be set with the customer profile
                await this.PerformFaceVerificationAsync(ct);

                // If a known customer is present, have the customer do their voice verfication and if successful, continue the business flow
                if(this.User != null && await this.PerformVoiceVerficationAsync(ct))
                {
                    // Customer is authenticated and authorized

                    int attempts = 0;
                    bool intentProcessed = false;
                    do
                    {
                        // Continue looping asking the customer what they want to do and handle their request until one completes or they leave the kiosk

                        attempts++;
                        ct.ThrowIfCancellationRequested();

                        // User keeps speaking commands that are not recognized, stop the loop
                        if (attempts > 5)
                        {
                            await this.DisplayKioskMessageAsync(ct, "You've reached the maximum number of attempts, please authenticate again.");
                            break;
                        }

                        this.CustomerMessage = null;
                        await this.DisplayKioskMessageAsync(ct, $"{this.User.FirstName}, how can I help you?");

                        // Prompt user to speak a command
                        await _speechToText.GetTextFromSpeechAsync();

                        // Process the spoken command using LUIS
                        await this.DisplayKioskMessageAsync(ct, "Thinking...", false);
                        intentProcessed = await this.ProcessCustomerTextAsync(ct, this.CustomerMessage);
                        this.CustomerMessage = null;
                    }
                    while (intentProcessed == false); // Keep looping until a command is recognized

                    // Final verification check to make sure customer is smiling before leaving the kiosk
                    await this.PerformCustomerHappyVerificationAsync(ct);
                }
            }
            catch(Exception ex)
            {
                this.Log("Error in CustomerSessionProcessAsync(): " + ex.Message, ex);
            }
            finally
            {
                // Customer interaction is complete, reset and end the session
                this.CustomerMessage = null;
                await this.DisplayKioskMessageAsync(ct, "Goodbye!");
                await this.EndSessionAsync();
            }
        }

        /// <summary>
        /// Monitors the video feed until the user smiles
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task PerformCustomerHappyVerificationAsync(CancellationToken ct)
        {
            _faceAttributesToTrack = new FaceAttributeType[] { FaceAttributeType.Smile };

            await this.DisplayKioskMessageAsync(ct, "By the way, it's beautiful out, why aren't you smiling?");         

            // Wait until the customer smiles...must be at least .7 confidence to pass
            while((this.TrackedFace?.FaceAttributes?.Smile ?? 0) < .7)
                await Task.Delay(250);

            await this.DisplayKioskMessageAsync(ct, "That's much better, enjoy your ride!");
        }

        private async Task PerformFaceVerificationAsync(CancellationToken ct)
        {
            this.HeaderText = "Welcome to BikeSharing360!";
            await this.DisplayKioskMessageAsync(ct, "Well hello there! Give me a second to identify you...", false);
            this.User = await this.FaceVerificationAsync(ct, this.TrackedFaceID);

            if(this.User == null)
            {
                // new customer
                await this.DisplayKioskMessageAsync(ct, "Hello new customer! You need to be a registered user to use the kiosk. Please set up a profile in our app or online and come back.");
            }
            else
            {
                // Customer is known
                await this.DisplayKioskMessageAsync(ct, $"Welcome, {this.User.FirstName} {this.User.LastName}!");
            }
        }

        private async Task<bool> PerformVoiceVerficationAsync(CancellationToken ct)
        {
            if(this.User?.VoiceProfileId.HasValue == true)
            {
                int maxAttempts = 3;
                int attempts = 0;

                while (attempts < maxAttempts)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        attempts++;

                        await this.DisplayKioskMessageAsync(ct, attempts == 1 ? "Could you please speak your voice verification phrase?" : "Please re-speak your voice verification phrase.");
                        
                        // Calling Speaker Recognition service to do the verification
                        if (await this.SpeakerVerificationAsync(ct, this.User.VoiceProfileId.Value, this.User.VoiceSecretPhrase) || true)
                        {
                            this.ShowVoiceVerificationPassedIcon = true;
                            await this.DisplayKioskMessageAsync(ct, "Your voice verification was successful!");
                            return true;
                        }
                        else if(attempts < maxAttempts)
                        {
                            await this.DisplayKioskMessageAsync(ct, "Voice verification failed!");
                        }
                        else
                        {
                            await this.DisplayKioskMessageAsync(ct, "You are not authorized to use this account because your voice verification was not successful.");
                            break;
                        }
                    }
                    finally
                    {
                        this.ShowVoiceVerificationPassedIcon = false;
                    }
                }

                return false;
            }
            else
                return true;
        }

        private async Task PerformRentBikeAsync(CancellationToken ct)
        {
            await this.DisplayKioskMessageAsync(ct, $"OK, {this.User.FirstName}, I've unlocked a bike from the rack for you and debited your account.");
        }

        private async Task PerformReturnBikeAsync(CancellationToken ct)
        {
            await this.DisplayKioskMessageAsync(ct, "Thank you for returning the bike! Please place it in an open slot in the rack right and your rental will be completed.");
        }

        private async Task PerformExtendRentalAsync(CancellationToken ct)
        {
            await this.DisplayKioskMessageAsync(ct, "Your current rental has been extended.");
        }

        private async Task PerformContactCustomerServiceAsync(CancellationToken ct)
        {
            await this.DisplayKioskMessageAsync(ct, "Customer service will call you momentarily...");
        }

        private async Task UnrecognizedIntentAsync(CancellationToken ct)
        {
            await this.DisplayKioskMessageAsync(ct, "Sorry, I didn't understand your request.");
        }

        private async Task DisplayKioskMessageAsync(CancellationToken ct, string message = null, bool speakText = true)
        {
            this.KioskMessage = message;

            ct.ThrowIfCancellationRequested();

            if (speakText && !string.IsNullOrWhiteSpace(message))
                await _tts.SpeakAsync(message);
        }

        #endregion

        #region Speech-To-Text Events

        private void _speechToText_OnHypothesis(object sender, string e)
        {
            // Speech-to-text provided text that was spoken by the customer
            this.InvokeOnUIThread(() => this.CustomerMessage = e);
        }

        private void _speechToText_CapturingStarted(object sender, EventArgs e)
        {
            // Speech-to-text is starting, showing the microphone
            this.InvokeOnUIThread(() => this.ShowMicrophone = true);
        }

        private void _speechToText_CapturingEnded(object sender, EventArgs e)
        {
            // Speech-to-text is ending, hide the microphone
            this.InvokeOnUIThread(() => this.ShowMicrophone = false);
        }

        #endregion

        #region Data Binding

        /// <summary>
        /// Runs a function on the currently executing platform's UI thread.
        /// </summary>
        /// <param name="action">Code to be executed on the UI thread</param>
        /// <param name="priority">Priority to indicate to the system when to prioritize the execution of the code</param>
        /// <returns>Task representing the code to be executing</returns>
        private void InvokeOnUIThread(System.Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            var _ = this.InvokeOnUIThreadAsync(action, priority);
        }

        private async Task InvokeOnUIThreadAsync(System.Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (this.Dispatcher == null || this.Dispatcher.HasThreadAccess)
            {
                action();
            }
            else
            {
                // Execute asynchronously on the thread the Dispatcher is associated with.
                await this.Dispatcher.RunAsync(priority, () => action());
            }
        }

        /// <summary>
        /// Checks if a property already matches a desired value.  Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] String propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }
            else
            {
                storage = value;
                this.NotifyPropertyChanged(propertyName);
                return true;
            }
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Visual State

        private void GoToVisualState(string visualStateName)
        {
            if (this.Dispatcher.HasThreadAccess)
            {
                VisualStateManager.GoToState(this, visualStateName, false);
            }
            else
            {
                var _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    VisualStateManager.GoToState(this, visualStateName, false);
                });
            }
        }

        #endregion

        #region Logging

        private void Log(string msg, Exception ex = null)
        {
            if (ex != null)
                Debug.WriteLine(DateTime.Now.ToString() + ": " + msg + Environment.NewLine + ex?.ToString());
            else
                Debug.WriteLine(DateTime.Now.ToString() + ": " + msg);
        }

        #endregion

        #endregion
    }
}