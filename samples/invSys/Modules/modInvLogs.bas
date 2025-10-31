Attribute VB_Name = "modInvLogs"

'// MODULE: modInvLogs
Option Explicit
' LogMultipleInventoryChanges now returns the number of rows inserted.
Public Function LogMultipleInventoryChanges(LogEntries As Collection) As Long
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim i As Long
    Dim newRow As ListRow
    Dim logData As Variant
    Dim rowsInserted As Long
    Dim newLogID As String
    Set ws = ThisWorkbook.Sheets("InventoryLog")
    Set tbl = ws.ListObjects("InventoryLog")
    For i = 1 To LogEntries.count
        logData = LogEntries(i)  ' logData array: 0=USER, 1=ACTION, 2=ITEM_CODE, 3=ITEM_NAME, 4=QUANTITY_CHANGE, 5=NEW_QUANTITY
        Set newRow = tbl.ListRows.Add
        ' Generate a new LOG_ID. You can use the GenerateGUID function or any other method.
        newLogID = modUR_Snapshot.GenerateGUID()
        With newRow.Range
            .Cells(1, 1).value = newLogID       ' LOG_ID assigned explicitly
            .Cells(1, 2).value = logData(0)       ' USER
            .Cells(1, 3).value = logData(1)       ' ACTION
            .Cells(1, 4).value = logData(2)       ' ITEM_CODE
            .Cells(1, 5).value = logData(3)       ' ITEM_NAME
            .Cells(1, 6).value = logData(4)       ' QUANTITY_CHANGE
            .Cells(1, 7).value = logData(5)       ' NEW_QUANTITY
            .Cells(1, 8).value = Now              ' TIMESTAMP
        End With
        rowsInserted = rowsInserted + 1
    Next i
    LogMultipleInventoryChanges = rowsInserted
End Function
' RemoveLastBulkLogEntries removes the last CountToRemove rows from InventoryLog,
' returning a collection of arrays. Each array holds the values from columns 2 to 8.
Public Function RemoveLastBulkLogEntries(ByVal CountToRemove As Long) As Collection
    Dim LogEntries As New Collection
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim i As Long, lastRow As Long
    Dim logRowData As Variant
    Set ws = ThisWorkbook.Sheets("InventoryLog")
    Set tbl = ws.ListObjects("InventoryLog")
    lastRow = tbl.ListRows.count
    For i = 1 To CountToRemove
        ' Capture columns 1 to 8 (LOG_ID, USER, ACTION, ITEM_CODE, ITEM_NAME, QUANTITY_CHANGE, NEW_QUANTITY, TIMESTAMP)
        logRowData = ws.Range(ws.Cells(tbl.DataBodyRange.row + lastRow - 1, 1), _
                               ws.Cells(tbl.DataBodyRange.row + lastRow - 1, 8)).value
        LogEntries.Add logRowData
        ws.Rows(tbl.DataBodyRange.row + lastRow - 1).Delete
        lastRow = lastRow - 1
    Next i
    Set RemoveLastBulkLogEntries = LogEntries
End Function
' ReAddBulkLogEntries reinserts each stored log entry into InventoryLog.
Public Sub ReAddBulkLogEntries(ByVal LogDataCollection As Collection)
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim i As Long
    Dim logRowData As Variant
    Dim newRow As ListRow
    Set ws = ThisWorkbook.Sheets("InventoryLog")
    Set tbl = ws.ListObjects("InventoryLog")
    For i = 1 To LogDataCollection.count
        logRowData = LogDataCollection(i)
        Set newRow = tbl.ListRows.Add
        With newRow.Range
            .Cells(1, 1).value = logRowData(1, 1)  ' LOG_ID
            .Cells(1, 2).value = logRowData(1, 2)  ' USER
            .Cells(1, 3).value = logRowData(1, 3)  ' ACTION
            .Cells(1, 4).value = logRowData(1, 4)  ' ITEM_CODE
            .Cells(1, 5).value = logRowData(1, 5)  ' ITEM_NAME
            .Cells(1, 6).value = logRowData(1, 6)  ' QUANTITY_CHANGE
            .Cells(1, 7).value = logRowData(1, 7)  ' NEW_QUANTITY
            .Cells(1, 8).value = logRowData(1, 8)  ' TIMESTAMP
        End With
    Next i
End Sub











