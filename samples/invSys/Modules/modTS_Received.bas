Attribute VB_Name = "modTS_Received"

Option Explicit

'==============================================
' Module: modTS_Received (TS Received Processing)
' Purpose: Process ReceivedTally and invSysData_Receiving without generating new REF_NUMBER
'==============================================
' It aggregates quantities by item and displays them in a list box.
Sub TallyReceived()
    On Error GoTo ErrorHandler
    ' Debug info
    Debug.Print "TallyReceived: Starting..."
    ' Verify the worksheet exists
    Dim ws As Worksheet
    On Error Resume Next
    Set ws = ThisWorkbook.Worksheets("ReceivedTally")
    On Error GoTo ErrorHandler
    If ws Is Nothing Then
        MsgBox "The worksheet 'ReceivedTally' does not exist!", vbExclamation
        Exit Sub
    End If
    ' Verify the table exists
    Dim tbl As ListObject
    On Error Resume Next
    Set tbl = ws.ListObjects("ReceivedTally")
    On Error GoTo ErrorHandler
    If tbl Is Nothing Then
        MsgBox "The table 'ReceivedTally' does not exist on worksheet 'ReceivedTally'!", vbExclamation
        Exit Sub
    End If
    ' Create and show form with received items data
    Dim frm As New frmReceivedTally
    ' Configure the form
    With frm
        ' Make sure the listbox exists and is configured properly
        .lstBox.Clear
        .lstBox.ColumnCount = 5  ' ITEM, QTY, UOM, ITEM_CODE(hidden), ROW(hidden)
        .lstBox.ColumnWidths = "150;50;50;0;0" ' Hide ITEM_CODE and ROW columns
        .lstBox.AddItem "ITEMS"
        .lstBox.List(0, 1) = "QUANTITY"
        .lstBox.List(0, 2) = "UOM"
    End With
    ' Populate form directly using our PopulateReceivedForm function
    PopulateReceivedForm frm
    ' Show the form if there are items
    If frm.lstBox.ListCount > 1 Then ' More than just the header row
        frm.Show vbModal
    Else
        MsgBox "No received items to tally", vbInformation
    End If
    Exit Sub
ErrorHandler:
    MsgBox "Error in TallyReceived: " & Err.Description & " (Error " & Err.Number & ")", vbCritical
    Debug.Print "Error in TallyReceived: " & Err.Description & " (Error " & Err.Number & ")"
End Sub

Private Sub PopulateReceivedForm(frm As frmReceivedTally)
    Dim ws As Worksheet
    Dim inputTbl As ListObject
    Dim dataArr As Variant
    Dim idxItems As Long, idxQty As Long, idxPrice As Long
    Dim i As Long
    Dim defaultUOM As String, uom As String
    Dim itemName As String, qty As Double, prc As Double
    Dim qtyDict As Object, priceDict As Object
    Dim key As Variant

    ' Initialize
    defaultUOM = "N/A"
    Set qtyDict = CreateObject("Scripting.Dictionary")
    Set priceDict = CreateObject("Scripting.Dictionary")
    Set ws = ThisWorkbook.Sheets("ReceivedTally")
    Set inputTbl = ws.ListObjects("ReceivedTally")

    ' Validate required columns
    idxItems = ColumnIndex(inputTbl, "ITEMS")
    idxQty = ColumnIndex(inputTbl, "QUANTITY")
    idxPrice = ColumnIndex(inputTbl, "PRICE")
    If idxItems * idxQty * idxPrice = 0 Then
        Err.Raise vbObjectError + 2001, , _
            "Required column missing in 'ReceivedTally': ITEMS, QUANTITY, or PRICE"
    End If

    ' Exit if no data rows
    If inputTbl.DataBodyRange Is Nothing Then
        frm.lstBox.Clear
        Exit Sub
    End If

    dataArr = inputTbl.DataBodyRange.Value

    ' Aggregate quantities and prices by item name
    For i = LBound(dataArr, 1) To UBound(dataArr, 1)
        itemName = CStr(dataArr(i, idxItems))
        qty = Val(dataArr(i, idxQty))
        prc = Val(dataArr(i, idxPrice))
        If qtyDict.Exists(itemName) Then
            qtyDict(itemName) = qtyDict(itemName) + qty
            priceDict(itemName) = priceDict(itemName) + prc
        Else
            qtyDict.Add itemName, qty
            priceDict.Add itemName, prc
        End If
    Next i

    ' Configure listbox headers
    With frm.lstBox
        .Clear
        .ColumnCount = 4
        .ColumnWidths = "150;70;50;70"
        .AddItem "ITEMS"
        .List(0, 1) = "QUANTITY"
        .List(0, 2) = "UOM"
        .List(0, 3) = "PRICE"
    End With

    ' Populate aggregated data rows
    For Each key In qtyDict.Keys
        ' Get UOM for the item (cast key to String)
        uom = GetUOMFromInvSys(CStr(key), "", "UOM")
        If Len(Trim(uom)) = 0 Then uom = defaultUOM

        With frm.lstBox
            .AddItem CStr(key)
            .List(.ListCount - 1, 1) = qtyDict(key)
            .List(.ListCount - 1, 2) = uom
            .List(.ListCount - 1, 3) = priceDict(key)
        End With
    Next key
