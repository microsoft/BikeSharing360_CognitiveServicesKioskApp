# BikeSharing360

During our Connect(); event this year we presented 15 demos in Scott Guthrie’s and Scott Hanselman’s keynotes. If you missed the keynotes, you can watch the recording in [Channel 9](https://channel9.msdn.com/Events/Connect/2016/Keynotes-Scott-Guthrie-and-Scott-Hanselman).

This year, we built the technology stack for a fictional company named BikeSharing360, which allows users to rent bikes from one location to another.

BikeSharing360 is a fictitious example of a smart bike sharing system with 10,000 bikes distributed in 650 stations located throughout New York City and Seattle. Their vision is to provide a modern and personalized experience to riders and to run their business with intelligence.

In this demo scenario, we built several apps for both the enterprise and the consumer (bike riders). You can find all other BikeSharing360 repos in the following locations:

* [Mobile Apps](https://github.com/Microsoft/BikeSharing360_MobileApps)
* [Backend Services](https://github.com/Microsoft/BikeSharing360_BackendServices)
* [Websites](https://github.com/Microsoft/BikeSharing360_Websites)
* [Single Container Apps](https://github.com/Microsoft/BikeSharing360_SingleContainer)
* [Multi Container Apps](https://github.com/Microsoft/BikeSharing360_MultiContainer)
* [Cognitive Services Kiosk App](https://github.com/Microsoft/BikeSharing360_CognitiveServicesKioskApp)
* [Azure Bot App](https://github.com/Microsoft/BikeSharing360_BotApps)

# BikeSharing360 Modern Kiosk with Cognitive Services

During Connect(); 2016 we showcased many technologies available to you as a developer across Azure, Office, Windows, Visual Studio and Visual Studio Team Services. We’ve also heard from you that you love to have real-world applications through which you can directly experience what’s possible using those technologies. This year, then, we built out a full bikerider scenario for our Connect(); 2016 demos and are delighted to share all the source code with you.

**Note:** This document is about the **Kiosk app** only.

This kiosk app leveraged Cognitive Services to enable customers to interact with the kiosk through face detection, face recognition, voice verification, text-to-speech, speech-to-text, language understanding, and emotion detection, which allowed them to complete a transaction without the need for traditional input or touch or even pulling out your wallet.  

![BikeRider](Images/hero_image.png)

## Requirements
* Windows 10
* [Visual Studio __2015__](https://www.visualstudio.com/en-us/products/vs-2015-product-editions.aspx) Update 3 (14.0 or higher) to compile C# 6 language features (or Visual Studio MacOS)
* Microsoft Azure subscription

## Screens
![Screenshot 1](Images/Screenshot1.png)
![Screenshot 2](Images/Screenshot2.png)

## Setup
Download or clone the repository. 

1. Create an Azure account if you don't already have one using the steps in the next section of this README.
1. Create a Cognitive Service Key for the Face SDK:
	1. In Azure portal, click + New button
	1. Type "Cognitive Service APIs" in the search box
	1. Click  Cognitive Service APIs (preview) in search result
	1. Click Create button
	1. Give a unique name to the Account Name field (for example, "FaceApiAccount")
	1. Click Api type to configure required settings
	1. Pick Face API (preview) from the list of services
	1. Click Pricing tier to select a proper pricing tier
	1. Set proper Resource Group option
	1. Click Legal terms to review and agree on the terms
	1. Click Create button
	1. Shortly you should receive a notification if the deployment succeeds. Click on the notification should bring you to the service account you just created
	1. Click on Keys and take a note of either one of the two keys.
1. Create a Cognitive Service Key for the Voice Verification SDK
	1. Similar to how you create the Face Api service, but this time, pick Speacker Recognition APIs (preview) from the list of services.
	1. Take note of either one of the two keys.  We will need it in the later steps.
1. Set up a LUIS model using the [instructions on setting up the LUIS model from the BOT sample]( https://github.com/Microsoft/BikeSharing360_BotApps)
1. Create a face verification profile for yourself
	1. Clone the repository [https://github.com/Microsoft/Cognitive-Face-Windows](https://github.com/Microsoft/Cognitive-Face-Windows)
	1. Open `Sample-WPF\Controls\FaceIdentificationPage.xaml.cs`, change Line 67 to have a more user-friendly name, instead of a GUID. 
	
			public static readonly string SampleGroupName = Guid.NewGuid().ToString();
	
	1. Follow the instruction in README.md to build the sample
	1. Run the sample
	1. Click on Subscription Key Management tab, paste in the Face API key you saved earlier
	1. The sample comes with a set of training data under Data/PersonGroup folder in the repository, create a new folder (with a name of your choice), and copy one or more of your profile images into that folder.  Frontal or near-frontal face works the best. Delete other folders in the example data set to reduce the api calls to your Face Api service
	1. Click on Face Identification tab
	1. Click Load PersonGroup button
	1. Select the Data/PersonGroup folder on your local disk
	1. If any of your profile images contains a valid face, the image will show up.  It means your Face profile is registered successfully.
	1. The status pane should also contain the face profile id for each person, take note of your face profile id. 
	
		Example log:
	
		```
		[12:22:42.547589]: Response: Success. Person "Family1-Dad" (PersonID:9acfe7e1-6196-4230-aed8-a0b172ee2298) created
		[12:22:43.913174]: Request: Creating person "Family1-Daughter"
		[12:22:44.228009]: Response: Success. Person "Family1-Daughter" (PersonID:c32d0abe-ef03-40fc-b50d-66c989a9957e) created
		```
	
	1. You need the group name, and the Face profile Id  
	
1. Create a voice verification profile for yourself
	1. Clone the repository https://github.com/Microsoft/Cognitive-SpeakerRecognition-Windows
	1. Follow the instruction in README.md to build Verification/SPIDVerficationAPI_WPF_Sample.sln
	1. Run the sample
	1. Paste your Speacker Recognition account key in the Subscription Key Management tab
	1. Click on Scenario 1: Make a new Enrollment tab
	1. Pick one phrases from the ten available phrases
	1. Click Record button then start speaking your chosen phrase via microphone. Click Stop Recording after wards, the status pane should show your phrase if everything works as expected:
	
		```
		[12:02:22.651469]: Your phrase: <XXX XXX X XXX>
		```

	1. The sample code doesn't show you the Speaker profile id in the status pane.  To work around it, you can click Reset Profile button as soon as it's enabled, the application  will show a message similar to the below in the status pane. Take note of this id.
	
		```
		[12:07:03.877365]: Resetting profile: 54aa9c1d-a815-44b6-9696-26be765dd840
		```

	1. Repeat the recoding/stop recording using your chosen phrase until Remaining Enrollments reaches 0.
	1. You need the Speaker profile Id.
	
6. Open the BikeSharing.Clients.CogServicesKiosk.sln solution
	a. Update the Cognitive Services keys in the App.xaml.cs
	b. Add a row representing yourself in the constructor of the Data\UserLookupServices.cs class
Build and run the application

## Running the demo
You can find the steps to run through the demo script found in the **Documents** folder of this repo.

Enjoy!

## How to sign up for Microsoft Azure

You need an Azure account to work with this demo code. You can:

- [Create a new Azure account, and try Cognitive Services for free.](https://azure.microsoft.com/free/cognitive-services/) You get credits that can be used to try out paid Azure services. Even after the credits are used up, you can keep the account and use free Azure services and features, such as the Web Apps feature in Azure App Service.
- [Activate Visual Studio subscriber benefits](https://www.visualstudio.com/products/visual-studio-dev-essentials-vs). Your Visual Studio subscription gives you credits every month that you can use for paid Azure services.
- Not a Visual Studio subscriber? Get a $25 monthly Azure credit by joining [Visual Studio Dev Essentials](https://www.visualstudio.com/products/visual-studio-dev-essentials-vs).

## Blogs posts

Here's links to blog posts related to this project:

- Xamarin Blog: [Microsoft Connect(); 2016 Recap](https://blog.xamarin.com/microsoft-connect-2016-recap/)
- The Visual Studio Blog: [Announcing the new Visual Studio for Mac](https://blogs.msdn.microsoft.com/visualstudio/2016/11/16/visual-studio-for-mac/)
- The Visual Studio Blog: [Introducing Visual Studio Mobile Center (Preview)](https://blogs.msdn.microsoft.com/visualstudio/2016/11/16/visual-studio-mobile-center/)
- The Visual Studio Blog: [Visual Studio 2017 Release Candidate](https://blogs.msdn.microsoft.com/visualstudio/2016/11/16/visual-studio-2017-rc/)

## Clean and Rebuild
If you see build issues when pulling updates from the repo, try cleaning and rebuilding the solution.

## Copyright and license
* Code and documentation copyright 2016 Microsoft Corp. Code released under the [MIT license](https://opensource.org/licenses/MIT).

## Code of Conduct 
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
