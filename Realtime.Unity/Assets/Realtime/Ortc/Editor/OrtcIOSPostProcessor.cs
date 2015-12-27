using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Assets.Realtime.Ortc.Editor
{
    public class OrtcIOSPostProcessor
    {
        [PostProcessBuild]
        public static void ChangeXcodePlist(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget == BuildTarget.iOS)
            {
                // Get plist
                string plistPath = pathToBuiltProject + "/Info.plist";
                PlistDocument plist = new PlistDocument();
                plist.ReadFromString(File.ReadAllText(plistPath));

                // Get root
                PlistElementDict rootDict = plist.root;

                // IOS9 security
                var bgModes = rootDict.CreateDict("NSAppTransportSecurity");
                bgModes.SetBoolean("NSAllowsArbitraryLoads", true);
                var exModes = bgModes.CreateDict("NSExceptionDomains");
                var rt = exModes.CreateDict("realtime.co");
                rt.SetBoolean("NSIncludesSubdomains", true);
                rt.SetBoolean("NSTemporaryExceptionAllowsInsecureHTTPLoads", true);

                //Obj Linker
                string projPath = pathToBuiltProject + "/Unity-iPhone.xcodeproj/project.pbxproj";
                PBXProject proj = new PBXProject();
                proj.ReadFromString(File.ReadAllText(projPath));
                string targetGUID = proj.TargetGuidByName("Unity-iPhone");
                proj.AddBuildProperty(targetGUID, "OTHER_LDFLAGS", "-ObjC");
                File.WriteAllText(projPath, proj.WriteToString());

                //libicucore
                proj.AddFileToBuild(targetGUID, proj.AddFile("usr/lib/libicucore.tbd", "Framework/libicucore.tbd", PBXSourceTree.Sdk));
                proj.AddFileToBuild(targetGUID, proj.AddFile("usr/lib/libicucore.tbd", "Framework/libicucore.tbd", PBXSourceTree.Build));

                proj.AddFrameworkToProject(targetGUID, "Security.framework", true);
                proj.AddFrameworkToProject(targetGUID, "libicucore.tbd", true);

                // Write to file
                File.WriteAllText(plistPath, plist.WriteToString());
            }
        }
    }
}