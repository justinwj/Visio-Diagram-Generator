Attribute VB_Name = "modTS_Log"

'==============================================
' Module: modTS_Log (TS stands for Tally System)
' Purpose: Log received items into ReceivedLog table
'==============================================
Option Explicit
'────────────────────────────────────────────────────────────
' Logs received items with provided REF_NUMBER into ReceivedLog table
' receivedSummary.Keys each map to a 10-element array:
'   [0]=REF_NUMBER, [1]=ITEMS, [2]=QUANTITY, [3]=PRICE,
'   [4]=UOM, [5]=VENDOR, [6]=LOCATION,
'   [7]=ITEM_CODE, [8]=ROW, [9]=ENTRY_DATE
'────────────────────────────────────────────────────────────
Public Sub LogReceivedDetailed(receivedSummary As Object)
    On Error GoTo ErrorHandler
    Dim key As Variant
    Dim newRow As ListRow
    Dim ws As Worksheet
    Dim tbl As ListObject
    Set ws = ThisWorkbook.Sheets("ReceivedLog")
    Set tbl = ws.ListObjects("ReceivedLog")

    ' Determine column indexes explicitly
    Dim idx As Long
    Dim colRefNum   As Long, colItems    As Long, colQty    As Long
    Dim colPrice    As Long, colUOM      As Long, colVendor As Long
    Dim colLocation As Long, colItemCode As Long, colRow As Long
    Dim colEntryDate As Long

    For idx = 1 To tbl.ListColumns.Count
        Select Case UCase(tbl.ListColumns(idx).Name)
            Case "REF_NUMBER":   colRefNum    = idx
            Case "ITEMS":        colItems     = idx
            Case "QUANTITY":     colQty       = idx
            Case "PRICE":        colPrice     = idx
            Case "UOM":          colUOM       = idx
            Case "VENDOR":       colVendor    = idx
            Case "LOCATION":     colLocation  = idx
            Case "ITEM_CODE":    colItemCode  = idx
            Case "ROW":          colRow       = idx
            Case "ENTRY_DATE":   colEntryDate = idx
        End Select
    Next idx

    Application.ScreenUpdating = False
    For Each key In receivedSummary.Keys
        Dim itemData As Variant
        itemData = receivedSummary(key)
        Set newRow = tbl.ListRows.Add
        With newRow.Range
            If colRefNum   > 0 Then .Cells(1, colRefNum   ).Value = itemData(0)
            If colItems    > 0 Then .Cells(1, colItems    ).Value = itemData(1)
            If colQty      > 0 Then .Cells(1, colQty      ).Value = itemData(2)
            If colPrice    > 0 Then .Cells(1, colPrice    ).Value = itemData(3)
            If colUOM      > 0 Then .Cells(1, colUOM      ).Value = itemData(4)
            If colVendor   > 0 Then .Cells(1, colVendor   ).Value = itemData(5)
            If colLocation > 0 Then .Cells(1, colLocation ).Value = itemData(6)
            If colItemCode > 0 Then .Cells(1, colItemCode ).Value = itemData(7)
            If colRow      > 0 Then .Cells(1, colRow      ).Value = itemData(8)
            If colEntryDate> 0 Then .Cells(1, colEntryDate).Value = itemData(9)
        End With
    Next key
    Application.ScreenUpdating = True
    Exit Sub

ErrorHandler:
    Application.ScreenUpdating = True
    MsgBox "Error in LogReceivedDetailed: " & Err.Description, vbCritical
End Sub







