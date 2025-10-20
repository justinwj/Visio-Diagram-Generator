VERSION 5.00
Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} frmItemSearch 
   Caption         =   "Item Search"
   ClientHeight    =   5085
   ClientLeft      =   120
   ClientTop       =   465
   ClientWidth     =   6480
   OleObjectBlob   =   "frmItemSearch.frx":0000
   StartUpPosition =   1  'CenterOwner
End
Attribute VB_Name = "frmItemSearch"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Option Explicit
Private FullItemList As Variant
Private LastSearchText As String
Private LastSearchTime As Double
Private SearchFirstCharIndex() As Long  ' Array to store first character indexes
Private Const MIN_SEARCH_INTERVAL As Double = 0.2  ' Minimum seconds between searches
Private Sub UserForm_MouseScroll()
    EnableMouseScroll frmItemSearch
End Sub
Private Sub UserForm_Activate()
    Me.txtBox.SetFocus
    ' Place the caret at the beginning with no selection.
    Me.txtBox.SelStart = 0
    Me.txtBox.SelLength = 0
End Sub
Private Sub UserForm_Initialize()
    ' Set up the list box columns
    Me.lstBox.ColumnCount = 4
    Me.lstBox.ColumnWidths = "40;60;80;150"
    ' Center the form
    Me.StartUpPosition = 1 'CenterOwner
    ' Load items
    Dim items As Variant
    items = modTS_Data.LoadItemList()
    If Not IsEmpty(items) Then
        PopulateListBox items
        FullItemList = items
        BuildFirstCharIndex
    End If
    ' Apply any search text right away
    txtBox_Change
End Sub
' Build an index of where each first character appears in the list for faster searching
Private Sub BuildFirstCharIndex()
    Dim i As Long, char As String
    Dim dict As Object
    ' Create a dictionary to track the first occurrence of each character
    Set dict = CreateObject("Scripting.Dictionary")
    ' Initialize the array with -1 (not found)
    ReDim SearchFirstCharIndex(0 To 255)
    For i = 0 To 255
        SearchFirstCharIndex(i) = -1
    Next i
    ' Go through the list and record the first occurrence of each first character
    For i = 0 To Me.lstBox.ListCount - 1
        ' Use index 3 for ITEM name instead of 2 (which is VENDOR)
        If Me.lstBox.List(i, 3) <> "" Then
            char = UCase(Left$(Me.lstBox.List(i, 3), 1))
            ' Only record the first occurrence
            If Asc(char) <= 255 And SearchFirstCharIndex(Asc(char)) = -1 Then
                SearchFirstCharIndex(Asc(char)) = i
            End If
        End If
    Next i
End Sub
' Update the txtBox_Change event with better error handling for fast typing
Private Sub txtBox_Change()
    Dim currentTime As Double
    Dim searchText As String, firstChar As String
    Dim i As Long, matchIndex As Long, startIndex As Long
    Dim visibleItems As Long, centerPos As Long
    ' Get current time and search text
    currentTime = Timer
    searchText = LCase(Trim(Me.txtBox.text))
    ' Only search if:
    ' 1. Search text has changed significantly, OR
    ' 2. Enough time has passed since last search, OR
    ' 3. Text is empty or very short
    If searchText <> LastSearchText And _
       (currentTime - LastSearchTime >= MIN_SEARCH_INTERVAL Or _
        Len(searchText) <= 2) Then
        ' Update tracking variables
        LastSearchTime = currentTime
        LastSearchText = searchText
        ' If the text box is empty, clear the list box selection and exit
        If Len(searchText) = 0 Then
            Me.lstBox.ListIndex = -1
            Exit Sub
        End If
        ' Get the first character and find its index position
        On Error Resume Next
        firstChar = UCase(Left$(searchText, 1))
        If Len(firstChar) > 0 And Asc(firstChar) <= 255 Then
            startIndex = SearchFirstCharIndex(Asc(firstChar))
        Else
            startIndex = 0
        End If
        On Error GoTo 0
        ' If first character not indexed, start from beginning
        If startIndex = -1 Then startIndex = 0
        ' Optimized search strategy
        matchIndex = -1
        On Error Resume Next
        ' First pass: Search from the first character index position
        For i = startIndex To Me.lstBox.ListCount - 1
            ' Use index 3 for ITEM name instead of 2
            If InStr(1, LCase(Me.lstBox.List(i, 3)), searchText) > 0 Then
                matchIndex = i
                Exit For
            End If
        Next i
        ' Second pass: If not found and we started from a specific index,
        ' search from beginning to that index
        If matchIndex = -1 And startIndex > 0 Then
            For i = 0 To startIndex - 1
                ' Use index 3 for ITEM name instead of 2
                If InStr(1, LCase(Me.lstBox.List(i, 3)), searchText) > 0 Then
                    matchIndex = i
                    Exit For
                End If
            Next i
        End If
        On Error GoTo 0
        ' Update UI with results
        If matchIndex <> -1 Then
            Me.lstBox.ListIndex = matchIndex
            ' FIXED: Better centering calculation with error handling
            On Error Resume Next
            ' Calculate visible items - ensure it's at least 1
            visibleItems = Int(Me.lstBox.Height / 15)  ' Approx height per item
            If visibleItems < 1 Then visibleItems = 1
            ' Safe calculation for center position
            If matchIndex > Int(visibleItems / 2) Then
                centerPos = matchIndex - Int(visibleItems / 2)
            Else
                centerPos = 0
            End If
            ' Set top index safely
            Me.lstBox.TopIndex = centerPos
            On Error GoTo 0
            ' Update description
            UpdateDescription
        Else
            Me.lstBox.ListIndex = -1
            Me.txtBox2.text = ""
        End If
    End If
