Attribute VB_Name = "ModuleNested"
Option Explicit

Public Sub LoopWithBranch()
    Dim i As Integer

    For i = 1 To 3
        If i Mod 2 = 0 Then
            Call ModuleNested.HelperEven
        Else
            Call ModuleNested.HelperOdd
        End If
    Next i
End Sub

Public Sub HelperEven()
End Sub

Public Sub HelperOdd()
End Sub

