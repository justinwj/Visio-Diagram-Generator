Attribute VB_Name = "modTestModule"

' Module: TestModule
' Subroutine to test for data integrity in the invSys table
Sub TestDataIntegrity()
    Dim ws As Worksheet, summaryWs As Worksheet
    Dim tbl As ListObject
    Dim row As ListRow
    Dim errorsFound As Boolean
    Dim logRow As Long
    On Error GoTo ErrorHandler
    Set ws = ThisWorkbook.Worksheets("INVENTORY MANAGEMENT")
    Set summaryWs = ThisWorkbook.Worksheets("TestSummary")
    Set tbl = ws.ListObjects("invSys")
    errorsFound = False
    ' Write header for TestSummary if empty
    If summaryWs.Cells(1, 1).value = "" Then
        summaryWs.Range("A1:D1").value = Array("Test Name", "Row", "Issue", "Timestamp")
    End If
    ' Initialize log row
    logRow = summaryWs.Cells(summaryWs.Rows.count, 1).End(xlUp).row + 1
    For Each row In tbl.ListRows
        With row
            ' Test: Item_Code must not be blank
            If IsEmpty(.Range("Item_Code").value) Then
                summaryWs.Cells(logRow, 1).value = "TestDataIntegrity"
                summaryWs.Cells(logRow, 2).value = row.Index
                summaryWs.Cells(logRow, 3).value = "Missing Item_Code"
                summaryWs.Cells(logRow, 4).value = Now
                logRow = logRow + 1
                errorsFound = True
            End If
            ' Test: TOTAL INV must not be negative
            If .Range("TOTAL INV").value < 0 Then
                summaryWs.Cells(logRow, 1).value = "TestDataIntegrity"
                summaryWs.Cells(logRow, 2).value = row.Index
                summaryWs.Cells(logRow, 3).value = "Negative TOTAL INV"
                summaryWs.Cells(logRow, 4).value = Now
                logRow = logRow + 1
                errorsFound = True
            End If
            ' Test: Mandatory columns (ITEM, DESCRIPTION) must not be blank
            If IsEmpty(.Range("ITEM").value) Or IsEmpty(.Range("DESCRIPTION").value) Then
                summaryWs.Cells(logRow, 1).value = "TestDataIntegrity"
                summaryWs.Cells(logRow, 2).value = row.Index
                summaryWs.Cells(logRow, 3).value = "Missing mandatory data (ITEM/DESCRIPTION)"
                summaryWs.Cells(logRow, 4).value = Now
                logRow = logRow + 1
                errorsFound = True
            End If
        End With
    Next row
    If Not errorsFound Then
        summaryWs.Cells(logRow, 1).value = "TestDataIntegrity"
        summaryWs.Cells(logRow, 3).value = "All tests passed successfully"
        summaryWs.Cells(logRow, 4).value = Now
    End If
ExitSub:
    Exit Sub
ErrorHandler:
    ErrorHandler.HandleError "TestDataIntegrity"
    Resume ExitSub
End Sub
' Subroutine to perform boundary tests on TOTAL INV column
Sub TestBoundaryConditions()
    Dim ws As Worksheet, summaryWs As Worksheet
    Dim tbl As ListObject
    Dim row As ListRow
    Dim maxLimit As Double
    Dim errorsFound As Boolean
    Dim logRow As Long
    On Error GoTo ErrorHandler
    Set ws = ThisWorkbook.Worksheets("INVENTORY MANAGEMENT")
    Set summaryWs = ThisWorkbook.Worksheets("TestSummary")
    Set tbl = ws.ListObjects("invSys")
    errorsFound = False
    maxLimit = 10000 ' Example maximum inventory limit
    ' Initialize log row
    logRow = summaryWs.Cells(summaryWs.Rows.count, 1).End(xlUp).row + 1
    For Each row In tbl.ListRows
        With row
            ' Test: TOTAL INV must not exceed maxLimit
            If .Range("TOTAL INV").value > maxLimit Then
                summaryWs.Cells(logRow, 1).value = "TestBoundaryConditions"
                summaryWs.Cells(logRow, 2).value = row.Index
                summaryWs.Cells(logRow, 3).value = "TOTAL INV exceeds limit (" & .Range("TOTAL INV").value & ")"
                summaryWs.Cells(logRow, 4).value = Now
                logRow = logRow + 1
                errorsFound = True
            End If
        End With
    Next row
    If Not errorsFound Then
        summaryWs.Cells(logRow, 1).value = "TestBoundaryConditions"
        summaryWs.Cells(logRow, 3).value = "All tests passed successfully"
        summaryWs.Cells(logRow, 4).value = Now
    End If
ExitSub:
    Exit Sub
ErrorHandler:
    ErrorHandler.HandleError "TestBoundaryConditions"
    Resume ExitSub
End Sub