End Sub
' When the user clicks on an item in the list box
Private Sub lstBox_Click()
    ' Keep the item highlighted but don't update the search text
    UpdateDescription
End Sub
' Add handler for keyboard navigation in list box
Private Sub lstBox_Change()
    UpdateDescription
End Sub
' Commit the selection if the user presses Tab or Enter in textbox
Private Sub txtBox_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Or KeyCode = vbKeyTab Then
        CommitSelectionAndClose
        KeyCode = 0  ' Prevent default handling
    End If
End Sub
' FIX 2: Ensure Enter key works when list box has focus
Private Sub lstBox_KeyPress(ByVal KeyAscii As MSForms.ReturnInteger)
    If KeyAscii = vbKeyReturn Then
        CommitSelectionAndClose
        KeyAscii = 0  ' Prevent default handling
    End If
End Sub
' Also handle key down for Enter in list box
Private Sub lstBox_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Or KeyCode = vbKeyTab Then
        CommitSelectionAndClose
        KeyCode = 0  ' Prevent default handling
    End If
End Sub
' Double-clicking on an item also commits the selection.
Private Sub lstBox_DblClick(ByVal Cancel As MSForms.ReturnBoolean)
    CommitSelectionAndClose
End Sub
' data flow between tables
Public Sub CommitSelectionAndClose()
    Static isRunning As Boolean
    ' Prevent recursive calls
    If isRunning Then Exit Sub
    isRunning = True
    Dim chosenValue As String
    Dim chosenItemCode As String
    Dim chosenRowNum As String
    Dim chosenVendor As String
    Dim location As String
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim dataTbl As ListObject
    ' Get selection from list box or text box
    If Me.lstBox.ListIndex <> -1 Then
        chosenRowNum = Me.lstBox.List(Me.lstBox.ListIndex, 0)    ' ROW
        chosenItemCode = Me.lstBox.List(Me.lstBox.ListIndex, 1)  ' ITEM_CODE
        chosenVendor = Me.lstBox.List(Me.lstBox.ListIndex, 2)    ' VENDOR
        chosenValue = Me.lstBox.List(Me.lstBox.ListIndex, 3)     ' Item name
        location = GetLocationByItem(chosenItemCode, chosenValue)
    ElseIf Trim(Me.txtBox.text) <> "" Then
        chosenValue = Me.txtBox.text
        chosenItemCode = ""
        chosenRowNum = ""
        chosenVendor = ""
        location = ""
    Else
        ' No selection made, just exit
        isRunning = False
        Unload Me
        Exit Sub
    End If
    ' Apply the selection to the cell
    If Not gSelectedCell Is Nothing Then
        ' Store the original value before making changes
        Dim originalValue As String
        originalValue = gSelectedCell.value
        ' Update the cell with new item name
        gSelectedCell.value = chosenValue
        ' If we have a valid item selection, update the data table
        If Me.lstBox.ListIndex <> -1 Then
            Set ws = gSelectedCell.Worksheet
            ' Determine which tables to work with based on which sheet we're on
            If ws.Name = "ShipmentsTally" Then
                Set tbl = ws.ListObjects("ShipmentsTally")
                Set dataTbl = ws.ListObjects("invSysData_Shipping")
            ElseIf ws.Name = "ReceivedTally" Then
                Set tbl = ws.ListObjects("ReceivedTally")
                Set dataTbl = ws.ListObjects("invSysData_Receiving")
            Else
                ' Not on a valid tally sheet
                isRunning = False
                Unload Me
                Exit Sub
            End If
            ' Get UOM for this item
            Dim itemUOM As String
            itemUOM = modGlobals.GetItemUOMByRowNum(chosenRowNum, chosenItemCode, chosenValue)
            ' Get the cell identifier (row number in the tally sheet)
            Dim tallyRowNum As Long
            tallyRowNum = gSelectedCell.row - tbl.HeaderRowRange.row
            ' ***** NEW CODE: Delete any existing data for this cell *****
            DeleteExistingDataForCell dataTbl, tallyRowNum
            ' Add a row to the corresponding data table
            If Not dataTbl Is Nothing Then
                Dim dataRow As ListRow
                Set dataRow = dataTbl.ListRows.Add
                ' Fill the data table row with all the item information
                FillDataTableRow dataRow, itemUOM, chosenVendor, location, chosenItemCode, chosenRowNum
                ' ***** NEW CODE: Add reference to the tally row number *****
                SetTallyRowNumber dataRow, tallyRowNum
            End If
        End If
        On Error GoTo 0
    End If
    isRunning = False
    Unload Me
