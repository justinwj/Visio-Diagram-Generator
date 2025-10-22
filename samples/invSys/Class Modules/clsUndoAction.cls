VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "clsUndoAction"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False

'// CLASS MODULE: clsUndoAction, a part of UR (UndoRedo system)
Option Explicit

Public ActionType As String
Public ItemCode As String
Public ColumnName As String
Public OldValue As Variant
Public newValue As Variant
Public SnapshotID As String
Public timestamp As Date
Public logData As Collection
Public LogCount As Long
Public RedoSnapshotID As String

