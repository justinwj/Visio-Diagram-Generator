PS D:\justinwj\Solutions\Visio Diagram Generator> . 'D:\justinwj\Solutions\Visio Diagram Generator\tools\render-fixture.ps1'
Running: dotnet run --project src/VDG.VBA.CLI -- vba2json --in D:\justinwj\Solutions\Visio Diagram Generator\tests\fixtures\vba\hello_world --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\hello_world\callgraph\hello_world.callgraph.ir.json --infer-metrics
Running: dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\hello_world\callgraph\hello_world.callgraph.ir.json --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\hello_world\callgraph\hello_world.callgraph.diagram.json --mode callgraph
modules:1 procedures:1 edges:0 (progress)

Hyperlink Summary (callgraph):
Procedure/Control | File Path | Module | Start Line | End Line | Hyperlink
Hello | Module1.bas | Module1 | 4 | 6 | Module1.bas#L4
modules:1 procedures:1 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:3
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:1
info: estimated straight-line crossings: 0
info: containers: 1
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\hello_world\callgraph\hello_world.callgraph.diagnostics.json
info: skipping Visio runner due to VDG_SKIP_RUNNER
Running: dotnet run --project src/VDG.VBA.CLI -- vba2json --in D:\justinwj\Solutions\Visio Diagram Generator\tests\fixtures\vba\cross_module_calls --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\cross_module_calls\callgraph\cross_module_calls.callgraph.ir.json --infer-metrics
Running: dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\cross_module_calls\callgraph\cross_module_calls.callgraph.ir.json --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\cross_module_calls\callgraph\cross_module_calls.callgraph.diagram.json --mode callgraph
modules:2 procedures:2 edges:1 (progress)

Hyperlink Summary (callgraph):
Procedure/Control | File Path | Module | Start Line | End Line | Hyperlink
Caller | Module1.bas | Module1 | 4 | 6 | Module1.bas#L4
Work | Module2.bas | Module2 | 4 | 6 | Module2.bas#L4
modules:2 procedures:2 edges:1 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:3
info: routing mode: orthogonal
info: connector count: 1
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:2
info: estimated straight-line crossings: 0
info: containers: 2
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\cross_module_calls\callgraph\cross_module_calls.callgraph.diagnostics.json
info: skipping Visio runner due to VDG_SKIP_RUNNER
Running: dotnet run --project src/VDG.VBA.CLI -- vba2json --in D:\justinwj\Solutions\Visio Diagram Generator\tests\fixtures\vba\cross_module_calls --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\cross_module_calls\module-structure\cross_module_calls.module-structure.ir.json --infer-metrics
Running: dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\cross_module_calls\module-structure\cross_module_calls.module-structure.ir.json --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\cross_module_calls\module-structure\cross_module_calls.module-structure.diagram.json --mode module-structure

Hyperlink Summary (module-structure):
Procedure/Control | File Path | Module | Start Line | End Line | Hyperlink
Caller | Module1.bas | Module1 | 4 | 6 | Module1.bas#L4
Work | Module2.bas | Module2 | 4 | 6 | Module2.bas#L4
modules:2 procedures:0 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:0 progressLastMs:23
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:2
info: estimated straight-line crossings: 0
info: containers: 2
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\cross_module_calls\module-structure\cross_module_calls.module-structure.diagnostics.json
info: skipping Visio runner due to VDG_SKIP_RUNNER
Running: dotnet run --project src/VDG.VBA.CLI -- vba2json --in D:\justinwj\Solutions\Visio Diagram Generator\tests\fixtures\vba\events_and_forms --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\events_and_forms\callgraph\events_and_forms.callgraph.ir.json --infer-metrics
Running: dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\events_and_forms\callgraph\events_and_forms.callgraph.ir.json --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\events_and_forms\callgraph\events_and_forms.callgraph.diagram.json --mode callgraph
modules:1 procedures:1 edges:0 (progress)

Hyperlink Summary (callgraph):
Procedure/Control | File Path | Module | Start Line | End Line | Hyperlink
Command1_Click | Form1.frm | Form1 | 8 | 10 | Form1.frm#L8
modules:1 procedures:1 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:3
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Forms' nodes per page => 1:1
info: estimated straight-line crossings: 0
info: containers: 1
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\events_and_forms\callgraph\events_and_forms.callgraph.diagnostics.json
info: skipping Visio runner due to VDG_SKIP_RUNNER
Running: dotnet run --project src/VDG.VBA.CLI -- vba2json --in D:\justinwj\Solutions\Visio Diagram Generator\samples\invSys --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\invSys\callgraph\invSys.callgraph.ir.json --infer-metrics
Applying overrides from: D:\justinwj\Solutions\Visio Diagram Generator\tests\fixtures\config\invSys\callgraph.diagram.override.json
Running: dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\invSys\callgraph\invSys.callgraph.ir.json --out D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\invSys\callgraph\invSys.callgraph.diagram.json --mode callgraph
modules:5 procedures:1 edges:2 (progress)
modules:10 procedures:7 edges:42 (progress)
modules:13 procedures:16 edges:143 (progress)
modules:16 procedures:41 edges:248 (progress)
modules:21 procedures:46 edges:279 (progress)
modules:26 procedures:50 edges:322 (progress)
modules:29 procedures:60 edges:423 (progress)
modules:30 procedures:69 edges:534 (progress)
modules:31 procedures:80 edges:652 (progress)
modules:33 procedures:87 edges:774 (progress)
modules:33 procedures:90 edges:882 (progress)
modules:35 procedures:95 edges:991 (progress)
modules:38 procedures:105 edges:1097 (progress)
modules:38 procedures:109 edges:1198 (progress)
modules:40 procedures:113 edges:1306 (progress)
modules:44 procedures:133 edges:1411 (progress)
modules:47 procedures:158 edges:1474 (progress)
modules:48 procedures:183 edges:1508 (progress)
modules:48 procedures:199 edges:1609 (progress)
modules:53 procedures:204 edges:1668 (progress)
modules:58 procedures:205 edges:1672 (progress)
modules:63 procedures:207 edges:1676 (progress)
modules:65 procedures:210 edges:1684 (progress)
warning: Procedure 'MouseScroll.ScrollY' calls itself.