End Sub
' Helper function to fill a data table row with item information
Private Sub FillDataTableRow(dataRow As ListRow, uom As String, vendor As String, location As String, ItemCode As String, rowNum As String)
    On Error Resume Next
    ' Find the column indexes
    Dim tbl As ListObject
    Dim colFound As Boolean
    Set tbl = dataRow.Parent
    Dim i As Long
    ' Set UOM value
    colFound = False
    For i = 1 To tbl.ListColumns.count
        If UCase(tbl.ListColumns(i).Name) = "UOM" Then
            dataRow.Range(1, i).value = uom
            colFound = True
            Exit For
        End If
    Next i
    If Not colFound Then Debug.Print "UOM column not found in data table"
    ' Set VENDOR value
    colFound = False
    For i = 1 To tbl.ListColumns.count
        If UCase(tbl.ListColumns(i).Name) = "VENDOR" Then
            dataRow.Range(1, i).value = vendor
            colFound = True
            Exit For
        End If
    Next i
    If Not colFound Then Debug.Print "VENDOR column not found in data table"
    ' Set LOCATION value
    colFound = False
    For i = 1 To tbl.ListColumns.count
        If UCase(tbl.ListColumns(i).Name) = "LOCATION" Then
            dataRow.Range(1, i).value = location
            colFound = True
            Exit For
        End If
    Next i
    If Not colFound Then Debug.Print "LOCATION column not found in data table"
    ' Set ITEM_CODE value - FIXED: Using correct parameter
    colFound = False
    For i = 1 To tbl.ListColumns.count
        If UCase(tbl.ListColumns(i).Name) = "ITEM_CODE" Then
            dataRow.Range(1, i).value = ItemCode  ' Using itemCode parameter
            colFound = True
            Exit For
        End If
    Next i
    If Not colFound Then Debug.Print "ITEM_CODE column not found in data table"
    ' Set ROW value - FIXED: Using correct parameter
    colFound = False
    For i = 1 To tbl.ListColumns.count
        If UCase(tbl.ListColumns(i).Name) = "ROW" Then
            dataRow.Range(1, i).value = rowNum  ' Using rowNum parameter
            colFound = True
            Exit For
        End If
    Next i
    If Not colFound Then Debug.Print "ROW column not found in data table"
    ' Set ENTRY_DATE value
    colFound = False
    For i = 1 To tbl.ListColumns.count
        If UCase(tbl.ListColumns(i).Name) = "ENTRY_DATE" Then
            dataRow.Range(1, i).value = Now()
            colFound = True
            Exit For
        End If
    Next i
    If Not colFound Then Debug.Print "ENTRY_DATE column not found in data table"
    On Error GoTo 0
