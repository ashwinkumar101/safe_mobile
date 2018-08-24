#addin nuget:?package=Cake.FileHelpers
#addin Cake.Issues
#addin Cake.Issues.InspectCode

var COMMON_PROJ = "../../CommonUtils/CommonUtils/CommonUtils.csproj";
var PROJ_SLN_PATH = "../SafeAuthenticator.sln";
var filePath = "../Tests/SafeAuth.Tests.Android/AndroidTestResult.xml";
var buildDirectory = Directory("../");

Func <System.Net.IPAddress, int, string, Task> DownloadTcpTextAsync = (System.Net.IPAddress TCP_LISTEN_HOST,int TCP_LISTEN_PORT,string RESULTS_PATH)=> System.Threading.Tasks.Task.Run (() => 
{
    System.Net.Sockets.TcpListener server = null;
    try
        { 
            server = new System.Net.Sockets.TcpListener(TCP_LISTEN_HOST, TCP_LISTEN_PORT);
            server.Start();
            while (true)
            {
                System.Net.Sockets.TcpClient client = server.AcceptTcpClient();
                System.Net.Sockets.NetworkStream stream = client.GetStream();
                StreamReader data_in = new StreamReader(client.GetStream());
                var result = data_in.ReadToEnd();
                System.IO.File.AppendAllText(RESULTS_PATH, result);
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


Task("Restore-NuGet-Packages")
    .Does(() =>
{
    NuGetRestore(COMMON_PROJ);
    NuGetRestore(PROJ_SLN_PATH);
});

Task("Analyze-Tests")
  .WithCriteria(IsRunningOnWindows())
  .Does(() =>
  {
    var issues = ReadIssues(
      InspectCodeIssuesFromFilePath(filePath),
      buildDirectory);
    
    if(issues.Count()>0) {
      foreach (var item in issues)
      {
        var issueMessage = $"Priority: {item.PriorityName}, Details: {item.Message}, Line: {item.Line}, File: {item.AffectedFileRelativePath}";
        if(AppVeyor.IsRunningOnAppVeyor)
          AppVeyor.AddMessage(item.Message,  AppVeyorMessageCategoryType.Error, issueMessage);
        else
          Information(issueMessage);
      }
      if(AppVeyor.IsRunningOnAppVeyor)
        throw new Exception("Build Failed : InspectCode issues found. Check message tab for details.");
    }
    else {
      if(AppVeyor.IsRunningOnAppVeyor)
        AppVeyor.AddMessage("No code issues.",  AppVeyorMessageCategoryType.Information);
      else
        Information("InspectCode : No code issues.");
    }
  });
