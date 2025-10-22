Attribute VB_Name = "modExportImportAll"

'────────────────────────────────────────────────────────────
' Module: modExportImportAll
' ----put these in the immediate window to run:----
' ExportAllCodeToSingleFiles
' ExportTablesHeadersAndControls
' SyncSheetsCodeBehind
' SyncFormsCodeBehind
' SyncClassModules
' SyncStandardModules

' ReplaceAllCodeFromFiles
' ExportAllModules

' ListSheetCodeNames
' ExportUserFormControls
' SyncSheetsCodeBehind_Diagnostics
'────────────────────────────────────────────────────────────

Option Explicit
' Subroutine to export all modules, classes, forms, and Excel objects (sheets, workbook)
Sub ExportAllModules()
    Dim vbComp As VBIDE.VBComponent
    Dim exportPath As String
    Dim fso As Object
    Dim fileItem As Object

    ' Root folder path; ensure subfolders "Sheets", "Forms", "Modules", and "Class Modules" exist.
    exportPath = "D:\justinwj\Workbooks\0_PROJECT_invSys"
    ' Ensure trailing backslash
    If Right(exportPath, 1) <> "\" Then exportPath = exportPath & "\"

    ' Make sure Excel is set to allow programmatic access to VBProject
    ' (Trust Center > Macro Settings > Trust access to the VBA project object model)

    For Each vbComp In ThisWorkbook.VBProject.VBComponents
        Select Case vbComp.Type
            Case vbext_ct_StdModule ' Standard Modules
                On Error Resume Next
                vbComp.Export exportPath & "Modules\" & vbComp.Name & ".bas"
                On Error GoTo 0

            Case vbext_ct_ClassModule ' Class Modules
                On Error Resume Next
                vbComp.Export exportPath & "Class Modules\" & vbComp.Name & ".cls"
                On Error GoTo 0

            Case vbext_ct_MSForm ' UserForms
                On Error Resume Next
                vbComp.Export exportPath & "Forms\" & vbComp.Name & ".frm"
                On Error GoTo 0

            Case vbext_ct_Document ' Sheets and ThisWorkbook
                On Error Resume Next
                vbComp.Export exportPath & "Sheets\" & vbComp.Name & ".cls"
                On Error GoTo 0
        End Select
    Next vbComp

    ' Remove FRX files from the Forms folder, if present
    Set fso = CreateObject("Scripting.FileSystemObject")
    If fso.FolderExists(exportPath & "Forms") Then
        For Each fileItem In fso.GetFolder(exportPath & "Forms").Files
            If LCase(fso.GetExtensionName(fileItem.Name)) = "frx" Then
                fileItem.Delete True
            End If
        Next fileItem
    End If

    MsgBox "Export complete!"
End Sub

Public Sub ReplaceAllCodeFromFiles()
    SyncStandardModules
    SyncClassModules
    SyncFormsCodeBehind
    SyncSheetsCodeBehind
    MsgBox "All VBA code synced!", vbInformation
End Sub
' Sync only .bas modules by removing and re-importing
Public Sub SyncStandardModules()
    Const ROOT_PATH As String = "D:\\justinwj\\Workbooks\\0_PROJECT_invSys\\Modules\\"
    Dim fso As Object
    Dim vbProj As VBIDE.VBProject
    Dim fileItem As Object
    Dim baseName As String
    Dim filePath As String

    Set fso = CreateObject("Scripting.FileSystemObject")
    Set vbProj = ThisWorkbook.VBProject

    If Not fso.FolderExists(ROOT_PATH) Then Exit Sub
    For Each fileItem In fso.GetFolder(ROOT_PATH).Files
        If LCase(fso.GetExtensionName(fileItem.Name)) = "bas" Then
            baseName = fso.GetBaseName(fileItem.Name)
            ' Skip the exporter module itself
            If LCase(baseName) = "modexportimportall" Then GoTo NextStandard
            filePath = fileItem.Path

            On Error Resume Next
            ' Remove existing module to avoid corruption
            vbProj.VBComponents.Remove vbProj.VBComponents(baseName)
            On Error GoTo 0
            ' Import fresh module
            vbProj.VBComponents.Import filePath
        End If
