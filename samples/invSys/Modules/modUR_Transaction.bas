Attribute VB_Name = "modUR_Transaction"
'// MODULE: modUR_Transaction
Option Explicit
Private TransactionBuffer As Collection
Private InTransaction As Boolean
Private PreTransactionSnapshotID As String
Private CurrentTransactionLogCount As Long   ' New variable to hold the log entry count
Public Sub TrackTransactionChange( _
    ByVal ActionType As String, _
    ByVal ItemCode As String, _
    ByVal ColumnName As String, _
    ByVal OldValue As Variant, _
    ByVal newValue As Variant)
    If Not InTransaction Then
        Call modUR_UndoRedo.TrackChange(ActionType, ItemCode, ColumnName, OldValue, newValue)
        Exit Sub
    End If
    If TransactionBuffer Is Nothing Then Set TransactionBuffer = New Collection
    Dim i As Integer
    Dim Action As clsUndoAction
    For i = 1 To TransactionBuffer.count
        Set Action = TransactionBuffer(i)
        If Action.ItemCode = ItemCode And Action.ColumnName = ColumnName Then
            Action.newValue = newValue
            Exit Sub
        End If
    Next i
    Set Action = New clsUndoAction
    With Action
        .ActionType = ActionType
        .ItemCode = ItemCode
        .ColumnName = ColumnName
        .OldValue = OldValue
        .newValue = newValue
        .timestamp = Now
    End With
    TransactionBuffer.Add Action
End Sub
Public Sub BeginTransaction()
    If InTransaction Then Exit Sub
    Set TransactionBuffer = New Collection
    InTransaction = True
    ' Capture the snapshot BEFORE the bulk operation begins.
    PreTransactionSnapshotID = modUR_Snapshot.CaptureSnapshot()
    CurrentTransactionLogCount = 0
End Sub
Public Sub CommitTransaction()
    If Not InTransaction Then Exit Sub
    If TransactionBuffer Is Nothing Or TransactionBuffer.count = 0 Then Exit Sub
    Dim BulkAction As New clsUndoAction
    BulkAction.ActionType = "BulkTransaction"
    BulkAction.SnapshotID = PreTransactionSnapshotID
    BulkAction.timestamp = Now
    BulkAction.LogCount = CurrentTransactionLogCount   ' Store the number of log rows inserted
    Dim Action As clsUndoAction
    Dim i As Integer
    For i = 1 To TransactionBuffer.count
        Set Action = TransactionBuffer(i)
        BulkAction.ItemCode = Action.ItemCode   ' for reference
        BulkAction.ColumnName = Action.ColumnName
        BulkAction.OldValue = Action.OldValue
        BulkAction.newValue = Action.newValue
        BulkAction.RedoSnapshotID = modUR_Snapshot.CaptureSnapshot()
    Next i
    Call modUR_UndoRedo.AddToUndoStack(BulkAction)
    Set TransactionBuffer = Nothing
    InTransaction = False
End Sub
' (Other procedures remain unchanged.)
Public Function IsInTransaction() As Boolean
    IsInTransaction = InTransaction
End Function
' Expose the current transaction's SnapshotID for logging purposes.
Public Function GetCurrentTransactionID() As String
    GetCurrentTransactionID = PreTransactionSnapshotID
End Function
' Allow setting the log count from the calling routine.
Public Sub SetCurrentTransactionLogCount(ByVal count As Long)
    CurrentTransactionLogCount = count
End Sub
Public Sub RollbackTransaction()
    If Not InTransaction Then Exit Sub
    ' Optionally, you could restore the pre-transaction snapshot:
    modUR_Snapshot.RestoreSnapshot PreTransactionSnapshotID
    Set TransactionBuffer = Nothing
    InTransaction = False
    MsgBox "Transaction rolled back.", vbInformation, "Transaction Rollback"
End Sub











