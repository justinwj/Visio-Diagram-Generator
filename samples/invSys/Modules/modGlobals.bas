Attribute VB_Name = "modGlobals"

'====================
' Modules: modGlobals
'====================
Option Explicit

Public Const STATUS_ACTIVE      As String = "ACTIVE"
Public Const STATUS_DEPRECATED  As String = "DEPRECATED"
Public Const STATUS_OBSOLETE    As String = "OBSOLETE"
Public Const STATUS_REMOVED     As String = "REMOVED"
Public Const STATUS_INACTIVE    As String = "INACTIVE"
Public gSelectedCell As Range
Public Sub CommitSelectionAndCloseWrapper()
    frmItemSearch.CommitSelectionAndClose
End Sub
' Add this function to initialize global variables
Public Sub InitializeGlobalVariables()
    ' Make sure the gSelectedCell variable is available
    On Error Resume Next
    Set gSelectedCell = Nothing
    On Error GoTo 0
End Sub
Public Function GetItemUOMByRowNum(rowNum As String, ItemCode As String, itemName As String) As String
    On Error GoTo ErrorHandler
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim foundCell As Range
    Dim foundRow As Long
    ' Default return value if not found
    GetItemUOMByRowNum = "each"
    Set ws = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Set tbl = ws.ListObjects("invSys")
    ' Try to find the item by ROW# first (most precise)
    If Trim(rowNum) <> "" Then
        Set foundCell = tbl.ListColumns("ROW").DataBodyRange.Find( _
                        What:=rowNum, _
                        LookIn:=xlValues, _
                        LookAt:=xlWhole, _
                        MatchCase:=False)
        If Not foundCell Is Nothing Then
            foundRow = foundCell.row - tbl.HeaderRowRange.row
            GetItemUOMByRowNum = tbl.DataBodyRange(foundRow, tbl.ListColumns("UOM").Index).value
            ' If UOM is empty, return default
            If Trim(GetItemUOMByRowNum) = "" Then GetItemUOMByRowNum = "each"
            Exit Function
        End If
    End If
    ' Try ITEM_CODE next
    If Trim(ItemCode) <> "" Then
        Set foundCell = tbl.ListColumns("ITEM_CODE").DataBodyRange.Find( _
                        What:=ItemCode, _
                        LookIn:=xlValues, _
                        LookAt:=xlWhole, _
                        MatchCase:=False)
        If Not foundCell Is Nothing Then
            foundRow = foundCell.row - tbl.HeaderRowRange.row
            GetItemUOMByRowNum = tbl.DataBodyRange(foundRow, tbl.ListColumns("UOM").Index).value
            ' If UOM is empty, return default
            If Trim(GetItemUOMByRowNum) = "" Then GetItemUOMByRowNum = "each"
            Exit Function
        End If
    End If
    ' Last resort: Try item name
    If Trim(itemName) <> "" Then
        Set foundCell = tbl.ListColumns("ITEM").DataBodyRange.Find( _
                        What:=itemName, _
                        LookIn:=xlValues, _
                        LookAt:=xlWhole, _
                        MatchCase:=False)
        If Not foundCell Is Nothing Then
            foundRow = foundCell.row - tbl.HeaderRowRange.row
            GetItemUOMByRowNum = tbl.DataBodyRange(foundRow, tbl.ListColumns("UOM").Index).value
            ' If UOM is empty, return default
            If Trim(GetItemUOMByRowNum) = "" Then GetItemUOMByRowNum = "each"
        End If
    End If
    Exit Function
ErrorHandler:
    Debug.Print "Error in GetItemUOMByRowNum: " & Err.Description
    GetItemUOMByRowNum = "each"
End Function
Public Sub OpenItemSearchForCurrentCell()
    ' Store the active cell as the selected cell
    Set gSelectedCell = ActiveCell
    ' Show the form
    frmItemSearch.Show vbModeless
End Sub
    ' Add to modGlobals.bas
Public Function IsFormLoaded(formName As String) As Boolean
    Dim frm As Object
    IsFormLoaded = False
    On Error Resume Next
    For Each frm In UserForms
        If frm.Name = formName Then
            IsFormLoaded = True
            Exit For
        End If
    Next frm
    On Error GoTo 0
End Function











