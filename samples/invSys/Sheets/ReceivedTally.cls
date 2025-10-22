VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "ReceivedTally"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = True

'=================
'ReceivedTally.cls
'=================
' Simple event handler for both worksheet class modules
Private Sub Worksheet_SelectionChange(ByVal Target As Range)
    ' Only for single cell selection
    If Target.Cells.count > 1 Then Exit Sub
    ' Check if cell is in ITEMS column
    If modTS_Data.IsInItemsColumn(Target) Then
        ' Set the global selected cell
        Set gSelectedCell = Target
        ' Show the form if not already loaded
        If Not modGlobals.IsFormLoaded("frmItemSearch") Then
            frmItemSearch.Show vbModeless
        End If
    End If
End Sub