NextStandard:
    Next fileItem
    ' Notify user when standard modules are imported
    MsgBox "Standard modules imported successfully!", vbInformation
End Sub

' Sync only .cls class modules by removing and re-importing
Public Sub SyncClassModules()
    Const ROOT_PATH As String = "D:\\justinwj\\Workbooks\\0_PROJECT_invSys\\Class Modules\\"
    Dim fso As Object
    Dim vbProj As VBIDE.VBProject
    Dim fileItem As Object
    Dim baseName As String
    Dim filePath As String

    Set fso = CreateObject("Scripting.FileSystemObject")
    Set vbProj = ThisWorkbook.VBProject

    If Not fso.FolderExists(ROOT_PATH) Then Exit Sub
    For Each fileItem In fso.GetFolder(ROOT_PATH).Files
        If LCase(fso.GetExtensionName(fileItem.Name)) = "cls" Then
            baseName = fso.GetBaseName(fileItem.Name)
            filePath = fileItem.Path

            On Error Resume Next
            ' Remove existing class module to avoid corruption
            vbProj.VBComponents.Remove vbProj.VBComponents(baseName)
            On Error GoTo 0
            ' Import fresh class module
            vbProj.VBComponents.Import filePath
        End If
    Next fileItem
    ' Notify user when class modules are imported
    MsgBox "Class modules imported successfully!", vbInformation
End Sub
    
