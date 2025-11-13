VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "clsBulkSnapshot"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False

'// CLASS MODULE: clsBulkSnapshot,a part of UR (UndoRedo system)
Option Explicit

Public SnapshotID As String
Public data As Variant  ' Stores inventory values
Public Formulas As Object ' Stores formulas using a Dictionary
Public SchemaHash As String
Public timestamp As Date
' Initialize Dictionary in Class Constructor
Private Sub class_initialize()
    Set Formulas = CreateObject("Scripting.Dictionary") ' ? Fix: Initialize Dictionary
End Sub

