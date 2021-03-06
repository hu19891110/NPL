---++ NPL Debug Engine
| author | LiXizhi | 
| date | 2010.5.10 | 

---+++ Install
Please note, there are extra install steps to complete the installation(either clean or upgrade should apply).

1. install normally to visual studio
2. run visual studio as Administrators, Select menu: Tools->Launch NPL debugger
3. Click register button to complete installation
4. restart visual studio (no administrators required), Tools->Launch NPL debugger,  click Attach to debug any running process
Note1: if you can not attach
1 make sure to click register button as administrators, and restarted vs.
2 you need to wait until the cursor turns to normal cusror before click attach button (and click twice)
3 you are actually attaching to an ParaEngine/NPL process. 

---+++ Overview 
This is a NPL debug engine based on interprocess communication (IPC). 
The visusal studio code is based on the offical SampleEngineWorker from microsoft. Basically, 
	- ProjectLauncher(C#) is a visual studio addon that launch a debugged process, 
	- NPLEngine(C#) implement the main interfaces with visual studio shell.
	- NPLEngineWorker(C++/CLI) implements the low evel debug event system which in turn communicate with the NPL process vis IPC
	- IPCDebugger.lua (lua/NPL) implements the actual debug hook and debug event. 

---+++ Advanced Functions
	- AD7StackFrame::ParseText() is rewritten to support expression evaluation. 

---++++ Code structure
C#/C++ code is mostly based on the Sample Debug Engine provided by microsoft. I only do the following modification. 
- ProjectLauncher(C#) is modified to launch a process in the normal way. 
- SampleEngineWorker(C++/CLI)'s WorkerAPI.cpp is modified to use IPC to translate event from IPCDebugger.lua, instead of using native C++ debug event. 
	- I preserved all native C++ debugging code, and use IsDebuggingNPL() macro to do the switch. 
	- DebuggedProcess.h is also modified, see the NPL region. 
	- symbol engine is not supported. instead we simply make a map between unsigned int fake address to NPL script filename/line pairs. 
	- step into/over/out is supported. 
- ProjectLauncher(C#) is only modified slightly to add Step support(see AD7Engine.cs) and OnStepComplete event(AD7Events.cs)

---+++ Basic functions
- In visual studio, we can launch or attach to a ParaEngine process to debug it. 
- Adding/Removing Breakpoints anytime, anywhere.
- step into/out of functions, step over lines
- Mouse over a local or global variable to see its value dumped. 
- Shift+F9 to bring up the expression window, we can type nested NPL table name like "A.B.C", "main_state" or we can exec a string in the current context like
	"i=1", "log('hello world')", 'i=i+1; return i'. if the expression has a return value, it will be shown in the window. 
- Adding watches is also supported. 
- Currently, we only support debugging one NPL state (usually the main state). To start the debugging, simply call IPCDebugger.StartDebugEngine(); in the NPL state to be debugged. 
	alternatively, we can start it automatically when loading IPCDebugger.lua, provided the command line parameter "debug" is the current NPL state name, such as "main". The queue name can be specified by "debugqueue", which defaults to "NPLDebug"
- Note: there is no performance penalties when starting a debug engine, it only starts a timer to receive from IPC queue. The IPCdebugger only starts the debug hook whenever visual studio attaches or launched the process. 
Use Lib:
-------------------------------------------------------
NPL.load("(gl)script/ide/Debugger/IPCDebugger.lua");
-- start debug engine in the calling NPL state manually.
IPCDebugger.StartDebugEngine();
-- start the debug engine automatically in the "main" state(thread), by loading "IPCDebugger.lua" file and launch with the command line debug="main"
"ParaEngineClient.exe" debug="main" bootstrapper="whateverfile"
-------------------------------------------------------

---+++ debug the debug engine
One need to start the experimental instance of visual studio. 
"D:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe" /RootSuffix Exp

---+++ Known Issues
   - Run as administrators in order to register C++ dll successfully. 
   - Attach process with mixed Native C++/NPL debugging will fail to detach, because the NPL handler thread are paused when the debug engine send the detach message causing the IDE to hang. 
	Avoid using mixed mode debugging, use only NPL debug engine to debug. Select menu Tools->Attach to Process->Select...-->Only check NPL debug engine. 
	Another solution is to use the Project launcher to attach to process. Note: need to press attach twice. 
   - Process2.Attach2("NPLDebugEngineV2") will cause the main thread to hang at delayhlp.cpp, which is pretty strange, press the attach button twice will solve the problem.  This has something to do with DELAYLOAD of dlls
   - IMPORTANT: One needs to wait a while until cursor is turned to normal cursor before clicking the attach button, otherwise attach will not work.  In case of error, try disable and reenable the plugin

---+++ How to upgrade to new version of visual studio
   - open NPLEngine.rgs, add new registry entry for new visual studio version. 
   - increase the version number in source.extension.vsixmanifest file
   - optional: modify CheckRegisterDebugEngine() in Connect.cs to allow auto registration.
   - modify NPLEngineWorker's include and lib to point to latest visual studio DIA SDK path. 

---+++ Changes
	- TODO: Stack view
	- TODO: show a hierarchy of table sub objects in expression evaluation result. 

2015.11.14
	- fixed debug engine dll registration
	- changed dll name and guid. 
	- added a register button for manual registration in case multiple instances are registered. 
	- fixed dll load error for a wrong version of Microsoft.VisualStudio.Shell.Interop.12.0

2014.2.5
	- ported to visual studio 2013

2010.5.15
	- Attach process in vs 2010 is supported in addition to launch process. 
	- stepping into/over/out are fully supported. 
	- IPCDebugger.lua's debug_hook function is optimized to run much faster. 