End Sub
' Helper function to get location information
Private Function GetLocationByItem(ItemCode As String, itemName As String) As String
    On Error GoTo ErrorHandler
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim foundRow As Long
    Dim locationCol As Long
    GetLocationByItem = ""  ' Default value
    Set ws = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Set tbl = ws.ListObjects("invSys")
    ' Get the location column index
    On Error Resume Next
    locationCol = tbl.ListColumns("LOCATION").Index
    On Error GoTo ErrorHandler
    If locationCol = 0 Then Exit Function
    ' Try to find the item by code first
    If ItemCode <> "" Then
        foundRow = FindRowByValue(tbl, "ITEM_CODE", ItemCode)
    End If
    ' If not found by code, try by name
    If foundRow = 0 And itemName <> "" Then
        foundRow = FindRowByValue(tbl, "ITEM", itemName)
    End If
    ' If found, return the location
    If foundRow > 0 Then
        GetLocationByItem = tbl.DataBodyRange(foundRow, locationCol).value
    End If
    Exit Function
ErrorHandler:
    Debug.Print "Error in GetLocationByItem: " & Err.Description
    ' Return empty string on error
    GetLocationByItem = ""
End Function
' Delete existing data for this cell *****
Private Sub DeleteExistingDataForCell(dataTbl As ListObject, tallyRowNum As Long)
    On Error Resume Next
    ' Find the column that contains the tally row number
    Dim tallyRowCol As Long
    Dim i As Long
    ' First, try to find a TALLY_ROW column
    For i = 1 To dataTbl.ListColumns.count
        If UCase(dataTbl.ListColumns(i).Name) = "TALLY_ROW" Then
            tallyRowCol = i
            Exit For
        End If
    Next i
    ' If we still haven't found it, add the column
    If tallyRowCol = 0 Then
        dataTbl.ListColumns.Add.Name = "TALLY_ROW"
        tallyRowCol = dataTbl.ListColumns.count
    End If
    ' Find and delete all rows that match this tally row number
    Dim rowsToDelete As Collection
    Set rowsToDelete = New Collection
    ' Gather rows to delete (can't delete while iterating)
    For i = dataTbl.ListRows.count To 1 Step -1
        If dataTbl.DataBodyRange(i, tallyRowCol).value = tallyRowNum Then
            rowsToDelete.Add i
        End If
    Next i
    ' Now delete them (in reverse order to avoid index changes)
    For i = rowsToDelete.count To 1 Step -1
        dataTbl.ListRows(rowsToDelete(i)).Delete
    Next i
    On Error GoTo 0
End Sub
' Set the tally row number in the data table *****
Private Sub SetTallyRowNumber(dataRow As ListRow, tallyRowNum As Long)
    On Error Resume Next
    ' Find the column for storing the tally row number
    Dim tbl As ListObject
    Dim colFound As Boolean
    Set tbl = dataRow.Parent
    Dim i As Long
    ' Look for TALLY_ROW column
    colFound = False
    For i = 1 To tbl.ListColumns.count
        If UCase(tbl.ListColumns(i).Name) = "TALLY_ROW" Then
            dataRow.Range(1, i).value = tallyRowNum
            colFound = True
            Exit For
        End If
    Next i
    ' If we still haven't found it, add the column
    If Not colFound Then
        tbl.ListColumns.Add.Name = "TALLY_ROW"
        dataRow.Range(1, tbl.ListColumns.count).value = tallyRowNum
    End If
    On Error GoTo 0