End Sub

Public Sub ProcessReceivedBatch()
    Dim wsRecv    As Worksheet: Set wsRecv    = ThisWorkbook.Sheets("ReceivedTally")
    Dim tblRecv   As ListObject: Set tblRecv   = wsRecv.ListObjects("ReceivedTally")
    Dim tblDet    As ListObject: Set tblDet    = wsRecv.ListObjects("invSysData_Receiving")
    Dim wsInv     As Worksheet: Set wsInv     = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Dim tblInv    As ListObject: Set tblInv    = wsInv.ListObjects("invSys")
    Dim wsLog     As Worksheet: Set wsLog     = ThisWorkbook.Sheets("ReceivedLog")
    Dim tblLog    As ListObject: Set tblLog    = wsLog.ListObjects("ReceivedLog")

    Dim rowCount  As Long:       rowCount     = tblRecv.ListRows.Count
    Dim j         As Long
    Dim refNum    As String
    Dim items     As String
    Dim qty       As Double
    Dim price     As Double
    Dim itemCode  As String
    Dim rowNum    As Long
    Dim uom       As String
    Dim vendor    As String
    Dim location  As String
    Dim entryDate As Date
    Dim newRow    As ListRow

    ' Process each matching row in both staging tables
    For j = 1 To rowCount
        ' 1) Read REF_NUMBER, ITEMS, QUANTITY, PRICE from ReceivedTally
        With tblRecv.DataBodyRange
            refNum = CStr(.Cells(j, tblRecv.ListColumns("REF_NUMBER").Index).Value)
            items  = CStr(.Cells(j, tblRecv.ListColumns("ITEMS").Index).Value)
            qty    = CDbl(.Cells(j, tblRecv.ListColumns("QUANTITY").Index).Value)
            price  = CDbl(.Cells(j, tblRecv.ListColumns("PRICE").Index).Value)
        End With

        ' 2) Read ROW, ITEM_CODE, UOM, VENDOR, LOCATION, ENTRY_DATE from invSysData_Receiving
        With tblDet.DataBodyRange
            rowNum    = CLng(.Cells(j, tblDet.ListColumns("ROW").Index).Value)
            itemCode  = CStr(.Cells(j, tblDet.ListColumns("ITEM_CODE").Index).Value)
            uom       = CStr(.Cells(j, tblDet.ListColumns("UOM").Index).Value)
            vendor    = CStr(.Cells(j, tblDet.ListColumns("VENDOR").Index).Value)
            location  = CStr(.Cells(j, tblDet.ListColumns("LOCATION").Index).Value)
            entryDate = CDate(.Cells(j, tblDet.ListColumns("ENTRY_DATE").Index).Value)
        End With

        ' 3) Append to ReceivedLog using the existing REF_NUMBER
        Set newRow = tblLog.ListRows.Add
        With tblLog.ListColumns
            newRow.Range(1, .Item("REF_NUMBER").Index ).Value = refNum
            newRow.Range(1, .Item("ITEMS").Index      ).Value = items
            newRow.Range(1, .Item("QUANTITY").Index   ).Value = qty
            newRow.Range(1, .Item("PRICE").Index      ).Value = price
            newRow.Range(1, .Item("UOM").Index        ).Value = uom
            newRow.Range(1, .Item("VENDOR").Index     ).Value = vendor
            newRow.Range(1, .Item("LOCATION").Index   ).Value = location
            newRow.Range(1, .Item("ITEM_CODE").Index  ).Value = itemCode
            newRow.Range(1, .Item("ROW").Index        ).Value = rowNum
            newRow.Range(1, .Item("ENTRY_DATE").Index ).Value = entryDate
        End With

        ' 4) Update inventory RECEIVED column in invSys table
        With tblInv.ListRows(rowNum).Range
            .Cells(tblInv.ListColumns("RECEIVED").Index).Value = _
                Val(.Cells(tblInv.ListColumns("RECEIVED").Index).Value) + qty
        End With
    Next j

    ' 5) Clear staging tables
    If Not tblRecv.DataBodyRange Is Nothing Then tblRecv.DataBodyRange.Delete
    If Not tblDet.DataBodyRange Is Nothing Then tblDet.DataBodyRange.Delete
End Sub

