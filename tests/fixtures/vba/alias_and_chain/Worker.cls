VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "Worker"
Option Explicit

Public Sub DoWork()
End Sub

Public Function Factory(Optional ByVal count As Integer = 0, Optional ByVal name As String = "") As Helper
    Set Factory = New Helper
End Function

Public Function RunFactory() As Helper
    Set RunFactory = New Helper
End Function