End Sub
' Populate the list box with items from invSys table - FIXED
Private Sub PopulateListBox(itemArray As Variant)
    ' Debug what we're getting
    Debug.Print "PopulateListBox: Received itemArray with dimensions: " & _
                LBound(itemArray, 1) & " to " & UBound(itemArray, 1) & ", " & _
                LBound(itemArray, 2) & " to " & UBound(itemArray, 2)
    Dim i As Long
    Dim rowNum As String, ItemCode As String, itemName As String, vendor As String
    Me.lstBox.Clear
    ' Check if itemArray is properly initialized
    If IsEmpty(itemArray) Or Not IsArray(itemArray) Then
        Debug.Print "PopulateListBox: Invalid itemArray received"
        Exit Sub
    End If
    On Error Resume Next
    For i = LBound(itemArray, 1) To UBound(itemArray, 1)
        ' Make sure we have valid data before adding the item
        If IsArray(itemArray) And UBound(itemArray, 2) >= 2 Then
            ' Extract values with appropriate error handling
            rowNum = CStr(itemArray(i, 0))  ' ROW - FIXED: Now correctly using index 0
            ItemCode = CStr(itemArray(i, 1))  ' ITEM_CODE - FIXED: Now correctly using index 1
            ' Get the item name - column index 2 in the array
            If UBound(itemArray, 2) >= 2 Then
                itemName = CStr(itemArray(i, 2))  ' ITEM name
            Else
                itemName = "Unknown"
            End If
            ' Get vendor data from the invSys table
            vendor = GetVendorByItem(ItemCode, itemName)
            ' Add the item to the list box - FIXED order
            Me.lstBox.AddItem ""
            Me.lstBox.List(Me.lstBox.ListCount - 1, 0) = rowNum      ' ROW
            Me.lstBox.List(Me.lstBox.ListCount - 1, 1) = ItemCode    ' ITEM_CODE
            Me.lstBox.List(Me.lstBox.ListCount - 1, 2) = vendor      ' VENDOR
            Me.lstBox.List(Me.lstBox.ListCount - 1, 3) = itemName    ' ITEM name
        End If
    Next i
    On Error GoTo 0
End Sub
' Helper function to get vendor information
Private Function GetVendorByItem(ItemCode As String, itemName As String) As String
    On Error GoTo ErrorHandler
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim foundRow As Long
    Dim vendorCol As Long
    GetVendorByItem = ""  ' Default value
    Set ws = ThisWorkbook.Sheets("INVENTORY MANAGEMENT")
    Set tbl = ws.ListObjects("invSys")
    ' Get the vendor column index
    On Error Resume Next
    vendorCol = tbl.ListColumns("VENDOR(s)").Index
    On Error GoTo ErrorHandler
    If vendorCol = 0 Then Exit Function
    ' Try to find the item by code first
    If ItemCode <> "" Then
        foundRow = FindRowByValue(tbl, "ITEM_CODE", ItemCode)
    End If
    ' If not found by code, try by name
    If foundRow = 0 And itemName <> "" Then
        foundRow = FindRowByValue(tbl, "ITEM", itemName)
    End If
    ' If found, return the vendor
    If foundRow > 0 Then
        GetVendorByItem = tbl.DataBodyRange(foundRow, vendorCol).value
    End If
    Exit Function
ErrorHandler:
    Debug.Print "Error in GetVendorByItem: " & Err.Description
    ' Return empty string on error
    GetVendorByItem = ""
End Function
' Helper function to find a row by column value (if not already defined)
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
            Exit Function
        End If
    Next i
End Function
' Helper function to update the description in txtBox2
Private Sub UpdateDescription()
    ' Clear existing description
    Me.txtBox2.text = ""  ' Changed from .text to .Text
    ' If an item is selected in the main listbox
    If Me.lstBox.ListIndex <> -1 Then
        ' Get the selected index
        Dim selectedIndex As Integer
        selectedIndex = Me.lstBox.ListIndex
        ' Get the description for this item from the FullItemList
        ' Add 1 because ListBox is 0-based but array is 1-based
        If selectedIndex + 1 <= UBound(FullItemList, 1) Then
            ' Set the description text
            Me.txtBox2.text = FullItemList(selectedIndex + 1, 4)  ' Changed from .text to .Text
        End If
    End If
End Sub
' Handle Tab key in the form
Private Sub UserForm_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    ' If Tab is pressed, we want to handle it specially
    If KeyCode = vbKeyTab Then
        ' If user has made a selection or has text in the box, commit it
        If Me.lstBox.ListIndex <> -1 Or Trim(Me.txtBox.text) <> "" Then
            CommitSelectionAndClose
        Else
            ' Otherwise just close the form without changes
            Unload Me
        End If
        KeyCode = 0 ' Prevent default tab handling
    End If
End Sub


