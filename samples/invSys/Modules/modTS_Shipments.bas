Attribute VB_Name = "modTS_Shipments"

Option Explicit

Sub TallyShipments()
    Dim frm As New frmShipmentsTally

    If Not FormHasRequiredControls(frm) Then
        MsgBox "The form is missing required controls.", vbCritical
        Exit Sub
    End If

    With frm.lstBox
        .Clear
        .ColumnCount   = 5
        .ColumnWidths  = "150;50;80;0;0"  ' Hide ITEM_CODE & ROW
        .AddItem       "ITEMS"
        .List(0, 1)    = "QUANTITY"
        .List(0, 2)    = "UOM"
    End With

    PopulateShipmentsForm frm

    If frm.lstBox.ListCount > 1 Then
        frm.Show vbModal
    Else
        MsgBox "No shipments to tally", vbInformation
    End If
End Sub

Sub PopulateShipmentsForm(frm As frmShipmentsTally)
    On Error GoTo ErrorHandler
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim dict As Object
    Dim i As Long
    Dim j As Long
    Dim key As Variant
    Dim itemInfo As Variant
    ' Get worksheet and table references
    Set ws = ThisWorkbook.Sheets("ShipmentsTally")
    Set tbl = ws.ListObjects("ShipmentsTally")
    Set dict = CreateObject("Scripting.Dictionary")
    dict.CompareMode = vbTextCompare
    ' Process and tally items from the table
    For i = 1 To tbl.ListRows.count
        ' Get basic values with error handling
        Dim item As String, quantity As Double, uom As String
        On Error Resume Next
        item = CStr(tbl.DataBodyRange(i, tbl.ListColumns("ITEMS").Index).value)
        ' Be extra careful with quantity conversion
        Dim rawQuantity As Variant
        rawQuantity = tbl.DataBodyRange(i, tbl.ListColumns("QUANTITY").Index).value
        If IsNumeric(rawQuantity) Then
            quantity = CDbl(rawQuantity)
        Else
            quantity = 0
        End If
        uom = CStr(tbl.DataBodyRange(i, tbl.ListColumns("UOM").Index).value)
        On Error GoTo ErrorHandler
        ' Skip empty rows or rows with zero quantity
        If Trim(item) <> "" And quantity > 0 Then
            ' Extract ROW and ITEM_CODE if available
            Dim rowNum As String, ItemCode As String
            rowNum = ""
            ItemCode = ""
            On Error Resume Next
            ' Check if ROW and ITEM_CODE are in columns
            For j = 1 To tbl.ListColumns.count
                If UCase(tbl.ListColumns(j).Name) = "ROW" Then
                    rowNum = CStr(tbl.DataBodyRange(i, j).value)
                ElseIf UCase(tbl.ListColumns(j).Name) = "ITEM_CODE" Then
                    ItemCode = CStr(tbl.DataBodyRange(i, j).value)
                End If
            Next j
            ' If we don't have a ROW yet, look up the item in inventory
            If rowNum = "" Then
                Dim invWs As Worksheet
                Dim invTbl As ListObject
                Dim lookupRow As Long
                Set invWs = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
                Set invTbl = invWs.ListObjects("invSys")
                If ItemCode <> "" Then
                    lookupRow = FindRowByValue(invTbl, "ITEM_CODE", ItemCode)
                End If
                If lookupRow = 0 Then
                    lookupRow = FindRowByValue(invTbl, "ITEM", item)
                End If
                If lookupRow > 0 Then
                    rowNum = CStr(invTbl.DataBodyRange(lookupRow, invTbl.ListColumns("ROW").Index).value)
                End If
            End If
            ' Create a unique key - FIXED: For shipments from inventory, ensure items from different rows stay separate
            Dim uniqueKey As String
            If rowNum <> "" Then
                ' Use ROW for uniqueness (most specific)
                uniqueKey = "ROW_" & rowNum
            ElseIf ItemCode <> "" Then
                ' Use ITEM_CODE as fallback
                uniqueKey = "CODE_" & ItemCode
            Else
                ' If no ROW or ITEM_CODE, treat each entry as unique by including row position
                uniqueKey = "NAME_" & LCase(Trim(item)) & "|" & LCase(Trim(uom)) & "|" & i
            End If
            ' Tally items using the unique key
            If dict.Exists(uniqueKey) Then
                dict(uniqueKey) = dict(uniqueKey) + quantity
            Else
                dict.Add uniqueKey, quantity
                ' Store reference information
                dict.Add "info_" & uniqueKey, Array(item, ItemCode, rowNum, uom)
            End If
        End If
    Next i
    ' Configure form list box
    frm.lstBox.Clear
    frm.lstBox.ColumnCount = 5 ' ITEM, QTY, UOM, ITEM_CODE, ROW
    frm.lstBox.ColumnWidths = "150;50;50;0;0" ' Hide ITEM_CODE and ROW
    ' Add header row
    frm.lstBox.AddItem "ITEMS"
    frm.lstBox.List(0, 1) = "QTY"
    frm.lstBox.List(0, 2) = "UOM"
    ' Add data rows
    If dict.count > 0 Then
        For Each key In dict.Keys
            If Left$(key, 5) <> "info_" Then
                itemInfo = dict("info_" & key)
                frm.lstBox.AddItem itemInfo(0) ' Item name
                frm.lstBox.List(frm.lstBox.ListCount - 1, 1) = dict(key)   ' Quantity
                frm.lstBox.List(frm.lstBox.ListCount - 1, 2) = itemInfo(3) ' UOM
                frm.lstBox.List(frm.lstBox.ListCount - 1, 3) = itemInfo(1) ' ITEM_CODE
                frm.lstBox.List(frm.lstBox.ListCount - 1, 4) = itemInfo(2) ' ROW
            End If
        Next key
    End If
    Exit Sub
