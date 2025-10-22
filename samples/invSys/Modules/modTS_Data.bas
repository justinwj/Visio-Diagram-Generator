Attribute VB_Name = "modTS_Data"

' ========================
' Module: modTS_Data
' ========================
Option Explicit
Public Function LoadItemList() As Variant
    On Error GoTo ErrorHandler
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim dataRange As Range
    Dim result As Variant
    Dim rowCount As Long, colCount As Long
    Dim i As Long
    ' Debug output
    Debug.Print "LoadItemList: Starting function"
    ' Get worksheet and table reference
    Set ws = ThisWorkbook.Worksheets("INVENTORY MANAGEMENT")
    Set tbl = ws.ListObjects("invSys")
    If tbl Is Nothing Then
        Debug.Print "LoadItemList: Table 'invSys' not found"
        GoTo ErrorHandler
    End If
    ' Get row count (exit if empty)
    rowCount = tbl.ListRows.count
    If rowCount = 0 Then
        Debug.Print "LoadItemList: No rows in table"
        GoTo ErrorHandler
    End If
    Debug.Print "LoadItemList: Found " & rowCount & " rows in table"
    ' Create result array with space for ROW, ITEM_CODE, ITEM, LOCATION
    ReDim result(1 To rowCount, 0 To 4)
    ' Get column references (safely)
    Dim itemCodeCol As Integer, rowCol As Integer, itemCol As Integer
    Dim locCol As Integer, descCol As Integer
    On Error Resume Next
    itemCodeCol = tbl.ListColumns("ITEM_CODE").Index
    rowCol = tbl.ListColumns("ROW").Index
    itemCol = tbl.ListColumns("ITEM").Index
    locCol = tbl.ListColumns("LOCATION").Index
    descCol = tbl.ListColumns("DESCRIPTION").Index
    On Error GoTo ErrorHandler
    Debug.Print "LoadItemList: Column indexes - ITEM_CODE: " & itemCodeCol & _
                ", ROW: " & rowCol & ", ITEM: " & itemCol & _
                ", LOCATION: " & locCol & ", DESCRIPTION: " & descCol
    ' Check if we found the required columns
    If itemCodeCol = 0 Or rowCol = 0 Or itemCol = 0 Then
        Debug.Print "LoadItemList: Required columns missing"
        GoTo ErrorHandler
    End If
    ' Fill the result array - CHANGED ORDER: ROW first, then ITEM_CODE
    For i = 1 To rowCount
        ' ROW (Column 0) - Now FIRST column
        result(i, 0) = tbl.DataBodyRange.Cells(i, rowCol).value
        ' ITEM_CODE (Column 1) - Now SECOND column
        result(i, 1) = tbl.DataBodyRange.Cells(i, itemCodeCol).value
        ' ITEM name (Column 2) - Same as before
        result(i, 2) = tbl.DataBodyRange.Cells(i, itemCol).value
        ' LOCATION (Column 3) - Same as before
        If locCol > 0 Then
            result(i, 3) = tbl.DataBodyRange.Cells(i, locCol).value
        End If
    Next i
    Debug.Print "LoadItemList: Successfully loaded " & rowCount & " items"
    LoadItemList = result
    Exit Function
ErrorHandler:
    Debug.Print "LoadItemList: Error " & Err.Number & " - " & Err.Description
    LoadItemList = Empty
End Function
Public Sub GenerateRowNumbers()
    On Error GoTo ErrorHandler
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim rowNumCol As Long
    Dim i As Long
    Dim maxRowNum As Long
    Dim newCol As ListColumn  ' Add this variable for the new column
    Set ws = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Set tbl = ws.ListObjects("invSys")
    ' Find the ROW# column
    On Error Resume Next
    rowNumCol = tbl.ListColumns("ROW").Index
    On Error GoTo ErrorHandler
    If rowNumCol = 0 Then
        ' Add the column if it doesn't exist - FIX THIS LINE:
        Set newCol = tbl.ListColumns.Add
        newCol.Name = "ROW"
        rowNumCol = tbl.ListColumns("ROW").Index
    End If
    ' Find the highest existing row number
    maxRowNum = 0
    For i = 1 To tbl.ListRows.count
        If IsNumeric(tbl.DataBodyRange(i, rowNumCol).value) Then
            maxRowNum = Application.WorksheetFunction.Max(maxRowNum, tbl.DataBodyRange(i, rowNumCol).value)
        End If
    Next i
    ' Assign row numbers to any blank cells
    For i = 1 To tbl.ListRows.count
        If Trim(tbl.DataBodyRange(i, rowNumCol).value & "") = "" Then
            ' Increment and assign new row number
            maxRowNum = maxRowNum + 1
            tbl.DataBodyRange(i, rowNumCol).value = maxRowNum
        End If
    Next i
    MsgBox "Row numbers have been generated successfully.", vbInformation
    Exit Sub
ErrorHandler:
    MsgBox "Error generating row numbers: " & Err.Description, vbExclamation
