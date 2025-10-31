VERSION 5.00
Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} frmReceivedTally 
   Caption         =   "Items Received Tally"
   ClientHeight    =   4560
   ClientLeft      =   120
   ClientTop       =   465
   ClientWidth     =   7005
   OleObjectBlob   =   "frmReceivedTally.frx":0000
   StartUpPosition =   1  'CenterOwner
End
Attribute VB_Name = "frmReceivedTally"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False

Option Explicit
' Handle btnSend click event
Private Sub btnSend_Click()
    Call modTS_Received.ProcessReceivedBatch
    Unload Me
End Sub

Private Sub UserForm_Initialize()
   ' The lstBox should already be populated by TallyOrders()
   ' Center the form on screen
   Me.StartUpPosition = 0 'Manual
   Me.Left = Application.Left + (Application.Width - Me.Width) / 2
   Me.Top = Application.Top + (Application.Height - Me.Height) / 2
End Sub
'────────────────────────────────────────────────────────────
' Function to update inventory based on ROW or ITEM_CODE
Private Sub UpdateInventory(itemsDict As Object, ColumnName As String)
    On Error GoTo ErrorHandler
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim key As Variant
    Dim foundRow As Long
    Dim currentQty As Double, newQty As Double
    Set ws = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Set tbl = ws.ListObjects("invSys")
    ' Get column index for the target column (e.g., "RECEIVED", "SHIPMENTS")
    Dim targetColIndex As Integer
    targetColIndex = tbl.ListColumns(ColumnName).Index
    ws.Unprotect
    Application.EnableEvents = False
    For Each key In itemsDict.Keys
        Dim itemData As Variant
        itemData = itemsDict(key)
        ' Extract info from the array
        Dim item As String, quantity As Double
        Dim ItemCode As String, rowNum As String
        item = itemData(0)
        quantity = itemData(1)
        ItemCode = itemData(3) ' itemCode at index 3
        rowNum = itemData(4)   ' rowNum at index 4
        foundRow = 0
        ' Try to find by ROW number first (most specific)
        If rowNum <> "" Then
            On Error Resume Next
            foundRow = FindRowByValue(tbl, "ROW", rowNum)
            On Error GoTo ErrorHandler
        End If
        ' If ROW didn't work, try ITEM_CODE
        If foundRow = 0 And ItemCode <> "" Then
            On Error Resume Next
            foundRow = FindRowByValue(tbl, "ITEM_CODE", ItemCode)
            On Error GoTo ErrorHandler
        End If
        ' As last resort, try finding by item name
        If foundRow = 0 Then
            On Error Resume Next
            foundRow = FindRowByValue(tbl, "ITEM", item)
            On Error GoTo ErrorHandler
        End If
        ' If we found the row, update it
        If foundRow > 0 Then
            ' Get current quantity
            currentQty = 0
            On Error Resume Next
            currentQty = tbl.DataBodyRange(foundRow, targetColIndex).value
            If IsEmpty(currentQty) Then currentQty = 0
            On Error GoTo ErrorHandler
            ' Update with new quantity
            newQty = currentQty + quantity
            tbl.DataBodyRange(foundRow, targetColIndex).value = newQty
            ' Log this change
            LogInventoryChange "UPDATE", ItemCode, item, quantity, newQty
        Else
            ' Log that we couldn't find the item
            LogInventoryChange "ERROR", ItemCode, item, quantity, 0
        End If
    Next key
    Application.EnableEvents = True
    ws.Protect
    Exit Sub
ErrorHandler:
    Application.EnableEvents = True
    ws.Protect
    MsgBox "Error updating inventory: " & Err.Description, vbCritical
End Sub

' Helper function to log inventory changes
Private Sub LogInventoryChange(Action As String, ItemCode As String, itemName As String, qtyChange As Double, newQty As Double)
    ' This would call your inventory logging system
    On Error Resume Next
    ' You might want to use the modTS_Log module for this
End Sub

 Private Function GetUOMFromDataTable(item As String, ItemCode As String, rowNum As String) As String
    On Error Resume Next
    Dim ws As Worksheet
    Dim dataTbl As ListObject
    Dim uom As String
    Dim uomCol As Long, codeCol As Long, rowCol As Long
    Dim i As Long               ' ← Declare your loop counter

    Set ws = ThisWorkbook.Sheets("ReceivedTally")
    Set dataTbl = ws.ListObjects("invSysData_Receiving")
    uom = "each"

    ' Find UOM column
    For i = 1 To dataTbl.ListColumns.Count
        Select Case UCase(dataTbl.ListColumns(i).Name)
            Case "UOM":        uomCol = i
            Case "ITEM_CODE":  codeCol = i
            Case "ROW":        rowCol  = i
        End Select
    Next i

    ' Search for match
    For i = 1 To dataTbl.ListRows.Count
        Dim found As Boolean
        found = False
        If rowNum <> "" And rowCol > 0 Then
            If CStr(dataTbl.DataBodyRange(i, rowCol).Value) = rowNum Then found = True
        ElseIf ItemCode <> "" And codeCol > 0 Then
            If CStr(dataTbl.DataBodyRange(i, codeCol).Value) = ItemCode Then found = True
        End If
        If found And uomCol > 0 Then
            uom = CStr(dataTbl.DataBodyRange(i, uomCol).Value)
            Exit For
        End If
    Next i

    GetUOMFromDataTable = uom
End Function



