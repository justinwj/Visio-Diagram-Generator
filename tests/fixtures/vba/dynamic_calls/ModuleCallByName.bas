Attribute VB_Name = "ModuleCallByName"
Option Explicit

Public Sub UsesCallByName()
    Dim o As Object
    Set o = CreateObject("Scripting.Dictionary")
    CallByName o, "Add", VbMethod, "k", 1
End Sub

