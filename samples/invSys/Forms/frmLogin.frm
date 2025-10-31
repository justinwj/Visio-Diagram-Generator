VERSION 5.00
Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} frmLogin 
   Caption         =   "Login Form"
   ClientHeight    =   5025
   ClientLeft      =   120
   ClientTop       =   465
   ClientWidth     =   4950
   OleObjectBlob   =   "frmLogin.frx":0000
   StartUpPosition =   1  'CenterOwner
End
Attribute VB_Name = "frmLogin"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Private Sub btnLogin_Click()
    Dim ws As Worksheet
    Dim tbl As ListObject
    Dim foundUser As Range
    Dim username As String
    Dim pin As String
    Dim lastLoginCell As Range
    Dim usernameCol As Integer, pinCol As Integer, lastLoginCol As Integer
    ' Set reference to UserCredentials worksheet and table
    Set ws = ThisWorkbook.Sheets("UserCredentials")
    Set tbl = ws.ListObjects("UserCredentials") ' Ensure correct table name
    ' Get user input
    username = Me.txtUsername.value
    pin = Me.txtPIN.value
    ' Validate inputs
    If username = "" Or pin = "" Then
        Me.lblMessage.Caption = "Please enter both Username and PIN."
        Exit Sub
    End If
    ' Get column indexes dynamically
    usernameCol = tbl.ListColumns("USERNAME").Index
    pinCol = tbl.ListColumns("PIN").Index
    lastLoginCol = tbl.ListColumns("LAST LOGIN").Index
    ' Find user in UserCredentials table
    Set foundUser = tbl.ListColumns("USERNAME").DataBodyRange.Find(What:=username, LookAt:=xlWhole)
    If Not foundUser Is Nothing Then
        ' Check PIN
        If foundUser.offset(0, pinCol - usernameCol).value = pin Then
            ' Update last login time
            foundUser.offset(0, lastLoginCol - usernameCol).value = Now
            ' Update message label instead of showing a popup
            Me.lblMessage.Caption = "Login successful"
            ' Close login form
            Application.OnTime Now + TimeValue("00:00:02"), "modUserAuth.HideLoginForm"
        Else
            Me.lblMessage.Caption = "Incorrect PIN. Try again."
        End If
    Else
        Me.lblMessage.Caption = "User not found."
    End If
End Sub
Private Sub btnResetPIN_Click()
    frmAdminEmail.Show
End Sub
Private Sub btnCloseWorkbook_Click()
    ' Ensure the workbook closes properly
    ThisWorkbook.Close SaveChanges:=True
End Sub
Private Sub UserForm_Initialize()
    ' Mask PIN input with asterisks
    Me.txtPIN.PasswordChar = "*"
End Sub
Private Sub UserForm_Activate()
    ' Apply password masking in case the form reloads
    Me.txtPIN.PasswordChar = "*"
End Sub

