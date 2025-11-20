Attribute VB_Name = "modUR_ExcelIntegration"
'// MODULE: modUR_ExcelIntegration
Option Explicit
'// MODULE VARIABLES
Private EventLock As Boolean  ' Prevents recursive event triggers
'=========================
' EXCEL EVENT HANDLING
'=========================
' No per-cell change logging is performed here since logging is handled by bulk click events.
Private Sub Worksheet_Change(ByVal Target As Range)
    ' Intentionally left blank.
End Sub
'// Prevent changes from triggering infinite loops
Private Sub DisableEvents()
    EventLock = True
    Application.EnableEvents = False
End Sub
Private Sub EnableEvents()
    EventLock = False
    Application.EnableEvents = True
End Sub
'// Attach event handlers on workbook open
Private Sub Workbook_Open()
    Application.EnableEvents = True
    EventLock = False
End Sub







