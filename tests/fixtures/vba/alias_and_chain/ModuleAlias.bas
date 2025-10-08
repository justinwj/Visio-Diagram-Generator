Attribute VB_Name = "ModuleAlias"
Option Explicit

Public Sub Caller()
    Dim worker As New Worker
    Dim workerAlias As Worker
    Set workerAlias = worker
    workerAlias.DoWork

    Dim helperAlias As Helper
    Set helperAlias = worker.Factory()
    helperAlias.RunAll

    worker.Factory().RunAll
End Sub

