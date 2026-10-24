using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace EasyJava2 {
public sealed class CommandResult {
 public string Output {get;internal set;}
 public string Error {get;internal set;}
 public int ExitCode {get;internal set;}
}

public static class CommandRunner {
 public const int MaxStreamCharacters=1000000;
 const uint CreateSuspended=0x4,CreateNoWindow=0x08000000,ExtendedStartupInfoPresent=0x80000;
 const uint StartUseStdHandles=0x100,KillOnJobClose=0x2000;
 const int JobExtendedLimitInformation=9;

 public static async Task<CommandResult> Run(string shell,string command,string workingDirectory,double timeoutSeconds,CancellationToken token){
  if(shell!="cmd"&&shell!="powershell")throw new ArgumentException("Wybierz CMD albo PowerShell.","shell");
  if(string.IsNullOrWhiteSpace(command))throw new ArgumentException("Wpisz polecenie do wykonania.","command");
  if(double.IsNaN(timeoutSeconds)||double.IsInfinity(timeoutSeconds)||timeoutSeconds<1||timeoutSeconds>3600)throw new ArgumentException("Limit polecenia musi wynosić od 1 do 3600 sekund.","timeoutSeconds");
  string folder=string.IsNullOrWhiteSpace(workingDirectory)?Environment.GetFolderPath(Environment.SpecialFolder.UserProfile):Path.GetFullPath(workingDirectory);
  if(!Directory.Exists(folder))throw new DirectoryNotFoundException("Nie istnieje folder roboczy polecenia: "+folder);
  token.ThrowIfCancellationRequested();
  string executable=Path.Combine(Environment.SystemDirectory,shell=="cmd"?"cmd.exe":@"WindowsPowerShell\v1.0\powershell.exe");
  if(!File.Exists(executable))throw new FileNotFoundException("Nie znaleziono programu "+(shell=="cmd"?"CMD":"Windows PowerShell")+".",executable);
  using(CommandScript batchScript=CommandScript.Create(shell,command)){
  if(batchScript.ScriptPath!=null)command="\""+batchScript.ScriptPath+"\"";
  string arguments;
  Encoding encoding;
  if(shell=="cmd"){
   // /U gives CMD's built-in commands a predictable Unicode output encoding.
   arguments="/D /U /S /C \""+command+"\"";encoding=Encoding.Unicode;
  }else{
   string script="$ProgressPreference = 'SilentlyContinue'\r\n$OutputEncoding = [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)\r\n"+
    "$ErrorActionPreference = 'Stop'\r\n$global:LASTEXITCODE = 0\r\ntry {\r\n& {\r\n"+command+
    "\r\n}\r\nif ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }\r\n} catch {\r\n[Console]::Error.WriteLine($_.ToString())\r\nexit 1\r\n}\r\n";
   arguments="-NoLogo -NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script));encoding=new UTF8Encoding(false,false);
  }
  string commandLine="\""+executable+"\" "+arguments;
  if(commandLine.Length>=32767)throw new ArgumentException("Polecenie jest za długie dla Windows. Skróć je albo wywołaj osobny plik.","command");
  using(NativeCommand running=NativeCommand.Start(executable,commandLine,folder))
  using(CancellationTokenSource finished=new CancellationTokenSource()){
   Task<string> output=Capture(running.Output,encoding,finished.Token),error=Capture(running.Error,encoding,finished.Token);
   Stopwatch watch=Stopwatch.StartNew();bool cancelled=false,timedOut=false;Exception failure=null;int exitCode=-1;
   try{
    running.Resume();
    while(true){
     if(token.IsCancellationRequested){cancelled=true;break;}
     if(watch.Elapsed.TotalSeconds>=timeoutSeconds){timedOut=true;break;}
     uint state=WaitForSingleObject(running.Process.DangerousGetHandle(),0);
     if(state==0){uint code;if(!GetExitCodeProcess(running.Process.DangerousGetHandle(),out code))throw NativeError("Nie można odczytać wyniku polecenia.");exitCode=unchecked((int)code);break;}
     if(state==0xffffffff)throw NativeError("Nie można sprawdzić stanu polecenia.");
     await Task.Delay(20).ConfigureAwait(false);
    }
   }catch(Exception ex){failure=ex;}
   // A successful shell may intentionally launch an application using start or
   // Start-Process. Detach its children; only an active/aborted command is killed.
   if(!cancelled&&!timedOut&&failure==null){try{running.DetachChildren();}catch(Exception ex){failure=ex;}}
   running.CloseJob();finished.Cancel();
   string stdout="",stderr="";Exception captureFailure=null;
   try{string[] captured=await Task.WhenAll(output,error).ConfigureAwait(false);stdout=captured[0];stderr=captured[1];}catch(Exception ex){captureFailure=ex;}
   if(cancelled)throw new OperationCanceledException(token);
   if(timedOut)throw new TimeoutException("Polecenie przekroczyło limit "+timeoutSeconds.ToString("0.##",System.Globalization.CultureInfo.GetCultureInfo("pl-PL"))+" s i zostało zatrzymane.");
   if(failure!=null)throw failure;
   if(captureFailure!=null)throw new IOException("Nie można odczytać wyniku polecenia.",captureFailure);
   token.ThrowIfCancellationRequested();
   return new CommandResult{Output=stdout,Error=stderr,ExitCode=exitCode};
  }
  }
 }

 // CMD /C reads only the first line of a direct command string. A UTF-8 batch
 // file preserves multi-line syntax; its first ASCII line selects UTF-8 before
 // CMD reads the following Unicode command lines. Single-line commands retain
 // normal interactive /C syntax, including FOR variables written as %i.
 sealed class CommandScript:IDisposable {
  public string ScriptPath;
  string directory;
  public static CommandScript Create(string shell,string command){
   CommandScript script=new CommandScript();
   if(shell!="cmd"||(command.IndexOf('\r')<0&&command.IndexOf('\n')<0))return script;
   script.directory=Path.Combine(Path.GetTempPath(),"EasyJava-CMD-"+Guid.NewGuid().ToString("N"));
   script.ScriptPath=Path.Combine(script.directory,"command.cmd");
   try{
    Directory.CreateDirectory(script.directory);
    string lines=command.Replace("\r\n","\n").Replace('\r','\n').Replace("\n","\r\n");
    byte[] bytes=new UTF8Encoding(false).GetBytes("@chcp 65001 >nul\r\n@echo off\r\n"+lines+"\r\n");
    using(FileStream file=new FileStream(script.ScriptPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read))file.Write(bytes,0,bytes.Length);
    return script;
   }catch{script.Dispose();throw;}
  }
  public void Dispose(){
   // Never recurse: if a command leaves additional files in this unique folder,
   // keep those files rather than deleting data created by the command.
   if(ScriptPath!=null)try{File.Delete(ScriptPath);}catch(IOException){}catch(UnauthorizedAccessException){}
   if(directory!=null)try{Directory.Delete(directory,false);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
 }

 // Peek before reading so a detached child retaining its standard handles cannot
 // hold the editor open. Keep draining after the capture limit to avoid pipe stalls.
 static async Task<string> Capture(SafeFileHandle handle,Encoding encoding,CancellationToken finished){
  byte[] bytes=new byte[8192];char[] chars=new char[encoding.GetMaxCharCount(bytes.Length)];Decoder decoder=encoding.GetDecoder();StringBuilder text=new StringBuilder();Stopwatch tail=null;bool truncated=false;
  while(true){
   if(finished.IsCancellationRequested){if(tail==null)tail=Stopwatch.StartNew();if(tail.ElapsedMilliseconds>=250)break;}
   uint available;
   if(!PeekNamedPipe(handle.DangerousGetHandle(),IntPtr.Zero,0,IntPtr.Zero,out available,IntPtr.Zero)){
    int code=Marshal.GetLastWin32Error();if(code==109||code==232)break;throw new Win32Exception(code,"Nie można odczytać wyjścia polecenia.");
   }
   if(available==0){if(finished.IsCancellationRequested)break;await Task.Delay(10).ConfigureAwait(false);continue;}
   uint read;
   if(!ReadFile(handle.DangerousGetHandle(),bytes,(uint)Math.Min(bytes.Length,(long)available),out read,IntPtr.Zero)){
    int code=Marshal.GetLastWin32Error();if(code==109||code==232)break;throw new Win32Exception(code,"Nie można odczytać wyjścia polecenia.");
   }
   if(read==0)break;
   int count=decoder.GetChars(bytes,0,(int)read,chars,0,false);truncated|=AppendBounded(text,chars,count);
   // Let cancellation and the other stream run even if a command continuously
   // produces more data than fits in an anonymous pipe.
   await Task.Yield();
  }
  truncated|=AppendBounded(text,chars,decoder.GetChars(new byte[0],0,0,chars,0,true));if(truncated){if(text.Length>0&&char.IsHighSurrogate(text[text.Length-1]))text.Length--;text.Append("\r\n[Wynik skrócony do 1 000 000 znaków.]");}return text.ToString();
 }
 static bool AppendBounded(StringBuilder builder,char[] chars,int count){int room=MaxStreamCharacters-builder.Length;if(room>0)builder.Append(chars,0,Math.Min(room,count));return count>room;}
 static Exception NativeError(string message){return new Win32Exception(Marshal.GetLastWin32Error(),message);}

 sealed class NativeCommand:IDisposable {
  public SafeFileHandle Process,Output,Error;
  SafeFileHandle thread,job;
  bool resumed;
  NativeCommand(){}
  public static NativeCommand Start(string executable,string commandLine,string directory){
   NativeCommand result=new NativeCommand();SafeFileHandle outputWrite=null,errorWrite=null,input=null;IntPtr attributes=IntPtr.Zero,inheritList=IntPtr.Zero;bool attributesInitialized=false;
   try{
    result.job=new SafeFileHandle(CreateJobObject(IntPtr.Zero,null),true);if(result.job.IsInvalid)throw NativeError("Nie można przygotować procesu polecenia.");
    result.SetJobLimits(KillOnJobClose);
    SecurityAttributes security=new SecurityAttributes{Length=Marshal.SizeOf(typeof(SecurityAttributes)),InheritHandle=true};
    IntPtr read,write;if(!CreatePipe(out read,out write,ref security,0))throw NativeError("Nie można utworzyć wyjścia polecenia.");result.Output=new SafeFileHandle(read,true);outputWrite=new SafeFileHandle(write,true);
    if(!SetHandleInformation(read,1,0))throw NativeError("Nie można przygotować wyjścia polecenia.");
    if(!CreatePipe(out read,out write,ref security,0))throw NativeError("Nie można utworzyć wyjścia błędów.");result.Error=new SafeFileHandle(read,true);errorWrite=new SafeFileHandle(write,true);
    if(!SetHandleInformation(read,1,0))throw NativeError("Nie można przygotować wyjścia błędów.");
    input=new SafeFileHandle(CreateFile("NUL",0x80000000,3,ref security,3,0,IntPtr.Zero),true);if(input.IsInvalid)throw NativeError("Nie można zamknąć wejścia polecenia.");
    IntPtr size=IntPtr.Zero;InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);attributes=Marshal.AllocHGlobal(size);
    if(!InitializeProcThreadAttributeList(attributes,1,0,ref size))throw NativeError("Nie można przygotować uchwytów polecenia.");
    attributesInitialized=true;
    inheritList=Marshal.AllocHGlobal(IntPtr.Size*3);Marshal.WriteIntPtr(inheritList,0,input.DangerousGetHandle());Marshal.WriteIntPtr(inheritList,IntPtr.Size,outputWrite.DangerousGetHandle());Marshal.WriteIntPtr(inheritList,IntPtr.Size*2,errorWrite.DangerousGetHandle());
    if(!UpdateProcThreadAttribute(attributes,0,new IntPtr(0x20002),inheritList,new IntPtr(IntPtr.Size*3),IntPtr.Zero,IntPtr.Zero))throw NativeError("Nie można przekazać wyjścia do polecenia.");
    StartupInfoEx startup=new StartupInfoEx();startup.StartupInfo.Size=Marshal.SizeOf(typeof(StartupInfoEx));startup.StartupInfo.Flags=StartUseStdHandles;startup.StartupInfo.StandardInput=input.DangerousGetHandle();startup.StartupInfo.StandardOutput=outputWrite.DangerousGetHandle();startup.StartupInfo.StandardError=errorWrite.DangerousGetHandle();startup.Attributes=attributes;
    ProcessInformation process;
    if(!CreateProcess(executable,new StringBuilder(commandLine),IntPtr.Zero,IntPtr.Zero,true,CreateSuspended|CreateNoWindow|ExtendedStartupInfoPresent,IntPtr.Zero,directory,ref startup,out process))throw NativeError("Nie można uruchomić polecenia.");
    result.Process=new SafeFileHandle(process.Process,true);result.thread=new SafeFileHandle(process.Thread,true);
    if(!AssignProcessToJobObject(result.job.DangerousGetHandle(),result.Process.DangerousGetHandle()))throw NativeError("Nie można objąć polecenia obsługą zatrzymywania.");
    return result;
   }catch{result.Dispose();throw;}
   finally{
    if(outputWrite!=null)outputWrite.Dispose();if(errorWrite!=null)errorWrite.Dispose();if(input!=null)input.Dispose();
    if(attributes!=IntPtr.Zero){if(attributesInitialized)DeleteProcThreadAttributeList(attributes);Marshal.FreeHGlobal(attributes);}if(inheritList!=IntPtr.Zero)Marshal.FreeHGlobal(inheritList);
   }
  }
  void SetJobLimits(uint flags){JobExtendedLimits limits=new JobExtendedLimits();limits.Basic.LimitFlags=flags;if(!SetInformationJobObject(job.DangerousGetHandle(),JobExtendedLimitInformation,ref limits,(uint)Marshal.SizeOf(typeof(JobExtendedLimits))))throw NativeError("Nie można przygotować zatrzymywania procesów potomnych.");}
  public void Resume(){if(ResumeThread(thread.DangerousGetHandle())==0xffffffff)throw NativeError("Nie można rozpocząć polecenia.");resumed=true;thread.Dispose();thread=null;}
  public void DetachChildren(){SetJobLimits(0);}
  public void CloseJob(){if(job!=null){job.Dispose();job=null;}}
  public void Dispose(){
   // A suspended process has not executed user code yet. It may not have been
   // assigned to the job if launch setup failed, so explicitly terminate it.
   if(!resumed&&Process!=null&&!Process.IsInvalid)TerminateProcess(Process.DangerousGetHandle(),1);
   CloseJob();if(thread!=null)thread.Dispose();if(Process!=null)Process.Dispose();if(Output!=null)Output.Dispose();if(Error!=null)Error.Dispose();
  }
 }

 [StructLayout(LayoutKind.Sequential)]struct SecurityAttributes{public int Length;public IntPtr Descriptor;[MarshalAs(UnmanagedType.Bool)]public bool InheritHandle;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct StartupInfo{public int Size;public string Reserved,Desktop,Title;public uint X,Y,XSize,YSize,XCountChars,YCountChars,FillAttribute,Flags;public short ShowWindow,ReservedSize;public IntPtr ReservedData,StandardInput,StandardOutput,StandardError;}
 [StructLayout(LayoutKind.Sequential)]struct StartupInfoEx{public StartupInfo StartupInfo;public IntPtr Attributes;}
 [StructLayout(LayoutKind.Sequential)]struct ProcessInformation{public IntPtr Process,Thread;public uint ProcessId,ThreadId;}
 [StructLayout(LayoutKind.Sequential)]struct JobBasicLimits{public long PerProcessTime,PerJobTime;public uint LimitFlags;public UIntPtr MinimumWorkingSet,MaximumWorkingSet;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;}
 [StructLayout(LayoutKind.Sequential)]struct IoCounters{public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
 [StructLayout(LayoutKind.Sequential)]struct JobExtendedLimits{public JobBasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
 [DllImport("kernel32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern IntPtr CreateJobObject(IntPtr attributes,string name);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int infoClass,ref JobExtendedLimits information,uint length);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool CreatePipe(out IntPtr read,out IntPtr write,ref SecurityAttributes attributes,uint size);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetHandleInformation(IntPtr handle,uint mask,uint flags);
 [DllImport("kernel32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern IntPtr CreateFile(string file,uint access,uint share,ref SecurityAttributes attributes,uint disposition,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool InitializeProcThreadAttributeList(IntPtr attributes,int count,int flags,ref IntPtr size);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool UpdateProcThreadAttribute(IntPtr attributes,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returnedSize);
 [DllImport("kernel32.dll")]static extern void DeleteProcThreadAttributeList(IntPtr attributes);
 [DllImport("kernel32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern bool CreateProcess(string application,StringBuilder commandLine,IntPtr processAttributes,IntPtr threadAttributes,bool inheritHandles,uint flags,IntPtr environment,string currentDirectory,ref StartupInfoEx startup,out ProcessInformation process);
 [DllImport("kernel32.dll",SetLastError=true)]static extern uint ResumeThread(IntPtr thread);
 [DllImport("kernel32.dll",SetLastError=true)]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetExitCodeProcess(IntPtr process,out uint exitCode);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateProcess(IntPtr process,uint exitCode);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool PeekNamedPipe(IntPtr pipe,IntPtr buffer,uint bufferSize,IntPtr bytesRead,out uint available,IntPtr bytesLeft);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadFile(IntPtr file,byte[] buffer,uint bytesToRead,out uint bytesRead,IntPtr overlapped);
}
}
