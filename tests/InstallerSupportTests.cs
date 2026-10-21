using System;
using Luma;
class InstallerSupportTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static int Main(){try{
        string path=@"C:\Users\Friend\AppData\Local\Programs\BlueCloakLauncher\蓝斗篷启动.exe";
        Check(StartupRegistration.CommandTargetsExecutable("\""+path+"\" --background",path),"uninstaller recognizes its own quoted login startup entry");
        Check(StartupRegistration.CommandTargetsExecutable("\""+path.ToUpperInvariant()+"\" --background",path),"startup cleanup path comparison is case-insensitive");
        Check(!StartupRegistration.CommandTargetsExecutable("\"C:\\Portable\\蓝斗篷启动.exe\" --background",path),"uninstaller preserves login entries belonging to another portable installation");
        Check(!StartupRegistration.CommandTargetsExecutable(null,path)&&!StartupRegistration.CommandTargetsExecutable("",path),"missing startup entries need no cleanup");
        Program.QaMode=true;bool blocked=false;try{StartupRegistration.RemoveForExecutable(path);}catch(InvalidOperationException){blocked=true;}
        Check(blocked,"installer QA cannot remove live registry or scheduled-task entries");
        return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
