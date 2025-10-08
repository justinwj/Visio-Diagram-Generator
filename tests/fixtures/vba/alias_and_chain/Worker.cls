VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "Worker"
Option Explicit

Public Sub DoWork()
End Sub

Public Function Factory() As Helper
    Set Factory = New Helper
End Function

