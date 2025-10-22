VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "ThisWorkbook"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = True

Private Sub Workbook_Open()
    ' Make sure Excel events are enabled
    Application.EnableEvents = True
    ' Set up all handlers
    modTS_Data.SetupAllHandlers
    ' Add the big search buttons (these work well)
    modTS_Data.AddBigSearchButton
End Sub
Private Sub Workbook_SheetActivate(ByVal Sh As Object)
    ' Re-enable events when activating sheets
    Application.EnableEvents = True
    ' Force login form to lock access
    ' frmLogin.Show
End Sub
Sub CheckForHiddenPasswords()
    Dim nm As name
    For Each nm In ThisWorkbook.Names
        Debug.Print nm.name & " = " & nm.RefersTo
    Next nm
End Sub