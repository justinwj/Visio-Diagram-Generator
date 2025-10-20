Attribute VB_Name = "modUR_Snapshot"
'// MODULE: modUR_Snapshot
Option Explicit
Private Snapshots As Collection
Private SnapshotsDict As Object
Private Sub InitializeSnapshots()
    If Snapshots Is Nothing Then Set Snapshots = New Collection
    If SnapshotsDict Is Nothing Then Set SnapshotsDict = CreateObject("Scripting.Dictionary")
End Sub
Public Function CaptureSnapshot() As String
    Dim Snapshot As New clsBulkSnapshot
    Dim tbl As ListObject
    Dim DataToStore As Variant
    Dim i As Integer, j As Integer
    InitializeSnapshots
    Set tbl = GetInventoryTable()
    If tbl Is Nothing Then Exit Function
    DataToStore = tbl.DataBodyRange.value
    Set Snapshot.Formulas = CreateObject("Scripting.Dictionary")
    For i = 1 To UBound(DataToStore, 1)
        For j = 1 To UBound(DataToStore, 2)
            If Len(tbl.DataBodyRange.Cells(i, j).Formula) > 0 Then
                Snapshot.Formulas.Add i & "," & j, tbl.DataBodyRange.Cells(i, j).Formula
            End If
        Next j
    Next i
    With Snapshot
        .SnapshotID = GenerateGUID()
        .data = DataToStore
        .SchemaHash = GetSchemaHash()
        .timestamp = Now
    End With
    If Snapshots.count >= 10 Then
        Snapshots.Remove 1
        SnapshotsDict.Remove SnapshotsDict.Keys()(0)
    End If
    Snapshots.Add Snapshot
    SnapshotsDict.Add Snapshot.SnapshotID, Snapshot
    CaptureSnapshot = Snapshot.SnapshotID
End Function
Public Sub RestoreSnapshot(ByVal SnapshotID As String)
    Dim Snapshot As clsBulkSnapshot
    Dim tbl As ListObject
    Dim rowCount As Long, colCount As Long
    Dim SafeRows As Long, SafeCols As Long
    Dim i As Integer, j As Integer
    Debug.Print "Restoring snapshot with ID:", SnapshotID
    Set tbl = GetInventoryTable()
    If tbl Is Nothing Then
        Debug.Print "Error: Table 'invSys' not found"
        Exit Sub
    End If
    If Not SnapshotsDict.Exists(SnapshotID) Then
        Debug.Print "Error: Snapshot not found in dictionary."
        Exit Sub
    End If
    Set Snapshot = SnapshotsDict(SnapshotID)
    Debug.Print "Snapshot Data Size:", UBound(Snapshot.data, 1), "x", UBound(Snapshot.data, 2)
    If IsEmpty(Snapshot.data) Then
        Debug.Print "Error: Snapshot Data is empty."
        Exit Sub
    End If
    With tbl.DataBodyRange
        rowCount = .Rows.count
        colCount = .Columns.count
        SafeRows = WorksheetFunction.Min(UBound(Snapshot.data, 1), rowCount)
        SafeCols = WorksheetFunction.Min(UBound(Snapshot.data, 2), colCount)
        Debug.Print "Restoring Table Data: Rows=", SafeRows, "Cols=", SafeCols
        .Resize(SafeRows, SafeCols).value = Snapshot.data
    End With
    Debug.Print "Snapshot restore completed."
    MsgBox "Snapshot restored successfully.", vbInformation, "Restore Snapshot"
End Sub
Private Function GetInventoryTable() As ListObject
    On Error Resume Next
    Set GetInventoryTable = ThisWorkbook.Sheets("INVENTORY MANAGEMENT").ListObjects("invSys")
    If Err.Number <> 0 Then
        MsgBox "Critical Error: invSys table not found", vbCritical
        Err.Clear
    End If
End Function
Public Function GenerateGUID() As String
    Dim i As Integer
    Dim GUID As String
    Dim Characters As String
    Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    Randomize
    For i = 1 To 32
        GUID = GUID & Mid(Characters, Int((Len(Characters) * Rnd) + 1), 1)
    Next i
    GenerateGUID = Left(GUID, 8) & "-" & Mid(GUID, 9, 4) & "-" & Mid(GUID, 13, 4) & "-" & Mid(GUID, 17, 4) & "-" & Right(GUID, 12)
End Function
Private Function GetSchemaHash() As String
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim col As Range
    Dim HashValue As String
    Dim i As Integer
    Dim HashTotal As Double
    Set ws = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Set tbl = ws.ListObjects("invSys")
    If tbl Is Nothing Then
        MsgBox "Error: Table 'invSys' not found in 'INVENTORY MANAGEMENT'.", vbCritical, "Schema Hash Error"
        Exit Function
    End If
    HashValue = ""
    For Each col In tbl.HeaderRowRange
        HashValue = HashValue & col.value & "|"
    Next col
    HashTotal = 0
    For i = 1 To Len(HashValue)
        HashTotal = HashTotal + Asc(Mid(HashValue, i, 1)) * i
    Next i
    GetSchemaHash = CStr(HashTotal)
End Function