Hyperlink Summary (callgraph):
Procedure/Control | File Path | Module | Start Line | End Line | Hyperlink
class_initialize | Class Modules/clsBulkSnapshot.cls | clsBulkSnapshot | 20 | 22 | Class Modules/clsBulkSnapshot.cls#L20
btnCreateDeleteUser_Click | Forms/frmAdminControls.frm | frmAdminControls | 16 | 18 | Forms/frmAdminControls.frm#L16
btnEditUser_Click | Forms/frmAdminControls.frm | frmAdminControls | 19 | 21 | Forms/frmAdminControls.frm#L19
btnCreateUser_Click | Forms/frmCreateDeleteUser.frm | frmCreateDeleteUser | 20 | 72 | Forms/frmCreateDeleteUser.frm#L20
btnDeleteUser_Click | Forms/frmCreateDeleteUser.frm | frmCreateDeleteUser | 80 | 107 | Forms/frmCreateDeleteUser.frm#L80
btnRandomPIN_Click | Forms/frmCreateDeleteUser.frm | frmCreateDeleteUser | 73 | 79 | Forms/frmCreateDeleteUser.frm#L73
UserForm_Initialize | Forms/frmCreateDeleteUser.frm | frmCreateDeleteUser | 16 | 19 | Forms/frmCreateDeleteUser.frm#L16
btnNewPIN_Click | Forms/frmEditUser.frm | frmEditUser | 80 | 86 | Forms/frmEditUser.frm#L80
btnUpdateUser_Click | Forms/frmEditUser.frm | frmEditUser | 22 | 79 | Forms/frmEditUser.frm#L22
UserForm_Initialize | Forms/frmEditUser.frm | frmEditUser | 16 | 21 | Forms/frmEditUser.frm#L16
BuildFirstCharIndex | Forms/frmItemSearch.frm | frmItemSearch | 49 | 70 | Forms/frmItemSearch.frm#L49
CommitSelectionAndClose | Forms/frmItemSearch.frm | frmItemSearch | 187 | 264 | Forms/frmItemSearch.frm#L187
DeleteExistingDataForCell | Forms/frmItemSearch.frm | frmItemSearch | 369 | 400 | Forms/frmItemSearch.frm#L369
FillDataTableRow | Forms/frmItemSearch.frm | frmItemSearch | 266 | 334 | Forms/frmItemSearch.frm#L266
FindRowByValue | Forms/frmItemSearch.frm | frmItemSearch | 496 | 511 | Forms/frmItemSearch.frm#L496
GetLocationByItem | Forms/frmItemSearch.frm | frmItemSearch | 336 | 367 | Forms/frmItemSearch.frm#L336
GetVendorByItem | Forms/frmItemSearch.frm | frmItemSearch | 463 | 494 | Forms/frmItemSearch.frm#L463
lstBox_Change | Forms/frmItemSearch.frm | frmItemSearch | 158 | 160 | Forms/frmItemSearch.frm#L158
lstBox_Click | Forms/frmItemSearch.frm | frmItemSearch | 153 | 156 | Forms/frmItemSearch.frm#L153
lstBox_DblClick | Forms/frmItemSearch.frm | frmItemSearch | 183 | 185 | Forms/frmItemSearch.frm#L183
lstBox_KeyDown | Forms/frmItemSearch.frm | frmItemSearch | 176 | 181 | Forms/frmItemSearch.frm#L176
lstBox_KeyPress | Forms/frmItemSearch.frm | frmItemSearch | 169 | 174 | Forms/frmItemSearch.frm#L169
PopulateListBox | Forms/frmItemSearch.frm | frmItemSearch | 426 | 461 | Forms/frmItemSearch.frm#L426
SetTallyRowNumber | Forms/frmItemSearch.frm | frmItemSearch | 402 | 424 | Forms/frmItemSearch.frm#L402
txtBox_Change | Forms/frmItemSearch.frm | frmItemSearch | 72 | 151 | Forms/frmItemSearch.frm#L72
txtBox_KeyDown | Forms/frmItemSearch.frm | frmItemSearch | 162 | 167 | Forms/frmItemSearch.frm#L162
UpdateDescription | Forms/frmItemSearch.frm | frmItemSearch | 513 | 528 | Forms/frmItemSearch.frm#L513
UserForm_Activate | Forms/frmItemSearch.frm | frmItemSearch | 25 | 30 | Forms/frmItemSearch.frm#L25
UserForm_Initialize | Forms/frmItemSearch.frm | frmItemSearch | 31 | 47 | Forms/frmItemSearch.frm#L31
UserForm_KeyDown | Forms/frmItemSearch.frm | frmItemSearch | 530 | 542 | Forms/frmItemSearch.frm#L530
UserForm_MouseScroll | Forms/frmItemSearch.frm | frmItemSearch | 22 | 24 | Forms/frmItemSearch.frm#L22
btnCloseWorkbook_Click | Forms/frmLogin.frm | frmLogin | 60 | 63 | Forms/frmLogin.frm#L60
btnLogin_Click | Forms/frmLogin.frm | frmLogin | 16 | 56 | Forms/frmLogin.frm#L16
btnResetPIN_Click | Forms/frmLogin.frm | frmLogin | 57 | 59 | Forms/frmLogin.frm#L57
UserForm_Activate | Forms/frmLogin.frm | frmLogin | 68 | 71 | Forms/frmLogin.frm#L68
UserForm_Initialize | Forms/frmLogin.frm | frmLogin | 64 | 67 | Forms/frmLogin.frm#L64
btnSend_Click | Forms/frmReceivedTally.frm | frmReceivedTally | 19 | 22 | Forms/frmReceivedTally.frm#L19
GetUOMFromDataTable | Forms/frmReceivedTally.frm | frmReceivedTally | 110 | 147 | Forms/frmReceivedTally.frm#L110
LogInventoryChange | Forms/frmReceivedTally.frm | frmReceivedTally | 104 | 108 | Forms/frmReceivedTally.frm#L104
UpdateInventory | Forms/frmReceivedTally.frm | frmReceivedTally | 33 | 101 | Forms/frmReceivedTally.frm#L33
UserForm_Initialize | Forms/frmReceivedTally.frm | frmReceivedTally | 24 | 30 | Forms/frmReceivedTally.frm#L24
btnSend_Click | Forms/frmShipmentsTally.frm | frmShipmentsTally | 25 | 29 | Forms/frmShipmentsTally.frm#L25
GetUOMFromDataTable | Forms/frmShipmentsTally.frm | frmShipmentsTally | 118 | 155 | Forms/frmShipmentsTally.frm#L118
LogInventoryChange | Forms/frmShipmentsTally.frm | frmShipmentsTally | 112 | 116 | Forms/frmShipmentsTally.frm#L112
UpdateInventory | Forms/frmShipmentsTally.frm | frmShipmentsTally | 41 | 109 | Forms/frmShipmentsTally.frm#L41
UserForm_Initialize | Forms/frmShipmentsTally.frm | frmShipmentsTally | 31 | 39 | Forms/frmShipmentsTally.frm#L31
Worksheet_Change | Sheets/InventoryManagement.cls | InventoryManagement | 11 | 79 | Sheets/InventoryManagement.cls#L11
Worksheet_TableUpdate | Sheets/InventoryManagement.cls | InventoryManagement | 81 | 87 | Sheets/InventoryManagement.cls#L81
Admin_Click | Modules/modAdmin.bas | modAdmin | 3 | 5 | Modules/modAdmin.bas#L3
Open_CreateDeleteUser | Modules/modAdmin.bas | modAdmin | 6 | 8 | Modules/modAdmin.bas#L6
HandleError | Modules/modErrorHandler.bas | modErrorHandler | 23 | 27 | Modules/modErrorHandler.bas#L23
HandleItemCodeOverflow | Modules/modErrorHandler.bas | modErrorHandler | 41 | 43 | Modules/modErrorHandler.bas#L41
LogAndHandleError | Modules/modErrorHandler.bas | modErrorHandler | 61 | 66 | Modules/modErrorHandler.bas#L61
LogError | Modules/modErrorHandler.bas | modErrorHandler | 45 | 59 | Modules/modErrorHandler.bas#L45
SafeExecute | Modules/modErrorHandler.bas | modErrorHandler | 28 | 40 | Modules/modErrorHandler.bas#L28
ValidateAndProcessInput | Modules/modErrorHandler.bas | modErrorHandler | 5 | 22 | Modules/modErrorHandler.bas#L5
ExportAllCodeToSingleFiles | Modules/modExportImportAll.bas | modExportImportAll | 398 | 464 | Modules/modExportImportAll.bas#L398   
ExportAllModules | Modules/modExportImportAll.bas | modExportImportAll | 23 | 72 | Modules/modExportImportAll.bas#L23
ExportTablesHeadersAndControls | Modules/modExportImportAll.bas | modExportImportAll | 296 | 363 | Modules/modExportImportAll.bas#L296
ExportUserFormControls | Modules/modExportImportAll.bas | modExportImportAll | 364 | 393 | Modules/modExportImportAll.bas#L364       
ListSheetCodeNames | Modules/modExportImportAll.bas | modExportImportAll | 466 | 473 | Modules/modExportImportAll.bas#L466
ReplaceAllCodeFromFiles | Modules/modExportImportAll.bas | modExportImportAll | 74 | 80 | Modules/modExportImportAll.bas#L74
SyncClassModules | Modules/modExportImportAll.bas | modExportImportAll | 115 | 142 | Modules/modExportImportAll.bas#L115
SyncFormsCodeBehind | Modules/modExportImportAll.bas | modExportImportAll | 145 | 199 | Modules/modExportImportAll.bas#L145
SyncSheetsCodeBehind | Modules/modExportImportAll.bas | modExportImportAll | 202 | 257 | Modules/modExportImportAll.bas#L202
SyncSheetsCodeBehind_Diagnostics | Modules/modExportImportAll.bas | modExportImportAll | 259 | 294 | Modules/modExportImportAll.bas#L259
SyncStandardModules | Modules/modExportImportAll.bas | modExportImportAll | 82 | 112 | Modules/modExportImportAll.bas#L82
CommitSelectionAndCloseWrapper | Modules/modGlobals.bas | modGlobals | 14 | 16 | Modules/modGlobals.bas#L14
GetItemUOMByRowNum | Modules/modGlobals.bas | modGlobals | 24 | 70 | Modules/modGlobals.bas#L24
InitializeGlobalVariables | Modules/modGlobals.bas | modGlobals | 18 | 23 | Modules/modGlobals.bas#L18
IsFormLoaded | Modules/modGlobals.bas | modGlobals | 78 | 89 | Modules/modGlobals.bas#L78
OpenItemSearchForCurrentCell | Modules/modGlobals.bas | modGlobals | 71 | 76 | Modules/modGlobals.bas#L71
ExportAllCodeToSingleFiles | Modules/modImportExportAll.bas | modImportExportAll | 397 | 463 | Modules/modImportExportAll.bas#L397   
ExportAllModules | Modules/modImportExportAll.bas | modImportExportAll | 22 | 71 | Modules/modImportExportAll.bas#L22
ExportTablesHeadersAndControls | Modules/modImportExportAll.bas | modImportExportAll | 295 | 362 | Modules/modImportExportAll.bas#L295
ExportUserFormControls | Modules/modImportExportAll.bas | modImportExportAll | 363 | 392 | Modules/modImportExportAll.bas#L363       
ListSheetCodeNames | Modules/modImportExportAll.bas | modImportExportAll | 465 | 472 | Modules/modImportExportAll.bas#L465
ReplaceAllCodeFromFiles | Modules/modImportExportAll.bas | modImportExportAll | 73 | 79 | Modules/modImportExportAll.bas#L73
SyncClassModules | Modules/modImportExportAll.bas | modImportExportAll | 114 | 141 | Modules/modImportExportAll.bas#L114
SyncFormsCodeBehind | Modules/modImportExportAll.bas | modImportExportAll | 144 | 198 | Modules/modImportExportAll.bas#L144
SyncSheetsCodeBehind | Modules/modImportExportAll.bas | modImportExportAll | 201 | 256 | Modules/modImportExportAll.bas#L201
SyncSheetsCodeBehind_Diagnostics | Modules/modImportExportAll.bas | modImportExportAll | 258 | 293 | Modules/modImportExportAll.bas#L258
SyncStandardModules | Modules/modImportExportAll.bas | modImportExportAll | 81 | 111 | Modules/modImportExportAll.bas#L81
LogMultipleInventoryChanges | Modules/modInvLogs.bas | modInvLogs | 6 | 34 | Modules/modInvLogs.bas#L6
ReAddBulkLogEntries | Modules/modInvLogs.bas | modInvLogs | 56 | 78 | Modules/modInvLogs.bas#L56
RemoveLastBulkLogEntries | Modules/modInvLogs.bas | modInvLogs | 37 | 54 | Modules/modInvLogs.bas#L37
AddGoodsReceived_Click | Modules/modInvMan.bas | modInvMan | 3 | 72 | Modules/modInvMan.bas#L3
AddMadeItems_Click | Modules/modInvMan.bas | modInvMan | 277 | 344 | Modules/modInvMan.bas#L277
Adjustments_Click | Modules/modInvMan.bas | modInvMan | 209 | 276 | Modules/modInvMan.bas#L209
DeductShipments_Click | Modules/modInvMan.bas | modInvMan | 141 | 208 | Modules/modInvMan.bas#L141
DeductUsed_Click | Modules/modInvMan.bas | modInvMan | 73 | 140 | Modules/modInvMan.bas#L73
DisplayMessage | Modules/modInvMan.bas | modInvMan | 345 | 360 | Modules/modInvMan.bas#L345
TestBoundaryConditions | Modules/modTestModule.bas | modTestModule | 65 | 103 | Modules/modTestModule.bas#L65
TestDataIntegrity | Modules/modTestModule.bas | modTestModule | 5 | 63 | Modules/modTestModule.bas#L5
AddBigSearchButton | Modules/modTS_Data.bas | modTS_Data | 160 | 194 | Modules/modTS_Data.bas#L160
ClearTableFilters | Modules/modTS_Data.bas | modTS_Data | 138 | 159 | Modules/modTS_Data.bas#L138
GenerateRowNumbers | Modules/modTS_Data.bas | modTS_Data | 69 | 108 | Modules/modTS_Data.bas#L69
IsInItemsColumn | Modules/modTS_Data.bas | modTS_Data | 109 | 136 | Modules/modTS_Data.bas#L109
LoadItemList | Modules/modTS_Data.bas | modTS_Data | 7 | 68 | Modules/modTS_Data.bas#L7
SetupAllHandlers | Modules/modTS_Data.bas | modTS_Data | 195 | 204 | Modules/modTS_Data.bas#L195
LaunchReceivedTally | Modules/modTS_Launchers.bas | modTS_Launchers | 15 | 19 | Modules/modTS_Launchers.bas#L15
LaunchShipmentsTally | Modules/modTS_Launchers.bas | modTS_Launchers | 9 | 13 | Modules/modTS_Launchers.bas#L9
LogReceivedDetailed | Modules/modTS_Log.bas | modTS_Log | 15 | 70 | Modules/modTS_Log.bas#L15
AppendReceivedLogRecord | Modules/modTS_Received.bas | modTS_Received | 239 | 264 | Modules/modTS_Received.bas#L239
GetReceivingDetails | Modules/modTS_Received.bas | modTS_Received | 202 | 236 | Modules/modTS_Received.bas#L202
GetUOMFromDataTable | Modules/modTS_Received.bas | modTS_Received | 266 | 307 | Modules/modTS_Received.bas#L266
PopulateReceivedForm | Modules/modTS_Received.bas | modTS_Received | 58 | 130 | Modules/modTS_Received.bas#L58
ProcessReceivedBatch | Modules/modTS_Received.bas | modTS_Received | 132 | 199 | Modules/modTS_Received.bas#L132
TallyReceived | Modules/modTS_Received.bas | modTS_Received | 10 | 56 | Modules/modTS_Received.bas#L10
GetUOMFromDataTable | Modules/modTS_Shipments.bas | modTS_Shipments | 202 | 243 | Modules/modTS_Shipments.bas#L202
PopulateShipmentsForm | Modules/modTS_Shipments.bas | modTS_Shipments | 31 | 141 | Modules/modTS_Shipments.bas#L31
ProcessShipmentsBatch | Modules/modTS_Shipments.bas | modTS_Shipments | 143 | 200 | Modules/modTS_Shipments.bas#L143
TallyShipments | Modules/modTS_Shipments.bas | modTS_Shipments | 5 | 29 | Modules/modTS_Shipments.bas#L5
ColumnIndex | Modules/modTS_Tally.bas | modTS_Tally | 31 | 40 | Modules/modTS_Tally.bas#L31
FindRowByValue | Modules/modTS_Tally.bas | modTS_Tally | 67 | 84 | Modules/modTS_Tally.bas#L67
GetInvSysValue | Modules/modTS_Tally.bas | modTS_Tally | 50 | 64 | Modules/modTS_Tally.bas#L50
GetItemUOMByRowNum | Modules/modTS_Tally.bas | modTS_Tally | 45 | 47 | Modules/modTS_Tally.bas#L45
GetUOMFromInvSys | Modules/modTS_Tally.bas | modTS_Tally | 86 | 124 | Modules/modTS_Tally.bas#L86
lstBox_DblClick | Modules/modTS_Tally.bas | modTS_Tally | 26 | 28 | Modules/modTS_Tally.bas#L26
lstBox_KeyDown | Modules/modTS_Tally.bas | modTS_Tally | 18 | 23 | Modules/modTS_Tally.bas#L18
NormalizeText | Modules/modTS_Tally.bas | modTS_Tally | 10 | 15 | Modules/modTS_Tally.bas#L10
DisableEvents | Modules/modUR_ExcelIntegration.bas | modUR_ExcelIntegration | 14 | 17 | Modules/modUR_ExcelIntegration.bas#L14       
EnableEvents | Modules/modUR_ExcelIntegration.bas | modUR_ExcelIntegration | 18 | 21 | Modules/modUR_ExcelIntegration.bas#L18        
Workbook_Open | Modules/modUR_ExcelIntegration.bas | modUR_ExcelIntegration | 23 | 26 | Modules/modUR_ExcelIntegration.bas#L23       
Worksheet_Change | Modules/modUR_ExcelIntegration.bas | modUR_ExcelIntegration | 10 | 12 | Modules/modUR_ExcelIntegration.bas#L10    
CaptureSnapshot | Modules/modUR_Snapshot.bas | modUR_Snapshot | 10 | 40 | Modules/modUR_Snapshot.bas#L10
GenerateGUID | Modules/modUR_Snapshot.bas | modUR_Snapshot | 82 | 92 | Modules/modUR_Snapshot.bas#L82
GetInventoryTable | Modules/modUR_Snapshot.bas | modUR_Snapshot | 74 | 81 | Modules/modUR_Snapshot.bas#L74
GetSchemaHash | Modules/modUR_Snapshot.bas | modUR_Snapshot | 93 | 115 | Modules/modUR_Snapshot.bas#L93
InitializeSnapshots | Modules/modUR_Snapshot.bas | modUR_Snapshot | 6 | 9 | Modules/modUR_Snapshot.bas#L6
RestoreSnapshot | Modules/modUR_Snapshot.bas | modUR_Snapshot | 41 | 73 | Modules/modUR_Snapshot.bas#L41
BeginTransaction | Modules/modUR_Transaction.bas | modUR_Transaction | 34 | 41 | Modules/modUR_Transaction.bas#L34
CommitTransaction | Modules/modUR_Transaction.bas | modUR_Transaction | 42 | 63 | Modules/modUR_Transaction.bas#L42
GetCurrentTransactionID | Modules/modUR_Transaction.bas | modUR_Transaction | 69 | 71 | Modules/modUR_Transaction.bas#L69
IsInTransaction | Modules/modUR_Transaction.bas | modUR_Transaction | 65 | 67 | Modules/modUR_Transaction.bas#L65
RollbackTransaction | Modules/modUR_Transaction.bas | modUR_Transaction | 76 | 83 | Modules/modUR_Transaction.bas#L76
SetCurrentTransactionLogCount | Modules/modUR_Transaction.bas | modUR_Transaction | 73 | 75 | Modules/modUR_Transaction.bas#L73      
TrackTransactionChange | Modules/modUR_Transaction.bas | modUR_Transaction | 8 | 33 | Modules/modUR_Transaction.bas#L8
AddToUndoStack | Modules/modUR_UndoRedo.bas | modUR_UndoRedo | 62 | 66 | Modules/modUR_UndoRedo.bas#L62
ClearRedoStack | Modules/modUR_UndoRedo.bas | modUR_UndoRedo | 67 | 69 | Modules/modUR_UndoRedo.bas#L67
GetUndoStack | Modules/modUR_UndoRedo.bas | modUR_UndoRedo | 70 | 72 | Modules/modUR_UndoRedo.bas#L70
PruneUndoStack | Modules/modUR_UndoRedo.bas | modUR_UndoRedo | 57 | 61 | Modules/modUR_UndoRedo.bas#L57
RedoLastAction | Modules/modUR_UndoRedo.bas | modUR_UndoRedo | 39 | 56 | Modules/modUR_UndoRedo.bas#L39
TrackChange | Modules/modUR_UndoRedo.bas | modUR_UndoRedo | 6 | 19 | Modules/modUR_UndoRedo.bas#L6
UndoLastAction | Modules/modUR_UndoRedo.bas | modUR_UndoRedo | 20 | 38 | Modules/modUR_UndoRedo.bas#L20
Class_Terminate | Class Modules/MouseOverControl.cls | MouseOverControl | 213 | 215 | Class Modules/MouseOverControl.cls#L213        
CreateFromControl | Class Modules/MouseOverControl.cls | MouseOverControl | 93 | 101 | Class Modules/MouseOverControl.cls#L93        
CreateFromForm | Class Modules/MouseOverControl.cls | MouseOverControl | 103 | 111 | Class Modules/MouseOverControl.cls#L103
FormHandle | Class Modules/MouseOverControl.cls | MouseOverControl | 204 | 209 | Class Modules/MouseOverControl.cls#L204
GetControl | Class Modules/MouseOverControl.cls | MouseOverControl | 200 | 202 | Class Modules/MouseOverControl.cls#L200
InitFromControl | Class Modules/MouseOverControl.cls | MouseOverControl | 113 | 133 | Class Modules/MouseOverControl.cls#L113        
InitFromForm | Class Modules/MouseOverControl.cls | MouseOverControl | 135 | 142 | Class Modules/MouseOverControl.cls#L135
IsAsyncCallback | Class Modules/MouseOverControl.cls | MouseOverControl | 210 | 212 | Class Modules/MouseOverControl.cls#L210        
m_CheckBox_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 144 | 146 | Class Modules/MouseOverControl.cls#L144   
m_ComboBox_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 147 | 149 | Class Modules/MouseOverControl.cls#L147   
m_CommandButton_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 150 | 152 | Class Modules/MouseOverControl.cls#L150
m_Frame_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 153 | 155 | Class Modules/MouseOverControl.cls#L153      
m_Frame_Scroll | Class Modules/MouseOverControl.cls | MouseOverControl | 192 | 195 | Class Modules/MouseOverControl.cls#L192
m_Image_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 156 | 158 | Class Modules/MouseOverControl.cls#L156      
m_Label_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 159 | 161 | Class Modules/MouseOverControl.cls#L159      
m_ListBox_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 162 | 164 | Class Modules/MouseOverControl.cls#L162    
m_ListView_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 165 | 167 | Class Modules/MouseOverControl.cls#L165   
m_MultiPage_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 168 | 170 | Class Modules/MouseOverControl.cls#L168  
m_MultiPage_Scroll | Class Modules/MouseOverControl.cls | MouseOverControl | 196 | 199 | Class Modules/MouseOverControl.cls#L196     
m_OptionButton_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 171 | 173 | Class Modules/MouseOverControl.cls#L171
M_TabStrip_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 174 | 176 | Class Modules/MouseOverControl.cls#L174   
m_TextBox_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 177 | 179 | Class Modules/MouseOverControl.cls#L177    
m_ToggleButton_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 180 | 182 | Class Modules/MouseOverControl.cls#L180
m_UserForm_MouseMove | Class Modules/MouseOverControl.cls | MouseOverControl | 183 | 185 | Class Modules/MouseOverControl.cls#L183   
m_UserForm_Scroll | Class Modules/MouseOverControl.cls | MouseOverControl | 188 | 191 | Class Modules/MouseOverControl.cls#L188      
AddForm | Modules/MouseScroll.bas | MouseScroll | 341 | 371 | Modules/MouseScroll.bas#L341
CollectionHasKey | Modules/MouseScroll.bas | MouseScroll | 439 | 444 | Modules/MouseScroll.bas#L439
DisableMouseScroll | Modules/MouseScroll.bas | MouseScroll | 68 | 269 | Modules/MouseScroll.bas#L68
EnableMouseScroll | Modules/MouseScroll.bas | MouseScroll | 66 | 67 | Modules/MouseScroll.bas#L66
GetCallbackPtr | Modules/MouseScroll.bas | MouseScroll | 294 | 301 | Modules/MouseScroll.bas#L294
GetControlType | Modules/MouseScroll.bas | MouseScroll | 1041 | 1066 | Modules/MouseScroll.bas#L1041
GetFormHandle | Modules/MouseScroll.bas | MouseScroll | 1072 | 1074 | Modules/MouseScroll.bas#L1072
GetParent | Modules/MouseScroll.bas | MouseScroll | 881 | 893 | Modules/MouseScroll.bas#L881
GetScrollAction | Modules/MouseScroll.bas | MouseScroll | 601 | 619 | Modules/MouseScroll.bas#L601
GetScrollAmount | Modules/MouseScroll.bas | MouseScroll | 639 | 650 | Modules/MouseScroll.bas#L639
GetUserScrollLines | Modules/MouseScroll.bas | MouseScroll | 655 | 660 | Modules/MouseScroll.bas#L655
GetWheelDelta | Modules/MouseScroll.bas | MouseScroll | 625 | 627 | Modules/MouseScroll.bas#L625
GetWindowCaption | Modules/MouseScroll.bas | MouseScroll | 592 | 596 | Modules/MouseScroll.bas#L592
GetWindowUnderCursor | Modules/MouseScroll.bas | MouseScroll | 1095 | 1105 | Modules/MouseScroll.bas#L1095
HiWord | Modules/MouseScroll.bas | MouseScroll | 632 | 634 | Modules/MouseScroll.bas#L632
HookMouse | Modules/MouseScroll.bas | MouseScroll | 284 | 293 | Modules/MouseScroll.bas#L284
IsControlKeyDown | Modules/MouseScroll.bas | MouseScroll | 1086 | 1090 | Modules/MouseScroll.bas#L1086
IsShiftKeyDown | Modules/MouseScroll.bas | MouseScroll | 1081 | 1085 | Modules/MouseScroll.bas#L1081
ListScrollX | Modules/MouseScroll.bas | MouseScroll | 963 | 983 | Modules/MouseScroll.bas#L963
ListScrollY | Modules/MouseScroll.bas | MouseScroll | 730 | 770 | Modules/MouseScroll.bas#L730
MouseOverControl | Modules/MouseScroll.bas | MouseScroll | 372 | 376 | Modules/MouseScroll.bas#L372
MouseProc | Modules/MouseScroll.bas | MouseScroll | 324 | 334 | Modules/MouseScroll.bas#L324
ProcessMouseData | Modules/MouseScroll.bas | MouseScroll | 482 | 587 | Modules/MouseScroll.bas#L482
RemoteAssign | Modules/MouseScroll.bas | MouseScroll | 426 | 430 | Modules/MouseScroll.bas#L426
RemoveDestroyedForms | Modules/MouseScroll.bas | MouseScroll | 394 | 421 | Modules/MouseScroll.bas#L394
RemoveForm | Modules/MouseScroll.bas | MouseScroll | 381 | 389 | Modules/MouseScroll.bas#L381
ResetLast | Modules/MouseScroll.bas | MouseScroll | 274 | 277 | Modules/MouseScroll.bas#L274
ScrollX | Modules/MouseScroll.bas | MouseScroll | 900 | 958 | Modules/MouseScroll.bas#L900
ScrollY | Modules/MouseScroll.bas | MouseScroll | 665 | 725 | Modules/MouseScroll.bas#L665
SetHoveredControl | Modules/MouseScroll.bas | MouseScroll | 449 | 464 | Modules/MouseScroll.bas#L449
TBoxScrollY | Modules/MouseScroll.bas | MouseScroll | 775 | 880 | Modules/MouseScroll.bas#L775
UnHookMouse | Modules/MouseScroll.bas | MouseScroll | 306 | 316 | Modules/MouseScroll.bas#L306
UpdateLastCombo | Modules/MouseScroll.bas | MouseScroll | 470 | 477 | Modules/MouseScroll.bas#L470
Zoom | Modules/MouseScroll.bas | MouseScroll | 988 | 1036 | Modules/MouseScroll.bas#L988
Worksheet_SelectionChange | Sheets/ReceivedTally.cls | ReceivedTally | 15 | 27 | Sheets/ReceivedTally.cls#L15
Worksheet_SelectionChange | Sheets/ShipmentsTally.cls | ShipmentsTally | 16 | 28 | Sheets/ShipmentsTally.cls#L16
RunTest_Click | Sheets/TestSummary.cls | TestSummary | 11 | 12 | Sheets/TestSummary.cls#L11
CheckForHiddenPasswords | Sheets/ThisWorkbook.cls | ThisWorkbook | 25 | 30 | Sheets/ThisWorkbook.cls#L25
Workbook_Open | Sheets/ThisWorkbook.cls | ThisWorkbook | 11 | 18 | Sheets/ThisWorkbook.cls#L11
Workbook_SheetActivate | Sheets/ThisWorkbook.cls | ThisWorkbook | 19 | 24 | Sheets/ThisWorkbook.cls#L19
modules:65 procedures:210 edges:1684 dynamicSkipped:1 dynamicIncluded:0 progressEmits:23 progressLastMs:5
info: routing mode: orthogonal
info: connector count: 1684
info: layout height 211.0in exceeds threshold 11.0in; consider pagination, reducing spacing, or vertical orientation.
info: lane 'Forms' contains 45 nodes (max 12); consider splitting the lane or paginating.
info: lane 'Classes' contains 31 nodes (max 12); consider splitting the lane or paginating.
info: lane 'Modules' contains 131 nodes (max 12); consider splitting the lane or paginating.
info: pagination analysis - usable height 7.50in, predicted pages 28.
info: lane 'Forms' nodes per page => 10:4, 11:5, 12:5, 13:4, 14:5, 15:5, 16:4, 17:5, 18:5, 19:3
info: lane 'Sheets' nodes per page => 14:2, 15:1
info: lane 'Classes' nodes per page => 11:2, 12:5, 13:4, 14:5, 15:5, 16:4, 17:5, 18:1
info: lane 'Modules' nodes per page => 1:5, 2:5, 3:5, 4:4, 5:5, 6:5, 7:4, 8:5, 9:5, 10:4, 11:5, 12:5, 13:4, 14:5, 15:5, 16:5, 17:4, 18:5, 19:5, 20:4, 21:5, 22:5, 23:4, 24:5, 25:5, 26:4, 27:5, 28:4
info: cross-page edges: 80
error: lane overcrowded: lane='Forms' page=11 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Forms' page=12 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Forms' page=14 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Forms' page=15 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Forms' page=17 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Forms' page=18 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Classes' page=12 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Classes' page=14 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Classes' page=15 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Classes' page=17 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=1 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=2 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=3 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=5 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=6 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=8 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=9 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=11 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=12 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=14 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=15 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=16 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=18 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=19 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=21 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=22 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=24 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=25 occupancy=99% nodes=5 usable=7.50in
error: lane overcrowded: lane='Modules' page=27 occupancy=99% nodes=5 usable=7.50in
warning: page crowded: page=11 occupancy=99% (usable 7.50in); top: [frmCreateDeleteUser.btnDeleteUser_Click(1.00in), frmCreateDeleteUser.btnRandomPIN_Click(1.00in), frmEditUser.UserForm_Initialize(1.00in)]
warning: page crowded: page=12 occupancy=99% (usable 7.50in); top: [frmItemSearch.BuildFirstCharIndex(1.00in), frmItemSearch.CommitSelectionAndClose(1.00in), frmItemSearch.DeleteExistingDataForCell(1.00in)]
warning: page crowded: page=14 occupancy=99% (usable 7.50in); top: [frmItemSearch.UpdateDescription(1.00in), modImportExportAll.ReplaceAllCodeFromFiles(1.00in), modImportExportAll.ListSheetCodeNames(1.00in)]
warning: page crowded: page=15 occupancy=99% (usable 7.50in); top: [frmItemSearch.lstBox_Change(1.00in), frmItemSearch.lstBox_Click(1.00in), frmItemSearch.lstBox_DblClick(1.00in)]
warning: page crowded: page=16 occupancy=99% (usable 7.50in); top: [frmItemSearch.txtBox_Change(1.00in), frmItemSearch.txtBox_KeyDown(1.00in), frmLogin.UserForm_Activate(1.00in)]
warning: page crowded: page=17 occupancy=99% (usable 7.50in); top: [frmLogin.btnCloseWorkbook_Click(1.00in), frmLogin.btnLogin_Click(1.00in), frmLogin.btnResetPIN_Click(1.00in)]
warning: page crowded: page=18 occupancy=99% (usable 7.50in); top: [frmReceivedTally.UpdateInventory(1.00in), frmReceivedTally.UserForm_Initialize(1.00in), frmReceivedTally.btnSend_Click(1.00in)]
warning: page crowded: page=19 occupancy=99% (usable 7.50in); top: [frmShipmentsTally.UpdateInventory(1.00in), frmShipmentsTally.UserForm_Initialize(1.00in), frmShipmentsTally.btnSend_Click(1.00in)]
warning: page crowded: page=1 occupancy=99% (usable 7.50in); top: [MouseScroll.AddForm(1.00in), MouseScroll.CollectionHasKey(1.00in), MouseScroll.DisableMouseScroll(1.00in)]
warning: page crowded: page=2 occupancy=99% (usable 7.50in); top: [MouseScroll.GetControlType(1.00in), MouseScroll.GetFormHandle(1.00in), MouseScroll.GetParent(1.00in)]
warning: page crowded: page=3 occupancy=99% (usable 7.50in); top: [MouseScroll.GetUserScrollLines(1.00in), MouseScroll.GetWheelDelta(1.00in), MouseScroll.GetWindowCaption(1.00in)]
warning: page crowded: page=5 occupancy=99% (usable 7.50in); top: [MouseScroll.ListScrollY(1.00in), MouseScroll.MouseOverControl(1.00in), MouseScroll.MouseProc(1.00in)]
warning: page crowded: page=6 occupancy=99% (usable 7.50in); top: [MouseScroll.RemoveDestroyedForms(1.00in), MouseScroll.RemoveForm(1.00in), MouseScroll.ResetLast(1.00in)]
warning: page crowded: page=8 occupancy=99% (usable 7.50in); top: [MouseScroll.Zoom(1.00in), modAdmin.Admin_Click(1.00in), modAdmin.Open_CreateDeleteUser(1.00in)]
warning: page crowded: page=9 occupancy=99% (usable 7.50in); top: [modErrorHandler.LogAndHandleError(1.00in), modErrorHandler.LogError(1.00in), modErrorHandler.SafeExecute(1.00in)]
warning: page crowded: page=21 occupancy=99% (usable 7.50in); top: [modTS_Shipments.PopulateShipmentsForm(1.00in), modTS_Shipments.ProcessShipmentsBatch(1.00in), modTS_Shipments.TallyShipments(1.00in)]
warning: page crowded: page=22 occupancy=99% (usable 7.50in); top: [modTS_Tally.GetInvSysValue(1.00in), modTS_Tally.GetItemUOMByRowNum(1.00in), modTS_Tally.GetUOMFromInvSys(1.00in)]
warning: page crowded: page=24 occupancy=99% (usable 7.50in); top: [modUR_ExcelIntegration.EnableEvents(1.00in), modUR_ExcelIntegration.Workbook_Open(1.00in), modUR_ExcelIntegration.Worksheet_Change(1.00in)]
warning: page crowded: page=25 occupancy=99% (usable 7.50in); top: [modUR_Snapshot.GetInventoryTable(1.00in), modUR_Snapshot.GetSchemaHash(1.00in), modUR_Snapshot.InitializeSnapshots(1.00in)]
warning: page crowded: page=27 occupancy=99% (usable 7.50in); top: [modUR_Transaction.SetCurrentTransactionLogCount(1.00in), modUR_Transaction.TrackTransactionChange(1.00in), modUR_UndoRedo.AddToUndoStack(1.00in)]
info: estimated straight-line crossings: 1276104
info: containers: 65
info: containers paddingIn=0.00in; cornerIn=0.12in
error: container crowded: id='MouseScroll' lane='Modules' occupancy=99% nodes=5
info: diagnostics JSON written: D:\justinwj\Solutions\Visio Diagram Generator\out\fixtures\invSys\callgraph\invSys.callgraph.diagnostics.json
fatal: Object reference not set to an instance of an object.
Exception: D:\justinwj\Solutions\Visio Diagram Generator\tools\render-fixture.ps1:337:9
Line |
 337 |          throw "VDG.CLI exited with code $($process.ExitCode) while re …
     |          ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
     | VDG.CLI exited with code 70 while rendering invSys (callgraph).