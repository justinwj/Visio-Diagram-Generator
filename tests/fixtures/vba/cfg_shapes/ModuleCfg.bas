Attribute VB_Name = "ModuleCfg"
Option Explicit

Public Sub WithIf()
    If True Then
        Call ModuleCfg.HelperA
    Else
        Call ModuleCfg.HelperB
    End If
End Sub

Public Sub WithLoop()
    Dim i As Integer
    For i = 1 To 3
        Call ModuleCfg.HelperB
    Next i
End Sub

Public Sub HelperA()
End Sub

Public Sub HelperB()
End Sub