'updates code to whatever is in ROOT_PATH & Forms
Public Sub SyncFormsCodeBehind()
    Const ROOT_PATH As String = "D:\justinwj\Workbooks\0_PROJECT_invSys\"
    Dim fso     As Object: Set fso = CreateObject("Scripting.FileSystemObject")
    Dim vbProj  As VBIDE.VBProject: Set vbProj = ThisWorkbook.VBProject
    Dim folder  As Object, fileItem As Object
    Dim fileText As String, lines As Variant
    Dim i        As Long, startIdx As Long
    Dim codeBody As String
    Dim compName As String
    Dim vbComp   As VBIDE.VBComponent
    Dim lineText As String

    If Not fso.FolderExists(ROOT_PATH & "Forms\") Then Exit Sub
    Set folder = fso.GetFolder(ROOT_PATH & "Forms\")

    For Each fileItem In folder.Files
        If LCase(fso.GetExtensionName(fileItem.Name)) = "frm" Then
            compName = fso.GetBaseName(fileItem.Name)
            On Error Resume Next
            Set vbComp = vbProj.VBComponents(compName)
            On Error GoTo 0

            If vbComp Is Nothing Then
                Debug.Print "Form not in project: " & compName
            Else
                fileText = fso.OpenTextFile(fileItem.Path, 1).ReadAll
                lines = Split(fileText, vbCrLf)

                ' find first real code line
                startIdx = -1
                For i = LBound(lines) To UBound(lines)
                    lineText = Trim(lines(i))
                    If lineText = "Option Explicit" _
                       Or LCase(Left(lineText, 4)) = "sub " _
                       Or LCase(Left(lineText, 8)) = "function" _
                       Or LCase(Left(lineText, 7)) = "private" _
                       Or LCase(Left(lineText, 6)) = "public " Then
                        startIdx = i: Exit For
                    End If
                Next i

                If startIdx >= 0 Then
                    codeBody = ""
                    For i = startIdx To UBound(lines)
                        codeBody = codeBody & lines(i) & vbCrLf
                    Next i
                    With vbComp.CodeModule
                        .DeleteLines 1, .CountOfLines
                        .InsertLines 1, codeBody
                    End With
                Else
                    Debug.Print "  ? no code found in " & fileItem.Name
                End If
            End If
        End If
    Next fileItem

    MsgBox "Forms code-behind synced.", vbInformation
End Sub

' Updates Sheet (Microsoft Excel Objects) code to whatever is in ROOT_PATH
Public Sub SyncSheetsCodeBehind()
    Const ROOT_PATH As String = "D:\justinwj\Workbooks\0_PROJECT_invSys\Sheets\"
    Dim fso       As Object: Set fso = CreateObject("Scripting.FileSystemObject")
    Dim vbProj    As VBIDE.VBProject: Set vbProj = ThisWorkbook.VBProject
    Dim folder    As Object, fileItem As Object
    Dim txt       As String, lines As Variant
    Dim codeBody  As String
    Dim i         As Long
    Dim trimmed   As String, lowerText As String
    Dim compName  As String
    Dim vbComp    As VBIDE.VBComponent

    If Not fso.FolderExists(ROOT_PATH) Then
        MsgBox "Sheets folder not found: " & ROOT_PATH, vbExclamation
        Exit Sub
    End If

    Set folder = fso.GetFolder(ROOT_PATH)
    For Each fileItem In folder.Files
        If LCase(fso.GetExtensionName(fileItem.Name)) = "cls" Then
            compName = fso.GetBaseName(fileItem.Name)

            On Error Resume Next
            Set vbComp = vbProj.VBComponents(compName)
            On Error GoTo 0
            If vbComp Is Nothing Then GoTo NextFile

            txt = fso.OpenTextFile(fileItem.Path, 1).ReadAll
            lines = Split(txt, vbCrLf)
            codeBody = ""

            For i = LBound(lines) To UBound(lines)
                trimmed = Trim(lines(i))
                lowerText = LCase(trimmed)

                If trimmed = "" Then
                    ' preserve blank lines
                    codeBody = codeBody & vbCrLf

                ElseIf Not ( _
                    lowerText Like "version *" Or _
                    lowerText Like "begin*" Or _
                    lowerText = "end" Or _
                    lowerText Like "attribute *" Or _
                    lowerText Like "mult?use *" _
                ) Then
                    codeBody = codeBody & lines(i) & vbCrLf
                End If
            Next i

            With vbComp.CodeModule
                .DeleteLines 1, .CountOfLines
                .InsertLines 1, codeBody
            End With

            Set vbComp = Nothing
        End If
NextFile:
    Next fileItem

    MsgBox "Sheets code-behind synced!", vbInformation
End Sub

Public Sub SyncSheetsCodeBehind_Diagnostics()
    Const ROOT_PATH As String = "D:\justinwj\Workbooks\0_PROJECT_invSys\Sheets\"
    Dim fso      As Object: Set fso = CreateObject("Scripting.FileSystemObject")
    Dim vbProj   As VBIDE.VBProject: Set vbProj = ThisWorkbook.VBProject
    Dim folder   As Object, fileItem As Object
    Dim compName As String
    Dim vbComp   As VBIDE.VBComponent
    
    If Not fso.FolderExists(ROOT_PATH) Then
        MsgBox "Folder not found: " & ROOT_PATH, vbExclamation
        Exit Sub
    End If
    
    Set folder = fso.GetFolder(ROOT_PATH)
    Debug.Print "=== Files in Sheets\ ==="
    For Each fileItem In folder.Files
        If LCase(fso.GetExtensionName(fileItem.Name)) = "cls" Then
            compName = fso.GetBaseName(fileItem.Name)
            Debug.Print "File: "; fileItem.Name; " ? looking for component: "; compName
            
            On Error Resume Next
            Set vbComp = vbProj.VBComponents(compName)
            On Error GoTo 0
            
            If vbComp Is Nothing Then
                Debug.Print "   ? No matching VBComponent for "; compName
            Else
                Debug.Print "   ? Found VBComponent: "; vbComp.Name
                ' (Here you could inject your DeleteLines/InsertLines logic)
            End If
            Set vbComp = Nothing
        End If
    Next fileItem
    
    MsgBox "Diagnostics complete—check the Immediate window (Ctrl+G).", vbInformation
End Sub

Sub ExportTablesHeadersAndControls()
    Dim ws As Worksheet
    Dim lo As ListObject
    Dim lc As ListColumn
    Dim ole As OLEObject
    Dim shp As Shape
    Dim folderPath As String, outputPath As String
    Dim Fnum As Long, hdrs As String
    Dim ctrlType As Long, ctrlTypeName As String
    ' 1) Set your folder (must already exist)
    folderPath = "D:\justinwj\Workbooks\0_PROJECT_invSys\"
    If Right(folderPath, 1) <> "\" Then folderPath = folderPath & "\"
    ' 2) Append filename
    outputPath = folderPath & "TablesHeadersAndControls.txt"
    Fnum = FreeFile
    Open outputPath For Output As #Fnum
    For Each ws In ThisWorkbook.Worksheets
        Print #Fnum, "Sheet (Tab):  " & ws.Name
        Print #Fnum, "Sheet (Code): " & ws.CodeName
        ' ? Tables & Headers ?
        For Each lo In ws.ListObjects
            Print #Fnum, "  Table: " & lo.Name
            hdrs = ""
            For Each lc In lo.ListColumns
                hdrs = hdrs & lc.Name & ", "
            Next lc
            If Len(hdrs) > 0 Then hdrs = Left(hdrs, Len(hdrs) - 2)
            Print #Fnum, "    Headers: " & hdrs
        Next lo
        ' ? ActiveX Controls ?
        For Each ole In ws.OLEObjects
            Print #Fnum, "  ActiveX Control: " & ole.Name & " (" & ole.progID & ")"
            On Error Resume Next
            Print #Fnum, "    LinkedCell: " & ole.LinkedCell
            Print #Fnum, "    TopLeft: " & ole.TopLeftCell.Address(False, False)
            Print #Fnum, "    Caption: " & ole.Object.Caption
            Print #Fnum, "    Value: " & ole.Object.value
            On Error GoTo 0
        Next ole
        ' ? Forms Controls ?
        For Each shp In ws.Shapes
            If shp.Type = msoFormControl Then
                ctrlType = shp.FormControlType
                Select Case ctrlType
                    Case 0: ctrlTypeName = "Button"
                    Case 1: ctrlTypeName = "Checkbox"
                    Case 2: ctrlTypeName = "DropDown"
                    Case 3: ctrlTypeName = "EditBox"
                    Case 4: ctrlTypeName = "ListBox"
                    Case 5: ctrlTypeName = "ScrollBar"
                    Case 6: ctrlTypeName = "Spinner"
                    Case Else: ctrlTypeName = "Unknown"
                End Select
                Print #Fnum, "  Form Control: " & shp.Name
                Print #Fnum, "    Type: " & ctrlTypeName & " (" & ctrlType & ")"
                On Error Resume Next
                Print #Fnum, "    LinkedCell: " & shp.ControlFormat.LinkedCell
                If shp.HasTextFrame Then
                    Print #Fnum, "    Text: " & Replace(shp.TextFrame.Characters.text, vbCr, " ")
                End If
                On Error GoTo 0
            End If
        Next shp
        Print #Fnum, String(60, "-")
    Next ws
    Close #Fnum
    MsgBox "Export complete:" & vbCrLf & outputPath, vbInformation
End Sub
Sub ExportUserFormControls()
    Dim vbProj As VBIDE.VBProject
    Dim vbComp As VBIDE.VBComponent
    Dim ctrl   As MSForms.Control
    Dim outputPath As String, Fnum As Long
    '? adjust folder as needed (must exist) ?
    outputPath = "D:\justinwj\Workbooks\0_PROJECT_invSys\UserFormControls.txt"
    Fnum = FreeFile
    Open outputPath For Output As #Fnum
    Set vbProj = ThisWorkbook.VBProject
    For Each vbComp In vbProj.VBComponents
        ' only UserForm components
        If vbComp.Type = vbext_ct_MSForm Then
            Print #Fnum, "UserForm: " & vbComp.Name
            ' iterate its controls
            For Each ctrl In vbComp.Designer.Controls
                Print #Fnum, "  Control: " & ctrl.Name & " (" & TypeName(ctrl) & ")"
                On Error Resume Next
                ' many controls have a Caption
                Print #Fnum, "    Caption: " & ctrl.Caption
                ' and many have a Value
                Print #Fnum, "    Value: " & ctrl.value
                On Error GoTo 0
            Next ctrl
            Print #Fnum, String(50, "-")
        End If
    Next vbComp
    Close #Fnum
    MsgBox "UserForm controls exported to:" & vbCrLf & outputPath, vbInformation
End Sub

' Requires reference to “Microsoft Visual Basic for Applications Extensibility 5.3”
' and Trust Center > Macro Settings > “Trust access to the VBA project object model” enabled.

Public Sub ExportAllCodeToSingleFiles()
    Dim exportPath As String
    Dim wsFileNum   As Long, frmFileNum As Long
    Dim clsFileNum  As Long, modFileNum As Long
    Dim vbComp      As VBIDE.VBComponent
    Dim codeMod     As VBIDE.CodeModule
    
    ' ? Modify this to your desired folder (must already exist)
    exportPath = "D:\justinwj\Workbooks\0_PROJECT_invSys"
    If Right(exportPath, 1) <> "\" Then exportPath = exportPath & "\"
    
    ' Open our four output files
    wsFileNum = FreeFile: Open exportPath & "SheetsCode.txt" For Output As #wsFileNum
    frmFileNum = FreeFile: Open exportPath & "FormsCode.txt" For Output As #frmFileNum
    clsFileNum = FreeFile: Open exportPath & "ClassModulesCode.txt" For Output As #clsFileNum
    modFileNum = FreeFile: Open exportPath & "StandardModulesCode.txt" For Output As #modFileNum
    
    ' Loop through every component in this workbook
    For Each vbComp In ThisWorkbook.VBProject.VBComponents
        Set codeMod = vbComp.CodeModule
        Select Case vbComp.Type
            Case vbext_ct_Document           ' Sheets & ThisWorkbook
                Print #wsFileNum, "''''''''''''''''''''''''''''''''''''"
                Print #wsFileNum, "' Component: " & vbComp.Name
                Print #wsFileNum, "''''''''''''''''''''''''''''''''''''"
                If codeMod.CountOfLines > 0 Then
                    Print #wsFileNum, codeMod.lines(1, codeMod.CountOfLines)
                End If
                Print #wsFileNum, vbCrLf
            
            Case vbext_ct_MSForm             ' UserForms
                Print #frmFileNum, "''''''''''''''''''''''''''''''''''''"
                Print #frmFileNum, "' UserForm: " & vbComp.Name
                Print #frmFileNum, "''''''''''''''''''''''''''''''''''''"
                If codeMod.CountOfLines > 0 Then
                    Print #frmFileNum, codeMod.lines(1, codeMod.CountOfLines)
                End If
                Print #frmFileNum, vbCrLf
            
            Case vbext_ct_ClassModule        ' Class modules
                Print #clsFileNum, "''''''''''''''''''''''''''''''''''''"
                Print #clsFileNum, "' Class Module: " & vbComp.Name
                Print #clsFileNum, "''''''''''''''''''''''''''''''''''''"
                If codeMod.CountOfLines > 0 Then
                    Print #clsFileNum, codeMod.lines(1, codeMod.CountOfLines)
                End If
                Print #clsFileNum, vbCrLf
            
            Case vbext_ct_StdModule          ' Standard (.bas) modules
                Print #modFileNum, "''''''''''''''''''''''''''''''''''''"
                Print #modFileNum, "' Module: " & vbComp.Name
                Print #modFileNum, "''''''''''''''''''''''''''''''''''''"
                If codeMod.CountOfLines > 0 Then
                    Print #modFileNum, codeMod.lines(1, codeMod.CountOfLines)
                End If
                Print #modFileNum, vbCrLf
        End Select
    Next vbComp
    
    ' Close all files
    Close #wsFileNum
    Close #frmFileNum
    Close #clsFileNum
    Close #modFileNum
    
    MsgBox "All code exported to:" & vbCrLf & _
           exportPath & vbCrLf & _
           "(SheetsCode.txt, FormsCode.txt, ClassModulesCode.txt, StandardModulesCode.txt)", _
           vbInformation
End Sub

Sub ListSheetCodeNames()
  Dim c As VBIDE.VBComponent
  For Each c In ThisWorkbook.VBProject.VBComponents
    If c.Type = vbext_ct_Document Then
      Debug.Print "Sheet CodeName: "; c.Name
    End If
  Next
End Sub

