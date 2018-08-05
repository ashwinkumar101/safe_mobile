#addin nuget:?package=Cake.Android.Adb&version=3.0.0
#addin nuget:?package=Cake.Android.AvdManager&version=1.0.3
#addin nuget:?package=Cake.FileHelpers
#addin "Cake.Powershell"

var TARGET = Argument("target", "Default");

var IOS_SIM_NAME = EnvironmentVariable("IOS_SIM_NAME") ?? "iPhone 8";
var IOS_SIM_RUNTIME = EnvironmentVariable("IOS_SIM_RUNTIME") ?? "iOS 11.4";
var IOS_PROJ = "../Tests/SafeAuth.Tests.IOS/SafeAuth.Tests.IOS.csproj";
var IOS_BUNDLE_ID = "com.xamarin.essentials.devicetests"; //set bundle ID
var IOS_IPA_PATH = "../Tests/SafeAuth.Tests.IOS/bin/iPhoneSimulator/Release/XamarinEssentialsDeviceTestsiOS.app"; //set correct
var IOS_TEST_RESULTS_PATH = "../Tests/SafeAuth.Tests.IOS/TestResult.xml";

var TCP_LISTEN_TIMEOUT = 60;
var TCP_LISTEN_PORT = 10579;
var TCP_LISTEN_HOST = System.Net.IPAddress.Parse("192.168.0.101");

// Func <Task> DownloadTcpTextAsync = ()=> System.Threading.Tasks.Task.Run (() => 
// {
//     System.Net.Sockets.TcpListener server = null;
//     try
//         { 
//             server = new System.Net.Sockets.TcpListener(TCP_LISTEN_HOST, TCP_LISTEN_PORT);
//             server.Start();
//             while (true)
//             {
//                 System.Net.Sockets.TcpClient client = server.AcceptTcpClient();
//                 System.Net.Sockets.NetworkStream stream = client.GetStream();
//                 StreamReader data_in = new StreamReader(client.GetStream());
//                 var result = data_in.ReadToEnd();
//                 System.IO.File.AppendAllText(ANDROID_TEST_RESULTS_PATH, result);
//                 client.Close();
//                 break;
//             }
//         }
//         catch (System.Net.Sockets.SocketException e)
//         {
//             Information("SocketException: {0}", e);
//         }
//         finally
//         {
//             server.Stop();
//         }
// });


Task ("build-ios")
    .Does (() =>
{
    // Setup the test listener config to be built into the app
    FileWriteText((new FilePath(IOS_PROJ)).GetDirectory().CombineWithFilePath("tests.cfg"), $"{TCP_LISTEN_HOST}:{TCP_LISTEN_PORT}");

    // Nuget restore
    MSBuild (IOS_PROJ, c => {
        c.Configuration = "Release";
        c.Targets.Clear();
        c.Targets.Add("Restore");
    });

    // Build the project (with ipa)
    MSBuild (IOS_PROJ, c => {
        c.Configuration = "Release";
        c.Properties["Platform"] = new List<string> { "iPhoneSimulator" };
        c.Properties["BuildIpa"] = new List<string> { "true" };
        c.Targets.Clear();
        c.Targets.Add("Rebuild");
    });
});


Task("Default")
  .IsDependentOn("build-ios")
  .Does(() => {
  });

RunTarget (TARGET);
