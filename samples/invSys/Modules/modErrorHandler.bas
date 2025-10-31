Attribute VB_Name = "modErrorHandler"
' Module: ErrorHandler
' Provides data validation and error handling.
Option Explicit
Public Sub ValidateAndProcessInput(inputValue As Variant, fieldName As String)
    On Error GoTo ErrorHandler
    If IsEmpty(inputValue) Or IsNull(inputValue) Then
        Err.Raise vbObjectError + 1, , "The field " & fieldName & " cannot be empty."
    End If
    If IsNumeric(inputValue) Then
        If inputValue < 0 Then
            Err.Raise vbObjectError + 2, , "The field " & fieldName & " cannot have a negative value."
        End If
    Else
        Err.Raise vbObjectError + 3, , "The field " & fieldName & " must be a numeric value."
    End If
    ' Additional processing logic can go here
    Exit Sub
ErrorHandler:
    MsgBox "Error in " & fieldName & ": " & Err.Description, vbCritical
    Err.Clear
End Sub
Public Sub HandleError(moduleName As String, procedureName As String)
    On Error Resume Next
    MsgBox "An error occurred in " & moduleName & "." & procedureName & ": " & Err.Description, vbCritical
    Err.Clear
End Sub
Public Sub SafeExecute(Action As String, ByRef actionProcedure As Variant)
    On Error GoTo ErrorHandler
    Application.EnableEvents = False
    Application.ScreenUpdating = False
    CallByName actionProcedure, Action, VbMethod
    Exit Sub
ErrorHandler:
    MsgBox "Error executing action: " & Action & vbNewLine & Err.Description, vbCritical
    Err.Clear
Finally:
    Application.EnableEvents = True
    Application.ScreenUpdating = True
End Sub
Public Sub HandleItemCodeOverflow()
    MsgBox "All possible item codes have been exhausted. Please contact support.", vbCritical
End Sub
' Logs error details into the ErrorLog sheet
Public Sub LogError(ByVal procedureName As String, ByVal errNumber As Long, ByVal errDescription As String)
    Dim ws As Worksheet
    Dim newRow As Range
    ' Set the error log worksheet
    Set ws = ThisWorkbook.Sheets("ErrorLog")
    ' Find the next available row
    Set newRow = ws.Cells(ws.Rows.count, 1).End(xlUp).offset(1, 0)
    ' Record the error details
    newRow.Cells(1, 1).value = Now()  ' Timestamp
    newRow.Cells(1, 2).value = procedureName
    newRow.Cells(1, 3).value = errNumber
    newRow.Cells(1, 4).value = errDescription
    ' Optional: Display an immediate alert (can be removed if not needed)
    MsgBox "An error occurred in " & procedureName & ": " & errDescription, vbExclamation, "Error " & errNumber
End Sub
' Renamed to LogAndHandleError to avoid conflicts
Public Sub LogAndHandleError(ByVal procedureName As String)
    If Err.Number <> 0 Then
        LogError procedureName, Err.Number, Err.Description
        Err.Clear
    End If
End Sub







