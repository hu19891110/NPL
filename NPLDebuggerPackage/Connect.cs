using System;
using EnvDTE;
using EnvDTE80;
using System.Resources;
using System.Reflection;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ParaEngine.NPLDebuggerPackage
{
	/// <summary>The object for implementing an Add-in.</summary>
	/// <seealso class='IDTExtensibility2' />
	public class NPLDebuggerConnect
	{
        public bool IsConnected = false;
        private NPLDebuggerPackage package;

        public NPLDebuggerConnect(object application, NPLDebuggerPackage package_)
		{
            _applicationObject = (DTE2)application;
            package = package_;

            CheckRegisterDebugEngine();
		}

        public static void RegisterNPLDebugEngineDll()
        {
            String sDebuggerPath = NPLDebuggerPackage.PackageRootPath + "Microsoft.VisualStudio.Debugger.NPLEngineWorker.dll";
            // System.Diagnostics.Process.Start("regsvr32", "\"" + sDebuggerPath + "\" /s");
            System.Diagnostics.Process.Start("regsvr32", "\"" + sDebuggerPath + "\"");
            MessageBox.Show(String.Format("If you have successfully registered NPL debugger at {0}. Please restart Visual Studio", sDebuggerPath));
        }

        static public string GetRegisteredDebuggerWorkerDllPath()
        {
            string [] vsVersions = new string[] { "14.0", "12.0", "10.0" };

            foreach(string version in vsVersions)
            {
                using (RegistryKey setupKey = Registry.LocalMachine.OpenSubKey(String.Format("Software\\Microsoft\\VisualStudio\\{0}\\AD7Metrics\\Engine\\{{0B18F022-A5F5-41EA-8532-4CF3B894A7C6}}", version)))
                {
                    if (setupKey != null)
                    {
                        string sCLSID = (string)setupKey.GetValue("CLSID");
                        string sProgramProvider = (string)setupKey.GetValue("ProgramProvider");
                        using (RegistryKey clsidKey = Registry.LocalMachine.OpenSubKey(String.Format("Software\\Microsoft\\VisualStudio\\{0}\\CLSID\\{1}", version, sCLSID)))
                        {
                            return (string)clsidKey.GetValue("CodeBase");
                        }
                    }
                }
            }
            return null;
        }
        /// <summary>
        /// register the NPL debug engine if not. 
        /// </summary>
        static public void CheckRegisterDebugEngine()
        {
            string sFilePath = GetRegisteredDebuggerWorkerDllPath();
            
            if(sFilePath!=null)
            {
                if(!sFilePath.StartsWith(NPLDebuggerPackage.PackageRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(String.Format("Currently registered NPL debugger dll at {0} is not the one at {1}. Please click `Register` button to register the latest version. If you still see this message after registration, you need to disable this plugin, restart visual studio and enable the plugin again.", sFilePath, NPLDebuggerPackage.PackageRootPath));
                }
            }
            else if (sFilePath == null)
            {
                MessageBox.Show("Please click `Register` button to register the latest NPL debugger");
            }
        }

        /// <summary>
        /// Launch the project selection form for the addin. Called from the Exec method above.
        /// </summary>
        public void DisplayLaunchForm()
        {
            // Show the form.
            LaunchForm lf = new LaunchForm(_applicationObject);
            System.Windows.Forms.DialogResult result = lf.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // The user clicked on Ok in the form, so launch the file using the sample debug engine.
                LaunchDebugTarget(lf.Command, lf.CommandArguments, lf.WorkingDir);
            }
            else if (result == System.Windows.Forms.DialogResult.Yes)
            {
                AttachDebugTarget(lf.SelectedProcessID);
            }
        }

        /// <summary>
        /// Attach to a process. 
        /// </summary>
        /// <param name="sProcessName"></param>
        public void AttachDebugTarget(int nProcessID)
        {
            Microsoft.VisualStudio.Shell.ServiceProvider sp =
                new Microsoft.VisualStudio.Shell.ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)_applicationObject);
            IVsDebugger dbg = (IVsDebugger)sp.GetService(typeof(SVsShellDebugger));
            
            foreach (Process lLocalProcess in _applicationObject.Debugger.LocalProcesses)
            {
                if (lLocalProcess.ProcessID == nProcessID)
                {
                    try
                    {
                        if(true)
                        {
                            var myThread = new System.Threading.Thread(() =>
                            {
                                (lLocalProcess as Process2).Attach2("NPLDebugEngineV2");
                                Console.Write("NPLDebugEngineV2 attached");
                            });
                            myThread.Start();
                        }
                        else
                        {
                            //strange: this function hangs 
                            // In VS,  TOOLS -> Options... -> Debugging -> General and checked Use Managed Compatibility Mode option:
                            (lLocalProcess as Process2).Attach2("NPLDebugEngineV2");
                        }
                    }
                    catch (Exception err)
                    {
                        Console.Write(err.ToString());
                        MessageBox.Show(String.Format("failed to attach to process {0}. because {1}. You many need to register the debug engine.", nProcessID, err.ToString()));
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Launch an executable using the sample debug engine.
        /// </summary>
        public void LaunchDebugTarget(string command, string arguments, string workingDir)
        {
           Microsoft.VisualStudio.Shell.ServiceProvider sp =
                new Microsoft.VisualStudio.Shell.ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)_applicationObject);

            IVsDebugger dbg = (IVsDebugger)sp.GetService(typeof(SVsShellDebugger));

            VsDebugTargetInfo info = new VsDebugTargetInfo();
            info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);
            info.dlo = Microsoft.VisualStudio.Shell.Interop.DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;

            info.bstrExe = command;
            info.bstrCurDir = String.IsNullOrEmpty(workingDir) ? System.IO.Path.GetDirectoryName(info.bstrExe) : workingDir;
            info.bstrArg = arguments; // command line parameters
            info.bstrRemoteMachine = null; // debug locally
            info.fSendStdoutToOutputWindow = 0; // Let stdout stay with the application.
            info.clsidCustom = new Guid("{0B18F022-A5F5-41EA-8532-4CF3B894A7C6}"); // Set the launching engine the sample engine guid
            info.grfLaunch = 0;

            IntPtr pInfo = System.Runtime.InteropServices.Marshal.AllocCoTaskMem((int)info.cbSize);
            System.Runtime.InteropServices.Marshal.StructureToPtr(info, pInfo, false);

            try
            {
                dbg.LaunchDebugTargets(1, pInfo);
            }
            finally
            {
                if (pInfo != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pInfo);
                }
            }

        }
		private DTE2 _applicationObject;
	}
}