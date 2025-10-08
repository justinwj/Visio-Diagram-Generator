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
    worker.Factory( _
        1, _
        "x" _
    ).RunAll

    Dim multiAlias As Helper
    Set multiAlias = worker. _
        Factory(). _
        RunFactory()
    multiAlias.RunAll
End Sub
