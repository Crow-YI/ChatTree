' ============================================================
'  TreeChat v2.0 — Double-click Launcher (No Console Window)
' ============================================================
Set WshShell = CreateObject("WScript.Shell")
Set FSO = CreateObject("Scripting.FileSystemObject")

' Get script directory
ScriptDir = FSO.GetParentFolderName(WScript.ScriptFullName)

' Find uv executable
uvPath = ""
homeDir = WshShell.ExpandEnvironmentStrings("%USERPROFILE%")
If FSO.FileExists(homeDir & "\.local\bin\uv.exe") Then
    uvPath = homeDir & "\.local\bin\uv.exe"
ElseIf FSO.FileExists(homeDir & "\.cargo\bin\uv.exe") Then
    uvPath = homeDir & "\.cargo\bin\uv.exe"
ElseIf FSO.FileExists(WshShell.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\Programs\uv\uv.exe") Then
    uvPath = WshShell.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\Programs\uv\uv.exe"
Else
    uvPath = "uv"
End If

' Check if Python backend is already running
On Error Resume Next
Set http = CreateObject("MSXML2.ServerXMLHTTP.6.0")
If Err.Number <> 0 Then
    Err.Clear
    Set http = CreateObject("MSXML2.ServerXMLHTTP")
End If
If Err.Number <> 0 Then
    ' HTTP component unavailable, skip health check
    Err.Clear
    Set http = Nothing
End If
On Error GoTo 0

backendRunning = False
If Not http Is Nothing Then
    On Error Resume Next
    http.Open "GET", "http://127.0.0.1:8800/api/v1/health", False
    http.setTimeouts 2000, 2000, 2000, 2000
    http.Send ""
    backendRunning = (http.Status = 200)
    On Error GoTo 0
End If

If Not backendRunning Then
    ' Launch Python backend in minimized window
    WshShell.Run "cmd /c cd /d """ & ScriptDir & "\backend"" && """ & uvPath & """ run uvicorn src.main:app --host 127.0.0.1 --port 8800", 7, False

    ' Wait for backend to be ready (max 30 seconds)
    If Not http Is Nothing Then
        Dim retries
        retries = 0
        Do While retries < 30
            WScript.Sleep 1000
            On Error Resume Next
            http.Open "GET", "http://127.0.0.1:8800/api/v1/health", False
            http.setTimeouts 2000, 2000, 2000, 2000
            http.Send ""
            If http.Status = 200 Then Exit Do
            On Error GoTo 0
            retries = retries + 1
        Loop

        If retries >= 30 Then
            MsgBox "Python backend startup timeout." & vbCrLf & vbCrLf & _
                   "Please check:" & vbCrLf & _
                   "  1. backend\.env — DEEPSEEK_API_KEY configured?" & vbCrLf & _
                   "  2. Run check.bat for environment diagnostics.", 48, "TreeChat Launch Failed"
            WScript.Quit 1
        End If
    Else
        ' Cannot detect backend status, wait 10 seconds then continue
        WScript.Sleep 10000
    End If
End If

' Launch WPF frontend
WpfDir = ScriptDir & "\gui"
ReleaseExe = WpfDir & "\bin\Release\net8.0-windows\TreeChat.exe"
DebugDll = WpfDir & "\bin\Debug\net8.0-windows\TreeChat.dll"

If FSO.FileExists(ReleaseExe) Then
    WshShell.Run """" & ReleaseExe & """", 1, False
ElseIf FSO.FileExists(DebugDll) Then
    WshShell.Run "dotnet run --project """ & WpfDir & """", 1, False
Else
    ' First run: compile
    Dim result
    result = WshShell.Run("dotnet build """ & WpfDir & """ -c Release", 1, True)
    If result = 0 Then
        WshShell.Run """" & ReleaseExe & """", 1, False
    Else
        MsgBox "WPF project build failed. Please check if .NET 8 SDK is installed." & vbCrLf & vbCrLf & _
               "Download: https://dotnet.microsoft.com/download/dotnet/8.0", 48, "TreeChat Launch Failed"
        WScript.Quit 1
    End If
End If
