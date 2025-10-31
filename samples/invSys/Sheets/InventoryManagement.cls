VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "InventoryManagement"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = True

Private Sub Worksheet_Change(ByVal Target As Range)
    Dim tbl As ListObject
    Dim emptyRows As Range
    Dim PREFIX As String
    Dim idNumber As Long
    Dim existingCodes As Object
    Dim cell As Range
    Dim maxRowNum As Long
    On Error GoTo ErrorHandler
    ' Debug info
    Debug.Print "Worksheet_Change triggered at " & Now()
    Debug.Print "Target address: " & Target.Address
    ' Only process if we're dealing with the table
    Set tbl = Me.ListObjects("invSys")
    If tbl Is Nothing Then
        Debug.Print "Table 'invSys' not found"
        Exit Sub
    End If
    ' Check if change is in the table
    If Not Intersect(Target, tbl.Range) Is Nothing Then
        Debug.Print "Change detected in table"
        Application.EnableEvents = False
        ' ----- Generate Unique Item_Code for New Rows -----
        Set existingCodes = CreateObject("Scripting.Dictionary")
        For Each cell In tbl.ListColumns("Item_Code").DataBodyRange
            If Not IsEmpty(cell.value) Then existingCodes(cell.value) = True
        Next cell
        For Each cell In tbl.ListColumns("Item_Code").DataBodyRange
            If IsEmpty(cell.value) Then
                Debug.Print "Empty Item_Code found at row " & cell.row
                PREFIX = "ITEM-"
                idNumber = 1
                Do While existingCodes.Exists(PREFIX & Format(idNumber, "0000"))
                    idNumber = idNumber + 1
                Loop
                cell.value = PREFIX & Format(idNumber, "0000")
                existingCodes.Add cell.value, True
                Debug.Print "Assigned " & cell.value & " to row " & cell.row
            End If
        Next cell
        ' ----- Generate Sequential ROW Numbers for New Rows -----
        ' Find the maximum existing row number
        maxRowNum = 0
        For Each cell In tbl.ListColumns("ROW").DataBodyRange
            If Not IsEmpty(cell.value) Then
                If IsNumeric(cell.value) Then
                    If CLng(cell.value) > maxRowNum Then maxRowNum = CLng(cell.value)
                End If
            End If
        Next cell
        Debug.Print "Maximum ROW number found: " & maxRowNum
        ' Assign next row number to empty cells in ROW column
        For Each cell In tbl.ListColumns("ROW").DataBodyRange
            If IsEmpty(cell.value) Then
                maxRowNum = maxRowNum + 1
                cell.value = maxRowNum
                Debug.Print "Assigned ROW number " & maxRowNum & " to row " & cell.row
            End If
        Next cell
    Else
        Debug.Print "Change not in table"
    End If
ExitHandler:
    Application.EnableEvents = True
    Exit Sub
ErrorHandler:
    Debug.Print "Error: " & Err.Description & " (Error " & Err.Number & ")"
    Resume ExitHandler
End Sub
' Add a TableChange event handler to better catch row additions
Private Sub Worksheet_TableUpdate(ByVal Target As TableObject)
    If Target.Name = "invSys" Then
        Debug.Print "Table updated at " & Now()
        ' Process the table changes by simulating a worksheet change
        Worksheet_Change Target.Range
    End If
End Sub

