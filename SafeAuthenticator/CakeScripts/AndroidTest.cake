#addin nuget:?package=Cake.Android.Adb&version=3.0.0
#addin nuget:?package=Cake.Android.AvdManager&version=1.0.3
#addin nuget:?package=Cake.FileHelpers
#addin "Cake.Powershell"

//var TARGET = Argument ("target", "Default");
var COMMON_PROJ = "../../CommonUtils/CommonUtils/CommonUtils.csproj";
var ANDROID_PROJ = "../Tests/SafeAuth.Tests.Android/SafeAuth.Tests.Android.csproj";

var ANDROID_APK_PATH = "../Tests/SafeAuth.Tests.Android/bin/Debug/com.safe.auth.tests-Signed.apk";
var ANDROID_TEST_RESULTS_PATH = "../Tests/SafeAuth.Tests.Android/TestResult.xml";
var ANDROID_AVD = "SafeEmulator";
var ANDROID_PKG_NAME = "com.safe.auth.tests";
var ANDROID_EMU_TARGET = EnvironmentVariable("ANDROID_EMU_TARGET") ?? "system-images;android-26;google_apis;x86";
var ANDROID_EMU_DEVICE = EnvironmentVariable("ANDROID_EMU_DEVICE") ?? "Nexus 6P";

var TCP_LISTEN_TIMEOUT = 60; //set timeout if needed
var TCP_LISTEN_PORT = 10500;

//var ANDROID_HOME = EnvironmentVariable("ANDROID_HOME");
var ANDROID_HOME = EnvironmentVariable("ANDROID_HOME") ?? "ANDROID_HOME";


Func <Task> DownloadTcpTextAsync = ()=> System.Threading.Tasks.Task.Run (() => 
{
    System.Net.Sockets.TcpListener server = null;
    try
        { 
            server = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, TCP_LISTEN_PORT);
            server.Start();
            while (true)
            {
                System.Net.Sockets.TcpClient client = server.AcceptTcpClient();
                System.Net.Sockets.NetworkStream stream = client.GetStream();
                StreamReader data_in = new StreamReader(client.GetStream());
                var result = data_in.ReadToEnd();
                System.IO.File.AppendAllText(ANDROID_TEST_RESULTS_PATH, result);
                client.Close();
                break;
            }
        }
        catch (System.Net.Sockets.SocketException e)
        {
            Information("SocketException: {0}", e);
        }
        finally
        {
            server.Stop();
        }
});



// Task("UnZip-Libs")
// .Does(()=>{

// });


// Task("Clean")
//     .Does(() =>
// {
//     CleanDirectory("./SafeAuthenticator.Android/bin/Debug");
// });

Task("Restore-NuGet-Packages")
    //.IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore(COMMON_PROJ);
    NuGetRestore("../SafeAuthenticator.sln");
});

Task ("build-android")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does (() =>
{
    // Build the app in debug mode
    // needs to be debug so unit tests get discovered
    MSBuild (ANDROID_PROJ, c => {
        c.Configuration = "Debug";
        c.Targets.Clear();
        c.Targets.Add("Rebuild");
    });
});


Task ("test-android-emu")
    .IsDependentOn ("build-android")
    .Does (async() =>
{        
    if (EnvironmentVariable("ANDROID_SKIP_AVD_CREATE") == null) {
        var avdSettings = new AndroidAvdManagerToolSettings  { SdkRoot = ANDROID_HOME };

        // Create the AVD if necessary
        Information ("Creating AVD if necessary: {0}...", ANDROID_AVD);     
        if (!AndroidAvdListAvds (avdSettings).Any (a => a.Name == ANDROID_AVD))
            AndroidAvdCreate (ANDROID_AVD, ANDROID_EMU_TARGET, ANDROID_EMU_DEVICE, force: true, settings: avdSettings);
    }
    // We need to find `emulator` and the best way is to try within a specified ANDROID_HOME
    var emulatorExt = IsRunningOnWindows() ? ".exe" : "";
    string emulatorPath = "emulator" + emulatorExt;
            
    if (ANDROID_HOME != null) {
        var andHome = new DirectoryPath(ANDROID_HOME);
        if (DirectoryExists(andHome)) {
            emulatorPath = MakeAbsolute(andHome.Combine("tools").CombineWithFilePath("emulator" + emulatorExt)).FullPath;
            if (!FileExists(emulatorPath))
                emulatorPath = MakeAbsolute(andHome.Combine("emulator").CombineWithFilePath("emulator" + emulatorExt)).FullPath;
            if (!FileExists(emulatorPath))
                emulatorPath = "emulator" + emulatorExt;
        }
    }
         
    // Start up the emulator by name
    var emu = StartAndReturnProcess (emulatorPath, new ProcessSettings { 
        Arguments = $"-avd {ANDROID_AVD}" });
        var adbSettings = new AdbToolSettings { SdkRoot = ANDROID_HOME };
        
        
        // Keep checking adb for an emulator with an AVD name matching the one we just started
        var emuSerial = string.Empty;
        for (int i = 0; i < 100; i++) {
        foreach (var device in AdbDevices(adbSettings).Where(d => d.Serial.StartsWith("emulator-"))) {
            if (AdbGetAvdName(device.Serial).Equals(ANDROID_AVD, StringComparison.OrdinalIgnoreCase)) {
                emuSerial = device.Serial;
                break;
            }
        }

        if (!string.IsNullOrEmpty(emuSerial))
            break;
        else
            System.Threading.Thread.Sleep(1000);
    }

    Information ("Matched ADB Serial: {0}", emuSerial);
    adbSettings = new AdbToolSettings { SdkRoot = ANDROID_HOME, Serial = emuSerial };

    // Wait for the emulator to enter a 'booted' state
    AdbWaitForEmulatorToBoot(TimeSpan.FromSeconds(100), adbSettings);
    Information ("Emulator finished booting.");

    // Try uninstalling the existing package (if installed)
    try { 
        AdbUninstall (ANDROID_PKG_NAME, false, adbSettings);
        Information ("Uninstalled old: {0}", ANDROID_PKG_NAME);
    } catch { }

    // Use the Install target to push the app onto emulator
    MSBuild (ANDROID_PROJ, c => {
        c.Configuration = "Debug";
        c.Properties["AdbTarget"] = new List<string> { "-s " + emuSerial };
        c.Targets.Clear();
        c.Targets.Add("Install");
    });

    //start the TCP Test results listener
    Information("Started TCP Test Results Listener on port: {0}", TCP_LISTEN_PORT);
    var tcpListenerTask = DownloadTcpTextAsync();
    // Launch the app on the emulator
    AdbShell ($"am start -n {ANDROID_PKG_NAME}/{ANDROID_PKG_NAME}.MainActivity", adbSettings);    

    // AdbShell("monkey -p com.safe.auth.tests -c android.intent.category.LAUNCHER 1",adbSettings);
        
    // // Wait for the test results to come back
    Information("Waiting for tests...");
    tcpListenerTask.Wait ();

    //AddPlatformToTestResults(ANDROID_TEST_RESULTS_PATH, "Android");

    // Close emulator    
    emu.Kill();

})
.Finally(() =>
  {  
    var resultsFile = File(ANDROID_TEST_RESULTS_PATH);
    if(AppVeyor.IsRunningOnAppVeyor)
    {
      AppVeyor.UploadTestResults(resultsFile.Path.FullPath, AppVeyorTestResultsType.MSTest);
    }
  });



// Task("Default")
//   .IsDependentOn("test-android-emu")
//   .Does(() => {
//   });

// RunTarget (TARGET);
