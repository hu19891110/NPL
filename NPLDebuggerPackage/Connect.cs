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
            System.Diagnostics.Process.Start("regsvr32", "\"" + sDebuggerPath + "\" /s");
            MessageBox.Show(String.Format("Successfully registered NPL debugger at {0}. Please restart Visual Studio", sDebuggerPath));
        }

        /// <summary>
        /// register the NPL debug engine if not. 
        /// </summary>
        static public void CheckRegisterDebugEngine()
        {
            bool bIsNPLEngineRegistered = false;
            String sNPLDebuggerKey;
            {
                sNPLDebuggerKey = "Software\\Microsoft\\VisualStudio\\14.0\\AD7Metrics\\Engine\\{0B18F022-A5F5-41EA-8532-4CF3B894A7C6}";
                using (RegistryKey setupKey = Registry.LocalMachine.OpenSubKey(sNPLDebuggerKey))
                {
                    if (setupKey != null)
                    {
                        bIsNPLEngineRegistered = true;
                    }
                }
            }
            {
                sNPLDebuggerKey = "Software\\Microsoft\\VisualStudio\\12.0\\AD7Metrics\\Engine\\{0B18F022-A5F5-41EA-8532-4CF3B894A7C6}";
                using (RegistryKey setupKey = Registry.LocalMachine.OpenSubKey(sNPLDebuggerKey))
                {
                    if (setupKey != null)
                    {
                        bIsNPLEngineRegistered = true;
                    }
                }
            }
            {
                sNPLDebuggerKey = "Software\\Microsoft\\VisualStudio\\10.0\\AD7Metrics\\Engine\\{0B18F022-A5F5-41EA-8532-4CF3B894A7C6}";
                using (RegistryKey setupKey = Registry.LocalMachine.OpenSubKey(sNPLDebuggerKey))
                {
                    if (setupKey != null)
                    {
                        bIsNPLEngineRegistered = true;
                    }
                }
            }
            if(!bIsNPLEngineRegistered)
            {
                RegisterNPLDebugEngineDll();
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
                        // Tricky: Attach2 will cause the main thread to hang at delayhlp.cpp, which is pretty strange, press the attach button twice will solve the problem. This has something to do with DELAYLOAD of dlls
                        // To workaround, we will call Attach2 in a separate thread. 

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