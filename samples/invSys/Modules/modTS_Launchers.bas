Attribute VB_Name = "modTS_Launchers"

Option Explicit

'==============================================
' Module: modTS_Launchers
' Purpose: Provide entry-point macros for launching tally forms
'==============================================
Public Sub LaunchShipmentsTally()
    Application.ScreenUpdating = False
    TallyShipments
    Application.ScreenUpdating = True
End Sub
' This should be in your ribbon callback or worksheet button
Public Sub LaunchReceivedTally()
    Application.ScreenUpdating = False
    TallyReceived
    Application.ScreenUpdating = True
End Sub