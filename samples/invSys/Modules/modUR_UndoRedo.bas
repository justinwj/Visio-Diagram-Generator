Attribute VB_Name = "modUR_UndoRedo"
'// MODULE: modUR_UndoRedo
Option Explicit
Private UndoStack As New Collection
Private RedoStack As New Collection
Public Sub TrackChange( _
    ByVal ActionType As String, _
    ByVal ItemCode As String, _
    Optional ByVal ColumnName As String, _
    Optional ByVal OldValue As Variant, _
    Optional ByVal newValue As Variant)
    Dim Action As New clsUndoAction
    With Action
        .ActionType = ActionType
        .ItemCode = ItemCode
        .ColumnName = ColumnName
        .OldValue = OldValue
        .newValue = newValue
        .timestamp = Now
    End With
    UndoStack.Add Action
    PruneUndoStack 50
    Set RedoStack = New Collection
End Sub
Public Sub UndoLastAction()
    If UndoStack.count = 0 Then
        MsgBox "No actions to undo.", vbExclamation, "Undo"
        Exit Sub
    End If
    Dim Action As clsUndoAction
    Set Action = UndoStack(UndoStack.count)
    UndoStack.Remove UndoStack.count
    Select Case Action.ActionType
        Case "BulkTransaction"
            ' Remove the last LogCount rows from InventoryLog and store them
            Set Action.logData = modInvLogs.RemoveLastBulkLogEntries(Action.LogCount)
            Call modUR_Snapshot.RestoreSnapshot(Action.SnapshotID)
        Case Else
            Debug.Print "Unknown Undo Action Type:", Action.ActionType
    End Select
    RedoStack.Add Action
    MsgBox "Undo successful.", vbInformation, "Undo"
End Sub
Public Sub RedoLastAction()
    If RedoStack.count = 0 Then
        MsgBox "No actions to redo.", vbExclamation, "Redo"
        Exit Sub
    End If
    Dim Action As clsUndoAction
    Set Action = RedoStack(RedoStack.count)
    RedoStack.Remove RedoStack.count
    Select Case Action.ActionType
        Case "BulkTransaction"
            Call modUR_Snapshot.RestoreSnapshot(Action.RedoSnapshotID)
            modInvLogs.ReAddBulkLogEntries Action.logData
        Case Else
            Debug.Print "Unknown Redo Action Type:", Action.ActionType
    End Select
    UndoStack.Add Action
    MsgBox "Redo successful.", vbInformation, "Redo"
End Sub
Private Sub PruneUndoStack(ByVal MaxSize As Long)
    Do While UndoStack.count > MaxSize
        UndoStack.Remove 1
    Loop
End Sub
Public Sub AddToUndoStack(ByVal Action As clsUndoAction)
    UndoStack.Add Action
    PruneUndoStack 50
    Set RedoStack = New Collection
End Sub
Public Sub ClearRedoStack()
    Set RedoStack = New Collection
End Sub
Public Function GetUndoStack() As Collection
    Set GetUndoStack = UndoStack
End Function











