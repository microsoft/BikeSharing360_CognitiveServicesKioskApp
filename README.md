# BikeSharing360_CognitiveServicesKioskApp
BikeSharing360 Cognitive Services Kiosk Demo App



	1. Create an Azure account if you don't already have one
	2. Create Cognitive Services Keys for:
		a. Face SDK
			i. In Azure portal, click + New button
			ii. Type "Cognitive Service APIs" in the search box
			iii. Click  Cognitive Service APIs (preview) in search result
			iv. Click Create button
			v. Give a unique name to the Account Name field (for example, "FaceApiAccount")
			vi. Click Api type to configure required settings
			vii. Pick Face API (preview) from the list of services
			viii. Click Pricing tier to select a proper pricing tier
			ix. Set proper Resource Group option
			x. Click Legal terms to review and agree on the terms
			xi. Click Create button
			xii. Shortly you should receive a notification if the deployment succeeds. Click on the notification should bring you to the service account you just created
			xiii. Click on Keys and take a note of either one of the two keys.
		b. Voice Verification SDK
			i. Similar to how you create the Face Api service, but this time, pick Speacker Recognition APIs (preview) from the list of services.
			ii. Also take note of either one of the two keys.  We will need it in the later steps.
	3. Set up a LUIS model 
		a. See instructions on setting up the LUIS model from the BOT sample project found here: https://github.com/Microsoft/BikeSharing360_BotApps/blob/master/README.md
	4. Create a face verification profile for yourself
		a. Clone the repository https://github.com/Microsoft/Cognitive-Face-Windows
		b. Open Sample-WPF\Controls\FaceIdentificationPage.xaml.cs, change Line 67
		
		        public static readonly string SampleGroupName = Guid.NewGuid().ToString();
		
		to have a more user-friendly name, instead of a GUID
		
		c. Follow the instruction in README.md to build the sample
		d. Run the sample
		e. Click on Subscription Key Management tab, paste in the Face API key you saved earlier
		f. The sample comes with a set of training data under Data/PersonGroup folder in the repository, create a new folder (with a name of your choice), and copy one or more of your profile images into that folder.  Frontal or near-frontal face works the best. Delete other folders in the example data set to reduce the api calls to your Face Api service
		g. Click on Face Identification tab
		h. Click Load PersonGroup button
		i. Select the Data/PersonGroup folder on your local disk
		j. If any of your profile images contains a valid face, the image will show up.  It means your Face profile is registered successfully.
		k. The status pane should also contain the face profile id for each person, take note of your face profile id. Example log:
		
		[12:22:42.547589]: Response: Success. Person "Family1-Dad" (PersonID:9acfe7e1-6196-4230-aed8-a0b172ee2298) created
		[12:22:43.913174]: Request: Creating person "Family1-Daughter"
		[12:22:44.228009]: Response: Success. Person "Family1-Daughter" (PersonID:c32d0abe-ef03-40fc-b50d-66c989a9957e) created
		
		l. You need the group name, and the Face profile Id  
		
	5. Create a voice verification profile for yourself
		a. Clone the repository https://github.com/Microsoft/Cognitive-SpeakerRecognition-Windows
		b. Follow the instruction in README.md to build Verification/SPIDVerficationAPI_WPF_Sample.sln
		c. Run the sample
		d. Paste your Speacker Recognition account key in the Subscription Key Management tab
		e. Click on Scenario 1: Make a new Enrollment tab
		f. Pick one phrases from the ten available phrases
		g. Click Record button then start speaking your chosen phrase via microphone. Click Stop Recording after wards, the status pane should show your phrase if everything works as expected
		
		[12:02:22.651469]: Your phrase: <XXX XXX X XXX>
		
		h. The sample code doesn't show you the Speaker profile id in the status pane.  To work around it, you can click Reset Profile button as soon as it's enabled, the application  will show a message similar to the below in the status pane. Take note of this id.
		
		[12:07:03.877365]: Resetting profile: 54aa9c1d-a815-44b6-9696-26be765dd840
		
		i. Repeat the recoding/stop recording using your chosen phrase until Remaining Enrollments reaches 0.
		j. You need the Speaker profile Id.
		
	6. Open the BikeSharing.Clients.CogServicesKiosk.sln solution
		a. Update the Cognitive Services keys in the App.xaml.cs
		b. Add a row representing yourself in the constructor of the Data\UserLookupServices.cs class
Build and run the application