End Sub
Public Function IsInItemsColumn(Target As Range) As Boolean
    ' Initialize to false
    IsInItemsColumn = False
    On Error Resume Next
    ' Get table that contains this cell
    Dim lo As ListObject
    Set lo = Target.ListObject
    ' If not in a table, exit
    If lo Is Nothing Then Exit Function
    ' Check if in one of our tally tables
    If lo.Name <> "ShipmentsTally" And lo.Name <> "ReceivedTally" Then Exit Function
    ' Find the ITEMS column
    Dim itemsCol As ListColumn
    On Error Resume Next
    Set itemsCol = lo.ListColumns("ITEMS")
    On Error GoTo 0
    ' If ITEMS column doesn't exist, exit
    If itemsCol Is Nothing Then Exit Function
    ' Check if Target is specifically in the ITEMS column data area
    If Target.Column = itemsCol.Range.Column Then
        ' Make sure we're below the header row
        If Target.row > lo.HeaderRowRange.row Then
            IsInItemsColumn = True
            Debug.Print "Cell " & Target.Address & " IS in the ITEMS column"
        End If
    End If
    On Error GoTo 0
End Function
' Add this function since it's being called but not defined
Public Sub ClearTableFilters()
    On Error Resume Next
    ' Clear filters on ShipmentsTally
    If Not ThisWorkbook.Worksheets("ShipmentsTally") Is Nothing Then
        If Not ThisWorkbook.Worksheets("ShipmentsTally").ListObjects("ShipmentsTally") Is Nothing Then
            ThisWorkbook.Worksheets("ShipmentsTally").ListObjects("ShipmentsTally").AutoFilter.ShowAllData
        End If
        If Not ThisWorkbook.Worksheets("ShipmentsTally").ListObjects("invSysData_Shipping") Is Nothing Then
            ThisWorkbook.Worksheets("ShipmentsTally").ListObjects("invSysData_Shipping").AutoFilter.ShowAllData
        End If
    End If
    ' Clear filters on ReceivedTally
    If Not ThisWorkbook.Worksheets("ReceivedTally") Is Nothing Then
        If Not ThisWorkbook.Worksheets("ReceivedTally").ListObjects("ReceivedTally") Is Nothing Then
            ThisWorkbook.Worksheets("ReceivedTally").ListObjects("ReceivedTally").AutoFilter.ShowAllData
        End If
        If Not ThisWorkbook.Worksheets("ReceivedTally").ListObjects("invSysData_Receiving") Is Nothing Then
            ThisWorkbook.Worksheets("ReceivedTally").ListObjects("invSysData_Receiving").AutoFilter.ShowAllData
        End If
    End If
    On Error GoTo 0
End Sub
Public Sub AddBigSearchButton()
    On Error Resume Next
    ' Add to ShipmentsTally
    If Not ThisWorkbook.Sheets("ShipmentsTally") Is Nothing Then
        ThisWorkbook.Sheets("ShipmentsTally").Shapes.Delete "BigSearchBtn"
        Dim shipBtn As Shape
        Set shipBtn = ThisWorkbook.Sheets("ShipmentsTally").Shapes.AddShape(msoShapeRoundedRectangle, 10, 10, 180, 40)
        With shipBtn
            .Name = "BigSearchBtn"
            .TextFrame.Characters.text = "SEARCH ITEMS"
            .Fill.ForeColor.RGB = RGB(0, 112, 192)  ' Blue
            .Line.ForeColor.RGB = RGB(0, 0, 128)    ' Dark blue
            .TextFrame.Characters.Font.Color = RGB(255, 255, 255)  ' White
            .TextFrame.Characters.Font.Bold = True
            .TextFrame.Characters.Font.Size = 12
            .OnAction = "modGlobals.OpenItemSearchForCurrentCell"
        End With
    End If
    ' Add to ReceivedTally
    If Not ThisWorkbook.Sheets("ReceivedTally") Is Nothing Then
        ThisWorkbook.Sheets("ReceivedTally").Shapes.Delete "BigSearchBtn"
        Dim recvBtn As Shape
        Set recvBtn = ThisWorkbook.Sheets("ReceivedTally").Shapes.AddShape(msoShapeRoundedRectangle, 10, 10, 180, 40)
        With recvBtn
            .Name = "BigSearchBtn"
            .TextFrame.Characters.text = "SEARCH ITEMS"
            .Fill.ForeColor.RGB = RGB(0, 112, 192)  ' Blue
            .Line.ForeColor.RGB = RGB(0, 0, 128)    ' Dark blue
            .TextFrame.Characters.Font.Color = RGB(255, 255, 255)  ' White
            .TextFrame.Characters.Font.Bold = True
            .TextFrame.Characters.Font.Size = 12
            .OnAction = "modGlobals.OpenItemSearchForCurrentCell"
        End With
    End If
End Sub
Public Sub SetupAllHandlers()
    ' Clear table filters if needed
    ClearTableFilters
    ' Initialize global variables
    modGlobals.InitializeGlobalVariables
    ' Setup F3 hotkey for search form
    On Error Resume Next
    Application.OnKey "{F3}", "modGlobals.OpenItemSearchForCurrentCell"
    On Error GoTo 0
End Sub











