Attribute VB_Name = "modTS_Tally"
' ================================================
' Module: modTS_Tally (TS stands for Tally System)
' ================================================
Option Explicit
' This module is responsible for tallying orders and displaying them in a user form.
' Track if we're already running a tally operation
Private isRunningTally As Boolean
' Helper function to normalize text
Private Function NormalizeText(text As String) As String
    ' Trim and convert to lowercase for consistent matching
    Dim result As String
    result = Trim(text)
    NormalizeText = LCase(result)
End Function  

' Handle Enter/Tab to commit selection
Private Sub lstBox_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Or KeyCode = vbKeyTab Then
        CommitSelectionAndClose
        KeyCode = 0
    End If
End Sub

' Handle double-click to commit
Private Sub lstBox_DblClick(ByVal Cancel As MSForms.ReturnBoolean)
    CommitSelectionAndClose
End Sub

' Helper: Get column index by header name
Private Function ColumnIndex(tbl As ListObject, header As String) As Long
    Dim col As ListColumn
    For Each col In tbl.ListColumns
        If StrComp(col.Name, header, vbTextCompare) = 0 Then
            ColumnIndex = col.Index
            Exit Function
        End If
    Next col
    ColumnIndex = 0
End Function

'*****************************************
' Helper: get UOM via existing global routine
'*****************************************
Private Function GetItemUOMByRowNum(rowNum As String, ItemCode As String, itemName As String) As String
    GetItemUOMByRowNum = modGlobals.GetItemUOMByRowNum(rowNum, ItemCode, itemName)
End Function

' Helper: Get a field from invSys master by ROW
Private Function GetInvSysValue(rowNum As String, itemCode As String, header As String) As String
    Dim invWs As Worksheet, invTbl As ListObject
    Dim findCol As Long, tgtCol As Long, cel As Range
    Set invWs = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Set invTbl = invWs.ListObjects("invSys")
    findCol = invTbl.ListColumns("ROW").Index
    tgtCol = invTbl.ListColumns(header).Index
    For Each cel In invTbl.DataBodyRange.Columns(findCol).Cells
        If CStr(cel.Value) = rowNum Then
            GetInvSysValue = cel.Offset(0, tgtCol - findCol).Value
            Exit Function
        End If
    Next
    GetInvSysValue = ""
End Function

' Helper function to find a row by column value
Private Function FindRowByValue(tbl As ListObject, colName As String, value As Variant) As Long
    Dim i As Long
    Dim colIndex As Integer
    FindRowByValue = 0 ' Default return value if not found
    On Error Resume Next
    colIndex = tbl.ListColumns(colName).Index
    On Error GoTo 0
    If colIndex = 0 Then Exit Function
    For i = 1 To tbl.ListRows.count
        ' Convert both values to strings for more reliable comparison
        If CStr(tbl.DataBodyRange(i, colIndex).value) = CStr(value) Then
            FindRowByValue = i
            Debug.Print "Found match in " & colName & " column: " & value & " at row " & i
            Exit Function
        End If
    Next i
    Debug.Print "No match found in " & colName & " column for value: " & CStr(value)
End Function

Public Function GetUOMFromInvSys(item As String, ItemCode As String, rowNum As String) As String
    Dim ws  As Worksheet
    Dim tbl As ListObject
    Dim findCol As Long
    Dim cel As Range
    
    Set ws = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Set tbl = ws.ListObjects("invSys")
    
    findCol = tbl.ListColumns("ROW").Index
    If rowNum <> "" Then
        For Each cel In tbl.DataBodyRange.Columns(findCol).Cells
            If CStr(cel.Value) = rowNum Then
                GetUOMFromInvSys = cel.Offset(0, tbl.ListColumns("UOM").Index - findCol).Value
                Exit Function
            End If
        Next
    End If
    
    findCol = tbl.ListColumns("ITEM_CODE").Index
    If ItemCode <> "" Then
        For Each cel In tbl.DataBodyRange.Columns(findCol).Cells
            If CStr(cel.Value) = ItemCode Then
                GetUOMFromInvSys = cel.Offset(0, tbl.ListColumns("UOM").Index - findCol).Value
                Exit Function
            End If
        Next
    End If
    
    findCol = tbl.ListColumns("ITEM").Index
    For Each cel In tbl.DataBodyRange.Columns(findCol).Cells
        If CStr(cel.Value) = item Then
            GetUOMFromInvSys = cel.Offset(0, tbl.ListColumns("UOM").Index - findCol).Value
            Exit Function
        End If
    Next
    
    GetUOMFromInvSys = ""
End Function