' Pulls UOM, VENDOR, LOCATION, ENTRY_DATE from invSysData_Receiving
Public Sub GetReceivingDetails( _
    ByVal itemCode  As String, _
    ByVal rowNum    As Long, _
    ByRef uom       As String, _
    ByRef vendor    As String, _
    ByRef location  As String, _
    ByRef entryDate As Date)

    Dim wsTable As Worksheet
    Dim tbl      As ListObject
    Dim lr       As ListRow
    Dim colUOM       As Long, colVendor As Long
    Dim colLocation  As Long, colRow As Long

    Set wsTable = ThisWorkbook.Sheets("ReceivedTally")
    Set tbl     = wsTable.ListObjects("invSysData_Receiving")

    ' Find column indexes once
    colUOM      = tbl.ListColumns("UOM").Index
    colVendor   = tbl.ListColumns("VENDOR").Index
    colLocation = tbl.ListColumns("LOCATION").Index
    colRow      = tbl.ListColumns("ROW").Index

    ' Default fallback
    uom       = ""
    vendor    = ""
    location  = ""
    entryDate = Now

    For Each lr In tbl.ListRows
        With lr.Range
            If .Cells(colRow).Value = rowNum Then
                uom       = CStr(.Cells(colUOM     ).Value)
                vendor    = CStr(.Cells(colVendor  ).Value)
                location  = CStr(.Cells(colLocation).Value)
                entryDate = CDate(.Cells(tbl.ListColumns("ENTRY_DATE").Index).Value)
                Exit Sub
            End If
        End With
    Next lr
End Sub

' Appends a single row into the ReceivedLog table
Public Sub AppendReceivedLogRecord( _
    ByVal refNum    As String, _
    ByVal itemName  As String, _
    ByVal qty       As Double, _
    ByVal price     As Double, _
    ByVal uom       As String, _
    ByVal vendor    As String, _
    ByVal location  As String, _
    ByVal itemCode  As String, _
    ByVal rowNum    As Long, _
    ByVal entryDate As Date)

    Dim wsLog As Worksheet
    Dim tblLog As ListObject
    Dim newRow As ListRow

    Set wsLog  = ThisWorkbook.Sheets("ReceivedLog")
    Set tblLog = wsLog.ListObjects("ReceivedLog")

    ' Debug to confirm we’re appending to the right table
    Debug.Print "[AppendReceivedLogRecord] sheet=" & wsLog.Name & "; table=" & tblLog.Name

    Set newRow = tblLog.ListRows.Add
    With tblLog.ListColumns
        newRow.Range(1, .Item("REF_NUMBER").Index ).Value = refNum
        newRow.Range(1, .Item("ITEMS").Index      ).Value = itemName
        newRow.Range(1, .Item("QUANTITY").Index   ).Value = qty
        newRow.Range(1, .Item("PRICE").Index      ).Value = price
        newRow.Range(1, .Item("UOM").Index        ).Value = uom
        newRow.Range(1, .Item("VENDOR").Index     ).Value = vendor
        newRow.Range(1, .Item("LOCATION").Index   ).Value = location
        newRow.Range(1, .Item("ITEM_CODE").Index  ).Value = itemCode
        newRow.Range(1, .Item("ROW").Index        ).Value = rowNum
        newRow.Range(1, .Item("ENTRY_DATE").Index ).Value = entryDate
    End With
End Sub

Public Function GetUOMFromDataTable(item As String, ItemCode As String, rowNum As String) As String
    Dim ws  As Worksheet
    Dim tbl As ListObject
    Dim findCol As Long
    Dim cel As Range
    
    Set ws = ThisWorkbook.Sheets("ReceivedTally")
    Set tbl = ws.ListObjects("invSysData_Receiving")
    findCol = tbl.ListColumns("ROW").Index
    
    ' Match by ROW
    If rowNum <> "" Then
        For Each cel In tbl.DataBodyRange.Columns(findCol).Cells
            If CStr(cel.Value) = rowNum Then
                GetUOMFromDataTable = cel.Offset(0, tbl.ListColumns("UOM").Index - findCol).Value
                Exit Function
            End If
        Next
    End If
    
    ' Match by ITEM_CODE
    findCol = tbl.ListColumns("ITEM_CODE").Index
    If ItemCode <> "" Then
        For Each cel In tbl.DataBodyRange.Columns(findCol).Cells
            If CStr(cel.Value) = ItemCode Then
                GetUOMFromDataTable = cel.Offset(0, tbl.ListColumns("UOM").Index - findCol).Value
                Exit Function
            End If
        Next
    End If
    
    ' Match by ITEM
    findCol = tbl.ListColumns("ITEM").Index
    For Each cel In tbl.DataBodyRange.Columns(findCol).Cells
        If CStr(cel.Value) = item Then
            GetUOMFromDataTable = cel.Offset(0, tbl.ListColumns("UOM").Index - findCol).Value
            Exit Function
        End If
    Next
    
    GetUOMFromDataTable = ""
End Function