ErrorHandler:
    MsgBox "Error in PopulateShipmentsForm: " & Err.Description, vbCritical
    Debug.Print "Error in PopulateShipmentsForm: " & Err.Description
    Resume Next
End Sub

Public Sub ProcessShipmentsBatch()
    Dim wsShip  As Worksheet: Set wsShip  = ThisWorkbook.Sheets("ShipmentsTally")
    Dim tblShip As ListObject:  Set tblShip = wsShip.ListObjects("ShipmentsTally")
    Dim tblDet  As ListObject:  Set tblDet  = wsShip.ListObjects("invSysData_Shipping")
    Dim wsInv   As Worksheet:   Set wsInv   = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Dim tblInv  As ListObject:  Set tblInv  = wsInv.ListObjects("invSys")
    Dim wsLog   As Worksheet:   Set wsLog   = ThisWorkbook.Sheets("ShipmentsLog")
    Dim tblLog  As ListObject:  Set tblLog  = wsLog.ListObjects("ShipmentsLog")
    Dim i       As Long

    For i = 1 To tblShip.ListRows.Count
        Dim refNum   As String, itm   As String
        Dim qtyNum   As Double
        Dim uom      As String, vendor  As String, location  As String
        Dim code     As String, rowNum  As Long
        Dim entryDate As Date, newRow As ListRow

        ' 1) Read ORDER_NUMBER, ITEMS, QUANTITY
        With tblShip.DataBodyRange
            refNum = CStr(.Cells(i, tblShip.ListColumns("ORDER_NUMBER").Index).Value)
            itm    = CStr(.Cells(i, tblShip.ListColumns("ITEMS"       ).Index).Value)
            qtyNum = CDbl(.Cells(i, tblShip.ListColumns("QUANTITY"    ).Index).Value)
        End With

        ' 2) Read detail fields
        With tblDet.DataBodyRange
            uom       = CStr(.Cells(i, tblDet.ListColumns("UOM"       ).Index).Value)
            vendor    = CStr(.Cells(i, tblDet.ListColumns("VENDOR"    ).Index).Value)
            location  = CStr(.Cells(i, tblDet.ListColumns("LOCATION"  ).Index).Value)
            code      = CStr(.Cells(i, tblDet.ListColumns("ITEM_CODE" ).Index).Value)
            rowNum    = CLng(.Cells(i, tblDet.ListColumns("ROW"       ).Index).Value)
            entryDate = CDate(.Cells(i, tblDet.ListColumns("ENTRY_DATE").Index).Value)
        End With

        ' 3) Append to ShipmentsLog
        Set newRow = tblLog.ListRows.Add
        With tblLog.ListColumns
            newRow.Range(1, .Item("ORDER_NUMBER").Index).Value = refNum
            newRow.Range(1, .Item("ITEMS"       ).Index).Value = itm
            newRow.Range(1, .Item("QUANTITY"    ).Index).Value = qtyNum
            newRow.Range(1, .Item("UOM"         ).Index).Value = uom
            newRow.Range(1, .Item("VENDOR"      ).Index).Value = vendor
            newRow.Range(1, .Item("LOCATION"    ).Index).Value = location
            newRow.Range(1, .Item("ITEM_CODE"   ).Index).Value = code
            newRow.Range(1, .Item("ROW"         ).Index).Value = rowNum
            newRow.Range(1, .Item("ENTRY_DATE"  ).Index).Value = entryDate
        End With

        ' 4) Update inventory “SHIPMENTS” column
        With tblInv.ListRows(rowNum).Range
            .Cells(tblInv.ListColumns("SHIPMENTS").Index).Value = _
              Val(.Cells(tblInv.ListColumns("SHIPMENTS").Index).Value) + qtyNum
        End With
    Next i

    ' 5) Clear staging tables
    If Not tblShip.DataBodyRange Is Nothing Then tblShip.DataBodyRange.Delete
    If Not tblDet.DataBodyRange Is Nothing Then tblDet .DataBodyRange.Delete
End Sub

    Public Function GetUOMFromDataTable(item As String, ItemCode As String, rowNum As String) As String
    Dim ws  As Worksheet
    Dim tbl As ListObject
    Dim findCol As Long
    Dim cel As Range
    
    Set ws = ThisWorkbook.Sheets("ShipmentsTally")
    Set tbl = ws.ListObjects("invSysData_Shipping")
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