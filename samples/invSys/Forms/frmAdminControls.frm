VERSION 5.00
Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} frmAdminControls 
   Caption         =   "Admin Controls"
   ClientHeight    =   3315
   ClientLeft      =   120
   ClientTop       =   465
   ClientWidth     =   2745
   OleObjectBlob   =   "frmAdminControls.frx":0000
   StartUpPosition =   1  'CenterOwner
End
Attribute VB_Name = "frmAdminControls"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Private Sub btnCreateDeleteUser_Click()
    frmCreateDeleteUser.Show
End Sub
Private Sub btnEditUser_Click()
    frmEditUser.Show
End Sub
